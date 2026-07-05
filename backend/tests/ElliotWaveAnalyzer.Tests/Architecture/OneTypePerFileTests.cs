using System.Text.RegularExpressions;

namespace ElliotWaveAnalyzer.Tests.Architecture;

/// <summary>
/// Enforces one top-level type per file across the API source: a class/record/interface/enum/
/// struct declared at namespace scope (column 0) must live in its own file. Nested types stay
/// with their parent (they are indented, so they do not count here). Keeps the codebase
/// navigable — the file name is the type name.
/// </summary>
[TestFixture]
public sealed class OneTypePerFileTests
{
    // A top-level type: line starts at column 0 (no leading whitespace, so nested/indented types
    // are excluded) with optional modifiers, then a type keyword.
    private static readonly Regex TopLevelType = new(
        @"^(public |internal |private |protected |sealed |abstract |static |partial |file |readonly |unsafe )*(class|record|interface|enum|struct)\b",
        RegexOptions.Multiline | RegexOptions.Compiled);

    [Test]
    public void EveryApiSourceFile_DeclaresAtMostOneTopLevelType()
    {
        var apiRoot = Path.Combine(FindBackendRoot(), "src", "ElliotWaveAnalyzer.Api");

        var offenders = Directory
            .EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Select(p => (Path: p, Count: TopLevelType.Matches(File.ReadAllText(p)).Count))
            .Where(x => x.Count > 1)
            .Select(x => $"{Path.GetFileName(x.Path)} ({x.Count} types)")
            .ToList();

        Assert.That(offenders, Is.Empty,
            "These files declare more than one top-level type — split them so each type has its own file:\n"
            + string.Join("\n", offenders));
    }

    /// <summary>Walks up from the test assembly until the folder that contains the source tree.</summary>
    private static string FindBackendRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "ElliotWaveAnalyzer.Api")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the backend source root from the test assembly.");
    }
}
