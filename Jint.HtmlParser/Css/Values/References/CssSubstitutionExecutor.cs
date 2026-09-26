namespace Jint.HtmlParser.Css.Values.References;

/// <summary>CSS Variables 1 §3, CSS Values 5 Appendix A, and CSS Env 1 §3.</summary>
internal static class CssSubstitutionExecutor
{
    internal static CssSubstitutionResult Resolve(CssReferenceInput input,
        CssSubstitutionSnapshot customProperties, CssEnvironmentSnapshot environment,
        CssSubstitutionContext context, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(customProperties);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(work);
        if (!context.IsValid) throw new ArgumentException("A substitution context is required.", nameof(context));
        work.CheckCancellation();
        var operation = new Operation(customProperties, environment, work);
        var source = operation.Source(input, context.Use);
        if (source.Kind == EvalKind.Pending)
        {
            work.CheckCancellation();
            return CssSubstitutionResult.Pending(source.Feature!);
        }
        var root = source.Segment!;
        var rootFrame = Frame.Node(root, Mode.Full, context);
        if (context.Use == CssReferenceUse.CustomPropertyValue)
            operation.PushActive(customProperties, context.PropertyName, rootFrame);
        var frames = new List<Frame> { rootFrame };
        var last = default(Eval);
        while (frames.Count != 0)
        {
            work.Charge(1);
            var frame = frames[^1];
            switch (frame.Kind)
            {
                case FrameKind.Sequence:
                    if (frame.Phase == 1)
                    {
                        if (last.Kind != EvalKind.Tokens)
                        {
                            Complete(frames, ref last, last);
                            continue;
                        }
                        if (frame.Mode == Mode.Early)
                        {
                            // Count inserted occurrences before opening shared concatenations for argument
                            // parsing. Untouched source stays linear in the input, including unused fallbacks.
                            var tokens = frame.SpreadChild ? last.Segment!.TokenCount : last.ExpandedTokens;
                            var spelling = frame.SpreadChild ? last.Segment!.SpellingLength : last.ExpandedSpelling;
                            frame.ExpandedTokens = Saturate(frame.ExpandedTokens, tokens, CssSubstitutedValue.MaxTokens);
                            frame.ExpandedSpelling = Saturate(frame.ExpandedSpelling, spelling, CssSubstitutedValue.MaxSpelling);
                            if (frame.ExpandedTokens > CssSubstitutedValue.MaxTokens ||
                                frame.ExpandedSpelling > CssSubstitutedValue.MaxSpelling)
                            {
                                Complete(frames, ref last, Eval.Invalid());
                                continue;
                            }
                        }
                        frame.Output!.Add(last.Segment!);
                        frame.SpreadChild = false;
                        frame.Phase = 0;
                    }
                    if (frame.Index == frame.Children!.Length)
                    {
                        work.CheckCancellation();
                        last = Eval.Tokens(CssSegment.Concat(frame.Output!.ToArray(), work),
                            frame.ExpandedTokens, frame.ExpandedSpelling);
                        frames.RemoveAt(frames.Count - 1);
                        continue;
                    }
                    var children = frame.Children;
                    var i = frame.Index;
                    if (frame.Mode == Mode.Early && i + 3 < children.Length &&
                        CssSubstitutionArguments.IsPeriod(children[i]) &&
                        CssSubstitutionArguments.IsPeriod(children[i + 1]) &&
                        CssSubstitutionArguments.IsPeriod(children[i + 2]) &&
                        CssSubstitutionArguments.IsReference(children[i + 3], out _))
                    {
                        frame.Index += 4;
                        frame.Phase = 1;
                        frame.SpreadChild = true;
                        work.CheckCancellation();
                        frames.Add(Frame.Invocation(children[i + 3], frame.Context));
                    }
                    else
                    {
                        frame.Index++;
                        frame.Phase = 1;
                        frames.Add(Frame.Node(children[i], frame.Mode, frame.Context));
                    }
                    break;

                case FrameKind.Node:
                    var segment = frame.Segment!;
                    if (frame.Phase == 1)
                    {
                        if (last.Kind != EvalKind.Tokens)
                        {
                            Complete(frames, ref last, last);
                            continue;
                        }
                        if (segment.Kind == CssSegmentKind.Concat)
                        {
                            Complete(frames, ref last, last);
                            continue;
                        }
                        var wrapped = CssSegment.Rebuild(segment, [last.Segment!], work);
                        Complete(frames, ref last, Eval.Tokens(wrapped, last.ExpandedTokens, last.ExpandedSpelling));
                        continue;
                    }
                    if (segment.Kind == CssSegmentKind.Token ||
                        frame.Mode == Mode.Early && CssSubstitutionArguments.IsArbitrary(segment))
                    {
                        Complete(frames, ref last, Eval.Tokens(segment));
                        continue;
                    }
                    if (segment.Kind == CssSegmentKind.Container && frame.Mode == Mode.Full &&
                        CssSubstitutionArguments.IsReference(segment, out _))
                    {
                        frame.Kind = FrameKind.Invocation;
                        frame.Phase = 0;
                        continue;
                    }
                    frame.Phase = 1;
                    frames.Add(Frame.Sequence(segment.Children, frame.Mode, frame.Context));
                    break;

                case FrameKind.Invocation:
                    ExecuteInvocation(frame, frames, ref last, operation);
                    break;

                case FrameKind.Binding:
                    ExecuteBinding(frame, frames, ref last, operation);
                    break;
            }
        }
        if (context.Use == CssReferenceUse.CustomPropertyValue)
        {
            if (rootFrame.Cycle) last = Eval.Invalid();
            operation.PopActive(customProperties, context.PropertyName);
        }
        work.CheckCancellation();
        if (last.Kind == EvalKind.Pending) return CssSubstitutionResult.Pending(last.Feature!);
        if (last.Kind == EvalKind.Invalid || last.Segment!.IsOversize)
            return CssSubstitutionResult.Invalid();
        var value = CssSubstitutedValue.Create(last.Segment, input.MaxNestingDepth, work);
        work.CheckCancellation();
        return CssSubstitutionResult.Tokens(value);
    }

    private static void ExecuteInvocation(Frame frame, List<Frame> frames, ref Eval last,
        Operation operation)
    {
        var work = operation.Work;
        var segment = frame.Segment!;
        if (frame.Phase == 0)
        {
            CssSubstitutionArguments.IsReference(segment, out frame.ReferenceKind);
            frame.Phase = 1;
            frames.Add(Frame.Sequence(segment.Children, Mode.Early, frame.Context));
            return;
        }
        if (frame.Phase == 1)
        {
            if (last.Kind == EvalKind.Pending) { Complete(frames, ref last, last); return; }
            if (last.Kind == EvalKind.Invalid) { Complete(frames, ref last, last); return; }
            var all = CssSubstitutionArguments.Flatten(last.Segment!, work);
            var commaIndex = -1;
            for (var i = 0; i < all.Length; i++)
            {
                work.Charge(1);
                if (!CssSubstitutionArguments.IsComma(all[i])) continue;
                commaIndex = i;
                break;
            }
            frame.Fallback = commaIndex < 0 ? null : Slice(all, commaIndex + 1,
                all.Length - commaIndex - 1, work);
            var header = Slice(all, 0, commaIndex < 0 ? all.Length : commaIndex, work);
            if (!CssSubstitutionArguments.TryHeaderArgument(header, work, out var headerContent) ||
                frame.Fallback is { } rawFallback &&
                !CssSubstitutionArguments.TryFallback(rawFallback, frame.ReferenceKind, work, out _))
            {
                Complete(frames, ref last, Eval.Invalid());
                return;
            }
            frame.Phase = 2;
            frames.Add(Frame.Sequence(headerContent, Mode.Full, frame.Context, work));
            return;
        }
        if (frame.Phase == 2)
        {
            if (last.Kind == EvalKind.Pending) { Complete(frames, ref last, last); return; }
            if (last.Kind == EvalKind.Invalid)
            {
                StartFallback(frame, frames, ref last, work);
                return;
            }
            if (last.Segment!.IsOversize)
            {
                // Normal header substitution follows argument division. Its failure can select
                // fallback, unlike an early spread failure. Never flatten an oversized shared header.
                StartFallback(frame, frames, ref last, work);
                return;
            }
            var header = CssSubstitutionArguments.Flatten(last.Segment!, work);
            if (!CssSubstitutionArguments.TryHeader(header, frame.ReferenceKind, work,
                    out var name, out var indices))
            {
                StartFallback(frame, frames, ref last, work);
                return;
            }
            if (frame.ReferenceKind == CssReferenceKind.Var)
            {
                if (!operation.Custom.TryGet(name, work, out var binding) ||
                    binding.Kind == CssSubstitutionBindingKind.Invalid ||
                    binding.Kind != CssSubstitutionBindingKind.Pending &&
                    binding.AnimationTainted && !frame.Context.IsAnimatable)
                {
                    StartFallback(frame, frames, ref last, work);
                    return;
                }
                if (binding.Kind == CssSubstitutionBindingKind.Pending)
                {
                    Complete(frames, ref last, Eval.Pending(binding.PendingFeature));
                    return;
                }
                frame.Phase = 3;
                frames.Add(Frame.Binding(binding, frame.Context));
                return;
            }
            if (!operation.Environment.TryGet(name, indices, work, out var value))
            {
                StartFallback(frame, frames, ref last, work);
                return;
            }
            var environmentValue = operation.Source(value!, CssReferenceUse.PropertyValue);
            if (environmentValue.Kind == EvalKind.Pending)
            {
                Complete(frames, ref last, environmentValue);
                return;
            }
            var replacement = environmentValue.Segment!;
            if (replacement.IsOversize) StartFallback(frame, frames, ref last, work);
            else Complete(frames, ref last, Eval.Tokens(replacement));
            return;
        }
        if (frame.Phase == 3)
        {
            if (last.Kind == EvalKind.Invalid || last.Kind == EvalKind.Tokens && last.Segment!.IsOversize)
                StartFallback(frame, frames, ref last, work);
            else Complete(frames, ref last, last);
            return;
        }
        if (frame.Phase == 4)
        {
            if (last.Kind != EvalKind.Tokens) { Complete(frames, ref last, last); return; }
            var fallback = CssSubstitutionArguments.Flatten(last.Segment!, work);
            if (!CssSubstitutionArguments.TryFallback(fallback, frame.ReferenceKind, work,
                    out var content))
            {
                Complete(frames, ref last, Eval.Invalid());
                return;
            }
            frame.Phase = 5;
            frames.Add(Frame.Sequence(content, Mode.Full, frame.Context, work));
            return;
        }
        if (last.Kind == EvalKind.Tokens && last.Segment!.IsOversize)
            Complete(frames, ref last, Eval.Invalid());
        else Complete(frames, ref last, last);
    }

    private static void StartFallback(Frame frame, List<Frame> frames, ref Eval last, CssValueWork work)
    {
        if (frame.Fallback is null)
        {
            Complete(frames, ref last, Eval.Invalid());
            return;
        }
        work.CheckCancellation();
        frame.Phase = 4;
        frames.Add(Frame.Sequence(frame.Fallback, Mode.Early, frame.Context, work));
    }

    private static void ExecuteBinding(Frame frame, List<Frame> frames, ref Eval last,
        Operation operation)
    {
        var work = operation.Work;
        var binding = frame.BindingValue;
        if (frame.Phase == 0)
        {
            var scope = binding.Scope ?? operation.Custom;
            if (operation.TryActive(scope, binding.Name, out var activeIndex))
            {
                operation.MarkCycle(activeIndex);
                Complete(frames, ref last, Eval.Invalid());
                return;
            }
            if (operation.TryMemo(scope, binding.Name, out var memo))
            {
                Complete(frames, ref last, memo);
                return;
            }
            operation.PushActive(scope, binding.Name, frame);
            frame.PreviousScope = operation.Custom;
            operation.Custom = scope;
            if (binding.Kind == CssSubstitutionBindingKind.Computed)
            {
                last = Eval.Tokens(binding.Value.Root);
                FinishBinding(frame, frames, ref last, operation);
                return;
            }
            var source = operation.Source(binding.Input, CssReferenceUse.CustomPropertyValue);
            if (source.Kind == EvalKind.Pending)
            {
                last = source;
                FinishBinding(frame, frames, ref last, operation);
                return;
            }
            frame.Phase = 1;
            var root = source.Segment!;
            var context = new CssSubstitutionContext(binding.Name,
                CssReferenceUse.CustomPropertyValue, true);
            frames.Add(Frame.Node(root, Mode.Full, context));
            return;
        }
        FinishBinding(frame, frames, ref last, operation);
    }

    private static void FinishBinding(Frame frame, List<Frame> frames, ref Eval last,
        Operation operation)
    {
        if (frame.Cycle) last = Eval.Invalid();
        if (last.Kind == EvalKind.Tokens && last.Segment!.IsOversize) last = Eval.Invalid();
        operation.PopActive(operation.Custom, frame.BindingValue.Name);
        operation.AddMemo(operation.Custom, frame.BindingValue.Name, last);
        operation.Custom = frame.PreviousScope!;
        operation.Work.CheckCancellation();
        frames.RemoveAt(frames.Count - 1);
    }

    private static CssSegment[] Slice(CssSegmentList source, int start, int count, CssValueWork work)
    {
        work.CheckCancellation();
        var result = new CssSegment[count];
        source.CopyTo(start, result, 0, count, work);
        work.Charge(count);
        work.CheckCancellation();
        return result;
    }

    private static CssSegment[] Slice(CssSegment[] source, int start, int count, CssValueWork work)
    {
        work.CheckCancellation();
        var result = new CssSegment[count];
        for (var i = 0; i < count; i++)
        {
            work.Charge(1);
            result[i] = source[start + i];
        }
        work.CheckCancellation();
        return result;
    }

    private static void Complete(List<Frame> frames, ref Eval last, Eval result)
    {
        last = result;
        frames.RemoveAt(frames.Count - 1);
    }

    private static int Saturate(int left, int right, int ceiling) =>
        left > ceiling - right ? ceiling + 1 : left + right;

    private enum Mode { Early, Full }
    private enum FrameKind { Node, Sequence, Invocation, Binding }
    private enum EvalKind { Uninitialized, Tokens, Invalid, Pending }

    private readonly struct Eval
    {
        private Eval(EvalKind kind, CssSegment? segment, string? feature,
            int expandedTokens = 0, int expandedSpelling = 0)
        {
            Kind = kind;
            Segment = segment;
            Feature = feature;
            ExpandedTokens = expandedTokens;
            ExpandedSpelling = expandedSpelling;
        }

        internal EvalKind Kind { get; }
        internal CssSegment? Segment { get; }
        internal string? Feature { get; }
        internal int ExpandedTokens { get; }
        internal int ExpandedSpelling { get; }
        internal static Eval Tokens(CssSegment segment, int expandedTokens = 0, int expandedSpelling = 0) =>
            new(EvalKind.Tokens, segment, null, expandedTokens, expandedSpelling);
        internal static Eval Invalid() => new(EvalKind.Invalid, null, null);
        internal static Eval Pending(string feature) => new(EvalKind.Pending, null, feature);
    }

    private sealed class Frame
    {
        internal FrameKind Kind;
        internal Mode Mode;
        internal CssSubstitutionContext Context;
        internal CssSegment? Segment;
        internal CssSegmentList? Children;
        internal List<CssSegment>? Output;
        internal CssSegment[]? Fallback;
        internal CssReferenceKind ReferenceKind;
        internal CssSubstitutionBinding BindingValue;
        internal CssSubstitutionSnapshot? PreviousScope;
        internal int Phase;
        internal int Index;
        internal bool Cycle;
        internal bool SpreadChild;
        internal int ExpandedTokens;
        internal int ExpandedSpelling;

        internal static Frame Node(CssSegment segment, Mode mode, CssSubstitutionContext context) =>
            new() { Kind = FrameKind.Node, Segment = segment, Mode = mode, Context = context };
        internal static Frame Sequence(CssSegmentList children, Mode mode, CssSubstitutionContext context) =>
            new()
            {
                Kind = FrameKind.Sequence,
                Children = children,
                Output = new List<CssSegment>(),
                Mode = mode,
                Context = context
            };
        internal static Frame Sequence(CssSegment[] children, Mode mode, CssSubstitutionContext context,
            CssValueWork work) => Sequence(new CssSegmentList(children, work), mode, context);
        internal static Frame Invocation(CssSegment segment, CssSubstitutionContext context) =>
            new() { Kind = FrameKind.Invocation, Segment = segment, Context = context };
        internal static Frame Binding(CssSubstitutionBinding binding, CssSubstitutionContext context) =>
            new() { Kind = FrameKind.Binding, BindingValue = binding, Context = context };
    }

    private sealed class Operation
    {
        private readonly Dictionary<(CssReferenceInput, CssReferenceUse), Eval> _sources = new();
        private readonly Dictionary<(CssSubstitutionSnapshot Scope, uint Hash), List<(string Name, Eval Result)>> _memo = new();
        private readonly Dictionary<(CssSubstitutionSnapshot Scope, uint Hash), List<int>> _activeBuckets = new();
        private readonly List<(CssSubstitutionSnapshot Scope, string Name, Frame Frame)> _active = new();

        internal Operation(CssSubstitutionSnapshot custom, CssEnvironmentSnapshot environment,
            CssValueWork work)
        {
            Custom = custom;
            Environment = environment;
            Work = work;
        }

        internal CssSubstitutionSnapshot Custom { get; set; }
        internal CssEnvironmentSnapshot Environment { get; }
        internal CssValueWork Work { get; }

        internal Eval Source(CssReferenceInput input, CssReferenceUse use)
        {
            if (_sources.TryGetValue((input, use), out var cached)) return cached;
            Work.CheckCancellation();
            var analysis = CssReferenceParser.Analyze(input, use, Work);
            if (analysis.Kind == CssReferenceAnalysisKind.InvalidSyntax)
                throw new ArgumentException("A source must pass reference syntax analysis.", nameof(input));
            if (use == CssReferenceUse.CustomPropertyValue && analysis.Kind == CssReferenceAnalysisKind.Literal &&
                CssPrimitiveParser.ParseWideKeyword(input.Components, Work).IsMatch)
                throw new ArgumentException("CSS-wide custom declarations must be selected at the cascade boundary.", nameof(input));
            var result = analysis.Kind == CssReferenceAnalysisKind.PendingFeature
                ? Eval.Pending(analysis.PendingFunction!)
                : Eval.Tokens(CssSegment.FromInput(input, Work));
            Work.CheckCancellation();
            _sources.Add((input, use), result);
            Work.CheckCancellation();
            return result;
        }

        internal bool TryMemo(CssSubstitutionSnapshot scope, string name, out Eval result)
        {
            var hash = CssSubstitutionArguments.Hash(name, Work);
            if (_memo.TryGetValue((scope, hash), out var bucket))
            {
                foreach (var item in bucket)
                {
                    Work.Charge(1);
                    if (!CssSubstitutionArguments.Equals(name, item.Name, Work)) continue;
                    result = item.Result;
                    return true;
                }
            }
            result = default;
            return false;
        }

        internal void AddMemo(CssSubstitutionSnapshot scope, string name, Eval result)
        {
            var hash = CssSubstitutionArguments.Hash(name, Work);
            if (!_memo.TryGetValue((scope, hash), out var bucket))
            {
                Work.CheckCancellation();
                bucket = new List<(string, Eval)>();
                _memo.Add((scope, hash), bucket);
            }
            bucket.Add((name, result));
            Work.CheckCancellation();
        }

        internal bool TryActive(CssSubstitutionSnapshot scope, string name, out int index)
        {
            var hash = CssSubstitutionArguments.Hash(name, Work);
            if (_activeBuckets.TryGetValue((scope, hash), out var bucket))
            {
                foreach (var candidate in bucket)
                {
                    Work.Charge(1);
                    if (!CssSubstitutionArguments.Equals(name, _active[candidate].Name, Work)) continue;
                    index = candidate;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        internal void PushActive(CssSubstitutionSnapshot scope, string name, Frame frame)
        {
            var hash = CssSubstitutionArguments.Hash(name, Work);
            if (!_activeBuckets.TryGetValue((scope, hash), out var bucket))
            {
                Work.CheckCancellation();
                bucket = new List<int>();
                _activeBuckets.Add((scope, hash), bucket);
            }
            bucket.Add(_active.Count);
            _active.Add((scope, name, frame));
            Work.CheckCancellation();
        }

        internal void MarkCycle(int start)
        {
            for (var i = start; i < _active.Count; i++)
            {
                Work.Charge(1);
                _active[i].Frame.Cycle = true;
            }
        }

        internal void PopActive(CssSubstitutionSnapshot scope, string name)
        {
            var last = _active.Count - 1;
            if (last < 0 || !ReferenceEquals(scope, _active[last].Scope) || !CssSubstitutionArguments.Equals(name, _active[last].Name, Work))
                throw new InvalidOperationException("The active substitution stack is inconsistent.");
            var hash = CssSubstitutionArguments.Hash(name, Work);
            var bucket = _activeBuckets[(scope, hash)];
            bucket.RemoveAt(bucket.Count - 1);
            if (bucket.Count == 0) _activeBuckets.Remove((scope, hash));
            _active.RemoveAt(last);
            Work.CheckCancellation();
        }
    }
}
