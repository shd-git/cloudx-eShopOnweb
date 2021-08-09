namespace Microsoft.eShopWeb
{
    public class ServicesSettings
    {
        public const string CONFIG_NAME = "servicesSettings";

        public string ServiceBusConnectionString { get; set; }

        public string ReservedItemsQueueName { get; set; } = "sbq-reserved-items";

        public string FunctionAppBaseUrl { get; set; } = "https://func-eshop.azurewebsites.net/";
    }
}
