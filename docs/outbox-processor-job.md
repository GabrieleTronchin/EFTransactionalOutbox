# OutboxMessageProcessorJob

← [Back to README](../README.md)

## Overview

`OutboxMessageProcessorJob` is a Quartz.NET background job that polls the `OutboxMessages` table for unprocessed domain events, deserializes them, and publishes them via MediatR. It is the final stage of the Transactional Outbox Pattern, responsible for reliably dispatching events that were atomically persisted alongside business data.

**Source:** [`src/Sample.TransactionalOutbox/Job/OutboxMessageProcessorJob.cs`](../src/Sample.TransactionalOutbox/Job/OutboxMessageProcessorJob.cs)

## How It Works

The job runs on a recurring schedule (configured via Quartz.NET, typically every 10 seconds). Each execution follows this sequence:

1. **Poll** the `DomainEvents` table for messages where `CompleteTime` is null (unprocessed), taking up to 10 at a time
2. **Skip** if no unprocessed messages are found
3. **For each message**, attempt to:
   - Deserialize the `Content` field back to an `IDomainEvent` using Newtonsoft.Json
   - Publish the event via MediatR's `IPublisher.Publish`
4. **On success**, mark the message for removal
5. **On failure**, capture the exception message in the `Exception` field
6. **Always** set `CompleteTime` to `DateTime.UtcNow` (whether success or failure)
7. **Remove** all successfully processed messages from the table
8. **Save** all changes to the database

## Key Method

### Execute

```csharp
public async Task Execute(IJobExecutionContext context)
```

This is the Quartz.NET `IJob` entry point, called on each scheduled trigger. The method handles the full poll-deserialize-publish-cleanup cycle.

**Polling:**

```csharp
var messages = await _context.DomainEvents
    .Where(de => de.CompleteTime == null)
    .Take(DEFAULT_TAKE_DOMAINS)
    .ToListAsync(context.CancellationToken);
```

The query filters for unprocessed messages (`CompleteTime == null`) and limits the batch size to `DEFAULT_TAKE_DOMAINS` (10). This prevents the job from loading too many messages at once.

**Deserialization:**

```csharp
var domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(
    message.Content,
    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }
);
```

The `TypeNameHandling.Auto` setting reads the embedded type metadata written by the [OrderDomainEventInterceptor](outbox-interceptor.md) (which uses `TypeNameHandling.All`) to reconstruct the correct concrete event type (e.g., `OrderConfirmed`, `OrderCancelled`).

**Publishing:**

```csharp
await _publisher.Publish(domainEvent, context.CancellationToken);
```

The deserialized event is published via MediatR's `IPublisher`, which dispatches it to all registered `INotificationHandler<T>` implementations. For example, `OrderConfirmedEventHandler` handles `OrderConfirmed` events by decrementing the product quantity. The job also handles `OrderCancelled` events raised when an order is cancelled.

## Error Handling

The job uses a `try/catch/finally` block for each message to ensure robust processing:

| Scenario | Behavior |
|---|---|
| **Deserialization returns null** | Logs an error and skips the message (`continue`). The message is still marked with `CompleteTime` in the `finally` block. |
| **Deserialization throws** | The exception is caught, `Exception` field is set on the message, and `CompleteTime` is set. The message is **not** removed from the table. |
| **Publishing throws** | Same as deserialization failure — exception is captured, message is retained in the table for inspection. |
| **Success** | `CompleteTime` is set and the message is removed from the table after the loop. |

Failed messages remain in the database with their `Exception` field populated, allowing developers to inspect and diagnose issues. Successfully processed messages are cleaned up to keep the table lean.

## Concurrency

The class is decorated with `[DisallowConcurrentExecution]`:

```csharp
[DisallowConcurrentExecution]
public class OutboxMessageProcessorJob : IJob
```

This Quartz.NET attribute ensures that only one instance of the job runs at a time, even if a previous execution takes longer than the trigger interval. This prevents duplicate event publishing.

## Design Decisions

- **Batch processing**: The job processes up to 10 messages per execution rather than all unprocessed messages. This bounds memory usage and processing time per cycle.
- **Separate serialization settings**: The interceptor uses `TypeNameHandling.All` (writes type info for all objects) while the processor uses `TypeNameHandling.Auto` (reads type info when the declared type differs from the runtime type). Both settings are compatible for round-tripping polymorphic events.
- **Cleanup after loop**: Successfully processed messages are removed in a single `RemoveRange` call after the loop, followed by a single `SaveChangesAsync`. This batches the database writes for efficiency.
- **No retry mechanism**: Failed messages are marked with `CompleteTime` and retained with their exception. The current implementation does not automatically retry failed messages — they require manual intervention or a separate retry process.

## Dependencies

| Dependency | Purpose |
|---|---|
| `ShopDbContext` | Access to the `DomainEvents` table (outbox) |
| `IPublisher` (MediatR) | Publishing deserialized domain events to handlers |
| `ILogger<OutboxMessageProcessorJob>` | Logging debug and error information |

## Lifecycle in the Outbox Pattern

1. The [DomainEventManager](domain-event-manager.md) buffers events during business operations
2. The [OrderDomainEventInterceptor](outbox-interceptor.md) persists events to the outbox table during `SaveChanges`
3. **This job** polls the outbox table, deserializes events, and publishes them via MediatR
4. MediatR dispatches events to registered handlers (e.g., `OrderConfirmedEventHandler`)

> **Note:** The `OrderEntity` now uses an `OrderStatus` enum (`Pending`, `Confirmed`, `Cancelled`) instead of a `bool Confirmed` property. Both `ConfirmPayment()` and `CancelOrder()` raise domain events (`OrderConfirmed` and `OrderCancelled` respectively) that flow through this outbox pipeline. The [InboxMessageProcessorJob](inbox-processor-job.md) can also trigger this flow when processing incoming `PaymentConfirmed` messages.
