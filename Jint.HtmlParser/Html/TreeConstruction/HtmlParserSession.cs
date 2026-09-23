using System;
using System.Threading;

namespace Jint.HtmlParser.Html;

internal readonly record struct HtmlDocumentContext(bool IsSrcdoc = false, bool CannotChangeMode = false);

internal enum HtmlParseStepKind { NeedInput, Yielded, Complete, MissingFeature }
internal enum HtmlMissingFeature { Tables, Formatting, Select, Templates, Framesets, ForeignContent }

internal readonly struct HtmlParseStep
{
    internal HtmlParseStep(HtmlParseStepKind kind, HtmlMissingFeature? missingFeature = null, long offset = 0)
    {
        Kind = kind;
        MissingFeature = missingFeature;
        Offset = offset;
    }

    internal HtmlParseStepKind Kind { get; }
    internal HtmlMissingFeature? MissingFeature { get; }
    internal long Offset { get; }
}

// HTML Standard §13.2.6: one tokenizer, one tree builder, and a single work budget.
internal sealed class HtmlParserSession
{
    private readonly HtmlTokenizer _tokenizer;
    private readonly HtmlTreeBuilder _builder;
    private bool _terminal;
    private bool _complete;

    internal HtmlParserSession(Document document, HtmlParseOptions? options = null,
        HtmlDocumentContext context = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Kind != DocumentKind.Html || document.ContentType != "text/html" || document.FirstChild is not null)
            throw new ArgumentException("An empty HTML document is required.", nameof(document));

        options ??= new HtmlParseOptions();
        Document = document;
        _tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(options.Limits, options.Diagnostics));
        _builder = new HtmlTreeBuilder(document, _tokenizer, options.Limits.MaxNestingDepth,
            options.ScriptingEnabled, options.Diagnostics, context);
    }

    internal Document Document { get; }
    internal long WorkCount => SaturatingAdd(_tokenizer.WorkCount, _builder.WorkCount);

    internal void AppendInput(string chunk, bool isFinal = false)
    {
        CheckActive();
        try
        {
            _tokenizer.AppendInput(chunk, isFinal);
        }
        catch (ParseLimitException)
        {
            _terminal = true;
            throw;
        }
    }

    internal HtmlParseStep Drive(int workQuota, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workQuota);
        if (_complete) return new HtmlParseStep(HtmlParseStepKind.Complete);
        CheckActive();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            long remaining = workQuota;
            while (remaining > 0)
            {
                if (_builder.HasToken)
                {
                    var before = _builder.WorkCount;
                    var result = _builder.Process(remaining, cancellationToken);
                    remaining -= _builder.WorkCount - before;
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
                var status = _tokenizer.Read(quota, cancellationToken, out var token);
                remaining -= _tokenizer.WorkCount - scanBefore;
                switch (status)
                {
                    case HtmlReadStatus.Token:
                        _builder.SetToken(token);
                        break;
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
        catch (OperationCanceledException)
        {
            _terminal = true;
            throw;
        }
        catch (ParseLimitException)
        {
            _terminal = true;
            throw;
        }
    }

    private void CheckActive()
    {
        if (_terminal || _complete) throw new InvalidOperationException("The parser session is terminal.");
    }

    private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
}
