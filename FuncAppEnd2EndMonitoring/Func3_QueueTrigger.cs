using System;
using FuncAppEnd2EndMonitoring.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace FuncAppEnd2EndMonitoring
{
    public static class Func3_QueueTrigger
    {
        private const string QUEUE_NAME = "myqueue-items";

        private static string key = TelemetryConfiguration.Active.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY_CUSTOM", EnvironmentVariableTarget.Process);
        private static TelemetryClient _telemetryClient = new TelemetryClient() { InstrumentationKey = key };

        private static Random _random = new Random();

        [FunctionName("Func3_QueueTrigger")]
        public static void Run([QueueTrigger(QUEUE_NAME, Connection = "AzureWebJobsStorage")]MessagePayload myQueueItem, ExecutionContext context, TraceWriter log)
        {
            _telemetryClient.Context.Cloud.RoleName = context.FunctionName;

            RequestTelemetry requestTelemetry = new RequestTelemetry { Name = "Dequeue " + QUEUE_NAME };
            requestTelemetry.Context.Operation.Id = CorrelationHelper.GetOperationId(myQueueItem.RequestId);
            requestTelemetry.Context.Operation.ParentId = myQueueItem.RequestId;

            var operation = _telemetryClient.StartOperation(requestTelemetry);

            try
            {
                // Only 10%
                if (_random.Next(1, 101) <= 10)
                {
                    throw new Exception("step 3 - bad things happen");
                }

                operation.Telemetry.Success = true;
                _telemetryClient.StopOperation(operation);
            }
            catch (Exception e)
            {
                _telemetryClient.TrackException(e);

                operation.Telemetry.Success = false;
                operation.Telemetry.ResponseCode = "500";
                _telemetryClient.StopOperation(operation);

                throw;
            }
        }
    }
}
