using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using eShop.Basket.API.Repositories;
using eShop.Basket.API.Extensions;
using eShop.Basket.API.Model;
using OpenTelemetry.Trace;
using GrpcStatus = Grpc.Core.Status;
using GrpcStatusCode = Grpc.Core.StatusCode;
using Prometheus;

namespace eShop.Basket.API.Grpc;

public class BasketService(
    IBasketRepository repository,
    ILogger<BasketService> logger) : Basket.BasketBase
{
    private static readonly ActivitySource ActivitySource = new("BasketAPI");

    [AllowAnonymous]
    public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("GetBasket")?.SetTag("basket.operation", "retrieve");

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("basket.user_id", "anonymous");
            return new();
        }

        activity?.SetTag("basket.user_id", userId);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var data = await repository.GetBasketAsync(userId);

        if (data is not null)
        {
            activity?.SetTag("basket.item_count", data.Items.Count);
            return MapToCustomerBasketResponse(data);
        }

        return new();
    }

    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        Console.WriteLine("🔹 Starting UpdateBasket activity..."); // <-- Teste para ver se o método é chamado

        using var activity = ActivitySource.StartActivity("UpdateBasket")?.SetTag("basket.operation", "update");

        if (activity == null)
        {
            Console.WriteLine("❌ Activity not created!"); // <-- Se isto aparecer, o OpenTelemetry não está a capturar spans
        }
        else
        {
            Console.WriteLine("✅ Activity created successfully!"); // <-- Confirma que o span foi criado
        }

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("basket.user_id", "anonymous");
            Console.WriteLine("❌ User not authenticated!");
            ThrowNotAuthenticated();
        }

        activity?.SetTag("basket.user_id", userId);
        activity?.SetTag("basket.item_count", request.Items.Count);

        foreach (var item in request.Items)
        {
            activity?.SetTag($"basket.item.{item.ProductId}", item.Quantity);
            Console.WriteLine($"📦 Item: {item.ProductId} - Quantity: {item.Quantity}");
        }

        Console.WriteLine("🔹 Sending span...");

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Updating basket for user {UserId} with {ItemCount} items", userId, request.Items.Count);
        }

        var customerBasket = MapToCustomerBasket(userId, request);
        var response = await repository.UpdateBasketAsync(customerBasket);
        if (response is null)
        {
            ThrowBasketDoesNotExist(userId);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.Dispose();

        return MapToCustomerBasketResponse(response);
    }

    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("DeleteBasket")?.SetTag("basket.operation", "delete");

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("basket.user_id", "anonymous");
            ThrowNotAuthenticated();
        }

        activity?.SetTag("basket.user_id", userId);

        await repository.DeleteBasketAsync(userId);
        return new();
    }

    [DoesNotReturn]
    private static void ThrowNotAuthenticated() =>
    throw new RpcException(new GrpcStatus(GrpcStatusCode.Unauthenticated, "The caller is not authenticated."));

    [DoesNotReturn]
    private static void ThrowBasketDoesNotExist(string userId) =>
        throw new RpcException(new GrpcStatus(GrpcStatusCode.NotFound, $"Basket with buyer id {userId} does not exist"));

    private static CustomerBasketResponse MapToCustomerBasketResponse(CustomerBasket customerBasket)
    {
        var response = new CustomerBasketResponse();

        foreach (var item in customerBasket.Items)
        {
            response.Items.Add(new BasketItem()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }

    private static CustomerBasket MapToCustomerBasket(string userId, UpdateBasketRequest customerBasketRequest)
    {
        var response = new CustomerBasket
        {
            BuyerId = userId
        };

        foreach (var item in customerBasketRequest.Items)
        {
            response.Items.Add(new()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }
}
