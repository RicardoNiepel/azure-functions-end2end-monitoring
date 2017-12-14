using FuncAppEnd2EndMonitoring.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FuncAppEnd2EndMonitoring
{
    public static class Func2_HttpTrigger
    {
        private static string key = TelemetryConfiguration.Active.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY_CUSTOM", EnvironmentVariableTarget.Process);
        private static TelemetryClient _telemetryClient = new TelemetryClient() { InstrumentationKey = key };

        private static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process));

        private static Random _random = new Random();

        [FunctionName("Func2_HttpTrigger")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequest req, ExecutionContext context, TraceWriter log)
        {
            _telemetryClient.Context.Cloud.RoleName = context.FunctionName;

            var requestTelemetry = new RequestTelemetry
            {
                Name = $"{req.Method} {req.Path}"
            };

            if (req.Headers.ContainsKey("Request-Id"))
            {
                var requestId = req.Headers.GetCommaSeparatedValues("Request-Id").Single();
                requestTelemetry.Context.Operation.Id = CorrelationHelper.GetOperationId(requestId);
                requestTelemetry.Context.Operation.ParentId = requestId;
            }

            var operation = _telemetryClient.StartOperation(requestTelemetry);

            ObjectResult response;
            try
            {
                // Only 10%
                if (_random.Next(1, 101) <= 10)
                {
                    throw new Exception("step 2 - bad things happen");
                }

                // Only 50%
                if (_random.Next(1, 101) <= 50)
                {
                    await CallQueueTriggerAsync();
                }

                response = new OkObjectResult($"All Done");

                operation.Telemetry.Success = response.StatusCode >= 200 && response.StatusCode <= 299;
                operation.Telemetry.ResponseCode = response.StatusCode.ToString();
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

            return response;
        }

        private static async Task CallQueueTriggerAsync()
        {
            var message = "Sample Message";

            CloudQueueClient queueClient = _storageAccount.CreateCloudQueueClient();
            CloudQueue queue = queueClient.GetQueueReference("myqueue-items");

            var operation = _telemetryClient.StartOperation<DependencyTelemetry>("enqueue " + queue.Name);
            operation.Telemetry.Type = "Queue";
            operation.Telemetry.Target = queue.StorageUri.PrimaryUri.Host;
            operation.Telemetry.Data = "Enqueue " + queue.Name;

            var jsonPayload = JsonConvert.SerializeObject(new MessagePayload
            {
                RequestId = operation.Telemetry.Id,
                Payload = message
            });

            CloudQueueMessage queueMessage = new CloudQueueMessage(jsonPayload);

            // Add operation.Telemetry.Id to the OperationContext to correlate Storage logs and Application Insights telemetry.
            OperationContext context = new OperationContext { ClientRequestID = operation.Telemetry.Id };

            try
            {
                await queue.AddMessageAsync(queueMessage, null, null, new QueueRequestOptions(), context);

                operation.Telemetry.Success = true;
                operation.Telemetry.ResultCode = "200";
            }
            catch (StorageException e)
            {
                _telemetryClient.TrackException(e);

                operation.Telemetry.Properties.Add("AzureServiceRequestID", e.RequestInformation.ServiceRequestID);
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = e.RequestInformation.HttpStatusCode.ToString();
            }
            finally
            {
                _telemetryClient.StopOperation(operation);
            }
        }
    }
}
