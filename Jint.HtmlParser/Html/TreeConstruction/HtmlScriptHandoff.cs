namespace Jint.HtmlParser.Html;

internal enum HtmlHostRequestKind { MicrotaskCheckpoint, PrepareScript, WaitForPendingScript, ExecutePendingScript, ProcessSvgScript }
internal enum HtmlHostRequestOutcome { Finished, PendingParsingBlockingScript }

// Opaque identities are never interchangeable across sessions or requests.
internal sealed class HtmlHostRequestId { }

internal sealed class HtmlScriptFrame
{
    internal HtmlScriptFrame(HtmlParserSession owner, HtmlInsertionPoint point, int nestingLevel)
    {
        Owner = owner;
        Point = point;
        NestingLevel = nestingLevel;
    }

    internal HtmlParserSession Owner { get; }
    internal HtmlInsertionPoint Point { get; }
    internal int NestingLevel { get; }
    internal bool Completed { get; set; }
}

internal sealed class HtmlHostRequest
{
    internal HtmlHostRequest(HtmlHostRequestKind kind, Element script, HtmlScriptFrame? frame, Document parserDocument)
    {
        Kind = kind;
        Script = script;
        Frame = frame;
        ParserDocument = parserDocument;
    }

    internal HtmlHostRequestId Id { get; } = new();
    internal HtmlHostRequestKind Kind { get; }
    internal Element Script { get; }
    internal Document ParserDocument { get; }
    internal int NestingLevel => Frame?.NestingLevel ?? 0;
    internal HtmlScriptFrame? Frame { get; }
}
