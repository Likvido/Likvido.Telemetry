using System;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace Likvido.ApplicationInsights.Telemetry
{
    internal static class OperationTelemetryExtensions
    {
        private const int PropertyMaxLength = 8192;

        /// <summary>
        /// Split long property values in chunks.
        /// Appinsights has a property length limit.
        /// </summary>
        public static void AddChunkedCustomProperty(this OperationTelemetry telemetry, string key, string value)
        {
            if (telemetry == null || value == null)
            {
                return;
            }

            if (value.Length <= PropertyMaxLength)
            {
                telemetry.Properties.Add(key, value);
            }
            else
            {
                for (
                    int i = 1, chunkStart = 0;
                    i < 4 && chunkStart < value.Length;
                    i++, chunkStart += PropertyMaxLength)
                {
                    telemetry.Properties.Add($"{key}-{i}", value.Substring(i, Math.Min(PropertyMaxLength, value.Length - i)));
                }
            }
        }
    }
}
