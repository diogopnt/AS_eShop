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
        using var activity = ActivitySource.StartActivity("GetBasket", ActivityKind.Server)?
            .SetTag("basket.operation", "retrieve");

        activity?.AddEvent(new ActivityEvent("Start processing GetBasket request"));

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("basket.user_id", "anonymous");
            activity?.SetStatus(ActivityStatusCode.Ok, "No user ID provided");
            activity?.AddEvent(new ActivityEvent("Anonymous user"));
            return new();
        }

        activity?.SetTag("basket.user_id", userId);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        // Log the request details
        using (var repoSpan = ActivitySource.StartActivity("repository.GetBasketAsync", ActivityKind.Internal))
        {
            repoSpan?.SetTag("repository.call", "GetBasketAsync");
            repoSpan?.AddEvent(new ActivityEvent("Fetching basket from DB"));

            var data = await repository.GetBasketAsync(userId);

            if (data is not null)
            {
                activity?.SetTag("basket.item_count", data.Items.Count);
                activity?.AddEvent(new ActivityEvent("Basket retrieved with items"));
                activity?.SetStatus(ActivityStatusCode.Ok);
                return MapToCustomerBasketResponse(data);
            }

            activity?.AddEvent(new ActivityEvent("No basket found for user"));
            activity?.SetStatus(ActivityStatusCode.Ok, "Empty basket");
            return new();
        }
    }


    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("UpdateBasket", ActivityKind.Server)?
            .SetTag("basket.operation", "update");

        activity?.AddEvent(new ActivityEvent("Start UpdateBasket request"));

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("basket.user_id", "anonymous");
            activity?.SetStatus(ActivityStatusCode.Error, "User not authenticated");
            activity?.AddEvent(new ActivityEvent("Missing user ID"));
            ThrowNotAuthenticated();
        }

        activity?.SetTag("basket.user_id", userId);
        activity?.SetTag("basket.item_count", request.Items.Count);

        foreach (var item in request.Items)
        {
            activity?.SetTag($"basket.item.{item.ProductId}.quantity", item.Quantity);
            activity?.AddEvent(new ActivityEvent($"Item added: {item.ProductId}, Quantity: {item.Quantity}"));
        }

        // Log the request details
        CustomerBasket customerBasket;
        using (var mapSpan = ActivitySource.StartActivity("MapToCustomerBasket", ActivityKind.Internal))
        {
            customerBasket = MapToCustomerBasket(userId, request);
            mapSpan?.AddEvent(new ActivityEvent("Basket object created from request"));
        }

        // Log the request details
        CustomerBasket updatedBasket;
        using (var repoSpan = ActivitySource.StartActivity("repository.UpdateBasketAsync", ActivityKind.Internal))
        {
            updatedBasket = await repository.UpdateBasketAsync(customerBasket);
            repoSpan?.SetTag("repository.call", "UpdateBasketAsync");

            if (updatedBasket == null)
            {
                repoSpan?.SetStatus(ActivityStatusCode.Error, "Basket not found");
                activity?.SetStatus(ActivityStatusCode.Error, "Basket not found during update");
                activity?.AddEvent(new ActivityEvent("Basket update failed - not found"));
                ThrowBasketDoesNotExist(userId);
            }

            repoSpan?.AddEvent(new ActivityEvent("Basket updated in DB"));
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.AddEvent(new ActivityEvent("UpdateBasket completed successfully"));

        return MapToCustomerBasketResponse(updatedBasket);
    }


    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        using var activity = ActivitySource.StartActivity("DeleteBasket", ActivityKind.Server)?
            .SetTag("basket.operation", "delete");

        activity?.AddEvent(new ActivityEvent("Start DeleteBasket request"));

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("basket.user_id", "anonymous");
            activity?.SetStatus(ActivityStatusCode.Error, "Unauthenticated user");
            activity?.AddEvent(new ActivityEvent("Missing user ID"));
            ThrowNotAuthenticated();
        }

        activity?.SetTag("basket.user_id", userId);

        // Log the request details
        using (var repoSpan = ActivitySource.StartActivity("repository.DeleteBasketAsync", ActivityKind.Internal))
        {
            repoSpan?.SetTag("repository.call", "DeleteBasketAsync");
            repoSpan?.AddEvent(new ActivityEvent("Deleting basket from DB"));

            await repository.DeleteBasketAsync(userId);
        }

        activity?.AddEvent(new ActivityEvent("Basket deleted"));
        activity?.SetStatus(ActivityStatusCode.Ok);

        return new DeleteBasketResponse();
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
