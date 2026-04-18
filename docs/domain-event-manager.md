# DomainEventManager

← [Back to README](../README.md)

## Overview

`DomainEventManager` is an abstract base class in the Domain layer that provides domain event management capabilities to any entity that inherits from it. It acts as a local event buffer, allowing domain entities to raise events during business operations and have those events collected later for persistence and dispatch.

In this project, `OrderEntity` inherits from `DomainEventManager`, which enables it to raise domain events during order lifecycle transitions — `OrderConfirmed` when a payment is confirmed and `OrderCancelled` when an order is cancelled.

**Source:** [`src/Sample.TransactionalOutbox.Domain/Primitives/DomainEventManager.cs`](../src/Sample.TransactionalOutbox.Domain/Primitives/DomainEventManager.cs)

## How It Works

`DomainEventManager` maintains a private list of `IDomainEvent` instances. Domain entities raise events by calling `RaiseEvent` during their business logic. These events are not dispatched immediately — they are held in memory until an external component (the [OrderDomainEventInterceptor](outbox-interceptor.md)) collects and persists them during `SaveChanges`.

This separation of concerns means the domain layer has no knowledge of how events are stored or published. It only knows how to buffer them.

## Key Methods

### RaiseEvent

```csharp
public void RaiseEvent(IDomainEvent domainEvent)
```

Adds a domain event to the internal event list. Called by the entity during a business operation. For example, `OrderEntity.ConfirmPayment()` calls `RaiseEvent(new OrderConfirmed(Id, ProductId))` to signal that an order has been confirmed, and `OrderEntity.CancelOrder()` calls `RaiseEvent(new OrderCancelled(Id, ProductId))` to signal cancellation.

Events are appended in the order they are raised, preserving the sequence of domain operations.

### GetEvents

```csharp
public IEnumerable<IDomainEvent> GetEvents()
```

Returns a snapshot (copy) of all currently buffered events. The returned list is a `ToList()` copy, so modifying it does not affect the internal state.

This method is called by the `OrderDomainEventInterceptor` during `SaveChangesAsync` to collect all pending events from tracked entities before persisting them to the outbox table.

### ClearEvents

```csharp
public void ClearEvents()
```

Removes all events from the internal list. Called by the interceptor after events have been collected, ensuring that events are not persisted more than once if `SaveChanges` is called again on the same entity instance.

## Design Decisions

- **Abstract class, not interface**: `DomainEventManager` is abstract because it encapsulates state (the event list) and behavior. An interface would require each entity to implement its own event storage.
- **In-memory buffering**: Events are held in a simple `IList<IDomainEvent>` rather than being dispatched immediately. This allows the interceptor to persist them atomically within the same database transaction as the entity changes.
- **Snapshot on read**: `GetEvents()` returns a `ToList()` copy to prevent external code from accidentally modifying the internal event list.
- **No dependency on infrastructure**: The class depends only on `IDomainEvent` (which extends MediatR's `INotification`), keeping the domain layer free of persistence or messaging concerns.

## Lifecycle in the Outbox Pattern

1. Entity calls `RaiseEvent(event)` during a business operation
2. EF Core's `SaveChangesAsync` is triggered
3. The [OrderDomainEventInterceptor](outbox-interceptor.md) calls `GetEvents()` on each tracked `OrderEntity`
4. The interceptor calls `ClearEvents()` to reset the buffer
5. Events are serialized and written to the `OutboxMessages` table in the same transaction
6. The [OutboxMessageProcessorJob](outbox-processor-job.md) later picks up and publishes the events
