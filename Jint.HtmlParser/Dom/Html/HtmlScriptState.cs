namespace Jint.HtmlParser;

// HTML Standard §4.12.1: intrinsic script state survives native identity/moves.
// Cloning copies already-started, but does not make the copy parser-inserted.
internal sealed class HtmlScriptState
{
    internal bool AlreadyStarted { get; set; }
    internal bool ForceAsync { get; set; } = true;
    internal Document? PreparationTimeDocument { get; set; }
    internal Document? ParserDocument { get; set; }
    internal bool ParserInserted => ParserDocument is not null;
}
