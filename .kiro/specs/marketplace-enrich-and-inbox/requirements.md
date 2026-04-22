# Requirements Document

## Introduction

This feature enriches the existing Sample.TransactionalOutbox project to represent a realistic marketplace "Orders" microservice. The changes include richer domain entities (Product and Order), a new Transactional Inbox pattern complementing the existing Outbox, meaningful seed data, enriched API endpoints with full Swagger documentation, an updated .http file, and comprehensive documentation covering both patterns.

## Glossary

- **API**: The ASP.NET Core Minimal API layer that exposes HTTP endpoints for the marketplace Orders microservice.
- **ProductEntity**: The domain entity representing a product in the marketplace catalog, with name, price, SKU, description, quantity, and active status.
- **OrderEntity**: The domain entity representing a customer order, with quantity, total amount, status, customer information, and shipping address.
- **OrderStatus**: An enumeration representing the lifecycle state of an order: Pending, Confirmed, or Cancelled.
- **OutboxMessageEntity**: The existing entity that stores serialized domain events for reliable asynchronous dispatch via the Transactional Outbox pattern.
- **InboxMessageEntity**: A new entity that stores incoming external messages for idempotent processing via the Transactional Inbox pattern.
- **Inbox_Processor**: The background job or processing logic that reads unprocessed inbox messages and executes the corresponding business logic.
- **Seed_Data**: The initial set of products and orders inserted into the in-memory database on application startup.
- **Swagger_UI**: The interactive API documentation interface served at `/swagger` in Development mode.
- **HTTP_File**: The `.http` file containing example HTTP requests for all API endpoints, used for manual testing in IDEs.
- **DomainEventManager**: The abstract base class that provides domain event buffering (RaiseEvent, GetEvents, ClearEvents) to entities.
- **OrderDomainEventInterceptor**: The EF Core SaveChangesInterceptor that persists domain events from tracked OrderEntity instances into the OutboxMessages table within the same transaction.

## Requirements

### Requirement 1: Enrich ProductEntity with Catalog Properties

**User Story:** As a developer exploring the sample, I want ProductEntity to have realistic catalog properties (name, price, SKU, description, active status, creation timestamp), so that the domain model represents a credible marketplace product.

#### Acceptance Criteria

1. THE ProductEntity SHALL expose the following properties: `Name` (string, required), `Price` (decimal, required), `Description` (string, nullable), `Sku` (string, required), `IsActive` (bool, required), and `CreatedAt` (DateTime, required).
2. WHEN a ProductEntity is created via the static factory method, THE ProductEntity SHALL require `name`, `price`, `sku`, and `quantity` as parameters and set `IsActive` to true and `CreatedAt` to the current UTC time.
3. IF an empty or whitespace `name` is provided to the factory method, THEN THE ProductEntity SHALL throw an ArgumentException.
4. IF a negative or zero `price` is provided to the factory method, THEN THE ProductEntity SHALL throw an ArgumentException.
5. IF an empty or whitespace `sku` is provided to the factory method, THEN THE ProductEntity SHALL throw an ArgumentException.
6. THE ProductEntity SHALL retain the existing `Id` (Guid) and `Quantity` (int) properties and the `HasBeenConfirmed` method with its current behavior.

### Requirement 2: Enrich OrderEntity with Marketplace Order Properties

**User Story:** As a developer exploring the sample, I want OrderEntity to have realistic marketplace order properties (quantity, total amount, status enum, customer name, shipping address, timestamps), so that the domain model represents a credible order lifecycle.

#### Acceptance Criteria

1. THE OrderEntity SHALL expose the following properties: `Quantity` (int, required), `TotalAmount` (decimal, required), `CreatedAt` (DateTime, required), `ConfirmedAt` (DateTime, nullable), `OrderStatus` (OrderStatus enum, required), `CustomerName` (string, required), and `ShippingAddress` (string, nullable).
2. THE OrderStatus enum SHALL define three values: Pending, Confirmed, and Cancelled.
3. WHEN an OrderEntity is created via the static factory method, THE OrderEntity SHALL require `productId`, `quantity`, `totalAmount`, `customerName`, and optionally `shippingAddress`, and set `OrderStatus` to Pending and `CreatedAt` to the current UTC time.
4. THE OrderEntity SHALL replace the existing `bool Confirmed` property with the `OrderStatus` enum and the existing `string Description` property with the new properties listed in acceptance criterion 1.
5. WHEN `ConfirmPayment` is called on an OrderEntity with status Pending, THE OrderEntity SHALL set `OrderStatus` to Confirmed, set `ConfirmedAt` to the current UTC time, and raise an `OrderConfirmed` domain event.
6. IF `ConfirmPayment` is called on an OrderEntity that does not have status Pending, THEN THE OrderEntity SHALL throw an InvalidOperationException.
7. WHEN `CancelOrder` is called on an OrderEntity with status Pending, THE OrderEntity SHALL set `OrderStatus` to Cancelled and raise an `OrderCancelled` domain event.
8. IF `CancelOrder` is called on an OrderEntity that does not have status Pending, THEN THE OrderEntity SHALL throw an InvalidOperationException.
9. THE `OrderCancelled` domain event SHALL be a record class implementing IDomainEvent and SHALL contain the `OrderId` and `ProductId` of the cancelled order.
10. IF an empty or whitespace `customerName` is provided to the factory method, THEN THE OrderEntity SHALL throw an ArgumentException.
11. IF a zero or negative `quantity` is provided to the factory method, THEN THE OrderEntity SHALL throw an ArgumentException.
12. IF a negative `totalAmount` is provided to the factory method, THEN THE OrderEntity SHALL throw an ArgumentException.

### Requirement 3: Transactional Inbox Pattern

**User Story:** As a developer exploring the sample, I want the project to implement the Transactional Inbox pattern as the complement to the existing Outbox, so that I can learn how to reliably receive and process external messages with idempotency guarantees.

#### Acceptance Criteria

1. THE InboxMessageEntity SHALL expose the following properties: `Id` (Guid), `MessageType` (string), `Payload` (string), `ReceivedAt` (DateTime), `ProcessedAt` (DateTime, nullable), and `Error` (string, nullable).
2. WHEN an external message is received via the inbox receive endpoint, THE API SHALL persist an InboxMessageEntity to the database within a transaction.
3. WHEN the Inbox_Processor processes an unprocessed inbox message, THE Inbox_Processor SHALL check whether a message with the same `Id` has already been processed.
4. IF a message with the same `Id` has already been processed (ProcessedAt is not null), THEN THE Inbox_Processor SHALL skip the message without re-executing the business logic.
5. WHEN the Inbox_Processor successfully processes an inbox message of type "PaymentConfirmed", THE Inbox_Processor SHALL confirm the corresponding order by calling `ConfirmPayment` on the OrderEntity identified by the OrderId in the message payload.
6. IF the Inbox_Processor encounters an error while processing an inbox message, THEN THE Inbox_Processor SHALL set the `Error` field on the InboxMessageEntity and set `ProcessedAt` to the current UTC time.
7. WHEN the Inbox_Processor successfully processes an inbox message, THE Inbox_Processor SHALL set `ProcessedAt` to the current UTC time and leave the `Error` field null.
8. THE InboxMessageEntity SHALL be registered as a DbSet in the ShopDbContext.
9. THE Inbox_Processor SHALL run as a Quartz.NET background job on a recurring schedule, consistent with the existing OutboxMessageProcessorJob pattern.
10. THE Inbox_Processor SHALL be decorated with `[DisallowConcurrentExecution]` to prevent duplicate processing from concurrent job executions.

### Requirement 4: Seed Data with Realistic Marketplace Products

**User Story:** As a developer exploring the sample, I want the database to be seeded with realistic marketplace products and orders, so that the API returns meaningful data without manual setup.

#### Acceptance Criteria

1. WHEN the application starts, THE Seed_Data SHALL insert at least six products with realistic marketplace names (e.g. "Wireless Headphones", "Mechanical Keyboard", "USB-C Hub"), prices, SKUs, descriptions, and quantities.
2. WHEN the application starts, THE Seed_Data SHALL insert at least two orders in Pending status referencing seeded products, with realistic customer names, quantities, total amounts, and shipping addresses.
3. THE Seed_Data SHALL use the enriched ProductEntity and OrderEntity factory methods, ensuring all validation rules are satisfied.

### Requirement 5: Enriched API Endpoints with Swagger Documentation

**User Story:** As a developer exploring the sample, I want new API endpoints (cancel order, get product by ID, inbox receive) with full Swagger annotations, so that the API is self-documenting and covers the complete feature set.

#### Acceptance Criteria

1. THE API SHALL expose a `POST /Orders/{id}/Cancel` endpoint that cancels an order by ID.
2. THE API SHALL expose a `GET /Products/{id}` endpoint that returns a single product by ID.
3. THE API SHALL expose a `POST /Inbox/Receive` endpoint that accepts an external message payload and persists it as an InboxMessageEntity.
4. WHEN a request is made to `POST /Inbox/Receive`, THE API SHALL accept a JSON body containing `Id` (Guid), `MessageType` (string), and `Payload` (string).
5. WHEN a request is made to `POST /Inbox/Receive` with an `Id` that already exists in the inbox table, THE API SHALL return HTTP 200 (idempotent acceptance) without creating a duplicate record.
6. WHEN a request is made to `GET /Products/{id}` with a non-existent ID, THE API SHALL return HTTP 404.
7. WHEN a request is made to `POST /Orders/{id}/Cancel` with a non-existent ID, THE API SHALL return HTTP 404.
8. WHEN a request is made to `POST /Orders/{id}/Cancel` for an order that is not in Pending status, THE API SHALL return HTTP 409 (Conflict).
9. THE API SHALL annotate all endpoints (existing and new) with `.WithSummary()` and `.WithDescription()` providing clear explanations of each endpoint's purpose and behavior.
10. THE API SHALL annotate all endpoints with proper response types using `.Produces<T>()` and `.ProducesProblem()` for error status codes.
11. THE existing `POST /PurchaseOrder/{id}` endpoint SHALL be updated to return HTTP 404 when the order is not found and HTTP 409 when the order is not in Pending status.

### Requirement 6: Update .http File with All Endpoints

**User Story:** As a developer exploring the sample, I want the .http file to include example requests for all API endpoints, so that I can quickly test the full feature set from my IDE.

#### Acceptance Criteria

1. THE HTTP_File SHALL contain example requests for all API endpoints: `GET /Products`, `GET /Products/{id}`, `GET /Orders`, `POST /PurchaseOrder/{id}`, `POST /Orders/{id}/Cancel`, and `POST /Inbox/Receive`.
2. THE HTTP_File SHALL include descriptive comments for each request explaining its purpose and expected behavior, consistent with the existing comment style.
3. THE HTTP_File SHALL include a realistic JSON body example for the `POST /Inbox/Receive` endpoint with a sample PaymentConfirmed message.

### Requirement 7: Update README and Documentation

**User Story:** As a developer exploring the sample, I want the README and docs/ folder to explain that this is a marketplace Orders microservice implementing both Transactional Outbox and Inbox patterns, so that the project narrative is clear and complete.

#### Acceptance Criteria

1. THE README SHALL describe the project as a sample marketplace Orders microservice implementing both the Transactional Outbox and Transactional Inbox patterns using C#, Entity Framework Core, MediatR, and Quartz.NET.
2. THE README SHALL include a Mermaid diagram showing both the Outbox pattern flow (domain events going out) and the Inbox pattern flow (external messages coming in).
3. THE README SHALL document all API endpoints including the new ones (cancel order, get product by ID, inbox receive).
4. THE README SHALL update the Key Components table to include the InboxMessageEntity and the Inbox_Processor job.
5. THE README SHALL update the Project Structure table to reflect the enriched entities and new Inbox components.
6. THE docs/ folder SHALL contain a new document explaining the Transactional Inbox pattern, its implementation, and its lifecycle, following the same style as the existing outbox documentation files.
7. THE existing docs/ files SHALL be updated to reflect the enriched OrderEntity (OrderStatus enum replacing bool Confirmed) and the new OrderCancelled domain event where referenced.
