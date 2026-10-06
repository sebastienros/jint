using Jint.HtmlParser.Html;

namespace Jint.HtmlParser;

// HTML Standard §4.12.1: intrinsic script state survives native identity/moves.
// Cloning copies already-started, but does not make the copy parser-inserted.
internal sealed class HtmlScriptState
{
    // Captured before insertion; clone/import deliberately copy only AlreadyStarted.
    // SVG processing has no HtmlScriptState and no HTML source-location attribution.
    internal HtmlSourceLocation? ParserSourceLocation { get; set; }
    internal long ParserSourceChanges { get; set; }
    internal bool AlreadyStarted { get; set; }
    internal bool ForceAsync { get; set; } = true;
    internal Document? PreparationTimeDocument { get; set; }
    internal Document? ParserDocument { get; set; }
    internal bool ParserInserted => ParserDocument is not null;
}
