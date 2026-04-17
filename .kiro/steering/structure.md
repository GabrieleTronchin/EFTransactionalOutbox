# Project Structure

The solution follows a layered architecture with three projects under `src/`.

```
src/
├── Sample.TransactionalOutbox.sln
├── Sample.TransactionalOutbox/            # API layer (entry point)
│   ├── Program.cs                         # Minimal API endpoints, DI setup, middleware
│   ├── Job/
│   │   └── OutboxMessageProcessorJob.cs   # Quartz job that polls and dispatches outbox events
│   └── Properties/
│       └── launchSettings.json
├── Sample.TransactionalOutbox.Domain/     # Domain layer (no infrastructure dependencies)
│   ├── Primitives/
│   │   ├── DomainEventManager.cs          # Abstract base class for entities that raise events
│   │   ├── IDomainEvent.cs                # Marker interface extending MediatR INotification
│   │   └── IRepository.cs                 # Generic repository interface
│   ├── Order/
│   │   ├── OrderEntity.cs                 # Order aggregate with ConfirmPayment logic
│   │   ├── IOrderRepository.cs            # Order repository contract
│   │   └── DomainEvents/
│   │       └── OrderConfirmed.cs          # Domain event raised on order confirmation
│   ├── Product/
│   │   ├── ProductEntity.cs               # Product entity with quantity management
│   │   ├── IProductRepository.cs          # Product repository contract
│   │   └── EventHandlers/
│   │       └── OrderConfirmedEventHandler.cs  # MediatR handler that decrements product quantity
│   └── OutboxMessageEntity.cs             # Outbox message data model
└── Sample.TransactionalOutbox.Persistence/  # Infrastructure/persistence layer
    ├── ShopDbContext.cs                   # EF Core DbContext
    ├── ServicesExtensions.cs              # DI registration for persistence services
    ├── SeedDb.cs                          # Database seeding on startup
    ├── Configuration/
    │   ├── OrderConfiguration.cs          # EF entity config for OrderEntity
    │   └── ProductConfiguration.cs        # EF entity config for ProductEntity
    ├── OutboxMessageConfiguration.cs      # EF entity config for OutboxMessageEntity
    ├── Interceptors/
    │   └── OrderDomainEventInterceptor.cs # SaveChangesInterceptor that persists domain events to outbox
    └── Repository/
        ├── OrderRepository.cs             # Order repository implementation
        └── ProductRepository.cs           # Product repository implementation
```

## Layer Dependencies

```
API → Domain, Persistence
Persistence → Domain
Domain → (no project dependencies, only MediatR NuGet)
```

## Conventions

- **Entities** use private constructors with static `Create` factory methods for construction.
- **Domain events** are record classes implementing `IDomainEvent` (which extends MediatR `INotification`).
- **Event handlers** are `internal sealed` classes implementing `INotificationHandler<T>`, placed in `EventHandlers/` folders next to the entity they affect.
- **Repository interfaces** live in the Domain layer alongside their entity. Implementations are `internal` classes in the Persistence layer.
- **EF configurations** implement `IEntityTypeConfiguration<T>` and are auto-discovered via `ApplyConfigurationsFromAssembly`.
- **DI registration** for persistence services is centralized in `ServicesExtensions.AddPersistence()`.
- Endpoints are defined as Minimal API `MapGet`/`MapPost` calls directly in `Program.cs`.
