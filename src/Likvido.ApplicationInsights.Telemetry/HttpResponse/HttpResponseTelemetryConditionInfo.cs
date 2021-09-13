using System.Net;
using Microsoft.ApplicationInsights.DataContracts;

namespace Likvido.ApplicationInsights.Telemetry
{
    public class HttpResponseTelemetryConditionInfo
    {
        public DependencyTelemetry Telemetry { get; set; }
        public HttpResponseCustomProperty Property { get; set; }
        public HttpStatusCode StatusCode { get; set; }

        public HttpResponseTelemetryConditionInfo(
            DependencyTelemetry telemetry,
            HttpResponseCustomProperty property,
            HttpStatusCode statusCode)
        {
            Telemetry = telemetry;
            Property = property;
            StatusCode = statusCode;
        }
    }
}
