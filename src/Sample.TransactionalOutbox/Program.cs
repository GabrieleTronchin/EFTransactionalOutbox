using System.Text.Json.Serialization;
using Quartz;
using Sample.TransactionalOutbox.Domain.Inbox;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Endpoints;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});


builder.Services.AddPersistence();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(Program).Assembly,
        typeof(IDomainEvent).Assembly)
);

var registry = new MessageTypeRegistry();
registry.Register<PaymentConfirmedInboxMessage>("PaymentConfirmed");
builder.Services.AddSingleton(registry);

builder.Services.AddEndpoints(typeof(Program).Assembly);

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

app.MapEndpoints();

app.UseHttpsRedirection();

SeedDb.Initialize(app);

app.Run();
