# Project Update Bugfix and Improvements — Design

## Overview

The previous `project-update` spec introduced several issues: all 6 `.csproj` files still target `net9.0` instead of `net10.0`, the `.sln` file has no Solution Folder grouping for test projects, `Scalar.AspNetCore` was introduced instead of the requested `Swashbuckle.AspNetCore` (Swagger), the `launchSettings.json` points to a Swagger URL that doesn't resolve because the Swagger middleware isn't configured, and the README documentation needs a structural rewrite (concise intro, two horizontal Mermaid diagrams, no "Getting Started" section, simplified "Testing the Flow", repositioned "Package Versions", merged API/Swagger sections).

This design formalizes each defect as a bug condition, defines the expected correct behavior, and outlines the fix implementation and testing strategy.

## Glossary

- **Bug_Condition (C)**: The set of conditions that identify a defective state — wrong target framework, missing Solution Folder, wrong OpenAPI UI package, broken launch URL, or incorrect README structure
- **Property (P)**: The desired correct state after the fix is applied
- **Preservation**: Existing behaviors that must remain unchanged — solution compiles, all tests pass, API endpoints work, Quartz.NET job processes outbox messages, docs/ files remain accurate
- **TargetFramework**: The `<TargetFramework>` MSBuild property in each `.csproj` file (currently `net9.0`, should be `net10.0`)
- **Solution Folder**: A virtual folder in the `.sln` file (GUID `{2150E333-8FDC-42A3-9474-1A3956D46DE8}`) used to group projects in Visual Studio
- **Scalar.AspNetCore**: The OpenAPI UI package that was incorrectly introduced (should be replaced with Swashbuckle)
- **Swashbuckle.AspNetCore**: The Swagger/OpenAPI UI package that should be used instead of Scalar

## Bug Details

### Bug Condition

The bugs manifest across six categories: (1) all `.csproj` files target the wrong framework version, (2) the `.sln` file lacks organizational structure for test projects, (3) `Program.cs` and the API `.csproj` use Scalar instead of Swashbuckle, (4) the launch URL points to a non-existent Swagger endpoint, (5) the README has structural and content issues, and (6) the `docs/` folder may reference Scalar or outdated information.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type ProjectFile (any .csproj, .sln, .cs, .json, or .md file in the solution)
  OUTPUT: boolean

  RETURN (input.type == "csproj" AND input.TargetFramework == "net9.0")
         OR (input.type == "csproj" AND input.name == "Sample.TransactionalOutbox.csproj"
             AND input.hasPackageReference("Scalar.AspNetCore")
             AND NOT input.hasPackageReference("Swashbuckle.AspNetCore"))
         OR (input.type == "sln" AND NOT input.hasSolutionFolder("Tests")
             AND NOT input.hasNestedProjects(testProjectGUIDs))
         OR (input.type == "cs" AND input.name == "Program.cs"
             AND input.contains("MapScalarApiReference")
             AND NOT input.contains("UseSwagger"))
         OR (input.type == "json" AND input.name == "launchSettings.json"
             AND input.launchUrl == "swagger"
             AND NOT programCsContains("UseSwaggerUI"))
         OR (input.type == "md" AND input.name == "README.md"
             AND (input.hasVerboseIntro
                  OR NOT input.hasTwoLRMermaidDiagrams
                  OR input.hasGettingStartedSection
                  OR input.hasVerboseTestingSection
                  OR NOT input.packageVersionsBeforeArticles
                  OR input.hasScalarSection
                  OR NOT input.hasMergedApiSwaggerSection))
END FUNCTION
```

### Examples

- **Framework bug**: `src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj` contains `<TargetFramework>net9.0</TargetFramework>` — expected `net10.0`. Same for all 5 other `.csproj` files.
- **Solution Folder bug**: `Sample.TransactionalOutbox.sln` lists all 6 projects at root level with no `Project("{2150E333-...}") = "Tests"` entry and no `NestedProjects` section.
- **Scalar bug**: `Program.cs` has `using Scalar.AspNetCore;` and `app.MapScalarApiReference();` — expected `builder.Services.AddSwaggerGen();`, `app.UseSwagger();`, `app.UseSwaggerUI();`.
- **Package bug**: API `.csproj` has `<PackageReference Include="Scalar.AspNetCore" Version="2.2.7" />` — expected `<PackageReference Include="Swashbuckle.AspNetCore" ... />`.
- **Launch URL bug**: `launchSettings.json` has `"launchUrl": "swagger"` but `Program.cs` doesn't configure Swagger middleware, so the browser opens to a 404.
- **README bugs**: Introduction is a full paragraph explaining the pattern and listing technologies; only one vertical Mermaid diagram; "Getting Started" section with clone/build/run instructions; verbose "Testing the Flow" with step-by-step manual instructions; "Package Versions" in the middle; separate "Scalar API Reference" section instead of merged Swagger subsection.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- The solution SHALL continue to compile without errors after all changes
- All existing unit and property-based tests SHALL continue to pass
- The 3 API endpoints (GET /Products, GET /Orders, POST /PurchaseOrder/{id}) SHALL continue to function correctly
- The 3 source projects (API, Domain, Persistence) SHALL remain at the root level of the solution (not nested in any Solution Folder)
- The Quartz.NET background job SHALL continue to process outbox messages correctly
- The `docs/` folder markdown files SHALL continue to contain accurate documentation with working links back to the README

**Scope:**
All inputs that do NOT involve the 6 bug categories (framework version, solution structure, OpenAPI UI package, launch URL, README content, docs/ content) should be completely unaffected by this fix. This includes:
- Domain entity logic (OrderEntity, ProductEntity, DomainEventManager)
- Persistence layer logic (interceptor, repositories, DbContext, configurations)
- Quartz.NET job logic (OutboxMessageProcessorJob)
- MediatR event handler logic (OrderConfirmedEventHandler)
- Database seeding logic (SeedDb)

## Hypothesized Root Cause

Based on the bug description, the issues stem from the previous `project-update` spec execution:

1. **Framework Version Not Upgraded**: The previous spec's design document stated "net9.0" as the target (it was written when .NET 9 was current). The `.csproj` files were either not updated or were updated to the same version they already had. All 6 files still contain `<TargetFramework>net9.0</TargetFramework>`.

2. **Unsolicited Scalar Introduction**: The previous spec's design document included a "Replace Swashbuckle with Scalar" decision, stating Swashbuckle was deprecated. This was incorrect — Swashbuckle is still widely used and was the originally intended package. The implementation followed the design and replaced Swashbuckle with Scalar.

3. **Missing Solution Folder**: The previous spec did not include a requirement to organize test projects in a Solution Folder. The 3 test projects were added to the `.sln` at root level alongside the 3 source projects.

4. **Launch URL Mismatch**: The `launchSettings.json` has `"launchUrl": "swagger"` (which is the correct Swagger UI path), but `Program.cs` was changed to use Scalar (which serves at `/scalar/v1`). The launch URL was not updated to match the Scalar path, and now needs to stay as "swagger" once Swashbuckle is restored.

5. **README Structure Issues**: The previous spec's README rewrite followed a different structure than what was desired — verbose introduction, single vertical diagram, included "Getting Started", verbose "Testing the Flow", mispositioned "Package Versions", and a separate Scalar section.

## Correctness Properties

Property 1: Bug Condition - Framework Version Upgrade

_For any_ `.csproj` file in the solution (all 6 projects), the `<TargetFramework>` element SHALL contain `net10.0` and all NuGet package versions SHALL be updated to the latest stable versions compatible with .NET 10.

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition - Solution Folder Organization

_For any_ test project in the solution (Domain.Tests, Persistence.Tests, Tests), the `.sln` file SHALL contain a Solution Folder named "Tests" and a `NestedProjects` section mapping each test project GUID to the Tests folder GUID, while source projects remain at root level.

**Validates: Requirements 2.3, 3.4**

Property 3: Bug Condition - Swashbuckle Restoration

_For any_ inspection of `Program.cs` and the API `.csproj`, the code SHALL use `Swashbuckle.AspNetCore` (`AddSwaggerGen`, `UseSwagger`, `UseSwaggerUI`) and SHALL NOT reference `Scalar.AspNetCore` in any form.

**Validates: Requirements 2.4, 2.5**

Property 4: Bug Condition - Launch URL Consistency

_For any_ launch profile in `launchSettings.json` with `"launchUrl": "swagger"`, the `Program.cs` SHALL have Swagger middleware configured so the URL resolves to the Swagger UI without 404 errors.

**Validates: Requirements 2.6**

Property 5: Bug Condition - README Structure Compliance

_For any_ inspection of `README.md`, the document SHALL have a concise one-line introduction, two horizontal (LR) Mermaid diagrams, no "Getting Started" section, a simplified "Testing the Flow" referencing the `.http` file and Swagger, "Package Versions" positioned before "Articles" at the end, and a merged "API Endpoints" section with Swagger as a subsection.

**Validates: Requirements 2.7, 2.8, 2.9, 2.10, 2.11, 2.12, 2.13**

Property 6: Preservation - Build and Test Integrity

_For any_ build or test execution after all changes are applied, the solution SHALL compile without errors and all existing tests SHALL pass, preserving the same functional behavior for all domain logic, persistence logic, and API endpoints.

**Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6, 3.7**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: All 6 `.csproj` files

**Specific Changes**:
1. **Target Framework Upgrade**: Change `<TargetFramework>net9.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>` in:
   - `src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj`
   - `src/Sample.TransactionalOutbox.Domain/Sample.TransactionalOutbox.Domain.csproj`
   - `src/Sample.TransactionalOutbox.Persistence/Sample.TransactionalOutbox.Persistence.csproj`
   - `test/Sample.TransactionalOutbox.Domain.Tests/Sample.TransactionalOutbox.Domain.Tests.csproj`
   - `test/Sample.TransactionalOutbox.Persistence.Tests/Sample.TransactionalOutbox.Persistence.Tests.csproj`
   - `test/Sample.TransactionalOutbox.Tests/Sample.TransactionalOutbox.Tests.csproj`

2. **NuGet Package Version Upgrades**: Update all packages to latest stable .NET 10-compatible versions:
   - `Microsoft.AspNetCore.OpenApi` → latest 10.0.x
   - `Microsoft.EntityFrameworkCore.InMemory` → latest 10.0.x
   - `Microsoft.Extensions.Logging.Abstractions` → latest 10.0.x
   - `MediatR` → latest stable (14.x or newer)
   - `Quartz` / `Quartz.Extensions.Hosting` → latest stable 3.x
   - `Newtonsoft.Json` → latest stable 13.x
   - Test packages (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FsCheck.Xunit`, `FluentAssertions`, `NSubstitute`) → latest stable versions

---

**File**: `src/Sample.TransactionalOutbox.sln`

**Specific Changes**:
3. **Add Tests Solution Folder**: Add a `Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Tests", "Tests", "{NEW-GUID}"` entry
4. **Add NestedProjects Section**: Add a `GlobalSection(NestedProjects) = preSolution` mapping the 3 test project GUIDs to the Tests folder GUID:
   - `{E01E4EFE-C2EC-487C-BA25-3046804BBFF6}` (Domain.Tests)
   - `{C8B66768-D869-4A77-A68E-3BE378ADE765}` (Persistence.Tests)
   - `{31B36C01-4C50-470D-9D18-F0BCFEA6DFA6}` (Tests)

---

**File**: `src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj`

**Specific Changes**:
5. **Replace Scalar with Swashbuckle**: Remove `<PackageReference Include="Scalar.AspNetCore" Version="2.2.7" />` and add `<PackageReference Include="Swashbuckle.AspNetCore" Version="..." />` (latest stable compatible with .NET 10)

---

**File**: `src/Sample.TransactionalOutbox/Program.cs`

**Specific Changes**:
6. **Remove Scalar using**: Remove `using Scalar.AspNetCore;`
7. **Add Swagger service registration**: Add `builder.Services.AddSwaggerGen();` after `builder.Services.AddOpenApi();`
8. **Replace Scalar middleware**: Replace `app.MapScalarApiReference();` with `app.UseSwagger();` and `app.UseSwaggerUI();`
9. **Keep OpenApi**: Retain `builder.Services.AddOpenApi();` and `app.MapOpenApi();` for OpenAPI document generation

---

**File**: `src/Sample.TransactionalOutbox/Properties/launchSettings.json`

**Specific Changes**:
10. **No change needed**: The `"launchUrl": "swagger"` is already correct for Swashbuckle's default Swagger UI path (`/swagger`). Once Swagger middleware is configured in `Program.cs`, this URL will resolve correctly.

---

**File**: `README.md`

**Specific Changes**:
11. **Concise Introduction**: Replace the verbose paragraph with the project title and a single-line description
12. **Project Structure**: Simplify to high-level purpose and main entities only
13. **Pattern Flow**: Replace the single vertical Mermaid diagram with two horizontal (LR) diagrams:
    - Diagram 1: Conceptual overview of the Transactional Outbox pattern
    - Diagram 2: Detailed implementation diagram highlighting EF Core interceptor, same-transaction persistence, and MediatR dispatch
14. **Remove Getting Started**: Delete the entire "Getting Started" section
15. **Simplify Testing the Flow**: Replace step-by-step instructions with a reference to the `.http` file and Swagger UI
16. **Reposition Package Versions**: Move to end of document (before "Articles")
17. **Merge API/Swagger sections**: Combine "API Endpoints" and "Scalar API Reference" into one section with Swagger as a subsection; remove all Scalar references
18. **Update Table of Contents**: Reflect the new section structure

---

**File**: `docs/` folder

**Specific Changes**:
19. **Update docs if needed**: Review `docs/domain-event-manager.md`, `docs/outbox-interceptor.md`, and `docs/outbox-processor-job.md` for any references to Scalar or outdated information. Update links and references to match the new README structure.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, verify the current defective state to confirm the bug conditions, then apply fixes and verify correctness plus preservation of existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Confirm the defective state BEFORE implementing fixes. Verify each bug condition exists as described.

**Test Plan**: Inspect project files to confirm the current defective state matches the bug description.

**Test Cases**:
1. **Framework Version Check**: Verify all 6 `.csproj` files contain `net9.0` (will confirm bug on unfixed code)
2. **Solution Folder Check**: Verify `.sln` has no Solution Folder for test projects (will confirm bug on unfixed code)
3. **Scalar Check**: Verify `Program.cs` contains `MapScalarApiReference` and `.csproj` references `Scalar.AspNetCore` (will confirm bug on unfixed code)
4. **Launch URL Check**: Verify `launchSettings.json` has `"launchUrl": "swagger"` while `Program.cs` has no Swagger middleware (will confirm bug on unfixed code)
5. **README Structure Check**: Verify README has verbose intro, single vertical diagram, "Getting Started" section, etc. (will confirm bug on unfixed code)

**Expected Counterexamples**:
- All `.csproj` files will show `net9.0` instead of `net10.0`
- `.sln` will have no `NestedProjects` section
- `Program.cs` will contain Scalar references instead of Swashbuckle

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed files produce the expected state.

**Pseudocode:**
```
FOR ALL file WHERE isBugCondition(file) DO
  result := applyFix(file)
  ASSERT expectedState(result)
END FOR
```

Specifically:
- All 6 `.csproj` files contain `<TargetFramework>net10.0</TargetFramework>`
- `.sln` contains a "Tests" Solution Folder with correct `NestedProjects` mappings
- `Program.cs` uses `AddSwaggerGen`, `UseSwagger`, `UseSwaggerUI` and has no Scalar references
- API `.csproj` references `Swashbuckle.AspNetCore` and not `Scalar.AspNetCore`
- README has the correct structure (concise intro, two LR diagrams, no "Getting Started", etc.)

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the solution behavior is unchanged.

**Pseudocode:**
```
FOR ALL behavior WHERE NOT isBugCondition(behavior) DO
  ASSERT originalBehavior(behavior) == fixedBehavior(behavior)
END FOR
```

**Testing Approach**: Build and test verification is the primary preservation checking mechanism:
- `dotnet build src/Sample.TransactionalOutbox.sln` must succeed with zero errors
- `dotnet test src/Sample.TransactionalOutbox.sln` must pass all existing tests
- All domain logic, persistence logic, and API endpoint behavior must remain unchanged

**Test Plan**: Run the full build and test suite after applying all fixes to verify no regressions.

**Test Cases**:
1. **Build Preservation**: Verify `dotnet build` succeeds with zero errors after all changes
2. **Test Preservation**: Verify `dotnet test` passes all existing unit and property-based tests
3. **Source Project Preservation**: Verify the 3 source projects are NOT nested in any Solution Folder
4. **Docs Preservation**: Verify `docs/` files still have accurate content and working links

### Unit Tests

- Verify each `.csproj` file contains the correct `TargetFramework` value
- Verify the `.sln` file has the correct Solution Folder structure
- Verify `Program.cs` contains Swashbuckle middleware calls and no Scalar references
- Verify the API `.csproj` has the correct package references
- Verify README section structure and content

### Property-Based Tests

- For all `.csproj` files in the solution, verify `TargetFramework` is `net10.0`
- For all NuGet package references, verify versions are compatible with .NET 10
- For all test project GUIDs in the `.sln`, verify they are nested under the Tests folder

### Integration Tests

- Build the full solution after all changes and verify zero errors
- Run the full test suite and verify all tests pass
- Verify the application starts and Swagger UI is accessible at `/swagger`
