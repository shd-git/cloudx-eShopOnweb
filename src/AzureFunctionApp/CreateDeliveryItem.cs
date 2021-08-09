using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureFunctionApp
{
    public static class CreateDeliveryItem
    {
        [FunctionName("CreateDeliveryItem")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [CosmosDB(
                databaseName: "cosmos-eshop",
                collectionName: "deliveryorders",
                ConnectionStringSetting = "CosmosEShopConnection")]
            IAsyncCollector<dynamic> orders, 
            ILogger log)
        {
            log.LogInformation($"C# HTTP trigger function {nameof(CreateDeliveryItem)} processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            dynamic orderDelivery = JsonConvert.DeserializeObject(requestBody);

            await orders.AddAsync(orderDelivery);

            return new OkObjectResult(requestBody);

        }
    }
}
