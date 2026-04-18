using Microsoft.EntityFrameworkCore;
using Quartz;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Product;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddPersistence();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(OutboxMessageEntity).Assembly)
);

builder.Services.AddQuartz(cfg =>
{
    var outboxJobKey = new JobKey(nameof(OutboxMessageProcessorJob));

    cfg.AddJob<OutboxMessageProcessorJob>(outboxJobKey)
        .AddTrigger(t =>
            t.ForJob(outboxJobKey).WithSimpleSchedule(s => s.WithIntervalInSeconds(10).RepeatForever())
        );

    var inboxJobKey = new JobKey(nameof(InboxMessageProcessorJob));

    cfg.AddJob<InboxMessageProcessorJob>(inboxJobKey)
        .AddTrigger(t =>
            t.ForJob(inboxJobKey).WithSimpleSchedule(s => s.WithIntervalInSeconds(10).RepeatForever())
        );
});

builder.Services.AddQuartzHostedService();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet(
        "/Products",
        async (IProductRepository productRepository) =>
        {
            return await productRepository.GetAsync(CancellationToken.None);
        }
    )
    .WithName("GetProducts")
    .WithSummary("Get all products")
    .WithDescription("Returns a list of all marketplace products with their catalog details, current quantities, and active status.")
    .Produces<IEnumerable<ProductEntity>>();

app.MapGet(
        "/Products/{id}",
        async (IProductRepository productRepository, Guid id) =>
        {
            try
            {
                var product = await productRepository.GetAsync(id, CancellationToken.None);
                return Results.Ok(product);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(statusCode: 404, title: "Product not found", detail: $"No product found with ID {id}.");
            }
        }
    )
    .WithName("GetProductById")
    .WithSummary("Get a product by ID")
    .WithDescription("Returns a single product by its unique identifier. Returns 404 if the product does not exist.")
    .Produces<ProductEntity>()
    .ProducesProblem(404);

app.MapGet(
        "/Orders",
        async (IOrderRepository orderRepository) =>
        {
            return await orderRepository.GetAsync(CancellationToken.None);
        }
    )
    .WithName("GetOrders")
    .WithSummary("Get all orders")
    .WithDescription("Returns a list of all marketplace orders including their status, customer information, and associated product details.")
    .Produces<IEnumerable<OrderEntity>>();

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

app.MapPost(
        "/Orders/{id}/Cancel",
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

app.MapPost(
        "/Inbox/Receive",
        async (ShopDbContext dbContext, InboxReceiveRequest request) =>
        {
            var existing = await dbContext.InboxMessages
                .AsNoTracking()
                .AnyAsync(m => m.Id == request.Id);

            if (existing)
                return Results.Ok();

            var message = new InboxMessageEntity
            {
                Id = request.Id,
                MessageType = request.MessageType,
                Payload = request.Payload,
                ReceivedAt = DateTime.UtcNow,
            };

            dbContext.InboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            return Results.Ok();
        }
    )
    .WithName("ReceiveInboxMessage")
    .WithSummary("Receive an external message")
    .WithDescription("Accepts an external message payload and persists it as an InboxMessageEntity for asynchronous processing by the Inbox Processor job. Idempotent — if a message with the same ID already exists, returns 200 without creating a duplicate.")
    .Produces(200)
    .ProducesProblem(400);

app.UseHttpsRedirection();

SeedDb.Initialize(app);

app.Run();

public record InboxReceiveRequest(Guid Id, string MessageType, string Payload);
