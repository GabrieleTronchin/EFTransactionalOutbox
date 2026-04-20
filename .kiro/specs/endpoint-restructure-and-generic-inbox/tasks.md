# Implementation Plan: Endpoint Restructure and Generic Inbox

## Overview

This plan restructures the application in two areas: (1) extracting inline Minimal API endpoints from `Program.cs` into dedicated `IEndpoint` classes with auto-discovery, and (2) making `InboxMessageProcessorJob` generic via MediatR and a `MessageTypeRegistry`. Tasks are ordered so each step builds on the previous one, with no orphaned code. Domain-layer types are created first, then persistence, then API-layer wiring, and finally the refactored inbox job and handler.

## Tasks

- [x] 1. Create domain-layer foundation types
  - [x] 1.1 Create the `IInboxMessage` interface in `Sample.TransactionalOutbox.Domain/Primitives/IInboxMessage.cs`
    - Define `IInboxMessage` extending MediatR `INotification`
    - Place alongside existing `IDomainEvent.cs`
    - _Requirements: 8.1, 8.2_

  - [x] 1.2 Create the `MessageTypeRegistry` class in `Sample.TransactionalOutbox.Domain/Inbox/MessageTypeRegistry.cs`
    - Implement `Register<T>(string messageType)` with `where T : IInboxMessage` constraint, returning `this` for fluent chaining
    - Implement `Resolve(string messageType)` returning `Type?` (null if not registered)
    - Use a `Dictionary<string, Type>` internally
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 1.3 Write property test for MessageTypeRegistry register-then-resolve round-trip
    - **Property 3: MessageTypeRegistry register-then-resolve round-trip**
    - Create test in `test/Sample.TransactionalOutbox.Domain.Tests/Inbox/MessageTypeRegistryTests.cs`
    - For any (messageType string, CLR type implementing IInboxMessage) pair, after registering, `Resolve` returns the exact registered type
    - **Validates: Requirements 9.1, 9.2**

  - [x] 1.4 Write property test for MessageTypeRegistry unregistered keys
    - **Property 4: MessageTypeRegistry returns null for unregistered keys**
    - Add test in the same `MessageTypeRegistryTests.cs` file
    - For any string not registered, `Resolve` returns null
    - **Validates: Requirements 9.3**

  - [x] 1.5 Create `PaymentConfirmedInboxMessage` record in `Sample.TransactionalOutbox.Domain/Inbox/PaymentConfirmedInboxMessage.cs`
    - Define as `public sealed record PaymentConfirmedInboxMessage(Guid OrderId) : IInboxMessage`
    - _Requirements: 11.5_

  - [x] 1.6 Create `IInboxMessageRepository` interface in `Sample.TransactionalOutbox.Domain/Inbox/IInboxMessageRepository.cs`
    - Define `Task<bool> ReceiveAsync(Guid id, string messageType, string payload, CancellationToken cancellationToken)`
    - Returns `true` if new message persisted, `false` if duplicate
    - _Requirements: 4.1, 4.2_

- [x] 2. Checkpoint - Ensure the solution builds
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Implement persistence layer for inbox repository
  - [x] 3.1 Create `InboxMessageRepository` in `Sample.TransactionalOutbox.Persistence/Repository/InboxMessageRepository.cs`
    - Implement as `internal class InboxMessageRepository : IInboxMessageRepository`
    - Inject `ShopDbContext`
    - Check for existing message by Id using `AsNoTracking().AnyAsync()`
    - If duplicate, return `false`
    - If new, create `InboxMessageEntity` with `ReceivedAt = DateTime.UtcNow`, add to context, call `SaveChangesAsync`, return `true`
    - _Requirements: 4.3, 4.4, 4.5_

  - [x] 3.2 Register `InboxMessageRepository` in `ServicesExtensions.AddPersistence()`
    - Add `services.AddTransient<IInboxMessageRepository, InboxMessageRepository>()` in `Sample.TransactionalOutbox.Persistence/ServicesExtensions.cs`
    - _Requirements: 4.6_

  - [x] 3.3 Write property test for inbox message persistence round-trip
    - **Property 1: Inbox message persistence round-trip**
    - Create test in `test/Sample.TransactionalOutbox.Persistence.Tests/Repository/InboxMessageRepositoryTests.cs`
    - For any valid (id, messageType, payload) where id does not exist, `ReceiveAsync` persists an entity whose fields match the input and `ReceivedAt` is non-default
    - Use in-memory EF Core provider
    - **Validates: Requirements 4.4**

  - [x] 3.4 Write property test for inbox message receive idempotency
    - **Property 2: Inbox message receive idempotency**
    - Add test in the same `InboxMessageRepositoryTests.cs` file
    - Calling `ReceiveAsync` twice with the same id returns `true` then `false`, and the database contains exactly one entity with that id
    - **Validates: Requirements 4.3**

- [x] 4. Checkpoint - Ensure the solution builds and persistence tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Create request DTOs and IEndpoint interface
  - [x] 5.1 Create request DTO records in `Sample.TransactionalOutbox/Contracts/CreateOrderRequest.cs` and `Sample.TransactionalOutbox/Contracts/InboxReceiveRequest.cs`
    - Move `CreateOrderRequest` and `InboxReceiveRequest` record definitions from `Program.cs` into separate files under `Contracts/`
    - Use namespace `Sample.TransactionalOutbox.Contracts`
    - _Requirements: 13.1, 13.2, 13.3_

  - [x] 5.2 Create the `IEndpoint` interface in `Sample.TransactionalOutbox/Endpoints/IEndpoint.cs`
    - Define `void MapEndpoint(IEndpointRouteBuilder app)`
    - Use namespace `Sample.TransactionalOutbox.Endpoints`
    - _Requirements: 1.1, 1.2_

  - [x] 5.3 Create the `ServiceExtension` class in `Sample.TransactionalOutbox/Endpoints/ServiceExtension.cs`
    - Implement `AddEndpoints(this IServiceCollection services, Assembly assembly)` — scans assembly for non-abstract `IEndpoint` implementations, registers each as transient
    - Implement `MapEndpoints(this WebApplication app)` — resolves all `IEndpoint` instances from DI and calls `MapEndpoint` on each
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 6. Implement endpoint classes
  - [x] 6.1 Create `ProductsEndpoint` in `Sample.TransactionalOutbox/Endpoints/ProductsEndpoint.cs`
    - Implement `IEndpoint`
    - Register `GET /Products` and `GET /Products/{id}` routes
    - Use `IProductRepository` for data access
    - Return HTTP 404 with ProblemDetails when product not found
    - Preserve all existing Swagger metadata (WithName, WithSummary, WithDescription, Produces)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 6.2 Create `OrdersEndpoint` in `Sample.TransactionalOutbox/Endpoints/OrdersEndpoint.cs`
    - Implement `IEndpoint`
    - Register `GET /Orders`, `POST /Orders`, `POST /PurchaseOrder/{id}` (at root level, not under group), `POST /Orders/{id}/Cancel`
    - Use `IOrderRepository` and `IProductRepository` for data access
    - Preserve all existing HTTP status codes, ProblemDetails responses, and Swagger metadata
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [x] 6.3 Create `InboxEndpoint` in `Sample.TransactionalOutbox/Endpoints/InboxEndpoint.cs`
    - Implement `IEndpoint`
    - Register `POST /Inbox/Receive` route
    - Delegate to `IInboxMessageRepository.ReceiveAsync()` — no direct `ShopDbContext` access
    - Return HTTP 200 for both new and duplicate messages
    - Preserve existing Swagger metadata
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [x] 7. Clean up Program.cs and wire endpoint discovery
  - Remove all inline `MapGet`/`MapPost` endpoint definitions from `Program.cs`
  - Remove the `InboxReceiveRequest` and `CreateOrderRequest` record definitions from the bottom of `Program.cs`
  - Add `builder.Services.AddEndpoints(typeof(Program).Assembly)` for endpoint registration
  - Add `app.MapEndpoints()` to map all discovered endpoints
  - Register `MessageTypeRegistry` as singleton with `PaymentConfirmed` mapping
  - Update MediatR assembly scanning to include the Domain assembly (for inbox handlers)
  - Retain all existing non-endpoint configuration (DI, Quartz, middleware, seeding)
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 12.1, 12.2_

- [x] 8. Checkpoint - Ensure the solution builds and all existing routes still work
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement generic inbox processing
  - [x] 9.1 Create `PaymentConfirmedHandler` in `Sample.TransactionalOutbox.Domain/Inbox/Handlers/PaymentConfirmedHandler.cs`
    - Implement `INotificationHandler<PaymentConfirmedInboxMessage>` as `internal sealed class`
    - Inject `IOrderRepository` and `ILogger<PaymentConfirmedHandler>`
    - Validate `OrderId` is not `Guid.Empty`, throw `InvalidOperationException` if so
    - Look up order via `IOrderRepository.GetAsync()` and call `order.ConfirmPayment()`
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [x] 9.2 Refactor `InboxMessageProcessorJob` to use MediatR and MessageTypeRegistry
    - Change constructor dependencies from `(ILogger, ShopDbContext, IOrderRepository)` to `(ILogger, ShopDbContext, IPublisher, MessageTypeRegistry)`
    - Remove the `switch` statement and `HandlePaymentConfirmed` private method
    - For each unprocessed message: skip if `ProcessedAt` is set, resolve type via `MessageTypeRegistry.Resolve()`, log warning if no mapping, deserialize payload via `Newtonsoft.Json`, publish via `IPublisher.Publish()`
    - Preserve error handling: on exception set `Error` and `ProcessedAt`
    - Preserve batch processing: poll up to 10 unprocessed messages
    - Call `SaveChangesAsync` at end of batch
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9_

  - [x] 9.3 Write property test for inbox message deserialization round-trip
    - **Property 5: Inbox message deserialization round-trip**
    - Create test in `test/Sample.TransactionalOutbox.Tests/InboxMessageProcessorJobPropertyTests.cs`
    - For any valid `PaymentConfirmedInboxMessage` with random non-empty OrderId, serialize to JSON, store as payload, run the job, and verify MediatR publishes a message equal to the original
    - Use mock `IPublisher` to capture published messages
    - **Validates: Requirements 10.2**

  - [x] 9.4 Write property test for InboxMessageProcessorJob error handling
    - **Property 6: InboxMessageProcessorJob error handling preserves error details**
    - Add test in the same `InboxMessageProcessorJobPropertyTests.cs` file
    - For any message whose processing throws an exception, the entity's `Error` field contains the exception message and `ProcessedAt` is non-null
    - **Validates: Requirements 10.7**

  - [x] 9.5 Write property test for InboxMessageProcessorJob skipping processed messages
    - **Property 7: InboxMessageProcessorJob skips already-processed messages**
    - Add test in the same `InboxMessageProcessorJobPropertyTests.cs` file
    - For any message where `ProcessedAt` is already set, the job does not invoke `IPublisher.Publish()` and the entity's fields remain unchanged
    - **Validates: Requirements 10.8**

  - [x] 9.6 Write unit tests for InboxMessageProcessorJob edge cases
    - Create/extend tests in `test/Sample.TransactionalOutbox.Tests/InboxMessageProcessorJobTests.cs`
    - Test unknown MessageType: job logs warning, sets `ProcessedAt` (Req 10.4)
    - Test batch limit of 10 messages per execution (Req 10.9)
    - Test `PaymentConfirmedHandler` with empty OrderId throws `InvalidOperationException` (Req 11.4)
    - _Requirements: 10.4, 10.9, 11.4_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after each major phase
- Property tests validate universal correctness properties from the design document (Properties 1–7)
- Unit tests validate specific examples and edge cases
- The design uses C# throughout — no language selection was needed
- Test projects already exist in the solution with FsCheck.Xunit, xUnit, FluentAssertions, and NSubstitute
