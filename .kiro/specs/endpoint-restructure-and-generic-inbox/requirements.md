# Requirements Document

## Introduction

This feature restructures the project in two areas:

1. **Endpoint reorganization**: the Minimal API endpoints currently defined inline in `Program.cs` are moved into separate classes organized by topic (Products, Orders, Inbox), following the `IEndpoint` pattern with auto-discovery via assembly scanning. `Program.cs` becomes clean and declarative.

2. **Generic inbox via MediatR**: the `InboxMessageProcessorJob` currently contains a hardcoded `switch` by message type with embedded business logic. It is made generic using MediatR to decouple message routing from handling logic, symmetrically to how `OutboxMessageProcessorJob` already uses MediatR to publish domain events.

## Glossary

- **Endpoint_Class**: a class that implements the `IEndpoint` interface and groups related Minimal API endpoints by topic (e.g., Products, Orders, Inbox)
- **IEndpoint**: interface with a method `void MapEndpoint(IEndpointRouteBuilder app)` that each Endpoint_Class implements to register its routes
- **Endpoint_Discovery**: assembly scanning mechanism that automatically finds all `IEndpoint` implementations and registers them in the DI container
- **Program_Cs**: the `Program.cs` file, the ASP.NET Core application entry point
- **InboxMessageProcessorJob**: Quartz.NET background job that processes unprocessed inbox messages
- **OutboxMessageProcessorJob**: Quartz.NET background job that processes outgoing domain events via MediatR
- **IInboxMessage**: MediatR interface (extends `INotification`) representing a deserialized inbox message ready to be handled
- **Inbox_Handler**: MediatR `INotificationHandler<T>` class that handles the business logic for a specific inbox message type
- **MessageType_Registry**: mechanism that maps the `MessageType` string field of an `InboxMessageEntity` to the corresponding CLR type implementing `IInboxMessage`
- **InboxMessageEntity**: EF Core entity representing an inbox message persisted in the `InboxMessages` table
- **MediatR_Publisher**: the MediatR `IPublisher` interface used to publish in-process notifications
- **ShopDbContext**: the application's EF Core DbContext
- **IInboxMessageRepository**: interface in the Domain layer defining the contract for inbox message persistence (idempotency check, saving)
- **InboxMessageRepository**: implementation in the Persistence layer of `IInboxMessageRepository`, responsible for duplicate checking and persisting the `InboxMessageEntity`

## Requirements

### Requirement 1: IEndpoint Interface

**User Story:** As a developer, I want a contract interface for endpoint definitions, so that every endpoint group follows a uniform structure and is auto-discoverable.

#### Acceptance Criteria

1. THE IEndpoint interface SHALL define a single method `void MapEndpoint(IEndpointRouteBuilder app)`
2. THE IEndpoint interface SHALL be located in the API project namespace `Sample.TransactionalOutbox`

### Requirement 2: Products Endpoint_Class

**User Story:** As a developer, I want the Products endpoints grouped in a dedicated class, so that I can find and maintain them easily.

#### Acceptance Criteria

1. THE ProductsEndpoint class SHALL implement the IEndpoint interface
2. WHEN `MapEndpoint` is called, THE ProductsEndpoint SHALL register the `GET /Products` route that returns all products
3. WHEN `MapEndpoint` is called, THE ProductsEndpoint SHALL register the `GET /Products/{id}` route that returns a single product by identifier
4. WHEN a product with the requested identifier does not exist, THE ProductsEndpoint SHALL return HTTP 404 with a ProblemDetails response
5. THE ProductsEndpoint SHALL preserve the existing Swagger metadata (WithName, WithSummary, WithDescription, Produces) for each route

### Requirement 3: Orders Endpoint_Class

**User Story:** As a developer, I want the Orders endpoints grouped in a dedicated class, so that order logic is separated from other endpoints.

#### Acceptance Criteria

1. THE OrdersEndpoint class SHALL implement the IEndpoint interface
2. WHEN `MapEndpoint` is called, THE OrdersEndpoint SHALL register the `GET /Orders` route that returns all orders
3. WHEN `MapEndpoint` is called, THE OrdersEndpoint SHALL register the `POST /Orders` route that creates a new order
4. WHEN `MapEndpoint` is called, THE OrdersEndpoint SHALL register the `POST /PurchaseOrder/{id}` route that confirms an order
5. WHEN `MapEndpoint` is called, THE OrdersEndpoint SHALL register the `POST /Orders/{id}/Cancel` route that cancels an order
6. THE OrdersEndpoint SHALL preserve the existing HTTP status codes and ProblemDetails responses for each route
7. THE OrdersEndpoint SHALL preserve the existing Swagger metadata (WithName, WithSummary, WithDescription, Produces) for each route

### Requirement 4: Inbox Receive Service in the Persistence Layer

**User Story:** As a developer, I want the inbox message receive logic (idempotency check, entity creation, persistence) encapsulated in a repository in the Persistence layer, so that it can be tested in isolation and kept separate from the HTTP endpoint.

#### Acceptance Criteria

1. THE IInboxMessageRepository interface SHALL be defined in the Domain layer alongside the other repository interfaces
2. THE IInboxMessageRepository SHALL expose a method to receive an inbox message that performs idempotency check and persistence
3. WHEN a message with the same identifier already exists, THE IInboxMessageRepository receive method SHALL return a result indicating the message was a duplicate without creating a new record
4. WHEN a message with the given identifier does not exist, THE IInboxMessageRepository receive method SHALL persist a new InboxMessageEntity with the provided Id, MessageType, Payload, and ReceivedAt timestamp
5. THE InboxMessageRepository implementation SHALL be an `internal` class in the Persistence layer, consistent with the existing repository conventions
6. THE InboxMessageRepository SHALL be registered in the DI container via `ServicesExtensions.AddPersistence()`

### Requirement 5: Inbox Endpoint_Class

**User Story:** As a developer, I want the Inbox endpoint grouped in a dedicated class that delegates persistence logic to the repository, so that the endpoint remains a thin wrapper.

#### Acceptance Criteria

1. THE InboxEndpoint class SHALL implement the IEndpoint interface
2. WHEN `MapEndpoint` is called, THE InboxEndpoint SHALL register the `POST /Inbox/Receive` route
3. THE InboxEndpoint SHALL delegate the idempotency check and message persistence to IInboxMessageRepository
4. THE InboxEndpoint SHALL NOT contain direct DbContext access or inline persistence logic
5. WHEN the repository indicates the message is a duplicate, THE InboxEndpoint SHALL return HTTP 200
6. WHEN the repository successfully persists a new message, THE InboxEndpoint SHALL return HTTP 200
7. THE InboxEndpoint SHALL preserve the existing Swagger metadata (WithName, WithSummary, WithDescription, Produces) for the route

### Requirement 6: Endpoint_Discovery and Automatic Registration

**User Story:** As a developer, I want the Endpoint_Classes to be discovered and registered automatically, so that I don't have to modify `Program.cs` every time I add a new endpoint group.

#### Acceptance Criteria

1. THE Endpoint_Discovery SHALL scan the specified assembly for all classes implementing IEndpoint
2. THE Endpoint_Discovery SHALL register all discovered IEndpoint implementations in the DI container
3. WHEN the application starts, THE Endpoint_Discovery SHALL resolve all IEndpoint instances and call `MapEndpoint` on each
4. THE Endpoint_Discovery SHALL be invocable from Program_Cs with two extension methods: `AddEndpoints(Assembly)` on `IServiceCollection` and `MapEndpoints()` on `WebApplication`

### Requirement 7: Program.cs Cleanup

**User Story:** As a developer, I want `Program.cs` to no longer contain inline endpoint definitions, so that the entry point is clean and readable.

#### Acceptance Criteria

1. THE Program_Cs SHALL use `builder.Services.AddEndpoints(assembly)` to register endpoint classes
2. THE Program_Cs SHALL use `app.MapEndpoints()` to map all registered endpoint routes
3. THE Program_Cs SHALL NOT contain any inline `MapGet` or `MapPost` endpoint definitions
4. THE Program_Cs SHALL retain all existing non-endpoint configuration (DI, Quartz, middleware, seeding)

### Requirement 8: IInboxMessage Interface

**User Story:** As a developer, I want a marker interface for inbox messages, so that MediatR can handle them as notifications.

#### Acceptance Criteria

1. THE IInboxMessage interface SHALL extend MediatR `INotification`
2. THE IInboxMessage interface SHALL be located in the Domain project alongside the existing `IDomainEvent` interface

### Requirement 9: MessageType_Registry for Type-Message Mapping

**User Story:** As a developer, I want a mechanism that maps the `MessageType` string to the corresponding CLR type, so that the inbox job can deserialize messages without a hardcoded switch.

#### Acceptance Criteria

1. THE MessageType_Registry SHALL map a `MessageType` string value to the corresponding CLR type implementing IInboxMessage
2. WHEN a registered MessageType string is provided, THE MessageType_Registry SHALL return the corresponding CLR type
3. WHEN an unregistered MessageType string is provided, THE MessageType_Registry SHALL return null or indicate that no mapping exists
4. THE MessageType_Registry SHALL support registration of new message type mappings without modifying the InboxMessageProcessorJob

### Requirement 10: Generic InboxMessageProcessorJob

**User Story:** As a developer, I want the inbox job to use MediatR to publish messages, so that the job is decoupled from the business logic specific to each message type.

#### Acceptance Criteria

1. THE InboxMessageProcessorJob SHALL use the MessageType_Registry to resolve the CLR type for each inbox message
2. WHEN a valid MessageType mapping exists, THE InboxMessageProcessorJob SHALL deserialize the payload into the corresponding IInboxMessage type
3. WHEN a valid IInboxMessage is deserialized, THE InboxMessageProcessorJob SHALL publish the message via MediatR_Publisher
4. WHEN the MessageType has no registered mapping, THE InboxMessageProcessorJob SHALL log a warning and mark the message as processed
5. THE InboxMessageProcessorJob SHALL NOT contain direct references to specific business entity repositories (e.g., IOrderRepository)
6. THE InboxMessageProcessorJob SHALL NOT contain a switch statement or conditional branching based on MessageType values
7. THE InboxMessageProcessorJob SHALL preserve the existing error handling behavior: on failure, set `Error` field and `ProcessedAt` timestamp
8. THE InboxMessageProcessorJob SHALL preserve the existing idempotency check: skip messages where `ProcessedAt` is already set
9. THE InboxMessageProcessorJob SHALL preserve the existing batch processing behavior: poll up to 10 unprocessed messages per execution

### Requirement 11: PaymentConfirmed Inbox_Handler

**User Story:** As a developer, I want a dedicated MediatR handler for the `PaymentConfirmed` message, so that the payment confirmation logic is decoupled from the inbox job.

#### Acceptance Criteria

1. THE PaymentConfirmed Inbox_Handler SHALL implement `INotificationHandler<PaymentConfirmedInboxMessage>`
2. WHEN a PaymentConfirmedInboxMessage is received, THE Inbox_Handler SHALL look up the order by OrderId via IOrderRepository
3. WHEN the order is found, THE Inbox_Handler SHALL call `ConfirmPayment()` on the order entity
4. IF the payload is null or contains an empty OrderId, THEN THE Inbox_Handler SHALL throw an `InvalidOperationException`
5. THE PaymentConfirmedInboxMessage type SHALL implement IInboxMessage and be registered in the MessageType_Registry with the key `"PaymentConfirmed"`

### Requirement 12: MediatR Registration for Inbox_Handlers

**User Story:** As a developer, I want the new inbox handlers to be discovered automatically by MediatR, so that I don't have to modify the DI configuration manually.

#### Acceptance Criteria

1. THE MediatR configuration in Program_Cs SHALL scan the assembly containing the Inbox_Handler implementations
2. WHEN a new Inbox_Handler is added to the scanned assembly, THE MediatR configuration SHALL discover and register the handler automatically without code changes to Program_Cs

### Requirement 13: DTO Records Remain Accessible

**User Story:** As a developer, I want the DTO records (`CreateOrderRequest`, `InboxReceiveRequest`) currently defined in `Program.cs` to remain accessible after the restructuring, so that the Endpoint_Classes can use them.

#### Acceptance Criteria

1. THE CreateOrderRequest record SHALL remain accessible to the OrdersEndpoint class
2. THE InboxReceiveRequest record SHALL remain accessible to the InboxEndpoint class
3. THE request records SHALL be defined in a location consistent with the project conventions (within the API project)
