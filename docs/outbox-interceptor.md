# OrderDomainEventInterceptor

← [Back to README](../README.md)

## Overview

`OrderDomainEventInterceptor` is an EF Core `SaveChangesInterceptor` that intercepts `SaveChangesAsync` calls to automatically persist domain events from tracked `OrderEntity` instances into the `OutboxMessages` table. This is the core mechanism that makes the Transactional Outbox Pattern work — events are written to the database in the same transaction as the business data changes, guaranteeing atomicity.

**Source:** [`src/Sample.TransactionalOutbox.Persistence/Interceptors/OrderDomainEventInterceptor.cs`](../src/Sample.TransactionalOutbox.Persistence/Interceptors/OrderDomainEventInterceptor.cs)

## How It Works

When `SaveChangesAsync` is called on the `ShopDbContext`, EF Core invokes the interceptor's `SavingChangesAsync` method before the actual save occurs. The interceptor then:

1. **Scans the Change Tracker** for all tracked `OrderEntity` instances
2. **Collects domain events** from each entity via `GetEvents()`
3. **Clears the events** from each entity via `ClearEvents()` to prevent duplicate persistence
4. **Maps each event** to an `OutboxMessageEntity` with a unique ID, timestamp, type name, and serialized content
5. **Adds the outbox messages** to the `DbContext` so they are saved in the same transaction
6. **Delegates to the base** `SavingChangesAsync` to continue the normal save pipeline

Because the outbox messages are added to the same `DbContext` before the save completes, they participate in the same database transaction as the entity changes. If the transaction fails, both the business data and the outbox messages are rolled back together.

## Key Method

### SavingChangesAsync

```csharp
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
```

This is the single override that implements the interception logic. The method:

- Returns early (delegates to base) if the `DbContext` is null
- Uses LINQ to flatten all events from all tracked `OrderEntity` instances
- Creates an `OutboxMessageEntity` for each event with:
  - `Id` — a new `Guid`
  - `CreationTime` — `DateTime.UtcNow`
  - `Type` — the event class name (e.g., `"OrderConfirmed"`)
  - `Content` — JSON-serialized event using Newtonsoft.Json with `TypeNameHandling.All`

The interceptor handles all domain events raised by `OrderEntity`, including `OrderConfirmed` (raised by `ConfirmPayment()`) and `OrderCancelled` (raised by `CancelOrder()`).

## Serialization

Events are serialized using Newtonsoft.Json with `TypeNameHandling.All`:

```csharp
Content = JsonConvert.SerializeObject(x, new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.All
})
```

`TypeNameHandling.All` embeds the full .NET type name in the JSON output. This is necessary because the [OutboxMessageProcessorJob](outbox-processor-job.md) deserializes the content back to `IDomainEvent`, and it needs the concrete type information to reconstruct the correct event class (e.g., `OrderConfirmed`).

> **Note:** `TypeNameHandling.All` is used for serialization in the interceptor, while the processor job uses `TypeNameHandling.Auto` for deserialization. Both settings preserve the type metadata needed for polymorphic round-tripping.

## Design Decisions

- **Interceptor over override**: Using a `SaveChangesInterceptor` keeps the outbox logic separate from the `DbContext` itself, following the single responsibility principle. The `ShopDbContext` does not need to know about domain event handling.
- **Eager collection and clear**: Events are collected and cleared from entities before the save completes. This prevents events from being persisted twice if `SaveChanges` is called multiple times on the same context.
- **Same-transaction persistence**: By adding `OutboxMessageEntity` records to the `DbContext` before the save, they are included in the same transaction. This is the fundamental guarantee of the outbox pattern — no events are lost and no phantom events are created.
- **Newtonsoft.Json over System.Text.Json**: The project uses Newtonsoft.Json because `TypeNameHandling` for polymorphic serialization is not equivalently supported in System.Text.Json.

## Lifecycle in the Outbox Pattern

1. Application code modifies an `OrderEntity` (e.g., `ConfirmPayment()` or `CancelOrder()`) and calls `SaveChangesAsync`
2. **This interceptor** collects events from the [DomainEventManager](domain-event-manager.md), serializes them, and adds `OutboxMessageEntity` records to the context
3. EF Core persists both the entity changes and the outbox messages in a single transaction
4. The [OutboxMessageProcessorJob](outbox-processor-job.md) later polls the outbox table and publishes the events
