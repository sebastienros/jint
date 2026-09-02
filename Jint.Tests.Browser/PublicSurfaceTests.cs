using Jint.Browser.Dom;

namespace Jint.Tests.Browser;

/// <summary>
/// <c>Jint.Browser</c> exposes nothing publicly, and this is what keeps that true.
/// </summary>
/// <remarks>
/// It is a decision with a date on it rather than an oversight: the binding is a working surface until the
/// browser runtime that consumes it has settled what a host actually holds, and a public type shipped now
/// would be a compatibility promise made before anybody had used it. When a seam is promoted, this test is
/// where the promotion is declared — and where an accidental one is caught before a release makes it
/// permanent. <c>Jint.Browser/AGENTS.md</c> names the four most likely candidates.
/// </remarks>
public sealed class PublicSurfaceTests
{
    [Test]
    public void NothingInThePackageIsPublicYet()
    {
        var exported = typeof(DomBindings).Assembly.GetExportedTypes();

        exported.Should().BeEmpty(
            "Jint.Browser's surface is internal until the browser runtime settles what a host holds; promote a seam deliberately, with XML docs and a docs/v5-migration.md row, and say so here");
    }
}
