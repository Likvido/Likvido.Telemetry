using System;
using System.Net;
using System.Net.Http;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Likvido.ApplicationInsights.Telemetry
{
    /// <summary>
    /// Enrich telemetry with response content.
    /// </summary>
    public class HttpResponseTelemetryInitializer : ITelemetryInitializer
    {
        private readonly Func<HttpResponseTelemetryConditionInfo, bool> _condition;

        public HttpResponseTelemetryInitializer(Func<HttpResponseTelemetryConditionInfo, bool> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public void Initialize(ITelemetry telemetry)
        {
            try
            {
                if (telemetry is DependencyTelemetry dependencyTelemetry)
                {
                    Initialize(dependencyTelemetry);
                }
            }
            catch
            {
                // Do not fail initializer.
            }
        }

        private void Initialize(DependencyTelemetry telemetry)
        {
            // Check if telemetry is for http response.
            if (telemetry.Type == "Http" &&
                telemetry.TryGetOperationDetail("HttpResponse", out var httpResponseObj) &&
                httpResponseObj is HttpResponseMessage httpResponse)
            {
                TryEnrichWithContentProperty(
                    telemetry,
                    httpResponse.StatusCode,
                    HttpResponseCustomProperty.ResponseContent,
                    httpResponse.Content);

                TryEnrichWithContentProperty(
                    telemetry,
                    httpResponse.StatusCode,
                    HttpResponseCustomProperty.RequestContent,
                    httpResponse.RequestMessage.Content);
            }
        }

        private void TryEnrichWithContentProperty(
            DependencyTelemetry telemetry,
            HttpStatusCode statusCode,
            HttpResponseCustomProperty property,
            HttpContent content)
        {
            if (_condition(new HttpResponseTelemetryConditionInfo(telemetry, property, statusCode)) &&
                TryReadContentString(content, out var contentString))
            {
                telemetry.AddChunkedCustomProperty(property.ToString(), contentString!);
            }
        }

        private static bool TryReadContentString(HttpContent content, out string? contentString)
        {
            if (content != null)
            {
                try
                {
                    contentString = content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return true;
                }
                catch
                {
                    // Failed to read content string.
                }
            }

            contentString = null;
            return false;
        }
    }
}
