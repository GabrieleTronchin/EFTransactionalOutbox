using Sample.TransactionalOutbox.Contracts;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Endpoints;

public class OrdersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Orders").WithTags("Orders");

        group.MapGet(
                "/",
                async (IOrderRepository orderRepository) =>
                {
                    return await orderRepository.GetAsync(CancellationToken.None);
                }
            )
            .WithName("GetOrders")
            .WithSummary("Get all orders")
            .WithDescription("Returns a list of all marketplace orders including their status, customer information, and associated product details.")
            .Produces<IEnumerable<OrderEntity>>();

        group.MapPost(
                "/",
                async (IOrderRepository orderRepository, IProductRepository productRepository, CreateOrderRequest request) =>
                {
                    try
                    {
                        await productRepository.GetAsync(request.ProductId, CancellationToken.None);
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(statusCode: 404, title: "Product not found", detail: $"No product found with ID {request.ProductId}.");
                    }

                    var order = OrderEntity.Create(
                        request.ProductId,
                        request.Quantity,
                        request.TotalAmount,
                        request.CustomerName,
                        request.ShippingAddress);

                    await orderRepository.AddAsync(order);
                    await orderRepository.SaveChangesAsync();

                    return Results.Created($"/Orders/{order.Id}", order);
                }
            )
            .WithName("CreateOrder")
            .WithSummary("Create a new order")
            .WithDescription("Creates a new order in Pending status for the specified product. The order can later be confirmed via the PurchaseOrder endpoint.")
            .Produces<OrderEntity>(201)
            .ProducesProblem(400)
            .ProducesProblem(404);

        group.MapPost(
                "/{id}/Cancel",
                async (IOrderRepository orderRepository, Guid id) =>
                {
                    OrderEntity order;
                    try
                    {
                        order = await orderRepository.GetAsync(id, CancellationToken.None);
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(statusCode: 404, title: "Order not found", detail: $"No order found with ID {id}.");
                    }

                    try
                    {
                        order.CancelOrder();
                        await orderRepository.SaveChangesAsync();
                        return Results.Ok();
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(statusCode: 409, title: "Conflict", detail: "Order is not in Pending status and cannot be cancelled.");
                    }
                }
            )
            .WithName("CancelOrder")
            .WithSummary("Cancel an order")
            .WithDescription("Cancels a pending order by ID. Raises an OrderCancelled domain event that is persisted to the outbox table. Returns 404 if the order does not exist, or 409 if the order is not in Pending status.")
            .Produces(200)
            .ProducesProblem(404)
            .ProducesProblem(409);

        // PurchaseOrder is at root level, not under /Orders group
        app.MapPost(
                "/PurchaseOrder/{id}",
                async (IOrderRepository orderRepository, Guid id) =>
                {
                    OrderEntity order;
                    try
                    {
                        order = await orderRepository.GetAsync(id, CancellationToken.None);
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(statusCode: 404, title: "Order not found", detail: $"No order found with ID {id}.");
                    }

                    try
                    {
                        order.ConfirmPayment();
                        await orderRepository.SaveChangesAsync();
                        return Results.Ok();
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(statusCode: 409, title: "Conflict", detail: "Order is not in Pending status and cannot be confirmed.");
                    }
                }
            )
            .WithName("ConfirmOrder")
            .WithSummary("Confirm an order")
            .WithDescription("Confirms a pending order by ID, triggering the transactional outbox pattern flow. The order's domain event is persisted to the outbox table within the same transaction. Returns 404 if the order does not exist, or 409 if the order is not in Pending status.")
            .Produces(200)
            .ProducesProblem(404)
            .ProducesProblem(409);
    }
}
