using System.Diagnostics.Metrics;
using eShop.Basket.API.Grpc;
using GrpcBasketItem = eShop.Basket.API.Grpc.BasketItem;
using GrpcBasketClient = eShop.Basket.API.Grpc.Basket.BasketClient;

namespace eShop.WebApp.Services;

public class BasketService
{
    private readonly GrpcBasketClient _basketClient;
    private readonly Counter<int> _totalRequests;
    private readonly Histogram<double> _requestDuration;

    public BasketService(GrpcBasketClient basketClient, Meter meter)
    {
        _basketClient = basketClient; 

        _totalRequests = meter.CreateCounter<int>("basket_api_requests_total"); //Total API requests
        _requestDuration = meter.CreateHistogram<double>("basket_api_request_duration_seconds"); // API request duration in seconds
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
        var result = await _basketClient.GetBasketAsync(new()); // Certo: Usar _basketClient
        return MapToBasket(result);
    }

    public async Task DeleteBasketAsync()
    {
        await _basketClient.DeleteBasketAsync(new DeleteBasketRequest()); // Certo: Usar _basketClient
    }

    public async Task UpdateBasketAsync(IReadOnlyCollection<BasketQuantity> basket)
    {
        var updatePayload = new UpdateBasketRequest();

        foreach (var item in basket)
        {
            var updateItem = new GrpcBasketItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            };
            updatePayload.Items.Add(updateItem);
        }

        await _basketClient.UpdateBasketAsync(updatePayload); // Certo: Usar _basketClient
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
