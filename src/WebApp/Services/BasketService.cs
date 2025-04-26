using System.Diagnostics.Metrics;
using eShop.Basket.API.Grpc;
using GrpcBasketItem = eShop.Basket.API.Grpc.BasketItem;
using GrpcBasketClient = eShop.Basket.API.Grpc.Basket.BasketClient;
using System.Diagnostics;

namespace eShop.WebApp.Services;

public class BasketService
{
    private readonly GrpcBasketClient _basketClient;

    private readonly Counter<int> _totalRequests;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<int> _itemsAddedCounter;
    private readonly Counter<int> _itemsRemovedCounter;
    private readonly Histogram<double> _timeBetweenAddAndRemove;

    private static readonly Dictionary<int, DateTime> _cartActivity = new();

    public BasketService(GrpcBasketClient basketClient, Meter meter)
    {
        _basketClient = basketClient;

        _totalRequests = meter.CreateCounter<int>("basket_api_requests_total");
        _requestDuration = meter.CreateHistogram<double>("basket_api_request_duration_seconds");

        _itemsAddedCounter = meter.CreateCounter<int>("basket_items_added_total");
        _itemsRemovedCounter = meter.CreateCounter<int>("basket_items_removed_total");
        _timeBetweenAddAndRemove = meter.CreateHistogram<double>("basket_item_lifetime_seconds");
    }

    public async Task HandleRequest(HttpContext context, Func<Task> next)
    {
        _totalRequests.Add(1);
        var start = DateTime.UtcNow;

        await next();

        var duration = (DateTime.UtcNow - start).TotalSeconds;
        _requestDuration.Record(duration);
    }

    public async Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync()
    {
        _totalRequests.Add(1);
        var timer = Stopwatch.StartNew();

        var result = await _basketClient.GetBasketAsync(new());

        timer.Stop();
        _requestDuration.Record(timer.Elapsed.TotalSeconds);

        return MapToBasket(result);
    }

    public async Task DeleteBasketAsync()
    {
        _totalRequests.Add(1);
        var timer = Stopwatch.StartNew();

        await _basketClient.DeleteBasketAsync(new DeleteBasketRequest());

        timer.Stop();
        _requestDuration.Record(timer.Elapsed.TotalSeconds);
    }

    public async Task UpdateBasketAsync(IReadOnlyCollection<BasketQuantity> basket)
    {
        _totalRequests.Add(1);
        var timer = Stopwatch.StartNew();

        // 1. Get the current status of the cart
        var existingBasket = await GetBasketAsync();
        var existingItems = existingBasket.ToDictionary(x => x.ProductId, x => x.Quantity);

        var updatePayload = new UpdateBasketRequest();
        var newItems = basket.ToDictionary(x => x.ProductId, x => x.Quantity);

        // 2. Process removals and decreases
        foreach (var (productId, oldQuantity) in existingItems)
        {
            if (!newItems.ContainsKey(productId))
            {
                _itemsRemovedCounter.Add(oldQuantity);

                if (_cartActivity.ContainsKey(productId))
                {
                    var addedTime = _cartActivity[productId];
                    var duration = (DateTime.UtcNow - addedTime).TotalSeconds;
                    _timeBetweenAddAndRemove.Record(duration);

                    _cartActivity.Remove(productId);
                }
            }
            else
            {
                var newQuantity = newItems[productId];
                if (newQuantity == 0)
                {
                    _itemsRemovedCounter.Add(oldQuantity);

                    if (_cartActivity.ContainsKey(productId))
                    {
                        var addedTime = _cartActivity[productId];
                        var duration = (DateTime.UtcNow - addedTime).TotalSeconds;
                        _timeBetweenAddAndRemove.Record(duration);

                        _cartActivity.Remove(productId);
                    }
                }
                else if (newQuantity < oldQuantity)
                {
                    var removed = oldQuantity - newQuantity;
                    _itemsRemovedCounter.Add(removed);

                }
            }
        }

        // 3. Process additions and updates
        foreach (var (productId, quantity) in newItems)
        {
            updatePayload.Items.Add(new GrpcBasketItem
            {
                ProductId = productId,
                Quantity = quantity,
            });

            if (existingItems.TryGetValue(productId, out var oldQuantity))
            {
                var added = quantity - oldQuantity;
                if (added > 0)
                {
                    _itemsAddedCounter.Add(added);
                }
            }
            else
            {
                _itemsAddedCounter.Add(quantity);
                _cartActivity[productId] = DateTime.UtcNow;
            }
        }

        await _basketClient.UpdateBasketAsync(updatePayload);

        timer.Stop();
        _requestDuration.Record(timer.Elapsed.TotalSeconds);
    }


    public async Task RemoveItemAsync(int productId)
    {
        _totalRequests.Add(1);
        var timer = Stopwatch.StartNew();

        _itemsRemovedCounter.Add(1);

        if (_cartActivity.ContainsKey(productId))
        {
            var addedTime = _cartActivity[productId];
            var duration = (DateTime.UtcNow - addedTime).TotalSeconds;
            _timeBetweenAddAndRemove.Record(duration);

            _cartActivity.Remove(productId);
        }

        await Task.Delay(10);

        timer.Stop();
        _requestDuration.Record(timer.Elapsed.TotalSeconds);
    }

    private static List<BasketQuantity> MapToBasket(CustomerBasketResponse response)
    {
        var result = new List<BasketQuantity>();
        foreach (var item in response.Items)
        {
            result.Add(new BasketQuantity(item.ProductId, item.Quantity));
        }

        return result;
    }
}

public record BasketQuantity(int ProductId, int Quantity);

