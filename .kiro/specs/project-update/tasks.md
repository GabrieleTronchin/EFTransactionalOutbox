# Implementation Plan: Transactional Outbox Project Update

## Overview

This plan modernizes the Sample.TransactionalOutbox solution by first establishing a comprehensive test suite as a regression baseline, then upgrading frameworks and packages, performing post-migration verification, and finally rewriting the documentation. Each phase builds on the previous one, and tests must be green before any upgrade begins.

## Tasks

- [x] 1. Create test project infrastructure and FsCheck generators
  - [x] 1.1 Create the Domain test project and add to solution
    - Create `test/Sample.TransactionalOutbox.Domain.Tests/Sample.TransactionalOutbox.Domain.Tests.csproj` targeting `net9.0`
    - Add NuGet packages: `xunit` (2.9.3), `xunit.runner.visualstudio` (2.8.2), `Microsoft.NET.Test.Sdk` (17.12.0), `FsCheck.Xunit` (3.1.0), `FluentAssertions` (7.0.0)
    - Add project reference to `Sample.TransactionalOutbox.Domain`
    - Add the test project to `src/Sample.TransactionalOutbox.sln` via `dotnet sln add`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Create the Persistence test project and add to solution
    - Create `test/Sample.TransactionalOutbox.Persistence.Tests/Sample.TransactionalOutbox.Persistence.Tests.csproj` targeting `net9.0`
    - Add NuGet packages: `xunit` (2.9.3), `xunit.runner.visualstudio` (2.8.2), `Microsoft.NET.Test.Sdk` (17.12.0), `FsCheck.Xunit` (3.1.0), `FluentAssertions` (7.0.0), `Microsoft.EntityFrameworkCore.InMemory` (9.0.2)
    - Add project references to `Sample.TransactionalOutbox.Persistence` and `Sample.TransactionalOutbox.Domain`
    - Add the test project to `src/Sample.TransactionalOutbox.sln` via `dotnet sln add`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.3 Create the API test project and add to solution
    - Create `test/Sample.TransactionalOutbox.Tests/Sample.TransactionalOutbox.Tests.csproj` targeting `net9.0`
    - Add NuGet packages: `xunit` (2.9.3), `xunit.runner.visualstudio` (2.8.2), `Microsoft.NET.Test.Sdk` (17.12.0), `FsCheck.Xunit` (3.1.0), `FluentAssertions` (7.0.0), `NSubstitute` (5.3.0), `Microsoft.EntityFrameworkCore.InMemory` (9.0.2)
    - Add project references to `Sample.TransactionalOutbox`, `Sample.TransactionalOutbox.Domain`, and `Sample.TransactionalOutbox.Persistence`
    - Add the test project to `src/Sample.TransactionalOutbox.sln` via `dotnet sln add`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.4 Create FsCheck Arbitrary generators for domain types
    - Create `test/Sample.TransactionalOutbox.Domain.Tests/Generators/DomainGenerators.cs`
    - Implement `ArbitraryDescription()` — generates valid non-empty, non-whitespace strings
    - Implement `ArbitraryQuantity()` — generates valid positive integers
    - Implement `ArbitraryOrderConfirmed()` — generates `OrderConfirmed` events with random Guids
    - Implement `ArbitraryDomainEvent()` — generates `IDomainEvent` instances for DomainEventManager tests
    - Follow project code style: file-scoped namespace, `_` prefix fields, sealed class
    - _Requirements: 2.1, 3.1, 4.1_

- [x] 2. Implement DomainEventManager tests
  - [x] 2.1 Write unit tests for DomainEventManager
    - Create `test/Sample.TransactionalOutbox.Domain.Tests/DomainEventManagerTests.cs`
    - Since `DomainEventManager` is abstract, create a private concrete subclass in the test file for testing
    - Test `RaiseEvent` adds event to list (`[Fact]`)
    - Test `GetEvents` returns all previously added events (`[Fact]`)
    - Test `ClearEvents` empties the event list (`[Fact]`)
    - Test `GetEvents` returns empty list on new instance (`[Fact]`)
    - Follow test naming convention: `MethodName_Scenario_ExpectedResult`
    - _Requirements: 2.1, 2.2, 2.3, 2.5_

  - [x] 2.2 Write property test for RaiseEvent/GetEvents round-trip
    - **Property 1: RaiseEvent/GetEvents round-trip preserves all events**
    - For any list of domain events, raising each and calling GetEvents returns exactly those events in order with matching count
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 2.1, 2.2, 2.4**

  - [x] 2.3 Write property test for ClearEvents
    - **Property 2: ClearEvents empties the event list**
    - For any DomainEventManager with any number of raised events, ClearEvents followed by GetEvents returns empty collection
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 2.3, 2.5**

- [x] 3. Implement OrderEntity tests
  - [x] 3.1 Write unit tests for OrderEntity
    - Create `test/Sample.TransactionalOutbox.Domain.Tests/OrderEntityTests.cs`
    - Test `Create_WithValidInputs_ReturnsUnconfirmedOrder` — verifies Confirmed is false, ProductId and Description match (`[Fact]`)
    - Test `Create_WithNullDescription_ThrowsArgumentException` (`[Fact]`)
    - Test `Create_WithEmptyDescription_ThrowsArgumentException` (`[Fact]`)
    - Test `Create_WithWhitespaceDescription_ThrowsArgumentException` (`[Fact]`)
    - Test `ConfirmPayment_OnUnconfirmedOrder_SetsConfirmedTrue` (`[Fact]`)
    - Test `ConfirmPayment_OnUnconfirmedOrder_RaisesOrderConfirmedEvent` — verifies event ProductId matches (`[Fact]`)
    - Test `ConfirmPayment_OnConfirmedOrder_ThrowsInvalidOperationException` (`[Fact]`)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 3.2 Write property test for OrderEntity.Create invariant
    - **Property 3: OrderEntity.Create invariant — new orders are unconfirmed**
    - For any valid productId and non-empty description, Create produces instance with Confirmed=false, matching ProductId and Description
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 3.1**

  - [x] 3.3 Write property test for OrderEntity.Create rejects invalid descriptions
    - **Property 4: OrderEntity.Create rejects invalid descriptions**
    - For any null, empty, or whitespace-only string, Create throws ArgumentException
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 3.2**

  - [x] 3.4 Write property test for ConfirmPayment sets Confirmed and raises event
    - **Property 5: ConfirmPayment sets Confirmed and raises correct event**
    - For any unconfirmed OrderEntity, ConfirmPayment sets Confirmed=true and adds exactly one OrderConfirmed event with matching ProductId
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 3.3, 3.4**

  - [x] 3.5 Write property test for double ConfirmPayment throws
    - **Property 6: Double ConfirmPayment throws**
    - For any already-confirmed OrderEntity, calling ConfirmPayment again throws InvalidOperationException
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 3.5**

- [x] 4. Implement ProductEntity tests
  - [x] 4.1 Write unit tests for ProductEntity
    - Create `test/Sample.TransactionalOutbox.Domain.Tests/ProductEntityTests.cs`
    - Test `Create_WithPositiveQuantity_ReturnsProductWithCorrectQuantity` (`[Fact]`)
    - Test `Create_WithZeroQuantity_ThrowsException` (`[Fact]`)
    - Test `Create_WithNegativeQuantity_ThrowsException` (`[Fact]`)
    - Test `HasBeenConfirmed_WithPositiveQuantity_DecrementsQuantityByOne` (`[Fact]`)
    - Test `HasBeenConfirmed_WithZeroQuantity_ThrowsInvalidOperationException` (`[Fact]`)
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 4.2 Write property test for ProductEntity.Create preserves quantity
    - **Property 7: ProductEntity.Create preserves quantity**
    - For any positive integer, Create produces instance where Quantity equals the input
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 4.1**

  - [x] 4.3 Write property test for ProductEntity.Create rejects non-positive quantities
    - **Property 8: ProductEntity.Create rejects non-positive quantities**
    - For any integer ≤ 0, Create throws an exception
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 4.2**

  - [x] 4.4 Write property test for HasBeenConfirmed decrement invariance
    - **Property 9: HasBeenConfirmed decrement invariance**
    - For any product with initial quantity Q > 0 and N calls (1 ≤ N ≤ Q), Quantity equals Q − N
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 4.3, 4.5**

- [x] 5. Implement OrderDomainEventInterceptor tests
  - [x] 5.1 Write unit tests for OrderDomainEventInterceptor
    - Create `test/Sample.TransactionalOutbox.Persistence.Tests/OrderDomainEventInterceptorTests.cs`
    - Set up InMemory `ShopDbContext` with `OrderDomainEventInterceptor` registered
    - Test `SavingChangesAsync_WithOrderEvents_CreatesOutboxMessages` — verify N events produce N OutboxMessageEntity records (`[Fact]`)
    - Test `SavingChangesAsync_WithOrderEvents_SetsCorrectTypeField` — verify Type matches event type name (`[Fact]`)
    - Test `SavingChangesAsync_WithOrderEvents_SerializesContentAsJson` — verify Content round-trips via Newtonsoft.Json deserialization (`[Fact]`)
    - Test `SavingChangesAsync_WithOrderEvents_ClearsEventsFromEntity` — verify GetEvents() returns empty after save (`[Fact]`)
    - Test `SavingChangesAsync_WithNoEvents_DoesNotCreateOutboxMessages` (`[Fact]`)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 5.2 Write property test for interceptor creates correct outbox messages
    - **Property 10: Interceptor creates correct outbox messages**
    - For any OrderEntity with N domain events (N ≥ 1), SaveChangesAsync produces exactly N OutboxMessageEntity records with correct Type and round-trippable Content
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [x] 5.3 Write property test for interceptor clears events after persistence
    - **Property 11: Interceptor clears events after persistence**
    - For any OrderEntity with domain events, after SaveChangesAsync, GetEvents() returns empty collection
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 5.4**

- [x] 6. Implement OutboxMessageProcessorJob tests
  - [x] 6.1 Write unit tests for OutboxMessageProcessorJob
    - Create `test/Sample.TransactionalOutbox.Tests/OutboxMessageProcessorJobTests.cs`
    - Set up InMemory `ShopDbContext`, mock `IPublisher` and `ILogger<OutboxMessageProcessorJob>` via NSubstitute
    - Create a mock/stub `IJobExecutionContext` via NSubstitute
    - Test `Execute_WithUnprocessedMessages_DeserializesAndPublishes` — verify IPublisher.Publish called for each message (`[Fact]`)
    - Test `Execute_WithSuccessfulMessages_RemovesFromTable` — verify successfully processed messages are removed (`[Fact]`)
    - Test `Execute_WithDeserializationFailure_SetsExceptionAndCompleteTime` — verify Exception field set, CompleteTime set (`[Fact]`)
    - Test `Execute_WithPublishFailure_SetsExceptionAndKeepsMessage` — verify Exception field set, message not removed (`[Fact]`)
    - Test `Execute_WithNoUnprocessedMessages_DoesNotPublish` — verify IPublisher.Publish not called (`[Fact]`)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 6.2 Write property test for successful message processing round-trip
    - **Property 12: Successful message processing round-trip**
    - For any set of valid unprocessed OutboxMessageEntity records, Execute deserializes and publishes each, then removes all successful messages
    - Use `[Property(MaxTest = 100)]` attribute from FsCheck.Xunit
    - **Validates: Requirements 6.1, 6.2**

- [x] 7. Checkpoint — Verify all tests green before upgrades
  - Ensure all tests pass by running `dotnet test src/Sample.TransactionalOutbox.sln`
  - All unit and property tests must be green before proceeding with any framework or package upgrades
  - Ask the user if questions arise.
  - _Requirements: 1.4_

- [x] 8. Upgrade NuGet packages
  - [x] 8.1 Upgrade NuGet packages across all projects
    - Upgrade `MediatR` from 12.4.1 to 14.1.0 in Domain `.csproj`
    - Upgrade `Quartz` and `Quartz.Extensions.Hosting` to latest stable 3.x in API `.csproj`
    - Upgrade `Newtonsoft.Json` to latest stable 13.x in Persistence `.csproj`
    - Upgrade `Microsoft.EntityFrameworkCore.InMemory` to latest 9.0.x patch in Persistence `.csproj`
    - Upgrade `Microsoft.AspNetCore.OpenApi` to latest 9.0.x patch in API `.csproj`
    - Upgrade `Microsoft.Extensions.Logging.Abstractions` to latest 9.0.x patch in Domain `.csproj`
    - Adapt source code if any package introduces breaking API changes (especially MediatR 14.x)
    - Run `dotnet build src/Sample.TransactionalOutbox.sln` and `dotnet test src/Sample.TransactionalOutbox.sln` to verify
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 9. Replace Swashbuckle with Scalar
  - [x] 9.1 Replace Swashbuckle with Scalar.AspNetCore in the API project
    - Remove `Swashbuckle.AspNetCore` package from `Sample.TransactionalOutbox.csproj`
    - Add `Scalar.AspNetCore` (latest stable) to `Sample.TransactionalOutbox.csproj`
    - Update `Program.cs`: remove `builder.Services.AddSwaggerGen()`, add `builder.Services.AddOpenApi()`
    - Update `Program.cs`: remove `app.UseSwagger()` and `app.UseSwaggerUI()`, add `app.MapOpenApi()` and `app.MapScalarApiReference()`
    - Run `dotnet build src/Sample.TransactionalOutbox.sln` and `dotnet test src/Sample.TransactionalOutbox.sln` to verify
    - _Requirements: 11.1, 11.2_

- [x] 10. Remove deprecated packages and post-migration verification
  - [x] 10.1 Remove deprecated Microsoft.AspNetCore.Http.Abstractions
    - Remove `Microsoft.AspNetCore.Http.Abstractions` (v2.3.0) from `Sample.TransactionalOutbox.Persistence.csproj`
    - Verify the project still compiles — the types are in the ASP.NET Core shared framework
    - _Requirements: 9.4_

  - [x] 10.2 Run post-migration verification checks
    - Run `dotnet build src/Sample.TransactionalOutbox.sln` and analyze output for warnings
    - Resolve any build warnings (deprecation, obsolete APIs, nullable reference types)
    - Run `dotnet list src/Sample.TransactionalOutbox.sln package --deprecated` and address any findings
    - Run `dotnet list src/Sample.TransactionalOutbox.sln package --vulnerable` and address any findings
    - Run `dotnet test src/Sample.TransactionalOutbox.sln` to confirm all tests still pass
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

- [x] 11. Checkpoint — Verify clean build and all tests green post-migration
  - Ensure `dotnet build` produces zero warnings and `dotnet test` passes all tests
  - Ask the user if questions arise.
  - _Requirements: 9.7_

- [x] 12. Rewrite documentation
  - [x] 12.1 Create docs/ folder with component deep-dive markdown files
    - Create `docs/domain-event-manager.md` — detailed explanation of DomainEventManager (RaiseEvent, GetEvents, ClearEvents), link back to README
    - Create `docs/outbox-interceptor.md` — detailed explanation of OrderDomainEventInterceptor (SaveChanges interception, serialization), link back to README
    - Create `docs/outbox-processor-job.md` — detailed explanation of OutboxMessageProcessorJob (polling, deserialization, publishing, error handling), link back to README
    - _Requirements: 10.5.8, 10.5.9_

  - [x] 12.2 Rewrite README.md
    - Repository title with short introductory paragraph
    - Table of Contents with anchor links
    - Project Structure table (Project, Description columns) covering all solution projects including test projects
    - Mermaid flowchart diagram illustrating the Transactional Outbox pattern flow
    - API Endpoints table (Method, Endpoint, Description columns) for Products, Orders, PurchaseOrder
    - Key Components overview with links to `docs/` files for deep-dives
    - Getting Started section: clone, build (`dotnet build`), run (`dotnet run`), test (`dotnet test`) flow
    - Step-by-step instructions to test the complete flow (retrieve product → retrieve order → confirm order → verify)
    - Package Versions table (Package, Version, Notes) reflecting updated versions
    - How to access Scalar API reference UI (URL and required environment)
    - Articles section at the **end** with Medium link ("\.NET 8 — Transactional Outbox Pattern With EF Core" by Gabriele Tronchin)
    - Do NOT include screenshots or reference the `assets/` folder
    - _Requirements: 10.1.1, 10.1.2, 10.1.3, 10.2.4, 10.3.5, 10.4.6, 10.5.7, 10.6.10, 10.6.11, 10.7.12, 10.7.13, 10.8.14, 11.3_

  - [x] 12.3 Delete the assets/ folder
    - Remove the `assets/` folder and all screenshot files (no longer referenced in documentation)
    - _Requirements: 10.1.3_

- [x] 13. Update the HTTP file
  - [x] 13.1 Rewrite Sample.TransactionalOutbox.http
    - Add `@host` variable for base URL (`http://localhost:5220`)
    - Add `GET {{host}}/Products` with descriptive comments explaining what the call does and expected response
    - Add `GET {{host}}/Orders` with descriptive comments explaining what the call does and expected response
    - Add `POST {{host}}/PurchaseOrder/{id}` with example order ID and descriptive comments explaining the usage context within the pattern flow
    - Follow logical test flow order: Products → Orders → PurchaseOrder
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6_

- [x] 14. Final checkpoint — Ensure all tests pass and build is clean
  - Run `dotnet build src/Sample.TransactionalOutbox.sln` — verify zero warnings
  - Run `dotnet test src/Sample.TransactionalOutbox.sln` — verify all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at key milestones
- Property tests validate universal correctness properties defined in the design document (Properties 1–12)
- Unit tests validate specific examples and edge cases
- All new code must follow existing project conventions (file-scoped namespaces, private constructors + static Create, sealed/internal sealed, `_` prefix fields, etc.)
- Tests must be green BEFORE any upgrade work begins (task 7 is the gate)
