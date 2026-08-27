#nullable enable

using System.Globalization;
using System.Text.RegularExpressions;

namespace Jint.Tests;

/// <summary>
/// <c>docs/v5-migration.md</c> held to the numbering rule it states for itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a test.</b> The guide's own "Keeping this document current" section already says numbering is
/// assigned at merge rather than at authoring, and explains exactly why: several pull requests are in flight
/// at once, each picks what looked like the next free subsection when it was written, and because two of them
/// append to different parts of one file <b>git merges them cleanly</b>. The duplicate exists only in the
/// rendered document, so nothing on the way in reports it — not the compiler, not the reviewer reading a diff
/// that shows one added section, not the merge. Stating the rule in prose was not enough: during the v5
/// campaign collisions were caught by hand roughly ten times and twice reached <c>main</c>, needing
/// <see href="https://github.com/sebastienros/jint/pull/3344">#3344</see> and
/// <see href="https://github.com/sebastienros/jint/pull/3347">#3347</see> to clean up after them.
/// </para>
/// <para>
/// <b>What it does not check.</b> Gaps are legitimate and common — a merged section can sit at 4.37 while
/// 4.36 is still in an open pull request — so only duplication and ordering are asserted, never contiguity.
/// </para>
/// </remarks>
[Parallelizable(ParallelScope.All)]
public class MigrationGuideTests
{
    private static readonly Regex ChapterHeading = new(@"^## (\d+)\. ", RegexOptions.Compiled);
    private static readonly Regex SectionHeading = new(@"^### (\d+)\.(\d+) ", RegexOptions.Compiled);

    /// <summary>
    /// Two sections cannot claim one number: that is the collision git merges without noticing.
    /// </summary>
    [Test]
    public void NoTwoSectionsShareANumber()
    {
        var duplicates = ReadSections()
            .GroupBy(static section => (section.Chapter, section.Number))
            .Where(static group => group.Count() > 1)
            .Select(static group =>
                $"§{group.Key.Chapter}.{group.Key.Number} is claimed by "
                + string.Join(" and ", group.Select(static s => $"line {s.Line} (\"{s.Title}\")")))
            .ToList();

        duplicates.Should().BeEmpty(
            "two pull requests each picked this number while in flight, and git merged both cleanly. "
            + "Whoever merges second renumbers, and repoints any [§x.y](#xy-...) link to the section — "
            + "see \"Keeping this document current\" at the end of the guide");
    }

    /// <summary>
    /// A section numbered <c>4.x</c> belongs under chapter 4, because the chapter is what the number means.
    /// </summary>
    [Test]
    public void EverySectionSitsUnderItsOwnChapter()
    {
        var misplaced = ReadSections()
            .Where(static section => section.Chapter != section.EnclosingChapter)
            .Select(static section =>
                $"line {section.Line}: \"### {section.Chapter}.{section.Number} {section.Title}\" "
                + $"sits under \"## {section.EnclosingChapter}.\"")
            .ToList();

        misplaced.Should().BeEmpty(
            "the chapter decides what the section number means, so a section renumbered into another "
            + "chapter has to move with its number");
    }

    /// <summary>
    /// Section numbers ascend in document order, so a renumbering that resolves a collision cannot leave the
    /// new entry sitting above the one it was numbered after. Gaps are fine — an open pull request holds them.
    /// </summary>
    [Test]
    public void SectionNumbersAscendInDocumentOrder()
    {
        var previous = new Dictionary<int, Section>();
        var outOfOrder = new List<string>();

        foreach (var section in ReadSections())
        {
            if (previous.TryGetValue(section.Chapter, out var earlier) && section.Number <= earlier.Number)
            {
                outOfOrder.Add(
                    $"line {section.Line}: §{section.Chapter}.{section.Number} follows "
                    + $"§{earlier.Chapter}.{earlier.Number} at line {earlier.Line}");
            }

            previous[section.Chapter] = section;
        }

        outOfOrder.Should().BeEmpty("a reader scanning for §4.12 reads downwards and stops when the numbers pass it");
    }

    private readonly record struct Section(int Chapter, int Number, string Title, int Line, int EnclosingChapter);

    private static List<Section> ReadSections()
    {
        var path = Path.Combine(AgentInstructionFiles.RepositoryRoot, "docs", "v5-migration.md");
        File.Exists(path).Should().BeTrue($"the migration guide is expected at {path}");

        var sections = new List<Section>();
        var enclosingChapter = 0;
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;

            var chapter = ChapterHeading.Match(line);
            if (chapter.Success)
            {
                enclosingChapter = int.Parse(chapter.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            var section = SectionHeading.Match(line);
            if (!section.Success)
            {
                continue;
            }

            sections.Add(new Section(
                int.Parse(section.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(section.Groups[2].Value, CultureInfo.InvariantCulture),
                line[section.Length..].Trim(),
                lineNumber,
                enclosingChapter));
        }

        sections.Should().NotBeEmpty("the guide is numbered by section, and finding none means this test stopped testing anything");
        return sections;
    }
}
