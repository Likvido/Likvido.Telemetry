using Grafana.OpenTelemetry;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;

namespace Likvido.Telemetry;

/// <summary>
/// Ships logs to the Grafana Alloy OTLP collector that runs in both Likvido clusters.
/// <para>
/// Shared by <c>Likvido.Web</c>'s <c>AddLikvidoWeb</c> and <c>Likvido.Robot</c>'s
/// <c>RobotOperation.Run</c>. It lives here rather than in either of them because the decision of
/// <em>when</em> to export is subtle (see <see cref="IsDeployedWorkload()"/>) and was previously
/// copy-pasted into both, which meant getting it wrong in two places at once.
/// </para>
/// </summary>
[PublicAPI]
public static class LikvidoTelemetry
{
    /// <summary>
    /// The in-cluster Grafana Alloy collector. The same address in staging and production — the two
    /// clusters are told apart by which Loki the collector writes to, not by this endpoint.
    /// </summary>
    public const string CollectorEndpoint =
        "http://grafana-alloy-otlp.grafana-alloy.svc.cluster.local:4317";

    private const string KubernetesServiceHostVariable = "KUBERNETES_SERVICE_HOST";
    private const string GitHubActionsVariable = "GITHUB_ACTIONS";
    private const string HostnameVariable = "HOSTNAME";

    /// <summary>
    /// Whether this process is a deployed Likvido workload, and should therefore export its logs to
    /// the cluster's collector.
    /// <para>
    /// ⚠️ This deliberately does <em>not</em> key off <c>DOTNET_RUNNING_IN_CONTAINER</c>, which is
    /// what both call sites used to do. That variable answers "am I in a container", which is not
    /// the question — and because <c>ghcr.io/actions/actions-runner</c> is built on a Microsoft .NET
    /// base image, it is <c>true</c> inside every GitHub Actions job as well. A test that boots a
    /// real application through <c>WebApplicationFactory</c> on an in-cluster runner therefore
    /// exported its logs to the staging Loki under the live service's name, and every negative-path
    /// test that logged at Error level paged OnCall.
    /// </para>
    /// </summary>
    public static bool IsDeployedWorkload() =>
        IsDeployedWorkload(
            Environment.GetEnvironmentVariable(KubernetesServiceHostVariable),
            Environment.GetEnvironmentVariable(GitHubActionsVariable));

    /// <summary>
    /// The decision, as a pure function of the two variables, so it can be tested without a cluster.
    /// </summary>
    /// <param name="kubernetesServiceHost">
    /// <c>KUBERNETES_SERVICE_HOST</c>. Injected by kubelet into every pod, so a non-empty value
    /// means "running in a Kubernetes pod" and nothing more. Absent on a developer machine.
    /// </param>
    /// <param name="gitHubActions">
    /// <c>GITHUB_ACTIONS</c>. Set to <c>true</c> in every GitHub Actions job step. Our self-hosted
    /// runners are pods in the staging cluster, so the variable above cannot tell a CI job from a
    /// deployed workload on its own — this is what separates them.
    /// </param>
    /// <remarks>
    /// Note the direction of the failure: an unrecognised CI system would start exporting again,
    /// which is noise. The alternative — requiring each deployment to opt in — would mean a
    /// workload whose manifest was missed goes silently dark in Loki, and nothing in the system
    /// would signal it. Noise is recoverable; silence is not.
    /// </remarks>
    internal static bool IsDeployedWorkload(string? kubernetesServiceHost, string? gitHubActions) =>
        !string.IsNullOrEmpty(kubernetesServiceHost)
        && !string.Equals(gitHubActions, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Adds the OTLP log exporter, but only when <see cref="IsDeployedWorkload()"/> says this
    /// process is a deployed workload. A no-op everywhere else, so callers can register it
    /// unconditionally.
    /// </summary>
    /// <param name="serviceName">
    /// Becomes the <c>service_name</c> Loki label, and therefore the name alert rules match on.
    /// </param>
    public static ILoggingBuilder AddLikvidoOtlpLogging(this ILoggingBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        if (!IsDeployedWorkload())
        {
            return builder;
        }

        return builder.AddOpenTelemetry(options =>
        {
            options.UseGrafana(settings =>
            {
                settings.ServiceName = serviceName;
                settings.ResourceAttributes.Add("k8s.pod.name", ResolvePodName());
                settings.ExporterSettings = new AgentOtlpExporter
                {
                    Protocol = OtlpExportProtocol.Grpc,
                    Endpoint = new Uri(CollectorEndpoint)
                };
            });

            options.IncludeScopes = true;
        });
    }

    /// <summary>
    /// In a pod <c>HOSTNAME</c> is the pod name. It is not set inside a BuildKit <c>RUN</c> step,
    /// and OpenTelemetry throws on a null attribute value, so fall back to
    /// <see cref="Environment.MachineName"/> — which in a pod is the pod name anyway.
    /// </summary>
    private static string ResolvePodName()
    {
        var podName = Environment.GetEnvironmentVariable(HostnameVariable);

        return string.IsNullOrWhiteSpace(podName) ? Environment.MachineName : podName;
    }
}
