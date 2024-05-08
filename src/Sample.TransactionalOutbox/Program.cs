using Quartz;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Product;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet(
        "/Products",
        async (IProductRepository productRepository) =>
        {
            return await productRepository.GetAsync(CancellationToken.None);
        }
    )
    .WithName("GetProducts")
    .WithOpenApi();

app.MapGet(
        "/Orders",
        async (IOrderRepository orderRepository) =>
        {
            return await orderRepository.GetAsync(CancellationToken.None);
        }
    )
    .WithName("GetOrder")
    .WithOpenApi();

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
    .WithOpenApi();

SeedDb.Initialize(app);

app.Run();
