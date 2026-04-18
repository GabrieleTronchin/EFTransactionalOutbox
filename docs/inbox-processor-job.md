# InboxMessageProcessorJob

← [Back to README](../README.md)

## Overview

`InboxMessageProcessorJob` is a Quartz.NET background job that polls the `InboxMessages` table for unprocessed external messages, checks idempotency, and executes the corresponding business logic. It is the processing stage of the Transactional Inbox Pattern — the complement to the [Transactional Outbox](outbox-processor-job.md). While the outbox reliably dispatches domain events *out* of the service, the inbox reliably processes messages coming *in* from external systems.

**Source:** [`src/Sample.TransactionalOutbox/Job/InboxMessageProcessorJob.cs`](../src/Sample.TransactionalOutbox/Job/InboxMessageProcessorJob.cs)

## The Transactional Inbox Pattern

The Transactional Inbox pattern solves the problem of reliably receiving and processing external messages in a microservices architecture. Without it, a service might:

- Process a message but crash before acknowledging it, leading to duplicate processing on retry
- Acknowledge a message but crash before processing it, leading to lost messages

The inbox pattern addresses this by:

1. **Persisting the message first** — When an external message arrives via `POST /Inbox/Receive`, it is immediately written to the `InboxMessages` table. This is the "receive" step.
2. **Processing asynchronously** — A background job polls for unprocessed messages and executes the business logic. This is the "process" step.
3. **Tracking processing state** — Each message has a `ProcessedAt` timestamp. Once set, the message is never processed again, guaranteeing idempotency.

## How It Works

The job runs on a recurring schedule (every 10 seconds, configured via Quartz.NET). Each execution follows this sequence:

1. **Poll** the `InboxMessages` table for messages where `ProcessedAt` is null, taking up to 10 at a time
2. **Skip** if no unprocessed messages are found
3. **For each message**:
   - Check idempotency — skip if `ProcessedAt` is already set (defensive check)
   - Route by `MessageType` to the appropriate handler method
   - Execute the business logic for that message type
4. **On success**: set `ProcessedAt` to `DateTime.UtcNow`, leave `Error` null
5. **On failure**: capture the exception message in the `Error` field, set `ProcessedAt` to `DateTime.UtcNow`
6. **Save** all changes to the database after the batch

## Key Method

### Execute

```csharp
public async Task Execute(IJobExecutionContext context)
```

This is the Quartz.NET `IJob` entry point, called on each scheduled trigger. The method handles the full poll-route-process cycle.

**Polling:**

```csharp
var messages = await _context
    .InboxMessages.Where(m => m.ProcessedAt == null)
    .Take(DEFAULT_TAKE_MESSAGES)
    .ToListAsync(context.CancellationToken);
```

The query filters for unprocessed messages (`ProcessedAt == null`) and limits the batch size to `DEFAULT_TAKE_MESSAGES` (10).

**Routing by message type:**

```csharp
switch (message.MessageType)
{
    case "PaymentConfirmed":
        await HandlePaymentConfirmed(message.Payload, context.CancellationToken);
        break;
    default:
        _logger.LogWarning($"Unknown inbox message type: {message.MessageType}...");
        break;
}
```

Each message type maps to a dedicated handler method. Unknown types are logged as warnings and marked as processed.

### HandlePaymentConfirmed

```csharp
private async Task HandlePaymentConfirmed(string payload, CancellationToken cancellationToken)
```

Deserializes the JSON payload to extract the `OrderId`, looks up the order via `IOrderRepository`, and calls `ConfirmPayment()` on the `OrderEntity`. This triggers the order's domain event (`OrderConfirmed`), which the [OrderDomainEventInterceptor](outbox-interceptor.md) persists to the outbox table during `SaveChanges` — connecting the inbox flow back to the outbox flow.

**Payload format:**

```json
{
  "OrderId": "guid-of-the-order"
}
```

## Idempotency Guarantees

Idempotency is enforced at two levels:

1. **API level** — The `POST /Inbox/Receive` endpoint checks if a message with the same `Id` already exists. If so, it returns HTTP 200 without creating a duplicate.
2. **Job level** — The job includes a defensive check (`if (message.ProcessedAt != null) continue`) to skip messages that may have been processed between the initial query and the loop iteration.

Together, these ensure that even if the same external message is delivered multiple times, the business logic executes exactly once.

## Error Handling

The job uses a `try/catch` block for each message:

| Scenario | Behavior |
|---|---|
| **Payload is null or invalid** | `HandlePaymentConfirmed` throws `InvalidOperationException`. The exception is caught, `Error` is set, `ProcessedAt` is set. |
| **Order not found** | The repository throws. The exception is caught, `Error` is set, `ProcessedAt` is set. |
| **Order not in Pending status** | `ConfirmPayment()` throws `InvalidOperationException`. The exception is caught, `Error` is set, `ProcessedAt` is set. |
| **Success** | `ProcessedAt` is set, `Error` is left null. |
| **Unknown message type** | Logged as a warning. `ProcessedAt` is set, `Error` is left null. |

Failed messages are marked as processed (with their `Error` field populated) to prevent infinite retry loops. They can be inspected in the database for diagnosis.

## Concurrency

The class is decorated with `[DisallowConcurrentExecution]`:

```csharp
[DisallowConcurrentExecution]
public class InboxMessageProcessorJob : IJob
```

This Quartz.NET attribute ensures that only one instance of the job runs at a time, even if a previous execution takes longer than the trigger interval. This prevents duplicate processing from concurrent job executions.

## Design Decisions

- **Batch processing**: The job processes up to 10 messages per execution, bounding memory usage and processing time per cycle. This is consistent with the [OutboxMessageProcessorJob](outbox-processor-job.md).
- **Single SaveChanges after batch**: All processing results (status updates, error captures, and any side effects from business logic) are saved in a single `SaveChangesAsync` call after the loop, batching database writes for efficiency.
- **Mark-as-processed on failure**: Failed messages have `ProcessedAt` set to prevent infinite retries. The `Error` field preserves the failure reason for manual inspection. A separate retry or dead-letter mechanism can be built on top if needed.
- **Message type routing via switch**: New message types can be added by extending the `switch` statement with additional cases and handler methods.
- **Newtonsoft.Json for deserialization**: Consistent with the outbox pattern's use of Newtonsoft.Json throughout the project.

## Relationship to the Outbox Pattern

The Inbox and Outbox patterns are complementary:

| Aspect | Outbox (Events Out) | Inbox (Messages In) |
|---|---|---|
| **Purpose** | Reliably dispatch domain events to handlers | Reliably process incoming external messages |
| **Trigger** | Business operation raises a domain event | External system sends a message via API |
| **Persistence** | Events written to `OutboxMessages` by the interceptor | Messages written to `InboxMessages` by the API endpoint |
| **Processing** | `OutboxMessageProcessorJob` deserializes and publishes via MediatR | `InboxMessageProcessorJob` deserializes and executes business logic |
| **Idempotency** | Messages removed after successful processing | `ProcessedAt` timestamp prevents reprocessing |

When the inbox job processes a `PaymentConfirmed` message and calls `ConfirmPayment()`, the resulting `OrderConfirmed` domain event flows through the outbox pattern — demonstrating how the two patterns work together in a real system.

## Dependencies

| Dependency | Purpose |
|---|---|
| `ShopDbContext` | Access to the `InboxMessages` table |
| `IOrderRepository` | Looking up orders by ID for business logic execution |
| `ILogger<InboxMessageProcessorJob>` | Logging debug, warning, and error information |

## Lifecycle in the Inbox Pattern

1. An external system sends a message to `POST /Inbox/Receive`
2. The API persists the message as an `InboxMessageEntity` in the `InboxMessages` table
3. **This job** polls for unprocessed messages, checks idempotency, and executes business logic
4. On success, the message is marked as processed; on failure, the error is captured
5. Any domain events raised during processing flow through the [Outbox pattern](outbox-processor-job.md)
