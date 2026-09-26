using System;
using System.Collections.Generic;
using System.Threading;

namespace Jint.HtmlParser.Html;

// Browser supplies already cached, engine-free facts. This lookup must not run
// JavaScript, invoke custom callbacks, or materialize DOM behavior.
internal interface IHtmlShadowHostContextProvider
{
    ShadowAttachmentContext GetShadowAttachmentContext(Element host);
}

internal readonly record struct HtmlDocumentContext(bool IsSrcdoc = false, bool CannotChangeMode = false,
    bool AllowDeclarativeShadowRoots = false, IHtmlShadowHostContextProvider? ShadowHostContextProvider = null);

internal enum HtmlParseStepKind { NeedInput, Yielded, Complete, MissingFeature, HostRequest, InsertionBoundary, PendingBlocker, ParserPaused }
internal enum HtmlParserScriptingMode { Normal, Disabled, Inert, Fragment }

internal enum HtmlMissingFeature { Tables, Select, Templates, ForeignContent }

internal readonly struct HtmlParseStep
{
    internal HtmlParseStep(HtmlParseStepKind kind, HtmlMissingFeature? missingFeature = null, long offset = 0, HtmlHostRequest? hostRequest = null)
    {
        Kind = kind;
        MissingFeature = missingFeature;
        Offset = offset;
        HostRequest = hostRequest;
    }

    internal HtmlParseStepKind Kind { get; }
    internal HtmlMissingFeature? MissingFeature { get; }
    internal long Offset { get; }
    internal HtmlHostRequest? HostRequest { get; }
}

// HTML Standard §13.2.6: one tokenizer, one tree builder, and a single work budget.
internal sealed partial class HtmlParserSession
{
    private readonly HtmlTokenizer _tokenizer;
    private readonly HtmlTreeBuilder _builder;
    private bool _terminal;
    private bool _complete;
    private int _entered;
    private readonly List<HtmlHostRequest> _requests = [];
    private HtmlHostRequest? _checkpoint;
    private Element? _pendingBlocker;
    private HtmlHostRequest? _waitRequest;
    private Element? _readyBlocker;
    private bool _parserPaused;

    internal HtmlParserSession(Document document, HtmlParseOptions? options = null,
        HtmlDocumentContext context = default, bool enableScriptRequests = false,
        HtmlParserScriptingMode? scriptingMode = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Kind != DocumentKind.Html || document.ContentType != "text/html" || document.FirstChild is not null)
            throw new ArgumentException("An empty HTML document is required.", nameof(document));

        options ??= new HtmlParseOptions();
        Document = document;
        _tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(options.Limits, options.Diagnostics));
        _builder = new HtmlTreeBuilder(document, _tokenizer, options.Limits.MaxNestingDepth,
            scriptingMode ?? (options.ScriptingEnabled ? HtmlParserScriptingMode.Normal : HtmlParserScriptingMode.Disabled),
            options.Diagnostics, context)
        {
            ScriptRequestsEnabled = enableScriptRequests
        };
    }

    internal Document Document { get; }
    internal bool ParserPaused => _parserPaused;
    internal int ScriptNestingLevel => _requests.Count;
    internal long WorkCount => SaturatingAdd(_tokenizer.WorkCount, _builder.WorkCount);

    // Read only at a returned parser boundary; insertion records precede stack Push.
    internal bool IsStyleOpen(Element element) => !_terminal && _builder.IsStyleOpen(element);

    internal bool TryTakeCompletedStyle(out Element? element, CancellationToken cancellationToken = default)
    {
        Enter();
        try { return _builder.TryTakeCompletedStyle(out element, cancellationToken); }
        catch (OperationCanceledException) { Invalidate(); throw; }
        finally { Exit(); }
    }

    internal void AppendInput(string chunk, bool isFinal = false)
    {
        Enter();
        try
        {
            CheckActive();
            _tokenizer.AppendInput(chunk, isFinal);
        }
        catch (ParseLimitException) { Invalidate(); throw; }
        finally { Exit(); }
    }

    // Invalidate immediately; release at most quota markers per cooperative call.
    // A host can repeat this after a Yielded result, including after cancellation.
    internal HtmlParseStep Abort(int workQuota = 256, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workQuota);
        Enter();
        try
        {
            InvalidateState();
            ReleaseFrames(workQuota, cancellationToken);
            return new HtmlParseStep(_requests.Count == 0 ? HtmlParseStepKind.Complete : HtmlParseStepKind.Yielded);
        }
        finally { Exit(); }
    }

    internal void CompleteHostRequest(HtmlHostRequestId id, HtmlHostRequestOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            CheckActive();
            cancellationToken.ThrowIfCancellationRequested();
            if (_checkpoint is { } checkpoint)
            {
                if (!ReferenceEquals(id, checkpoint.Id) || outcome != HtmlHostRequestOutcome.Finished)
                    throw new InvalidOperationException("The checkpoint completion is stale or invalid.");
                _checkpoint = null;
                _builder.CompleteScriptCheckpoint();
                return;
            }
            if (_waitRequest is { } wait)
            {
                if (!ReferenceEquals(id, wait.Id) || outcome != HtmlHostRequestOutcome.Finished)
                    throw new InvalidOperationException("The resource-wait completion is stale or invalid.");
                _waitRequest = null;
                _readyBlocker = wait.Script;
                return;
            }
            if (_requests.Count == 0 || !ReferenceEquals(_requests[^1].Id, id))
                throw new InvalidOperationException("The host completion is stale, foreign or out of order.");
            var request = _requests[^1];
            if (outcome is not (HtmlHostRequestOutcome.Finished or HtmlHostRequestOutcome.PendingParsingBlockingScript) ||
                outcome == HtmlHostRequestOutcome.PendingParsingBlockingScript &&
                (request.Kind != HtmlHostRequestKind.PrepareScript || _pendingBlocker is not null))
                throw new InvalidOperationException("Invalid host request outcome.");
            _tokenizer.ReleaseInsertionPoint(request.Frame!.Point);
            request.Frame.Completed = true;
            _requests.RemoveAt(_requests.Count - 1);
            if (outcome == HtmlHostRequestOutcome.PendingParsingBlockingScript)
            {
                _pendingBlocker = request.Script;
                _parserPaused = true;
            }
            if (_requests.Count == 0) _parserPaused = false;
        }
        catch (OperationCanceledException) { Invalidate(); throw; }
        finally { Exit(); }
    }

    internal void InsertInput(HtmlScriptFrame frame, string text, CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            CheckActiveFrame(frame);
            _tokenizer.InsertInput(frame.Point, text, cancellationToken);
        }
        catch (OperationCanceledException) { Invalidate(); throw; }
        catch (ParseLimitException) { Invalidate(); throw; }
        finally { Exit(); }
    }

    internal HtmlParseStep Drive(int workQuota, CancellationToken cancellationToken)
        => DriveCore(null, workQuota, cancellationToken);

    internal HtmlParseStep DriveInsertedInput(HtmlScriptFrame frame, int workQuota, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return DriveCore(frame, workQuota, cancellationToken);
    }

    private HtmlParseStep DriveCore(HtmlScriptFrame? frame, int workQuota, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workQuota);
        Enter();
        try
        {
            if (frame is not null) CheckActiveFrame(frame);
            if (_builder.HasCompletedStyles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _builder.BeginDriveRootTracking();
                return new HtmlParseStep(HtmlParseStepKind.Yielded);
            }
            if (_complete && !_terminal) return new HtmlParseStep(HtmlParseStepKind.Complete);
            CheckActive();
            cancellationToken.ThrowIfCancellationRequested();
            _builder.BeginDriveRootTracking();
            if (_checkpoint is not null) return RequestStep(_checkpoint);
            if (_waitRequest is not null) return RequestStep(_waitRequest);
            if (frame is null && _requests.Count != 0) return RequestStep(_requests[^1]);
            if (frame is not null && _parserPaused)
                return new HtmlParseStep(_pendingBlocker is not null ? HtmlParseStepKind.PendingBlocker : HtmlParseStepKind.ParserPaused);
            if (_readyBlocker is { } ready)
            {
                _readyBlocker = null;
                return BeginRequest(HtmlHostRequestKind.ExecutePendingScript, ready);
            }
            if (_pendingBlocker is { } blocker)
            {
                if (_requests.Count != 0) return new HtmlParseStep(HtmlParseStepKind.PendingBlocker);
                _pendingBlocker = null;
                _waitRequest = new HtmlHostRequest(HtmlHostRequestKind.WaitForPendingScript, blocker, null, Document);
                return RequestStep(_waitRequest);
            }
            long remaining = workQuota;
            while (remaining > 0)
            {
                if (_builder.FragmentBootstrapPending)
                {
                    var beforeBootstrap = _builder.WorkCount;
                    _builder.AdvanceFragmentBootstrap(remaining, cancellationToken);
                    remaining -= _builder.WorkCount - beforeBootstrap;
                    if (_builder.FragmentBootstrapPending || remaining <= 0)
                        return new HtmlParseStep(HtmlParseStepKind.Yielded);
                    continue;
                }
                if (_builder.HasToken)
                {
                    var before = _builder.WorkCount;
                    var result = _builder.Process(remaining, cancellationToken);
                    remaining -= _builder.WorkCount - before;
                    if (_builder.HasCompletedStyles)
                    {
                        if (result.Kind == HtmlParseStepKind.Complete) _complete = true;
                        return new HtmlParseStep(HtmlParseStepKind.Yielded);
                    }
                    if (_builder.ScriptBoundary is { } script)
                    {
                        if (_requests.Count == 0)
                        {
                            _checkpoint = new HtmlHostRequest(HtmlHostRequestKind.MicrotaskCheckpoint, script, null, Document);
                            return RequestStep(_checkpoint);
                        }
                        _builder.CompleteScriptCheckpoint();
                        // Controlled continuation after the boundary; no host code runs here.
                        continue;
                    }
                    if (_builder.ClosedScript is { } closed)
                    {
                        var kind = _builder.ClosedScriptIsSvg ? HtmlHostRequestKind.ProcessSvgScript : HtmlHostRequestKind.PrepareScript;
                        _builder.TakeClosedScript();
                        return BeginRequest(kind, closed);
                    }
                    if (result.Kind == HtmlParseStepKind.MissingFeature)
                    {
                        _terminal = true;
                        return result;
                    }
                    if (result.Kind == HtmlParseStepKind.Complete)
                    {
                        _complete = true;
                        return result;
                    }
                    if (_builder.HasToken || remaining <= 0) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                    continue;
                }

                var scanBefore = _tokenizer.WorkCount;
                var quota = (int) Math.Min(int.MaxValue, remaining);
                var status = frame is null
                    ? _tokenizer.Read(quota, _builder.AllowCData, cancellationToken, out var token)
                    : _tokenizer.ReadUntil(frame.Point, quota, _builder.AllowCData, cancellationToken, out token);
                remaining -= _tokenizer.WorkCount - scanBefore;
                switch (status)
                {
                    case HtmlReadStatus.Token:
                        _builder.SetToken(token);
                        break;
                    case HtmlReadStatus.InsertionBoundary:
                        return new HtmlParseStep(HtmlParseStepKind.InsertionBoundary);
                    case HtmlReadStatus.NeedInput:
                        return new HtmlParseStep(HtmlParseStepKind.NeedInput);
                    case HtmlReadStatus.Yielded:
                        return new HtmlParseStep(HtmlParseStepKind.Yielded);
                    case HtmlReadStatus.Complete:
                        throw new InvalidOperationException("Tokenizer completed before tree EOF processing.");
                    default:
                        throw new InvalidOperationException("Unknown tokenizer status.");
                }
            }
            return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }
        catch { Invalidate(); throw; }
        finally { _builder.EndDriveRootTracking(); Exit(); }
    }

    private HtmlParseStep BeginRequest(HtmlHostRequestKind kind, Element script)
    {
        var frame = new HtmlScriptFrame(this, _tokenizer.CreateInsertionPoint(), _requests.Count + 1);
        var request = new HtmlHostRequest(kind, script, frame, Document);
        _requests.Add(request);
        if (kind == HtmlHostRequestKind.ProcessSvgScript) _parserPaused = true;
        return RequestStep(request);
    }

    private static HtmlParseStep RequestStep(HtmlHostRequest request)
        => new(HtmlParseStepKind.HostRequest, hostRequest: request);

    private void CheckActiveFrame(HtmlScriptFrame frame)
    {
        CheckActive();
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Owner != this || frame.Completed || _checkpoint is not null ||
            _requests.Count == 0 || !ReferenceEquals(_requests[^1].Frame, frame))
            throw new InvalidOperationException("The script frame is stale, foreign or not active.");
    }

    private void InvalidateState()
    {
        _terminal = true;
        _builder.ClearCompletedStyles();
        _checkpoint = null;
        _waitRequest = null;
        _pendingBlocker = null;
        _readyBlocker = null;
        _parserPaused = false;
    }

    private void Invalidate()
    {
        InvalidateState();
        // Bound cleanup on the failing call; Abort can drain any deeper stack.
        ReleaseFrames(256, CancellationToken.None);
    }

    private void ReleaseFrames(int quota, CancellationToken cancellationToken)
    {
        while (_requests.Count > 0 && quota-- > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = _requests[^1].Frame!;
            _tokenizer.ReleaseInsertionPoint(frame.Point);
            frame.Completed = true;
            _requests.RemoveAt(_requests.Count - 1);
        }
    }

    private void Enter()
    {
        if (Interlocked.CompareExchange(ref _entered, 1, 0) != 0)
            throw new InvalidOperationException("A parser mutation is already active.");
    }

    private void Exit() => Volatile.Write(ref _entered, 0);

    private void CheckActive()
    {
        if (_terminal || _complete) throw new InvalidOperationException("The parser session is terminal.");
    }

    private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
}
