using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FuncAppEnd2EndMonitoring
{
    public static class Func1_TimerTrigger
    {
        private static string key = TelemetryConfiguration.Active.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY_CUSTOM", EnvironmentVariableTarget.Process);
        private static TelemetryClient _telemetryClient = new TelemetryClient() { InstrumentationKey = key };

        private static HttpClient _httpClient = new HttpClient();

        private static Random _random = new Random();

        [FunctionName("Func1_TimerTrigger")]
        public static async Task Run([TimerTrigger("*/1 * * * * *")]TimerInfo myTimer, ExecutionContext context, TraceWriter log)
        {
            _telemetryClient.Context.Cloud.RoleName = context.FunctionName;

            var requestTelemetry = new RequestTelemetry
            {
                Name = $"TimerTrigger {myTimer.Schedule}",
                Source = "Timer"
            };
            //requestTelemetry.Context.Operation.Id = context.InvocationId.ToString();
            //requestTelemetry.Context.Operation.Name = "Scenario 1";

            var operation = _telemetryClient.StartOperation(requestTelemetry);

            try
            {
                // Only 50%
                if (_random.Next(1, 101) <= 50)
                {
                    await CallHttpTriggerAsync();
                }

                operation.Telemetry.Success = true;
                _telemetryClient.StopOperation(operation);
            }
            catch (Exception e)
            {
                _telemetryClient.TrackException(e);

                operation.Telemetry.Success = false;
                _telemetryClient.StopOperation(operation);
                throw;
            }
        }

        private static async Task CallHttpTriggerAsync()
        {
            var url = Environment.GetEnvironmentVariable("HTTP_TRIGGER_URL", EnvironmentVariableTarget.Process);
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            var operation = _telemetryClient.StartOperation<DependencyTelemetry>($"{requestMessage.Method} {requestMessage.RequestUri.AbsolutePath}");
            operation.Telemetry.Type = "HTTP";
            operation.Telemetry.Target = requestMessage.RequestUri.Host;
            operation.Telemetry.Data = url;

            HttpResponseMessage response = null;
            try
            {
                requestMessage.Headers.Add("Request-Id", operation.Telemetry.Id);
                response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                operation.Telemetry.Success = true;
                operation.Telemetry.ResultCode = response.StatusCode.ToString();
                _telemetryClient.StopOperation(operation);
            }
            catch (Exception)
            {
                if (response == null)
                {
                    operation.Telemetry.Success = false;
                }
                else
                {
                    operation.Telemetry.Success = response.IsSuccessStatusCode;
                    operation.Telemetry.ResultCode = response.StatusCode.ToString();
                }

                _telemetryClient.StopOperation(operation);

                throw;
            }
        }
    }
}
