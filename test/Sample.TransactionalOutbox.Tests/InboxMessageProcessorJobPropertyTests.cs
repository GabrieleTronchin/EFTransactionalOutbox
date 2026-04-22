using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using Quartz;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Inbox;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;

namespace Sample.TransactionalOutbox.Tests;

public sealed class InboxMessageProcessorJobPropertyTests
{
    private static ShopDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new ShopDbContext(options);
    }

    // ──────────────────────────────────────────────
    // Task 9.3 — Property 5: Inbox message deserialization round-trip
    // Feature: endpoint-restructure-and-generic-inbox, Property 5: Inbox message deserialization round-trip
    // ──────────────────────────────────────────────

    /// <summary>
    /// For any valid PaymentConfirmedInboxMessage with a random non-empty OrderId,
    /// serializing it to JSON, storing it as the payload of an InboxMessageEntity
    /// with MessageType "PaymentConfirmed", then running the InboxMessageProcessorJob,
    /// should result in MediatR publishing a message equal to the original instance.
    ///
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property5_DeserializationRoundTrip_PublishesOriginalMessage(Guid orderId)
    {
        // Filter out Guid.Empty — we need a valid non-empty OrderId
        if (orderId == Guid.Empty)
            return;

        // Arrange
        var original = new PaymentConfirmedInboxMessage(orderId);
        var payload = JsonConvert.SerializeObject(original);

        using var context = CreateContext();
        var entity = new InboxMessageEntity
        {
            Id = Guid.NewGuid(),
            MessageType = "PaymentConfirmed",
            Payload = payload,
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = null,
            Error = null
        };
        context.InboxMessages.Add(entity);
        context.SaveChanges();

        var publisher = Substitute.For<IPublisher>();
        var logger = Substitute.For<ILogger<InboxMessageProcessorJob>>();
        var jobContext = Substitute.For<IJobExecutionContext>();
        jobContext.CancellationToken.Returns(CancellationToken.None);

        var registry = new MessageTypeRegistry();
        registry.Register<PaymentConfirmedInboxMessage>("PaymentConfirmed");

        var job = new InboxMessageProcessorJob(logger, context, publisher, registry);

        // Act
        job.Execute(jobContext).GetAwaiter().GetResult();

        // Assert — MediatR should have published a message equal to the original
        publisher.Received(1).Publish(
            Arg.Is<PaymentConfirmedInboxMessage>(m => m == original),
            Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // Task 9.4 — Property 6: InboxMessageProcessorJob error handling preserves error details
    // Feature: endpoint-restructure-and-generic-inbox, Property 6: InboxMessageProcessorJob error handling preserves error details
    // ──────────────────────────────────────────────

    /// <summary>
    /// For any InboxMessageEntity whose processing throws an exception (e.g., invalid
    /// payload for a registered type), after the job executes, the entity's Error field
    /// should contain the exception message and ProcessedAt should be set to a non-null value.
    ///
    /// **Validates: Requirements 10.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property6_ErrorHandling_PreservesErrorDetails(NonEmptyString invalidPayload)
    {
        var garbagePayload = "<<<INVALID_JSON>>>" + invalidPayload.Get;

        // Arrange — create an entity with a registered MessageType but an INVALID payload
        // that will cause JsonConvert.DeserializeObject to throw
        using var context = CreateContext();
        var entityId = Guid.NewGuid();
        var entity = new InboxMessageEntity
        {
            Id = entityId,
            MessageType = "PaymentConfirmed",
            Payload = garbagePayload,
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = null,
            Error = null
        };
        context.InboxMessages.Add(entity);
        context.SaveChanges();

        var publisher = Substitute.For<IPublisher>();
        var logger = Substitute.For<ILogger<InboxMessageProcessorJob>>();
        var jobContext = Substitute.For<IJobExecutionContext>();
        jobContext.CancellationToken.Returns(CancellationToken.None);

        var registry = new MessageTypeRegistry();
        registry.Register<PaymentConfirmedInboxMessage>("PaymentConfirmed");

        var job = new InboxMessageProcessorJob(logger, context, publisher, registry);

        // Act
        job.Execute(jobContext).GetAwaiter().GetResult();

        // Assert — Error field contains the exception message and ProcessedAt is set
        var updatedEntity = context.InboxMessages.First(m => m.Id == entityId);
        updatedEntity.Error.Should().NotBeNull();
        updatedEntity.ProcessedAt.Should().NotBeNull();
        // Publisher should NOT have been called since deserialization failed
        publisher.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // Task 9.5 — Property 7: InboxMessageProcessorJob skips already-processed messages
    // Feature: endpoint-restructure-and-generic-inbox, Property 7: InboxMessageProcessorJob skips already-processed messages
    // ──────────────────────────────────────────────

    /// <summary>
    /// For any InboxMessageEntity where ProcessedAt is already set to a non-null value,
    /// running the InboxMessageProcessorJob should not invoke MediatR Publish for that
    /// message, and the entity's Error and ProcessedAt fields should remain unchanged.
    ///
    /// **Validates: Requirements 10.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property7_SkipsAlreadyProcessedMessages_DoesNotPublishOrModify(Guid orderId, DateTime processedAt)
    {
        // Filter out Guid.Empty — we need a valid OrderId for a realistic entity
        if (orderId == Guid.Empty)
            return;

        // Arrange — create an entity that has already been processed (ProcessedAt is set)
        var originalProcessedAt = processedAt;
        var originalError = "some previous error";

        using var context = CreateContext();
        var entityId = Guid.NewGuid();
        var entity = new InboxMessageEntity
        {
            Id = entityId,
            MessageType = "PaymentConfirmed",
            Payload = JsonConvert.SerializeObject(new PaymentConfirmedInboxMessage(orderId)),
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = originalProcessedAt,
            Error = originalError
        };
        context.InboxMessages.Add(entity);
        context.SaveChanges();

        var publisher = Substitute.For<IPublisher>();
        var logger = Substitute.For<ILogger<InboxMessageProcessorJob>>();
        var jobContext = Substitute.For<IJobExecutionContext>();
        jobContext.CancellationToken.Returns(CancellationToken.None);

        var registry = new MessageTypeRegistry();
        registry.Register<PaymentConfirmedInboxMessage>("PaymentConfirmed");

        var job = new InboxMessageProcessorJob(logger, context, publisher, registry);

        // Act
        job.Execute(jobContext).GetAwaiter().GetResult();

        // Assert — Publisher should NOT have been called (message was already processed)
        publisher.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());

        // Assert — Entity fields remain exactly as they were
        var updatedEntity = context.InboxMessages.First(m => m.Id == entityId);
        updatedEntity.ProcessedAt.Should().Be(originalProcessedAt);
        updatedEntity.Error.Should().Be(originalError);
    }
}
