# Likvido.Telemetry

Shared telemetry wiring for Likvido services: ships logs to the Grafana Alloy OTLP collector that
runs in both clusters, from where they land in Loki.

Used by [Likvido.Web](https://github.com/Likvido/Likvido.Web) (`AddLikvidoWeb`) and
[Likvido.Robot](https://github.com/Likvido/Likvido.Robot) (`RobotOperation.Run`). Application code
does not normally call it directly.

## Usage

```csharp
builder.Logging.AddLikvidoOtlpLogging("my-service");
```

The `serviceName` becomes the `service_name` label in Loki, and therefore the name alert rules
match on.

## When it exports

`AddLikvidoOtlpLogging` is a no-op unless `LikvidoTelemetry.IsDeployedWorkload()` is true, so it is
safe to register unconditionally. A process counts as a deployed workload when:

| Condition | Why |
| --- | --- |
| `KUBERNETES_SERVICE_HOST` is set | kubelet injects it into every pod, so a value means "running in a pod". Absent on a developer machine. |
| `GITHUB_ACTIONS` is not `true` | Our self-hosted runners are pods in the staging cluster, so the check above cannot tell a CI job from a deployed workload on its own. |

### Why not `DOTNET_RUNNING_IN_CONTAINER`

Both call sites used to gate on `DOTNET_RUNNING_IN_CONTAINER == "true"`. That variable answers "am I
in a container", which is not the question — and because `ghcr.io/actions/actions-runner` is built on
a Microsoft .NET base image, it is `true` inside every GitHub Actions job too.

The consequence was not theoretical: a test suite booting a real application through
`WebApplicationFactory` on an in-cluster runner exported its logs to the staging Loki under the live
service's name, and every negative-path test that logged at `Error` level paged OnCall.

### Direction of failure

The gate errs towards exporting. An unrecognised CI system would start producing noise again, which
is visible and recoverable. The alternative — requiring each deployment to opt in via its manifest —
means a workload whose manifest was missed goes silently dark in Loki, and nothing in the system
would signal it.
