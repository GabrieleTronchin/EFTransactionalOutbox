# Implementation Plan

- [ ] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Project Configuration Defects
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bugs exist
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bugs exist across all 6 categories
  - **Scoped PBT Approach**: Scope the property to the concrete failing cases — deterministic file content checks
  - Verify all 6 `.csproj` files contain `<TargetFramework>net9.0</TargetFramework>` (Bug Condition: `input.TargetFramework == "net9.0"`)
  - Verify `src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj` references `Scalar.AspNetCore` and does NOT reference `Swashbuckle.AspNetCore`
  - Verify `src/Sample.TransactionalOutbox.sln` has NO Solution Folder entry with GUID `{2150E333-8FDC-42A3-9474-1A3956D46DE8}` and NO `NestedProjects` section
  - Verify `src/Sample.TransactionalOutbox/Program.cs` contains `using Scalar.AspNetCore;` and `app.MapScalarApiReference()` and does NOT contain `UseSwagger` or `UseSwaggerUI` or `AddSwaggerGen`
  - Verify `README.md` has verbose introduction (more than 2 lines), contains "Getting Started" section, contains "Scalar API Reference" section, has only one Mermaid diagram using `flowchart TD`
  - Run test on UNFIXED code — expect FAILURE (this confirms the bugs exist)
  - Document counterexamples found (e.g., "all .csproj files have net9.0", "Program.cs uses Scalar instead of Swashbuckle", ".sln has no Tests folder")
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 1.11, 1.12, 1.13_

- [ ] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Build and Test Suite Baseline
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: Run `dotnet build src/Sample.TransactionalOutbox.sln` on unfixed code — observe it compiles successfully
  - Observe: Run `dotnet test src/Sample.TransactionalOutbox.sln` on unfixed code — observe all existing unit and property-based tests pass
  - Observe: Verify `docs/domain-event-manager.md`, `docs/outbox-interceptor.md`, `docs/outbox-processor-job.md` contain accurate content with working `← [Back to README](../README.md)` links
  - Observe: Verify the 3 source projects (API, Domain, Persistence) are at root level in `.sln` (not nested in any Solution Folder)
  - Record the baseline: build succeeds, all tests pass, docs are accurate, source projects at root level
  - Write property-based test: for all non-buggy inputs (domain logic, persistence logic, API endpoint behavior, Quartz job logic), behavior is unchanged
  - The preservation baseline is the existing test suite — `dotnet build` + `dotnet test` passing confirms preservation
  - Verify tests pass on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 3. Upgrade .NET target framework and NuGet packages

  - [ ] 3.1 Update TargetFramework to net10.0 in all 6 .csproj files
    - Change `<TargetFramework>net9.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>` in:
      - `src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj`
      - `src/Sample.TransactionalOutbox.Domain/Sample.TransactionalOutbox.Domain.csproj`
      - `src/Sample.TransactionalOutbox.Persistence/Sample.TransactionalOutbox.Persistence.csproj`
      - `test/Sample.TransactionalOutbox.Domain.Tests/Sample.TransactionalOutbox.Domain.Tests.csproj`
      - `test/Sample.TransactionalOutbox.Persistence.Tests/Sample.TransactionalOutbox.Persistence.Tests.csproj`
      - `test/Sample.TransactionalOutbox.Tests/Sample.TransactionalOutbox.Tests.csproj`
    - _Bug_Condition: isBugCondition(input) where input.type == "csproj" AND input.TargetFramework == "net9.0"_
    - _Expected_Behavior: all .csproj files contain `<TargetFramework>net10.0</TargetFramework>`_
    - _Preservation: Domain logic, persistence logic, API endpoints, Quartz job unchanged_
    - _Requirements: 2.1_

  - [ ] 3.2 Update NuGet package versions to latest stable .NET 10-compatible versions
    - Update `Microsoft.AspNetCore.OpenApi` to latest 10.0.x in API .csproj
    - Update `Microsoft.EntityFrameworkCore.InMemory` to latest 10.0.x in Persistence .csproj and test .csproj files that reference it
    - Update `Microsoft.Extensions.Logging.Abstractions` to latest 10.0.x in Domain .csproj
    - Update `MediatR` to latest stable in Domain .csproj
    - Update `Quartz` and `Quartz.Extensions.Hosting` to latest stable 3.x in API .csproj
    - Update `Newtonsoft.Json` to latest stable 13.x in Persistence .csproj
    - Update test packages (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FsCheck.Xunit`, `FluentAssertions`, `NSubstitute`) to latest stable in all 3 test .csproj files
    - Run `dotnet restore src/Sample.TransactionalOutbox.sln` to verify all packages resolve
    - _Bug_Condition: isBugCondition(input) where packages are .NET 9-era versions_
    - _Expected_Behavior: all packages at latest stable .NET 10-compatible versions_
    - _Preservation: All existing functionality unchanged_
    - _Requirements: 2.2_

- [ ] 4. Add "Tests" Solution Folder to .sln file

  - [ ] 4.1 Edit Sample.TransactionalOutbox.sln to add Solution Folder and NestedProjects
    - Add `Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Tests", "Tests", "{NEW-GUID}"` entry after the existing project entries
    - Add `GlobalSection(NestedProjects) = preSolution` section mapping the 3 test project GUIDs to the Tests folder GUID:
      - `{E01E4EFE-C2EC-487C-BA25-3046804BBFF6}` = `{Tests-Folder-GUID}` (Domain.Tests)
      - `{C8B66768-D869-4A77-A68E-3BE378ADE765}` = `{Tests-Folder-GUID}` (Persistence.Tests)
      - `{31B36C01-4C50-470D-9D18-F0BCFEA6DFA6}` = `{Tests-Folder-GUID}` (Tests)
    - Verify the 3 source projects (API, Domain, Persistence) are NOT nested — they remain at root level
    - _Bug_Condition: isBugCondition(input) where input.type == "sln" AND NOT input.hasSolutionFolder("Tests")_
    - _Expected_Behavior: .sln has "Tests" Solution Folder with NestedProjects mapping all 3 test project GUIDs_
    - _Preservation: Source projects remain at root level (Requirements 3.4)_
    - _Requirements: 2.3, 3.4_

- [ ] 5. Revert Scalar to Swashbuckle

  - [ ] 5.1 Replace Scalar.AspNetCore with Swashbuckle.AspNetCore in API .csproj
    - Remove `<PackageReference Include="Scalar.AspNetCore" Version="2.2.7" />`
    - Add `<PackageReference Include="Swashbuckle.AspNetCore" Version="..." />` (latest stable compatible with .NET 10)
    - _Bug_Condition: isBugCondition(input) where input.hasPackageReference("Scalar.AspNetCore") AND NOT input.hasPackageReference("Swashbuckle.AspNetCore")_
    - _Expected_Behavior: .csproj references Swashbuckle.AspNetCore, not Scalar.AspNetCore_
    - _Requirements: 2.5_

  - [ ] 5.2 Update Program.cs to use Swashbuckle instead of Scalar
    - Remove `using Scalar.AspNetCore;`
    - Add `builder.Services.AddSwaggerGen();` after `builder.Services.AddOpenApi();`
    - Remove `app.MapScalarApiReference();`
    - Add `app.UseSwagger();` and `app.UseSwaggerUI();` inside the `if (app.Environment.IsDevelopment())` block
    - Keep `builder.Services.AddOpenApi();` and `app.MapOpenApi();` for OpenAPI document generation
    - _Bug_Condition: isBugCondition(input) where input.contains("MapScalarApiReference") AND NOT input.contains("UseSwagger")_
    - _Expected_Behavior: Program.cs uses AddSwaggerGen, UseSwagger, UseSwaggerUI; no Scalar references_
    - _Preservation: OpenApi document generation unchanged; launchSettings.json "swagger" launchUrl now resolves correctly_
    - _Requirements: 2.4, 2.5, 2.6_

- [ ] 6. Rewrite README.md

  - [ ] 6.1 Rewrite introduction and Table of Contents
    - Replace verbose introduction paragraph with concise one-line project description
    - Update Table of Contents to reflect new section structure (no "Getting Started", no "Scalar API Reference", merged API/Swagger section, repositioned "Package Versions")
    - _Bug_Condition: isBugCondition(input) where input.hasVerboseIntro_
    - _Expected_Behavior: Concise one-line introduction_
    - _Requirements: 2.7_

  - [ ] 6.2 Simplify Project Structure section
    - Describe only high-level purpose of each project and its main entities
    - Remove implementation details from descriptions
    - _Bug_Condition: isBugCondition(input) where Project Structure has implementation details_
    - _Expected_Behavior: High-level descriptions with main entities only_
    - _Requirements: 2.8_

  - [ ] 6.3 Replace Pattern Flow with two horizontal Mermaid diagrams
    - Replace single vertical (TD) Mermaid diagram with two horizontal (LR) diagrams:
      - Diagram 1: Conceptual overview — what the Transactional Outbox pattern is
      - Diagram 2: Detailed implementation — EF Core interceptor, same-transaction persistence, MediatR dispatch
    - _Bug_Condition: isBugCondition(input) where NOT input.hasTwoLRMermaidDiagrams_
    - _Expected_Behavior: Two `flowchart LR` Mermaid diagrams_
    - _Requirements: 2.9_

  - [ ] 6.4 Remove Getting Started section
    - Delete the entire "Getting Started" section (clone/build/run instructions)
    - _Bug_Condition: isBugCondition(input) where input.hasGettingStartedSection_
    - _Expected_Behavior: No "Getting Started" section in README_
    - _Requirements: 2.10_

  - [ ] 6.5 Simplify Testing the Flow section
    - Replace step-by-step manual instructions with a reference to the `.http` file and Swagger UI for interactive API exploration
    - _Bug_Condition: isBugCondition(input) where input.hasVerboseTestingSection_
    - _Expected_Behavior: Simple reference to .http file and Swagger UI_
    - _Requirements: 2.11_

  - [ ] 6.6 Merge API Endpoints and Swagger into one section, reposition Package Versions
    - Combine "API Endpoints" and "Scalar API Reference" into a single "API Endpoints" section with Swagger as a subsection
    - Remove all Scalar references, replace with Swagger
    - Move "Package Versions" section to end of document (before "Articles")
    - Update package versions table to reflect .NET 10 and Swashbuckle
    - _Bug_Condition: isBugCondition(input) where input.hasScalarSection OR NOT input.packageVersionsBeforeArticles OR NOT input.hasMergedApiSwaggerSection_
    - _Expected_Behavior: Merged API/Swagger section; Package Versions at end before Articles; no Scalar references_
    - _Requirements: 2.12, 2.13_

- [ ] 7. Update docs/ folder

  - [ ] 7.1 Review and update docs/ files for Scalar references and accuracy
    - Review `docs/domain-event-manager.md`, `docs/outbox-interceptor.md`, `docs/outbox-processor-job.md`
    - Check for any references to Scalar and replace with Swagger if found
    - Verify `← [Back to README](../README.md)` links still work with new README structure
    - Verify source file path references are still accurate
    - Update any outdated information to match .NET 10 upgrade
    - _Preservation: docs/ files continue to contain accurate deep-dive documentation with working links_
    - _Requirements: 3.7_

- [ ] 8. Build and test verification

  - [ ] 8.1 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Project Configuration Defects Fixed
    - **IMPORTANT**: Re-run the SAME test from task 1 — do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied:
      - All 6 `.csproj` files target `net10.0`
      - `.sln` has "Tests" Solution Folder with correct NestedProjects
      - `Program.cs` uses Swashbuckle, no Scalar references
      - API `.csproj` references Swashbuckle, not Scalar
      - README has correct structure
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bugs are fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 2.10, 2.11, 2.12, 2.13_

  - [ ] 8.2 Verify preservation tests still pass
    - **Property 2: Preservation** - Build and Test Suite Integrity
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run `dotnet build src/Sample.TransactionalOutbox.sln` — must succeed with zero errors
    - Run `dotnet test src/Sample.TransactionalOutbox.sln` — all existing tests must pass
    - Verify docs/ files still have accurate content and working links
    - Verify source projects remain at root level in .sln
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
