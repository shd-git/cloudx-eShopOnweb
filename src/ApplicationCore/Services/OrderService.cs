using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Services
{
    public class OrderService : IOrderService
    {
        private readonly IAsyncRepository<Order> _orderRepository;
        private readonly IUriComposer _uriComposer;
        private readonly IOptions<ServicesSettings> _servicesSettings;
        private readonly IAppLogger<OrderService> _appLogger;
        private readonly IAsyncRepository<Basket> _basketRepository;
        private readonly IAsyncRepository<CatalogItem> _itemRepository;

        public OrderService(IAsyncRepository<Basket> basketRepository,
            IAsyncRepository<CatalogItem> itemRepository,
            IAsyncRepository<Order> orderRepository,
            IUriComposer uriComposer,
            IOptions<ServicesSettings> servicesSettings,
            IAppLogger<OrderService> appLogger)
        {
            _orderRepository = orderRepository;
            _uriComposer = uriComposer;
            _servicesSettings = servicesSettings;
            _appLogger = appLogger;
            _basketRepository = basketRepository;
            _itemRepository = itemRepository;
        }

        public async Task CreateOrderAsync(int basketId, Address shippingAddress)
        {
            var basketSpec = new BasketWithItemsSpecification(basketId);
            var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

            Guard.Against.NullBasket(basketId, basket);
            Guard.Against.EmptyBasketOnCheckout(basket.Items);

            var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
            var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

            var items = basket.Items.Select(basketItem =>
            {
                var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
                var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
                var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
                return orderItem;
            }).ToList();

            var order = new Order(basket.BuyerId, shippingAddress, items);

            await _orderRepository.AddAsync(order);

            await ReserveItems();

            await SendReserveItemMessageAsync(order);

            async Task SendReserveItemMessageAsync(Order order, CancellationToken cancellationToken = default)
            {
                var orderItems = order
                    .OrderItems
                    .Select(x => new { itemId = x.ItemOrdered.CatalogItemId, quantity = x.Units })
                    .ToArray();

                // Because ServiceBusClient implements IAsyncDisposable, we'll create it 
                // with "await using" so that it is automatically disposed for us.
                await using var client = new ServiceBusClient(_servicesSettings.Value.ServiceBusConnectionString);

                // The sender is responsible for publishing messages to the queue.
                ServiceBusSender sender = client.CreateSender(_servicesSettings.Value.ReservedItemsQueueName);

                // Send messages.
                try
                {
                    string messageBody = JsonSerializer.Serialize(orderItems);

                    ServiceBusMessage message = new ServiceBusMessage(messageBody);
                    await sender.SendMessageAsync(message);

                }
                catch (Exception exception)
                {
                    // TODO Add compensation logic
                    _appLogger.LogError($"Message Bus exception: {exception.Message}");
                }
            }

            async Task ReserveItems()
            {
                // TODO: Use HttpClientFactory
                var httpClient = new HttpClient() 
                { 
                    BaseAddress = new Uri(_servicesSettings.Value.FunctionAppBaseUrl),
                };

                var orderItemApis =
                    order
                    .OrderItems
                    .Select(x => new { ItemId = x.ItemOrdered.CatalogItemId, Quantity = x.Units })
                    .ToArray();

                dynamic deliveryItem = new {order.ShipToAddress, FinalPrice = order.Total(), OrderItems = orderItemApis };

                string messageBody = JsonSerializer.Serialize(deliveryItem);

                StringContent stringContent = new (messageBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response =
                await httpClient
                .PostAsync(
                    "api/CreateDeliveryItem",
                    stringContent,
                    cancellationToken: CancellationToken.None);

                if (!response.IsSuccessStatusCode)
                {
                    // TODO Add compensation logic

                    string errorMessage = await response.Content.ReadAsStringAsync();
                    _appLogger.LogError($"Error response from api/CreateDeliveryItem: {response.StatusCode} - {errorMessage}");
                }
            }
        }
    }
}
