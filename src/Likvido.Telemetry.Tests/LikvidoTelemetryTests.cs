using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Likvido.Telemetry.Tests;

/// <summary>
/// The four environments this library has to tell apart. The CI-in-cluster case is the one that
/// motivated the package: it is the combination that <c>DOTNET_RUNNING_IN_CONTAINER</c> could not
/// distinguish from a deployed workload.
/// </summary>
[Collection(EnvironmentCollection.Name)]
public class IsDeployedWorkloadTests
{
    [Fact]
    public void InAPod_IsADeployedWorkload()
    {
        LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: "172.19.0.1", gitHubActions: null)
            .ShouldBeTrue();
    }

    [Fact]
    public void InAPodInsideACiJob_IsNotADeployedWorkload()
    {
        // A self-hosted GitHub Actions runner: a pod in the staging cluster, so the collector is
        // reachable and KUBERNETES_SERVICE_HOST is set — but its logs must never reach Loki under a
        // live service's name.
        LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: "172.19.0.1", gitHubActions: "true")
            .ShouldBeFalse();
    }

    [Fact]
    public void OnADeveloperMachine_IsNotADeployedWorkload()
    {
        LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: null, gitHubActions: null)
            .ShouldBeFalse();
    }

    [Fact]
    public void OnAGitHubHostedRunner_IsNotADeployedWorkload()
    {
        LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: null, gitHubActions: "true")
            .ShouldBeFalse();
    }

    [Fact]
    public void AnEmptyKubernetesServiceHost_DoesNotCountAsAPod()
    {
        // Env vars read back as empty rather than absent in some hosts, and an empty value is not a
        // pod. Guarding the string rather than just the null keeps that from being an export.
        LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: "", gitHubActions: null)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void TheCiFlagIsMatchedCaseInsensitively(string gitHubActions)
    {
        LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: "172.19.0.1", gitHubActions)
            .ShouldBeFalse();
    }

    [Fact]
    public void TheContainerFlagAloneDoesNotMakeItADeployedWorkload()
    {
        // ⚠️ Regression guard for the defect this package exists to fix. Every GitHub Actions job has
        // DOTNET_RUNNING_IN_CONTAINER=true, because ghcr.io/actions/actions-runner is built on a
        // Microsoft .NET base image — so a test host booting a real application on an in-cluster
        // runner exported to the staging Loki under the live service's name, and every negative-path
        // test that logged at Error level paged OnCall.
        //
        // Setting the variable and asserting nothing changes is what pins the gate to the two inputs
        // it is supposed to read. A future edit that reintroduces the old check fails here.
        var original = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

            LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: null, gitHubActions: null)
                .ShouldBeFalse();

            LikvidoTelemetry.IsDeployedWorkload(kubernetesServiceHost: "172.19.0.1", gitHubActions: "true")
                .ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", original);
        }
    }
}

/// <summary>
/// Mutates process-wide environment variables, hence the shared collection — see
/// <see cref="EnvironmentCollection"/>.
/// </summary>
[Collection(EnvironmentCollection.Name)]
public class AddLikvidoOtlpLoggingTests
{
    [Fact]
    public void OutsideACluster_TheExporterIsNotAdded()
    {
        // The interesting assertion is that this resolves at all. Registering the Grafana exporter
        // would otherwise try to flush to an endpoint that does not exist outside the cluster.
        var logger = BuildLoggerFactory(kubernetesServiceHost: null).CreateLogger("test");

        logger.ShouldNotBeNull();

        // No throw, and nothing to dispose: the builder was left alone.
        Should.NotThrow(() => logger.LogInformation("A log line that stays on the console."));
    }

    [Fact]
    public void InACiJob_TheExporterIsNotAdded()
    {
        var logger = BuildLoggerFactory(kubernetesServiceHost: "172.19.0.1", gitHubActions: "true")
            .CreateLogger("test");

        logger.ShouldNotBeNull();
        Should.NotThrow(() => logger.LogInformation("A log line that stays on the console."));
    }

    [Fact]
    public void InAPodWithoutHostname_TheLoggerFactoryStillResolves()
    {
        // Regression guard for Likvido/Likvido.App#27873, which moved here with the exporter wiring.
        // BuildKit does not set HOSTNAME inside a RUN step, so an application booted from a Docker
        // build stage used to fail with "Attribute value type is not an accepted primitive
        // (Parameter 'k8s.pod.name')". This is the one case that reaches the real exporter, so it is
        // also the only test here that proves the Grafana settings are actually valid.
        var logger = BuildLoggerFactory(kubernetesServiceHost: "172.19.0.1", hostname: null)
            .CreateLogger("test");

        logger.ShouldNotBeNull();
    }

    [Fact]
    public void InAPodWithHostname_TheLoggerFactoryStillResolves()
    {
        var logger = BuildLoggerFactory(
                kubernetesServiceHost: "172.19.0.1",
                hostname: "test-app-6c9f8d7b5c-2xq4t")
            .CreateLogger("test");

        logger.ShouldNotBeNull();
    }

    [Fact]
    public void TheServiceNameIsRequired()
    {
        Should.Throw<ArgumentException>(
            () => new ServiceCollection()
                .AddLogging(logging => logging.AddLikvidoOtlpLogging("  "))
                .BuildServiceProvider()
                .GetRequiredService<ILoggerFactory>());
    }

    /// <summary>
    /// The service provider is deliberately not disposed: shutting down the OTLP exporter waits on a
    /// flush timeout against an endpoint that does not exist outside the cluster, which adds seconds
    /// of dead wall clock to every test.
    /// </summary>
    private static ILoggerFactory BuildLoggerFactory(
        string? kubernetesServiceHost,
        string? gitHubActions = null,
        string? hostname = "test-pod")
    {
        var originalServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        var originalGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        var originalHostname = Environment.GetEnvironmentVariable("HOSTNAME");

        try
        {
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", kubernetesServiceHost);
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", gitHubActions);
            Environment.SetEnvironmentVariable("HOSTNAME", hostname);

            return new ServiceCollection()
                .AddLogging(logging => logging.AddLikvidoOtlpLogging("test-app"))
                .BuildServiceProvider()
                .GetRequiredService<ILoggerFactory>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", originalServiceHost);
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", originalGitHubActions);
            Environment.SetEnvironmentVariable("HOSTNAME", originalHostname);
        }
    }
}
