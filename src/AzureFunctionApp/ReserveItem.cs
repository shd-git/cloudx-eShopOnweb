using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace AzureFunctionApp
{
    public static class ReserveItem
    {
        [FunctionName("ReserveItem")]
        public static void Run(
            [ServiceBusTrigger("sbq-reserved-items", Connection = "ReservedItemsQueueConnection")] string myQueueItem,
            [Blob(blobPath: "reserved/{DateTime}.json", access: FileAccess.Write, Connection = "ReservedItemsBlobConnection")] out string blob,
            ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
            
            blob = myQueueItem;
        }
    }
}
