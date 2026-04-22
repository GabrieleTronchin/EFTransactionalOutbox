# Bugfix Requirements Document

## Introduction

After completing the `project-update` spec, several issues were identified in the **Sample.TransactionalOutbox** project. The target framework remained on .NET 9 instead of being upgraded to the latest stable version (.NET 10), test projects are not organized in a Solution Folder in Visual Studio, Scalar was introduced without being requested (Swagger/Swashbuckle should be used instead), and the README documentation needs significant improvements in structure and content.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN the project is compiled THEN all 6 `.csproj` files (API, Domain, Persistence, Domain.Tests, Persistence.Tests, Tests) target `net9.0` instead of the latest stable version `net10.0`

1.2 WHEN NuGet packages are resolved THEN package versions are compatible with .NET 9 rather than .NET 10 (e.g., `Microsoft.AspNetCore.OpenApi 9.0.15`, `Microsoft.EntityFrameworkCore.InMemory 9.0.15`)

1.3 WHEN the solution `Sample.TransactionalOutbox.sln` is opened in Visual Studio THEN the 3 test projects (`Domain.Tests`, `Persistence.Tests`, `Tests`) appear at the root level of the solution without being grouped in a "Tests" Solution Folder

1.4 WHEN the API project `Program.cs` is inspected THEN it uses `Scalar.AspNetCore` (`app.MapScalarApiReference()`) instead of Swagger/Swashbuckle, which was not requested and was introduced as an unsolicited change

1.5 WHEN the API project `.csproj` is inspected THEN it references `Scalar.AspNetCore` (2.2.7) instead of `Swashbuckle.AspNetCore`

1.6 WHEN the `launchSettings.json` is inspected THEN the `launchUrl` is set to `"swagger"` but the Swagger middleware is not configured in `Program.cs`, resulting in a 404 when the browser opens

1.7 WHEN the README.md introduction is read THEN it is too verbose — it explains what the pattern is, what technologies are used, and their purposes, instead of being a concise project description

1.8 WHEN the README.md "Project Structure" section is read THEN it contains too much implementation detail for each project instead of a high-level description with only the main entities

1.9 WHEN the README.md "Pattern Flow" Mermaid diagram is viewed THEN it uses a vertical (TD) layout which is visually unappealing, and it only shows a basic linear flow without highlighting the key architectural insight: the EF Core interceptor persisting domain events in the same transaction as business data

1.10 WHEN the README.md "Getting Started" section is read THEN it contains unnecessary clone/build/run instructions that are self-evident for any .NET developer

1.11 WHEN the README.md "Testing the Flow" section is read THEN it provides step-by-step manual instructions instead of simply referencing the `.http` file and Swagger UI

1.12 WHEN the README.md "Package Versions" section is positioned THEN it appears in the middle of the document instead of at the end

1.13 WHEN the README.md "Scalar API Reference" section is read THEN it references Scalar instead of Swagger, and it is a separate section instead of being merged with "API Endpoints"

### Expected Behavior (Correct)

2.1 WHEN the project is compiled THEN all 6 `.csproj` files SHALL target `net10.0`

2.2 WHEN NuGet packages are resolved THEN all package versions SHALL be updated to the latest stable versions compatible with .NET 10

2.3 WHEN the solution `Sample.TransactionalOutbox.sln` is opened in Visual Studio THEN the 3 test projects SHALL be grouped under a Solution Folder named "Tests"

2.4 WHEN the API project `Program.cs` is inspected THEN it SHALL use `Swashbuckle.AspNetCore` with `builder.Services.AddSwaggerGen()`, `app.UseSwagger()`, and `app.UseSwaggerUI()` instead of Scalar

2.5 WHEN the API project `.csproj` is inspected THEN it SHALL reference `Swashbuckle.AspNetCore` (latest stable compatible with .NET 10) and SHALL NOT reference `Scalar.AspNetCore`

2.6 WHEN the application is launched in Development mode THEN the browser SHALL open on the Swagger UI page and the page SHALL load correctly without 404 errors

2.7 WHEN the README.md introduction is read THEN it SHALL be concise — just the project title and a short one-line description without explaining the pattern or listing technologies

2.8 WHEN the README.md "Project Structure" section is read THEN it SHALL describe only the high-level purpose of each project and its main entities, without implementation details

2.9 WHEN the README.md "Pattern Flow" section is viewed THEN it SHALL contain two Mermaid diagrams using horizontal (LR) layout:
- Diagram 1: A high-level overview explaining what the Transactional Outbox pattern is conceptually
- Diagram 2: A detailed implementation diagram highlighting the EF Core interceptor, same-transaction persistence of domain events, and MediatR dispatch

2.10 WHEN the README.md is read THEN it SHALL NOT contain a "Getting Started" section

2.11 WHEN the README.md "Testing the Flow" section is read THEN it SHALL simply reference the `.http` file for testing and mention that Swagger UI is available for interactive API exploration

2.12 WHEN the README.md "Package Versions" section is positioned THEN it SHALL appear at the end of the document (before "Articles")

2.13 WHEN the README.md API documentation is read THEN "API Endpoints" and "Swagger" SHALL be merged into a single section, with Swagger as a subsection under API Endpoints

### Unchanged Behavior (Regression Prevention)

3.1 WHEN the project is compiled after the upgrade to `net10.0` THEN the solution SHALL CONTINUE TO compile without errors

3.2 WHEN tests are executed after the upgrade to `net10.0` THEN all existing unit and property-based tests SHALL CONTINUE TO pass without failures

3.3 WHEN the application is run after the upgrade THEN the 3 API endpoints (GET /Products, GET /Orders, POST /PurchaseOrder/{id}) SHALL CONTINUE TO function correctly

3.4 WHEN the solution is opened in Visual Studio after adding the Solution Folder THEN the 3 source projects (API, Domain, Persistence) SHALL CONTINUE TO be visible at the root level of the solution (not moved into the Tests folder)

3.5 WHEN the application is launched in Development mode THEN the Swagger UI SHALL display all API endpoints correctly

3.6 WHEN the Quartz.NET background job runs after the upgrade THEN outbox message processing SHALL CONTINUE TO work correctly (polling, deserialization, publishing via MediatR)

3.7 WHEN the `docs/` folder markdown files are read THEN they SHALL CONTINUE TO contain accurate deep-dive documentation for DomainEventManager, OrderDomainEventInterceptor, and OutboxMessageProcessorJob with working links back to the README
