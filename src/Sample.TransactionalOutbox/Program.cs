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
    var jobKey = new JobKey(nameof(OutboxMessageProcessorJob));

    cfg.AddJob<OutboxMessageProcessorJob>(jobKey)
        .AddTrigger(t =>
            t.ForJob(jobKey).WithSimpleSchedule(s => s.WithIntervalInSeconds(10).RepeatForever())
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
    .WithDescription("Returns a list of all products with their current quantities.");

app.MapGet(
        "/Orders",
        async (IOrderRepository orderRepository) =>
        {
            return await orderRepository.GetAsync(CancellationToken.None);
        }
    )
    .WithName("GetOrder")
    .WithSummary("Get all orders")
    .WithDescription("Returns a list of all orders and their confirmation status.");

app.MapPost(
        "/PurchaseOrder/{id}",
        async (IOrderRepository orderRepository, Guid id) =>
        {
            var order = await orderRepository.GetAsync(id, CancellationToken.None);
            order.ConfirmPayment();
            await orderRepository.SaveChangesAsync();
        }
    )
    .WithName("Order")
    .WithSummary("Confirm an order")
    .WithDescription("Confirms an order by ID, triggering the transactional outbox pattern flow. The order's domain event is persisted to the outbox table within the same transaction.");

app.UseHttpsRedirection();

SeedDb.Initialize(app);

app.Run();
