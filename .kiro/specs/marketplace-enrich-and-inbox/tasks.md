# Implementation Plan: Marketplace Enrich and Inbox

## Overview

This plan implements the marketplace enrichment and Transactional Inbox pattern in incremental steps. Each task builds on the previous, starting with domain model changes, then persistence, then API endpoints, and finally documentation. The approach ensures no orphaned code — every change is wired in before moving to the next step.

## Tasks

- [ ] 1. Enrich ProductEntity with catalog properties
  - [ ] 1.1 Update `ProductEntity` with new properties and factory method
    - Add `Name` (string), `Price` (decimal), `Sku` (string), `Description` (string?), `IsActive` (bool), `CreatedAt` (DateTime) properties with private setters
    - Update the `Create` factory method signature to `Create(string name, decimal price, string sku, int quantity, string? description = null)`
    - Set `IsActive = true` and `CreatedAt = DateTime.UtcNow` in the factory method
    - Add validation: throw `ArgumentException` for empty/whitespace `name`, non-positive `price`, empty/whitespace `sku`
    - Retain existing `Id`, `Quantity`, and `HasBeenConfirmed()` behavior
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [ ] 1.2 Update `ProductConfiguration` for new properties
    - Configure `Name`, `Price`, `Sku` as required properties
    - Configure `Description` as nullable
    - _Requirements: 1.1_

  - [ ]* 1.3 Update `ProductEntityTests` for enriched factory method
    - Update existing unit tests to use the new `Create(name, price, sku, quantity, description)` signature
    - Add tests for `ArgumentException` on empty/whitespace `name`, non-positive `price`, empty/whitespace `sku`
    - Add tests verifying `IsActive` defaults to true and `CreatedAt` is set
    - Update `DomainGenerators` to generate valid product parameters
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [ ] 2. Enrich OrderEntity with marketplace order properties
  - [ ] 2.1 Create `OrderStatus` enum and `OrderCancelled` domain event
    - Add `OrderStatus` enum with `Pending`, `Confirmed`, `Cancelled` values in the `Order/` folder
    - Add `OrderCancelled` record class implementing `IDomainEvent` with `OrderId` and `ProductId` properties in `Order/DomainEvents/`
    - _Requirements: 2.2, 2.9_

  - [ ] 2.2 Update `OrderEntity` with new properties and lifecycle methods
    - Replace `bool Confirmed` with `OrderStatus OrderStatus` property
    - Replace `string Description` with `Quantity` (int), `TotalAmount` (decimal), `CustomerName` (string), `ShippingAddress` (string?), `CreatedAt` (DateTime), `ConfirmedAt` (DateTime?) properties
    - Update `Create` factory method to `Create(Guid productId, int quantity, decimal totalAmount, string customerName, string? shippingAddress = null)` setting `OrderStatus = Pending` and `CreatedAt = DateTime.UtcNow`
    - Add validation: throw `ArgumentException` for empty/whitespace `customerName`, non-positive `quantity`, negative `totalAmount`
    - Update `ConfirmPayment()` to check `OrderStatus == Pending`, set `OrderStatus = Confirmed`, set `ConfirmedAt = DateTime.UtcNow`, raise `OrderConfirmed` event; throw `InvalidOperationException` if not Pending
    - Add `CancelOrder()` method: check `OrderStatus == Pending`, set `OrderStatus = Cancelled`, raise `OrderCancelled` event; throw `InvalidOperationException` if not Pending
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.10, 2.11, 2.12_

  - [ ] 2.3 Update `OrderConfiguration` for new properties
    - Configure `OrderStatus` stored as string conversion
    - Configure `CustomerName` as required, `ShippingAddress` as nullable, `ConfirmedAt` as nullable
    - _Requirements: 2.1_

  - [ ] 2.4 Update `OrderConfirmed` event to include `OrderId`
    - Update `OrderConfirmed` record to include both `OrderId` (Guid) and `ProductId` (Guid) to match the enriched domain
    - Update `OrderConfirmedEventHandler` to use the updated event shape
    - Update `OrderDomainEventInterceptor` if needed (it already handles generic domain events)
    - _Requirements: 2.5, 2.9_

  - [ ]* 2.5 Update `OrderEntityTests` for enriched entity
    - Update all existing tests to use the new `Create` signature and `OrderStatus` enum instead of `bool Confirmed`
    - Add tests for `CancelOrder()` — sets Cancelled status, raises `OrderCancelled` event, throws if not Pending
    - Add tests for validation: empty `customerName`, non-positive `quantity`, negative `totalAmount`
    - Update `DomainGenerators` to generate valid order parameters (customerName, quantity, totalAmount)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.10, 2.11, 2.12_

- [ ] 3. Checkpoint — Ensure domain layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Implement InboxMessageEntity and persistence
  - [ ] 4.1 Create `InboxMessageEntity` in the Domain layer
    - Create `InboxMessageEntity` class alongside `OutboxMessageEntity` with properties: `Id` (Guid), `MessageType` (string), `Payload` (string), `ReceivedAt` (DateTime), `ProcessedAt` (DateTime?), `Error` (string?)
    - _Requirements: 3.1_

  - [ ] 4.2 Add `InboxMessageEntity` to persistence layer
    - Add `DbSet<InboxMessageEntity> InboxMessages` to `ShopDbContext`
    - Create `InboxMessageConfiguration` in `Configuration/` folder following existing pattern
    - _Requirements: 3.1, 3.8_

  - [ ]* 4.3 Update `OrderDomainEventInterceptorTests` for enriched OrderEntity
    - Update all tests to use the new `OrderEntity.Create` signature
    - Verify interceptor still correctly persists outbox messages with the enriched entity
    - _Requirements: 3.8_

- [ ] 5. Update seed data with realistic marketplace products and orders
  - [ ] 5.1 Update `SeedDb` with enriched seed data
    - Update `SeedDb.Initialize` to use the new `ProductEntity.Create(name, price, sku, quantity, description)` signature
    - Seed at least 6 products with realistic marketplace data (e.g., "Wireless Headphones", "Mechanical Keyboard", "USB-C Hub") with prices, SKUs, descriptions, and quantities
    - Seed at least 2 orders in Pending status referencing seeded products with realistic customer names, quantities, total amounts, and shipping addresses
    - _Requirements: 4.1, 4.2, 4.3_

- [ ] 6. Implement InboxMessageProcessorJob
  - [ ] 6.1 Create `InboxMessageProcessorJob` in the API `Job/` folder
    - Follow the `OutboxMessageProcessorJob` pattern: implement `IJob`, decorate with `[DisallowConcurrentExecution]`
    - Inject `ShopDbContext`, `IOrderRepository`, and `ILogger<InboxMessageProcessorJob>`
    - Poll `InboxMessages` where `ProcessedAt == null`, take up to 10 messages
    - For each message: check idempotency (skip if `ProcessedAt != null`), handle `PaymentConfirmed` message type by deserializing payload to extract `OrderId`, look up order via repository, call `ConfirmPayment()`
    - On success: set `ProcessedAt = DateTime.UtcNow`, leave `Error` null
    - On failure: set `Error` to exception message, set `ProcessedAt = DateTime.UtcNow`
    - Save changes after processing the batch
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.9, 3.10_

  - [ ] 6.2 Register `InboxMessageProcessorJob` in DI and Quartz
    - Register the job in `Program.cs` Quartz configuration with a 10-second recurring schedule, consistent with the outbox job
    - _Requirements: 3.9_

  - [ ]* 6.3 Write unit tests for `InboxMessageProcessorJob`
    - Test successful processing of a `PaymentConfirmed` message (sets `ProcessedAt`, calls `ConfirmPayment`)
    - Test idempotency: already-processed messages are skipped
    - Test error handling: exception sets `Error` field and `ProcessedAt`
    - Test no-op when no unprocessed messages exist
    - Add tests in `test/Sample.TransactionalOutbox.Tests/InboxMessageProcessorJobTests.cs`
    - _Requirements: 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 7. Checkpoint — Ensure inbox pattern compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implement new and updated API endpoints
  - [ ] 8.1 Add `GET /Products/{id}` endpoint
    - Add endpoint in `Program.cs` that returns a single product by ID
    - Return 404 when product not found
    - Add `.WithName()`, `.WithSummary()`, `.WithDescription()`, `.Produces<ProductEntity>()`, `.ProducesProblem(404)` annotations
    - _Requirements: 5.2, 5.6, 5.9, 5.10_

  - [ ] 8.2 Add `POST /Orders/{id}/Cancel` endpoint
    - Add endpoint in `Program.cs` that cancels an order by ID
    - Return 404 when order not found, 409 when order is not in Pending status (catch `InvalidOperationException`)
    - Add full Swagger annotations
    - _Requirements: 5.1, 5.7, 5.8, 5.9, 5.10_

  - [ ] 8.3 Add `POST /Inbox/Receive` endpoint
    - Define `InboxReceiveRequest` record with `Id` (Guid), `MessageType` (string), `Payload` (string) — can be defined in `Program.cs` or a `Models/` folder
    - Add endpoint that accepts the request body and persists an `InboxMessageEntity` to the database
    - Implement idempotency: if an `InboxMessageEntity` with the same `Id` already exists, return 200 without creating a duplicate
    - Add full Swagger annotations
    - _Requirements: 5.3, 5.4, 5.5, 5.9, 5.10_

  - [ ] 8.4 Update existing `POST /PurchaseOrder/{id}` endpoint
    - Return 404 when order not found, 409 when order is not in Pending status
    - Add full `.Produces()` and `.ProducesProblem()` annotations
    - _Requirements: 5.11, 5.9, 5.10_

  - [ ] 8.5 Add Swagger annotations to existing `GET /Products` and `GET /Orders` endpoints
    - Add `.Produces<T>()` response type annotations to existing endpoints
    - Ensure `.WithSummary()` and `.WithDescription()` are present and descriptive
    - _Requirements: 5.9, 5.10_

- [ ] 9. Update .http file with all endpoints
  - [ ] 9.1 Update `Sample.TransactionalOutbox.http` with all API endpoints
    - Add `GET /Products/{id}` request with a sample product ID from seed data
    - Add `POST /Orders/{id}/Cancel` request with a sample order ID
    - Add `POST /Inbox/Receive` request with a realistic `PaymentConfirmed` JSON body example
    - Update existing requests to reflect enriched response shapes
    - Include descriptive comments for each request explaining purpose and expected behavior, consistent with existing style
    - _Requirements: 6.1, 6.2, 6.3_

- [ ] 10. Checkpoint — Ensure full solution builds and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Update README and documentation
  - [ ] 11.1 Update `README.md` for marketplace Orders microservice
    - Update project description to describe a marketplace Orders microservice implementing both Transactional Outbox and Inbox patterns
    - Add Mermaid diagrams showing both Outbox flow (events going out) and Inbox flow (messages coming in)
    - Update API Endpoints table to include `GET /Products/{id}`, `POST /Orders/{id}/Cancel`, and `POST /Inbox/Receive`
    - Update Key Components table to include `InboxMessageEntity` and `InboxMessageProcessorJob`
    - Update Project Structure table to reflect enriched entities and new Inbox components
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [ ] 11.2 Create Transactional Inbox documentation in `docs/`
    - Create `docs/inbox-processor-job.md` following the style of existing docs (e.g., `outbox-processor-job.md`)
    - Document the Inbox pattern, its implementation, lifecycle, idempotency guarantees, error handling, and relationship to the Outbox pattern
    - _Requirements: 7.6_

  - [ ] 11.3 Update existing docs for enriched domain model
    - Update `docs/domain-event-manager.md`, `docs/outbox-interceptor.md`, and `docs/outbox-processor-job.md` to reflect the enriched `OrderEntity` (`OrderStatus` enum replacing `bool Confirmed`) and the new `OrderCancelled` domain event where referenced
    - _Requirements: 7.7_

- [ ] 12. Final checkpoint — Ensure full solution builds and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after major milestones
- The implementation language is C# (.NET 10), matching the existing codebase
- Test framework: xunit, FsCheck.Xunit, FluentAssertions, NSubstitute
- No Correctness Properties section exists in the design, so property-based test tasks are not included
