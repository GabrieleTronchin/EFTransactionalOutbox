# Requirements Document — Transactional Outbox Project Update

## Introduction

This document defines the requirements for updating the **Sample.TransactionalOutbox** project, an implementation of the Transactional Outbox Pattern with EF Core in .NET. The update includes: adding unit tests as a regression baseline, upgrading to the latest .NET version and NuGet packages, post-migration verification of warnings and deprecated packages, and documentation updates.

## Glossary

- **Solution**: the set of three .NET projects that make up the system (API, Domain, Persistence)
- **NuGet_Package**: an external dependency managed via the NuGet package manager
- **Target_Framework**: the .NET framework version specified in `.csproj` files
- **README**: the `README.md` file at the repository root that documents the project
- **DomainEventManager**: abstract class that manages the list of domain events (RaiseEvent, GetEvents, ClearEvents)
- **OrderEntity**: domain entity representing an order that inherits from DomainEventManager
- **ProductEntity**: domain entity representing a product with available quantity
- **OutboxMessageEntity**: entity representing a domain message persisted in the outbox table
- **OrderDomainEventInterceptor**: EF Core SaveChanges interceptor that persists domain events to the OutboxMessage table
- **OutboxMessageProcessorJob**: Quartz.NET job that reads unprocessed messages, deserializes them, and publishes them via MediatR
- **Test_Project**: the xUnit project dedicated to unit testing the solution
- **ShopDbContext**: the EF Core DbContext managing the Orders, Products, and DomainEvents tables

## Requirements

### Requirement 1: Add Unit Test Project

**User Story:** As a developer, I want to have a working unit test project with all tests green BEFORE starting any upgrade, so that I have a reliable regression baseline.

#### Acceptance Criteria

1. THE Solution SHALL include a Test_Project based on xUnit
2. THE Test_Project SHALL be referenced in the `.sln` solution file
3. THE Test_Project SHALL use the same Target_Framework as the other projects in the solution
4. WHEN the Test_Project is created, all unit tests SHALL be executed and pass (green) BEFORE proceeding with any framework or package upgrades

### Requirement 2: Unit Tests for DomainEventManager

**User Story:** As a developer, I want to test the DomainEventManager, so that I can verify that domain event management works correctly.

#### Acceptance Criteria

1. WHEN RaiseEvent is invoked with a valid event, THE DomainEventManager SHALL add the event to the event list
2. WHEN GetEvents is invoked, THE DomainEventManager SHALL return all previously added events
3. WHEN ClearEvents is invoked, THE DomainEventManager SHALL empty the event list
4. WHEN N events are added via RaiseEvent, THE DomainEventManager SHALL return exactly N events via GetEvents (size invariance property)
5. WHEN ClearEvents is invoked followed by GetEvents, THE DomainEventManager SHALL return an empty list

### Requirement 3: Unit Tests for OrderEntity

**User Story:** As a developer, I want to test the OrderEntity, so that I can verify correct order creation and payment confirmation.

#### Acceptance Criteria

1. WHEN Create is invoked with a valid productId and description, THE OrderEntity SHALL create an instance with Confirmed set to false
2. WHEN Create is invoked with an empty or null description, THE OrderEntity SHALL throw an ArgumentException
3. WHEN ConfirmPayment is invoked on an unconfirmed order, THE OrderEntity SHALL set Confirmed to true
4. WHEN ConfirmPayment is invoked on an unconfirmed order, THE OrderEntity SHALL raise an OrderConfirmed event with the correct ProductId
5. WHEN ConfirmPayment is invoked on an already confirmed order, THE OrderEntity SHALL throw an InvalidOperationException

### Requirement 4: Unit Tests for ProductEntity

**User Story:** As a developer, I want to test the ProductEntity, so that I can verify correct product creation and quantity decrement.

#### Acceptance Criteria

1. WHEN Create is invoked with a quantity greater than zero, THE ProductEntity SHALL create an instance with the specified quantity
2. WHEN Create is invoked with a quantity equal to or less than zero, THE ProductEntity SHALL throw an exception
3. WHEN HasBeenConfirmed is invoked on a product with quantity greater than zero, THE ProductEntity SHALL decrement the quantity by one
4. WHEN HasBeenConfirmed is invoked on a product with quantity equal to zero, THE ProductEntity SHALL throw an InvalidOperationException
5. WHEN HasBeenConfirmed is invoked N times on a product with initial quantity Q (where N ≤ Q), THE ProductEntity SHALL have quantity equal to Q - N (invariance property)

### Requirement 5: Unit Tests for OrderDomainEventInterceptor

**User Story:** As a developer, I want to test the domain event interceptor, so that I can verify that events are correctly persisted to the outbox table during SaveChanges.

#### Acceptance Criteria

1. WHEN SaveChangesAsync is invoked on a DbContext containing an OrderEntity with domain events, THE OrderDomainEventInterceptor SHALL create an OutboxMessageEntity for each domain event
2. WHEN an OutboxMessageEntity is created, THE OrderDomainEventInterceptor SHALL set the Type field with the event type name
3. WHEN an OutboxMessageEntity is created, THE OrderDomainEventInterceptor SHALL serialize the event in the Content field as JSON with TypeNameHandling.All
4. WHEN SaveChangesAsync is invoked, THE OrderDomainEventInterceptor SHALL clear the event list from the OrderEntity after persistence
5. WHEN the DbContext does not contain OrderEntity instances with events, THE OrderDomainEventInterceptor SHALL proceed without creating OutboxMessageEntity records

### Requirement 6: Unit Tests for OutboxMessageProcessorJob

**User Story:** As a developer, I want to test the outbox message processor job, so that I can verify that messages are correctly read, deserialized, and published.

#### Acceptance Criteria

1. WHEN unprocessed messages exist (CompleteTime is null) in the DomainEvents table, THE OutboxMessageProcessorJob SHALL deserialize and publish each message via MediatR
2. WHEN a message is successfully processed, THE OutboxMessageProcessorJob SHALL remove the message from the DomainEvents table
3. IF an error occurs during message deserialization, THEN THE OutboxMessageProcessorJob SHALL set the Exception field on the message and set CompleteTime
4. IF an exception occurs during message publishing, THEN THE OutboxMessageProcessorJob SHALL set the Exception field on the message without removing it from the table
5. WHEN no unprocessed messages exist, THE OutboxMessageProcessorJob SHALL terminate without performing any publish operations

### Requirement 7: Upgrade .NET Target Framework

**User Story:** As a developer, I want to upgrade the project to the latest stable .NET version, so that I can benefit from the latest features and security fixes.

#### Acceptance Criteria

1. THE Solution SHALL use the latest stable Target_Framework version available in all `.csproj` files
2. WHEN the Target_Framework is upgraded, THE Solution SHALL compile without errors
3. WHEN the Target_Framework is upgraded, THE Solution SHALL pass all existing unit tests without failures
4. WHEN the Target_Framework is upgraded, THE Solution SHALL maintain the same three-project structure (API, Domain, Persistence)

### Requirement 8: Upgrade NuGet Packages

**User Story:** As a developer, I want to upgrade all NuGet packages to the latest versions, so that I have the latest bug fixes and features.

#### Acceptance Criteria

1. THE Solution SHALL use the latest stable version available for each NuGet_Package referenced in `.csproj` files
2. WHEN NuGet_Packages are upgraded, THE Solution SHALL compile without errors
3. WHEN NuGet_Packages are upgraded, THE Solution SHALL pass all existing unit tests without failures
4. WHEN NuGet_Packages are upgraded, THE Solution SHALL maintain compatibility between dependencies (no version conflicts)
5. IF a NuGet_Package introduces breaking changes, THEN THE Solution SHALL adapt the source code to maintain existing functional behavior

### Requirement 9: Post-Migration Verification of Warnings and Deprecated Packages

**User Story:** As a developer, I want to verify that after migration there are no build warnings, deprecated packages, or obsolete APIs, so that I have a clean and maintainable project.

#### Acceptance Criteria

1. WHEN migration is complete, THE Solution SHALL be compiled with `dotnet build` and the output SHALL be analyzed to identify any warnings
2. WHEN build warnings are detected (including deprecation warnings, obsolete APIs, nullable reference types), THE Solution SHALL resolve all identified warnings
3. WHEN migration is complete, THE Solution SHALL check for deprecated NuGet packages via `dotnet list package --deprecated`
4. IF deprecated packages are found, THEN THE Solution SHALL replace them with recommended alternatives or remove them if no longer needed
5. WHEN migration is complete, THE Solution SHALL check for vulnerable NuGet packages via `dotnet list package --vulnerable`
6. IF vulnerable packages are found, THEN THE Solution SHALL upgrade or replace them to eliminate known vulnerabilities
7. WHEN all post-migration checks are complete, THE Solution SHALL compile with zero warnings and all unit tests SHALL pass

### Requirement 10: Documentation Update

**User Story:** As a developer, I want the README to be updated with a professional and well-organized structure (inspired by the MediatRPipelines project format), with a `docs/` folder for deep-dive documentation, so that other developers can easily understand the architecture and usage of the system.

#### Acceptance Criteria

##### 10.1 README Structure

1. THE README SHALL start with the **repository title** and a short introductory paragraph describing what this repository is (a sample implementation of the Transactional Outbox Pattern with EF Core)
2. THE README SHALL include a **Table of Contents** with anchor links to the main sections immediately after the introduction
3. THE README SHALL NOT include screenshots or reference the `assets/` folder

##### 10.2 Project Structure

4. THE README SHALL include a **Project Structure** section with a markdown table (columns: Project, Description) describing each of the solution projects and their responsibilities

##### 10.3 Pattern Flow Diagram

5. THE README SHALL include a **Mermaid diagram** (flowchart) illustrating the complete Transactional Outbox pattern flow: from order confirmation, through the DomainEventManager, the EF Core SaveChanges interceptor, persistence to the OutboxMessage table, processing by the Quartz.NET job, and publishing via MediatR

##### 10.4 API Endpoints

6. THE README SHALL include an **API Endpoints** section with a markdown table (columns: Method, Endpoint, Description) documenting the three API endpoints: Products (GET), Orders (GET), PurchaseOrder (POST)

##### 10.5 Key Components

7. THE README SHALL provide a brief overview of the key components (DomainEventManager, OrderDomainEventInterceptor, OutboxMessageProcessorJob) with links to detailed `docs/` markdown files where deeper explanation is needed
8. THE `docs/` folder SHALL contain navigable markdown files for components that require detailed explanation (e.g., `docs/domain-event-manager.md`, `docs/outbox-interceptor.md`, `docs/outbox-processor-job.md`)
9. EACH `docs/` markdown file SHALL include a link back to the main README for navigation

##### 10.6 Getting Started

10. THE README SHALL include a **Getting Started** section with instructions to clone the repository, build the solution (`dotnet build`), and run the project (`dotnet run`)
11. THE README SHALL include step-by-step instructions to test the complete flow: retrieve product, retrieve order, confirm order, verify quantity decrement and order status

##### 10.7 Package Versions

12. THE README SHALL include a **Package Versions** section with a markdown table (columns: Package, Version, Notes) listing the .NET version and all NuGet packages used in the solution
13. THE README SHALL reflect the updated Target_Framework and NuGet_Package versions after the upgrade

##### 10.8 Articles

14. THE README SHALL include an **Articles** section at the **end** of the document with a link to the author's Medium article (".NET 8 — Transactional Outbox Pattern With EF Core" by Gabriele Tronchin)

### Requirement 11: Swagger/OpenAPI Documentation

**User Story:** As a developer, I want the project to expose Swagger/OpenAPI documentation, so that API endpoints are easily explorable and testable via browser.

#### Acceptance Criteria

1. THE Solution SHALL expose Swagger UI documentation accessible via browser in the Development environment
2. THE Solution SHALL generate the OpenAPI specification for all API endpoints (Products, Orders, PurchaseOrder)
3. THE README SHALL document how to access the Swagger UI (URL and required environment)

### Requirement 12: HTTP File with Endpoint Documentation

**User Story:** As a developer, I want to have a complete and commented `.http` file with all endpoints and example payloads, so that I can quickly test the APIs without leaving the IDE.

#### Acceptance Criteria

1. THE Solution SHALL include an updated `.http` file in the API project folder that replaces the current placeholder content
2. THE `.http` file SHALL contain a request for each of the three endpoints: GET Products, GET Orders, POST PurchaseOrder
3. THE `.http` file SHALL include an example payload for the POST PurchaseOrder endpoint (with a sample order ID)
4. THE `.http` file SHALL include descriptive comments for each request explaining: what the call does, what to expect as a response, and the usage context within the pattern flow
5. THE `.http` file SHALL use a variable for the host base URL (e.g., `@host = http://localhost:5220`)
6. THE `.http` file SHALL follow the logical test flow order: first retrieve products, then retrieve orders, finally confirm order
