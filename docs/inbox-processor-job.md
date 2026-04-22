# InboxMessageProcessorJob

← [Back to README](../README.md)

## Overview

`InboxMessageProcessorJob` is a Quartz.NET background job that polls the `InboxMessages` table for unprocessed external messages and dispatches them to their corresponding MediatR handlers via a generic, registry-based routing mechanism. It is the processing stage of the Transactional Inbox Pattern — the complement to the [Transactional Outbox](outbox-processor-job.md). While the outbox reliably dispatches domain events *out* of the service, the inbox reliably processes messages coming *in* from external systems.

The job is fully decoupled from specific message types: it uses a `MessageTypeRegistry` to resolve CLR types and MediatR `IPublisher` to dispatch deserialized `IInboxMessage` notifications to their handlers. This mirrors the [OutboxMessageProcessorJob](outbox-processor-job.md) pattern.

**Source:** [`src/Sample.TransactionalOutbox/Job/InboxMessageProcessorJob.cs`](../src/Sample.TransactionalOutbox/Job/InboxMessageProcessorJob.cs)

## The Transactional Inbox Pattern

The Transactional Inbox pattern solves the problem of reliably receiving and processing external messages in a microservices architecture. Without it, a service might:

- Process a message but crash before acknowledging it, leading to duplicate processing on retry
- Acknowledge a message but crash before processing it, leading to lost messages

The inbox pattern addresses this by:

1. **Persisting the message first** — When an external message arrives via `POST /Inbox/Receive`, it is immediately written to the `InboxMessages` table via `IInboxMessageRepository`. This is the "receive" step.
2. **Processing asynchronously** — A background job polls for unprocessed messages and dispatches them via MediatR. This is the "process" step.
3. **Tracking processing state** — Each message has a `ProcessedAt` timestamp. Once set, the message is never processed again, guaranteeing idempotency.

## How It Works

The job runs on a recurring schedule (every 10 seconds, configured via Quartz.NET). Each execution follows this sequence:

1. **Poll** the `InboxMessages` table for messages where `ProcessedAt` is null, taking up to 10 at a time
2. **Skip** if no unprocessed messages are found
3. **For each message**:
   - Check idempotency — skip if `ProcessedAt` is already set (defensive check)
   - Resolve the CLR type via `MessageTypeRegistry.Resolve(message.MessageType)`
   - If no mapping exists — log a warning, set `ProcessedAt`, continue
   - Deserialize the payload into the resolved `IInboxMessage` type via Newtonsoft.Json
   - Publish the message via MediatR `IPublisher.Publish()`
4. **On success**: set `ProcessedAt` to `DateTime.UtcNow`, clear `Error`
5. **On failure**: capture the exception message in the `Error` field, set `ProcessedAt` to `DateTime.UtcNow`
6. **Save** all changes to the database after the batch

## Key Method

### Execute

```csharp
public async Task Execute(IJobExecutionContext context)
```

This is the Quartz.NET `IJob` entry point, called on each scheduled trigger. The method handles the full poll-resolve-deserialize-publish cycle.

**Polling:**

```csharp
var messages = await _context
    .InboxMessages.Where(m => m.ProcessedAt == null)
    .Take(DEFAULT_TAKE_MESSAGES)
    .ToListAsync(context.CancellationToken);
```

The query filters for unprocessed messages (`ProcessedAt == null`) and limits the batch size to `DEFAULT_TAKE_MESSAGES` (10).

**Type resolution and dispatch:**

```csharp
var type = _registry.Resolve(message.MessageType);

if (type == null)
{
    _logger.LogWarning($"Unknown inbox message type: {message.MessageType}. Message Id: {message.Id}");
    message.ProcessedAt = DateTime.UtcNow;
    continue;
}

var inboxMessage = (IInboxMessage)JsonConvert.DeserializeObject(message.Payload, type)!;

await _publisher.Publish(inboxMessage, context.CancellationToken);
```

The `MessageTypeRegistry` maps the `MessageType` string to a CLR type implementing `IInboxMessage`. The payload is deserialized into that type and published via MediatR, which dispatches it to the registered `INotificationHandler<T>`.

## Message Type Registration

Message types are registered at startup in `Program.cs`:

```csharp
var registry = new MessageTypeRegistry();
registry.Register<PaymentConfirmedInboxMessage>("PaymentConfirmed");
builder.Services.AddSingleton(registry);
```

To add a new message type:
1. Create a record implementing `IInboxMessage` (e.g., `public sealed record MyMessage(Guid Id) : IInboxMessage`)
2. Create an `INotificationHandler<MyMessage>` handler
3. Register the mapping in `Program.cs`: `registry.Register<MyMessage>("MyMessageType")`

No changes to the job itself are needed.

## Inbox Handlers

Business logic for each message type is encapsulated in dedicated MediatR handlers. For example, `PaymentConfirmedHandler` handles `PaymentConfirmedInboxMessage`:

- Validates that `OrderId` is not `Guid.Empty` (throws `InvalidOperationException` if so)
- Looks up the order via `IOrderRepository`
- Calls `order.ConfirmPayment()`, which triggers the `OrderConfirmed` domain event
- The domain event flows through the [Outbox pattern](outbox-processor-job.md), connecting inbox processing back to outbox dispatch

**Payload format for PaymentConfirmed:**

```json
{
  "OrderId": "guid-of-the-order"
}
```

## Idempotency Guarantees

Idempotency is enforced at two levels:

1. **API level** — The `POST /Inbox/Receive` endpoint delegates to `IInboxMessageRepository`, which checks if a message with the same `Id` already exists. If so, it returns `false` (duplicate) and the endpoint returns HTTP 200 without creating a duplicate.
2. **Job level** — The job includes a defensive check (`if (message.ProcessedAt != null) continue`) to skip messages that may have been processed between the initial query and the loop iteration.

Together, these ensure that even if the same external message is delivered multiple times, the business logic executes exactly once.

## Error Handling

The job uses a `try/catch` block for each message:

| Scenario | Behavior |
|---|---|
| **No registry mapping** | Logged as a warning. `ProcessedAt` is set, `Error` is left null. |
| **Deserialization fails** | The exception is caught, `Error` is set, `ProcessedAt` is set. |
| **Handler throws** (e.g., order not found, invalid state) | The exception is caught, `Error` is set, `ProcessedAt` is set. |
| **Success** | `ProcessedAt` is set, `Error` is cleared. |

Failed messages are marked as processed (with their `Error` field populated) to prevent infinite retry loops. They can be inspected in the database for diagnosis.

## Concurrency

The class is decorated with `[DisallowConcurrentExecution]`:

```csharp
[DisallowConcurrentExecution]
public class InboxMessageProcessorJob : IJob
```

This Quartz.NET attribute ensures that only one instance of the job runs at a time, even if a previous execution takes longer than the trigger interval. This prevents duplicate processing from concurrent job executions.

## Design Decisions

- **Generic dispatch via MediatR**: The job is fully decoupled from specific message types. It uses `MessageTypeRegistry` for type resolution and `IPublisher` for dispatch, mirroring the `OutboxMessageProcessorJob` pattern. New message types require no changes to the job.
- **Batch processing**: The job processes up to 10 messages per execution, bounding memory usage and processing time per cycle. This is consistent with the [OutboxMessageProcessorJob](outbox-processor-job.md).
- **Single SaveChanges after batch**: All processing results (status updates, error captures) are saved in a single `SaveChangesAsync` call after the loop, batching database writes for efficiency.
- **Mark-as-processed on failure**: Failed messages have `ProcessedAt` set to prevent infinite retries. The `Error` field preserves the failure reason for manual inspection. A separate retry or dead-letter mechanism can be built on top if needed.
- **Newtonsoft.Json for deserialization**: Consistent with the outbox pattern's use of Newtonsoft.Json throughout the project.
- **Handlers in the Domain layer**: Inbox handlers (e.g., `PaymentConfirmedHandler`) are `internal sealed` classes in the Domain layer, following the same convention as `OrderConfirmedEventHandler`. MediatR discovers them via assembly scanning.

## Relationship to the Outbox Pattern

The Inbox and Outbox patterns are complementary:

| Aspect | Outbox (Events Out) | Inbox (Messages In) |
|---|---|---|
| **Purpose** | Reliably dispatch domain events to handlers | Reliably process incoming external messages |
| **Trigger** | Business operation raises a domain event | External system sends a message via API |
| **Persistence** | Events written to `OutboxMessages` by the interceptor | Messages written to `InboxMessages` by `IInboxMessageRepository` |
| **Processing** | `OutboxMessageProcessorJob` deserializes and publishes via MediatR | `InboxMessageProcessorJob` resolves type, deserializes, and publishes via MediatR |
| **Idempotency** | Messages removed after successful processing | `ProcessedAt` timestamp prevents reprocessing |

When the inbox job processes a `PaymentConfirmed` message, MediatR dispatches it to `PaymentConfirmedHandler`, which calls `ConfirmPayment()`. The resulting `OrderConfirmed` domain event flows through the outbox pattern — demonstrating how the two patterns work together in a real system.

## Dependencies

| Dependency | Purpose |
|---|---|
| `ShopDbContext` | Access to the `InboxMessages` table |
| `IPublisher` (MediatR) | Publishing deserialized inbox messages to their handlers |
| `MessageTypeRegistry` | Resolving `MessageType` strings to CLR types implementing `IInboxMessage` |
| `ILogger<InboxMessageProcessorJob>` | Logging debug, warning, and error information |

## Lifecycle in the Inbox Pattern

1. An external system sends a message to `POST /Inbox/Receive`
2. The `InboxEndpoint` delegates to `IInboxMessageRepository`, which persists the message as an `InboxMessageEntity`
3. **This job** polls for unprocessed messages, resolves types via `MessageTypeRegistry`, deserializes payloads, and publishes via MediatR
4. MediatR dispatches to the registered handler (e.g., `PaymentConfirmedHandler`)
5. On success, the message is marked as processed; on failure, the error is captured
6. Any domain events raised during handler execution flow through the [Outbox pattern](outbox-processor-job.md)
