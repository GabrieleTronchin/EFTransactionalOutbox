using FluentAssertions;
using FsCheck.Xunit;
using System.Text.RegularExpressions;

namespace Sample.TransactionalOutbox.Tests;

/// <summary>
/// Bug Condition Exploration Tests — Property 1: Project Configuration Defects
///
/// These tests encode the EXPECTED (correct) behavior after all bugfixes are applied.
/// On UNFIXED code, these tests MUST FAIL — failure confirms the bugs exist.
/// Once the fixes are implemented, these same tests will PASS.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 1.11, 1.12, 1.13**
/// </summary>
public sealed class BugConditionExplorationTests
{
    // Resolve the repository root from the test assembly location
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        // Walk up until we find README.md at the repo root
        while (dir is not null && !File.Exists(Path.Combine(dir, "README.md")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new InvalidOperationException(
            "Could not find repository root (looked for README.md)");
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    // ──────────────────────────────────────────────
    // All 6 .csproj file paths relative to repo root
    // ──────────────────────────────────────────────
    private static readonly string[] AllCsprojPaths =
    [
        "src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj",
        "src/Sample.TransactionalOutbox.Domain/Sample.TransactionalOutbox.Domain.csproj",
        "src/Sample.TransactionalOutbox.Persistence/Sample.TransactionalOutbox.Persistence.csproj",
        "test/Sample.TransactionalOutbox.Domain.Tests/Sample.TransactionalOutbox.Domain.Tests.csproj",
        "test/Sample.TransactionalOutbox.Persistence.Tests/Sample.TransactionalOutbox.Persistence.Tests.csproj",
        "test/Sample.TransactionalOutbox.Tests/Sample.TransactionalOutbox.Tests.csproj",
    ];

    // ──────────────────────────────────────────────
    // Bug Condition 1: Target Framework — all .csproj files must target net10.0
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2**
    ///
    /// For ALL .csproj files in the solution, the TargetFramework element
    /// SHALL contain net10.0. On unfixed code this will fail because all
    /// files currently have net9.0.
    /// </summary>
    [Property(MaxTest = 1)]
    public void BugCondition1_AllCsprojFiles_ShouldTarget_Net10()
    {
        foreach (var csprojPath in AllCsprojPaths)
        {
            var content = ReadRepoFile(csprojPath);
            content.Should().Contain("<TargetFramework>net10.0</TargetFramework>",
                because: $"{csprojPath} should target net10.0");
        }
    }

    // ──────────────────────────────────────────────
    // Bug Condition 2: Swashbuckle in API .csproj (not Scalar)
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 1.4, 1.5**
    ///
    /// The API .csproj SHALL reference Swashbuckle.AspNetCore and SHALL NOT
    /// reference Scalar.AspNetCore. On unfixed code this will fail because
    /// the file currently references Scalar.
    /// </summary>
    [Property(MaxTest = 1)]
    public void BugCondition2_ApiCsproj_ShouldReference_Swashbuckle_NotScalar()
    {
        var content = ReadRepoFile(
            "src/Sample.TransactionalOutbox/Sample.TransactionalOutbox.csproj");

        content.Should().Contain("Swashbuckle.AspNetCore",
            because: "API project should use Swashbuckle for Swagger UI");
        content.Should().NotContain("Scalar.AspNetCore",
            because: "API project should not reference Scalar");
    }

    // ──────────────────────────────────────────────
    // Bug Condition 3: Solution Folder in .sln
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 1.3**
    ///
    /// The .sln file SHALL have a Solution Folder entry with GUID
    /// {2150E333-8FDC-42A3-9474-1A3956D46DE8} and SHALL have a
    /// NestedProjects section. On unfixed code this will fail because
    /// neither exists.
    /// </summary>
    [Property(MaxTest = 1)]
    public void BugCondition3_SlnFile_ShouldHave_TestsSolutionFolder_AndNestedProjects()
    {
        var content = ReadRepoFile("src/Sample.TransactionalOutbox.sln");

        content.Should().Contain("{2150E333-8FDC-42A3-9474-1A3956D46DE8}",
            because: ".sln should have a Solution Folder entry (GUID {2150E333-...})");
        content.Should().Contain("NestedProjects",
            because: ".sln should have a NestedProjects section mapping test projects to the Tests folder");
    }

    // ──────────────────────────────────────────────
    // Bug Condition 4: Program.cs uses Swashbuckle, not Scalar
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 1.4, 1.6**
    ///
    /// Program.cs SHALL contain UseSwagger, UseSwaggerUI, and AddSwaggerGen,
    /// and SHALL NOT contain "using Scalar.AspNetCore;" or "MapScalarApiReference".
    /// On unfixed code this will fail because Program.cs uses Scalar.
    /// </summary>
    [Property(MaxTest = 1)]
    public void BugCondition4_ProgramCs_ShouldUse_Swashbuckle_NotScalar()
    {
        var content = ReadRepoFile("src/Sample.TransactionalOutbox/Program.cs");

        content.Should().Contain("UseSwagger",
            because: "Program.cs should configure Swagger middleware");
        content.Should().Contain("UseSwaggerUI",
            because: "Program.cs should configure Swagger UI middleware");
        content.Should().Contain("AddSwaggerGen",
            because: "Program.cs should register Swagger generation services");
        content.Should().NotContain("using Scalar.AspNetCore;",
            because: "Program.cs should not import Scalar namespace");
        content.Should().NotContain("MapScalarApiReference",
            because: "Program.cs should not use Scalar API reference");
    }

    // ──────────────────────────────────────────────
    // Bug Condition 5: README.md structure
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 1.7, 1.8, 1.9, 1.10, 1.11, 1.12, 1.13**
    ///
    /// README.md SHALL have a concise introduction (2 lines or fewer before
    /// the first ## heading), SHALL NOT contain a "Getting Started" section,
    /// SHALL NOT contain a "Scalar API Reference" section, and SHALL have
    /// two Mermaid diagrams using "flowchart LR". On unfixed code this will
    /// fail because the README has a verbose intro, Getting Started, Scalar
    /// section, and one TD diagram.
    /// </summary>
    [Property(MaxTest = 1)]
    public void BugCondition5_ReadmeMd_ShouldHave_CorrectStructure()
    {
        var content = ReadRepoFile("README.md");
        var lines = content.Split('\n');

        // Check 1: Concise introduction — lines between the first # heading and first ## heading
        // should be 2 or fewer non-empty lines
        var firstH1Index = Array.FindIndex(lines, l => l.TrimStart().StartsWith("# "));
        var firstH2Index = Array.FindIndex(lines, (firstH1Index >= 0 ? firstH1Index + 1 : 0),
            l => l.TrimStart().StartsWith("## "));

        var introLines = (firstH1Index >= 0 && firstH2Index > firstH1Index)
            ? lines[(firstH1Index + 1)..firstH2Index]
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray()
            : Array.Empty<string>();

        introLines.Length.Should().BeLessThanOrEqualTo(2,
            because: "README introduction should be concise (2 non-empty lines or fewer)");

        // Check 2: No "Getting Started" section
        var hasGettingStarted = lines.Any(l =>
            Regex.IsMatch(l.Trim(), @"^#{1,3}\s+Getting Started", RegexOptions.IgnoreCase));
        hasGettingStarted.Should().BeFalse(
            because: "README should not contain a 'Getting Started' section");

        // Check 3: No "Scalar API Reference" section
        var hasScalarSection = lines.Any(l =>
            Regex.IsMatch(l.Trim(), @"^#{1,3}\s+Scalar", RegexOptions.IgnoreCase));
        hasScalarSection.Should().BeFalse(
            because: "README should not contain a 'Scalar' section");

        // Check 4: Two Mermaid diagrams using "flowchart LR"
        var flowchartLRCount = Regex.Matches(content, @"flowchart\s+LR").Count;
        flowchartLRCount.Should().Be(2,
            because: "README should have exactly two Mermaid diagrams using 'flowchart LR'");
    }
}
