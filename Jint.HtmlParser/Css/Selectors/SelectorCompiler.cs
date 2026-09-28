using System.Numerics;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;
using ComplexSelector = Jint.HtmlParser.Css.Selectors.CompiledSelector.Complex;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors Level 4, §3–4 and §14–17: https://drafts.csswg.org/selectors/#grammar
internal static class SelectorCompiler
{
    internal static CompiledSelector Compile(string source, SelectorParseContext? context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        context ??= new SelectorParseContext();
        using var valuesParser = new CssSyntaxParser(source, new CssParseOptions { Limits = context.Limits },
            cancellationToken);
        var values = valuesParser.ParseComponentValues();
        var compiler = new Worker(source, context, cancellationToken);
        return compiler.Compile(values);
    }

    // CSS Conditional 4: one complex selector, without forgiving-list recovery.
    internal static CompiledSelector CompileSupports(string source, CssComponentValueList values,
        CssParseOptions? options, CssValueWork work)
    {
        work.CheckCancellation();
        var result = new Worker(source, new SelectorParseContext(limits: options?.Limits), work.Token, supportsWork: work).Compile(values);
        work.CheckCancellation();
        return result;
    }

    internal sealed class Worker
    {
        private readonly int _sourceLength;
        private readonly string _source;
        private readonly SelectorParseContext _context;
        private readonly CancellationToken _cancellation;
        private readonly Action? _checkpoint;
        private int _work;
        private readonly CssValueWork? _supportsWork;
        private HashSet<int>? _nestingContainers;

        internal Worker(string source, SelectorParseContext context, CancellationToken cancellation,
            Action? checkpoint = null, CssValueWork? supportsWork = null)
        {
            _supportsWork = supportsWork;
            _source = source;
            _sourceLength = source.Length;
            _context = context;
            _cancellation = cancellation;
            _checkpoint = checkpoint;
        }

        internal CompiledSelector Compile(CssComponentValueList values)
        {
            if (_context.NestingParent is not null) FindNestingContainers(values);
            var stack = new List<Frame>
            {
                new(values, false, _context.NestingParent is not null, false, true, _sourceLength,
                    nestingRoot: _context.NestingParent is not null, singleBranch: _supportsWork is not null)
            };
            while (stack.Count != 0)
            {
                Poll();
                var frame = stack[^1];
                try
                {
                    var child = Step(frame);
                    if (child is not null)
                    {
                        stack.Add(child);
                        continue;
                    }
                    if (!frame.Done) continue;
                    var result = new CompiledSelector(Freeze(frame.Branches), frame.MaximumSpecificity, frame.ContainsNesting);
                    stack.RemoveAt(stack.Count - 1);
                    if (stack.Count == 0)
                    {
                        Check();
                        return result;
                    }
                    stack[^1].Accept(result);
                }
                catch (SelectorParseException)
                {
                    var forgiving = -1;
                    for (var i = stack.Count - 1; i >= 0; i--)
                    {
                        Poll();
                        if (_supportsWork is not null || !stack[i].Forgiving) continue;
                        forgiving = i;
                        break;
                    }
                    if (forgiving < 0) throw;
                    stack.RemoveRange(forgiving + 1, stack.Count - forgiving - 1);
                    stack[forgiving].Recover(this);
                }
            }
            throw new InvalidOperationException();
        }

        // §3.1 counts & inside unknown/forgiven functions too. Scan once, without recursion;
        // quoted ampersands remain string tokens and never count as nesting selectors.
        private void FindNestingContainers(CssComponentValueList values)
        {
            var containers = new HashSet<int>();
            _nestingContainers = containers;
            var pending = new Stack<(CssComponentValue Value, bool Exit)>();
            foreach (var value in values) { Poll(); pending.Push((value, false)); }
            while (pending.TryPop(out var item))
            {
                Poll();
                var value = item.Value;
                if (value.Kind == CssComponentKind.Token) continue;
                if (!item.Exit)
                {
                    pending.Push((value, true));
                    foreach (var child in value.Values) { Poll(); pending.Push((child, false)); }
                    continue;
                }
                foreach (var child in value.Values)
                {
                    Poll();
                    if (IsDelim(child, '&') || containers.Contains(child.Span.Start))
                    {
                        containers.Add(value.Span.Start);
                        break;
                    }
                }
            }
        }

        private void ObserveNesting(Frame frame, CssComponentValue value)
        {
            if (_context.NestingParent is not null &&
                (IsDelim(value, '&') || _nestingContainers?.Contains(value.Span.Start) == true))
                frame.BranchContainsNesting = frame.ContainsNesting = true;
        }

        private Frame? Step(Frame f)
        {
            if (f.Index >= f.Values.Count)
            {
                f.End(this, true);
                return null;
            }
            var value = f.Values[f.Index];
            ObserveNesting(f, value);
            if (IsSpace(value))
            {
                f.HadSpace = true;
                f.Index++;
                return null;
            }
            if (IsToken(value, CssTokenKind.Comma))
            {
                if (f.SingleBranch) throw Error("selector/invalid-syntax", value.Span.Start);
                f.End(this, false);
                f.Index++;
                return null;
            }
            if (TryCombinator(f.Values, f.Index, out var combinator, out var width))
            {
                if (f.PseudoElementContext)
                    throw Error("selector/invalid-syntax", value.Span.Start);
                if (f.Compound is not null)
                {
                    if (f.Compound.PseudoElement)
                        throw Error("selector/invalid-syntax", value.Span.Start);
                    f.FinishCompound(this);
                    f.PendingCombinator = combinator;
                }
                else if (f.Relative && f.Compounds.Count == 0 && f.Leading is null)
                {
                    f.Leading = combinator;
                    f.LeadingStart = value.Span.Start;
                }
                else throw Error("selector/invalid-syntax", value.Span.Start);
                f.HadSpace = false;
                f.Index += width;
                return null;
            }
            if (f.HadSpace && f.Compound is not null)
            {
                if (f.Compound.PseudoElement)
                    throw Error("selector/invalid-syntax", value.Span.Start);
                f.FinishCompound(this);
                f.PendingCombinator = Combinator.Descendant;
            }
            f.HadSpace = false;
            if (f.Compound is null)
            {
                var compound = f.StartCompound(value.Span.Start);
                if (!f.PseudoElementContext && TryType(f, compound)) return null;
            }
            var current = f.Compound!;
            if (TrySimple(f, current, out var child)) return child;
            throw Error("selector/invalid-syntax", value.Span.Start);
        }

        private bool TryType(Frame f, CompoundBuilder compound)
        {
            var values = f.Values;
            var i = f.Index;
            var first = values[i];
            var prefix = false;
            string? prefixName = null;
            var nameIndex = i;
            if (IsDelim(first, '|'))
            {
                prefix = true;
                prefixName = string.Empty;
                nameIndex++;
            }
            else if ((IsIdent(first) || IsDelim(first, '*')) && i + 1 < values.Count &&
                     IsDelim(values[i + 1], '|') &&
                     !(i + 2 < values.Count && IsDelim(values[i + 2], '|')))
            {
                prefix = true;
                prefixName = IsDelim(first, '*') ? null : first.Token.Text;
                nameIndex += 2;
            }
            else if (!IsIdent(first) && !IsDelim(first, '*')) return false;
            if (nameIndex >= values.Count || !(IsIdent(values[nameIndex]) || IsDelim(values[nameIndex], '*')))
            {
                if (prefix) throw Error("selector/invalid-syntax", nameIndex < values.Count ? values[nameIndex].Span.Start : f.EndOffset);
                return false;
            }
            var name = values[nameIndex];
            if (prefix) SetNamespace(compound, prefixName, first.Span.Start);
            else SetDefaultNamespace(compound);
            compound.TypeName = IsIdent(name) ? name.Token.Text : null;
            compound.ExplicitType = true;
            compound.End = name.Span.Start + name.Span.Length;
            f.Index = nameIndex + 1;
            return true;
        }

        private bool TrySimple(Frame f, CompoundBuilder compound, out Frame? child)
        {
            child = null;
            var values = f.Values;
            var value = values[f.Index];
            if ((compound.PseudoElement || f.PseudoElementContext) && !IsToken(value, CssTokenKind.Colon))
                throw Error("selector/invalid-syntax", value.Span.Start);
            // CSS Nesting §4: & matches the parent list as :is(), including its maximum specificity.
            if (IsDelim(value, '&') && _context.NestingParent is { } parent)
            {
                compound.Predicates.Add(new Predicate(PredicateKind.Is, value.Span, arguments: parent, nestingReference: true));
                compound.End = value.Span.Start + value.Span.Length;
                f.BranchContainsNesting = f.ContainsNesting = true;
                f.Index++;
                return true;
            }
            if (value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '[')
            {
                compound.Predicates.Add(ParseAttribute(value));
                compound.End = value.Span.Start + value.Span.Length;
                f.Index++;
                return true;
            }
            if (IsToken(value, CssTokenKind.Hash))
            {
                if (!value.Token.IsIdHash) throw Error("selector/invalid-syntax", value.Span.Start);
                compound.Predicates.Add(new Predicate(PredicateKind.Id, value.Span, value.Token.Text));
                compound.End = value.Span.Start + value.Span.Length;
                f.Index++;
                return true;
            }
            if (IsDelim(value, '.'))
            {
                if (f.Index + 1 >= values.Count || !IsIdent(values[f.Index + 1]))
                    throw Error("selector/invalid-syntax", f.Index + 1 < values.Count ? values[f.Index + 1].Span.Start : f.EndOffset);
                var className = values[f.Index + 1];
                compound.Predicates.Add(new Predicate(PredicateKind.Class,
                    Span(value.Span.Start, className.Span.Start + className.Span.Length), className.Token.Text));
                compound.End = className.Span.Start + className.Span.Length;
                f.Index += 2;
                return true;
            }
            if (!IsToken(value, CssTokenKind.Colon)) return false;
            var start = value.Span.Start;
            var pseudoElement = f.Index + 1 < values.Count && IsToken(values[f.Index + 1], CssTokenKind.Colon);
            var nameIndex = f.Index + (pseudoElement ? 2 : 1);
            if (nameIndex >= values.Count) throw Error("selector/invalid-syntax", f.EndOffset);
            var nameValue = values[nameIndex];
            ObserveNesting(f, nameValue);
            var name = nameValue.Kind == CssComponentKind.Function ? nameValue.FunctionName :
                IsIdent(nameValue) ? nameValue.Token.Text : null;
            if (name is null) throw Error("selector/invalid-syntax", nameValue.Span.Start);
            var function = nameValue.Kind == CssComponentKind.Function;
            var kind = Identify(name, pseudoElement, function, nameValue.Span.Start);
            if ((pseudoElement || kind == PredicateKind.PseudoElement) && !f.AllowPseudoElements)
                throw Error("selector/invalid-syntax", start);
            if (f.PseudoElementContext &&
                kind is not (PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or
                    PredicateKind.Hover or PredicateKind.Active or PredicateKind.Focus or
                    PredicateKind.FocusWithin or PredicateKind.FocusVisible))
                throw Error("selector/invalid-syntax", start);
            if (compound.PseudoElement &&
                kind is not (PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or
                    PredicateKind.Hover or PredicateKind.Active or PredicateKind.Focus or
                    PredicateKind.FocusWithin or PredicateKind.FocusVisible) &&
                !(compound.LastPseudoElement == PredicateKind.Slotted &&
                  kind == PredicateKind.PseudoElement &&
                  (EqualsAscii(name, "before") || EqualsAscii(name, "after"))))
                throw Error("selector/invalid-syntax", start);
            if (kind == PredicateKind.Has && f.InsideHas)
                throw Error("selector/invalid-syntax", nameValue.Span.Start);
            var span = Span(start, nameValue.Span.Start + nameValue.Span.Length);
            compound.End = span.Start + span.Length;
            compound.PseudoElement |= pseudoElement || kind == PredicateKind.PseudoElement;
            if (pseudoElement || kind == PredicateKind.PseudoElement)
                compound.LastPseudoElement = kind;
            f.Index = nameIndex + 1;
            if (!function)
            {
                compound.Predicates.Add(new Predicate(kind, span,
                    kind is PredicateKind.PseudoElement or PredicateKind.WebkitUnknownPseudoElement ? name : null));
                return true;
            }
            var args = nameValue.Values;
            var argumentEnd = ContainerEndOffset(nameValue, ')');
            switch (kind)
            {
                case PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or PredicateKind.Has or PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted:
                    {
                        f.Pending = new Pending(kind, span);
                        var pseudoElementContext = (kind is PredicateKind.Is or PredicateKind.Where or PredicateKind.Not) &&
                            (f.PseudoElementContext || compound.PseudoElement);
                        child = new Frame(args, _supportsWork is null && (kind is PredicateKind.Is or PredicateKind.Where),
                            kind == PredicateKind.Has, kind != PredicateKind.Has && f.InsideHas,
                            false, argumentEnd,
                            compoundOnly: pseudoElementContext || kind is PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted,
                            singleBranch: kind is PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted,
                            pseudoElementContext: pseudoElementContext);
                        // The relative list itself is inside :has for nested-has rejection.
                        if (kind == PredicateKind.Has) child.InsideHas = true;
                        return true;
                    }
                case PredicateKind.NthChild or PredicateKind.NthLastChild or PredicateKind.NthOfType or PredicateKind.NthLastOfType or PredicateKind.NthCol or PredicateKind.NthLastCol:
                    {
                        ParseNth(args, kind is PredicateKind.NthChild or PredicateKind.NthLastChild, argumentEnd,
                            out var a, out var b, out var ofValues);
                        if (ofValues is not null)
                        {
                            f.Pending = new Pending(kind, span, a, b);
                            child = new Frame(ofValues, false, false, f.InsideHas, false,
                                argumentEnd);
                        }
                        else compound.Predicates.Add(new Predicate(kind, span, a: a, b: b));
                        return true;
                    }
                case PredicateKind.Lang or PredicateKind.Dir:
                    {
                        var text = kind == PredicateKind.Lang ? ParseTextArguments(args, argumentEnd) :
                            Freeze(new List<string> { ParseSingleIdent(args, argumentEnd) });
                        compound.Predicates.Add(new Predicate(kind, span, textArguments: text));
                        return true;
                    }
                case PredicateKind.Picker:
                    {
                        var text = ParseSingleIdent(args, argumentEnd);
                        if (!EqualsAscii(text, "select"))
                            throw Error("selector/invalid-syntax", nameValue.Span.Start);
                        compound.Predicates.Add(new Predicate(kind, span, name: "select"));
                        return true;
                    }
            }
            throw Error("selector/unsupported-construct", nameValue.Span.Start);
        }

        private Predicate ParseAttribute(CssComponentValue block)
        {
            var values = block.Values;
            var endOffset = ContainerEndOffset(block, ']');
            var i = 0;
            SkipSpace(values, ref i);
            if (i >= values.Count) throw Error("selector/invalid-syntax", endOffset);
            var first = values[i];
            string? prefix = null;
            bool hasPrefix = false;
            if (IsDelim(first, '|'))
            {
                hasPrefix = true;
                prefix = string.Empty;
                i++;
            }
            else if ((IsIdent(first) || IsDelim(first, '*')) && i + 2 < values.Count &&
                     IsDelim(values[i + 1], '|') && IsIdent(values[i + 2]))
            {
                hasPrefix = true;
                prefix = IsDelim(first, '*') ? null : first.Token.Text;
                i += 2;
            }
            if (i >= values.Count || !IsIdent(values[i]))
                throw Error("selector/invalid-syntax", i < values.Count ? values[i].Span.Start : endOffset);
            var attrName = values[i++].Token.Text;
            var nsMode = NamespaceMode.None;
            string? uri = null;
            if (hasPrefix) ResolveNamespace(prefix, first.Span.Start, out nsMode, out uri);
            SkipSpace(values, ref i);
            if (i == values.Count)
                return new Predicate(PredicateKind.Attribute, block.Span, attrName, nsMode, uri);
            var opValue = values[i++];
            var op = AttributeOperator.Presence;
            if (IsDelim(opValue, '=')) op = AttributeOperator.Exact;
            else if (opValue.Kind == CssComponentKind.Token && opValue.Token.Kind == CssTokenKind.Delim &&
                     opValue.Token.Delimiter is '~' or '|' or '^' or '$' or '*')
            {
                op = opValue.Token.Delimiter switch
                {
                    '~' => AttributeOperator.Includes,
                    '|' => AttributeOperator.DashMatch,
                    '^' => AttributeOperator.Prefix,
                    '$' => AttributeOperator.Suffix,
                    _ => AttributeOperator.Substring
                };
                if (i >= values.Count || !IsDelim(values[i], '='))
                    throw Error("selector/invalid-syntax", i < values.Count ? values[i].Span.Start : endOffset);
                i++;
            }
            else throw Error("selector/invalid-syntax", opValue.Span.Start);
            SkipSpace(values, ref i);
            if (i >= values.Count || !(IsIdent(values[i]) || IsToken(values[i], CssTokenKind.String)))
                throw Error("selector/invalid-syntax", i < values.Count ? values[i].Span.Start : endOffset);
            var text = values[i++].Token.Text;
            SkipSpace(values, ref i);
            var modifier = '\0';
            if (i < values.Count && IsIdent(values[i]) && values[i].Token.Text.Length == 1)
            {
                var c = char.ToLowerInvariant(values[i].Token.Text[0]);
                if (c is 'i' or 's') { modifier = c; i++; SkipSpace(values, ref i); }
            }
            if (i != values.Count) throw Error("selector/invalid-syntax", values[i].Span.Start);
            return new Predicate(PredicateKind.Attribute, block.Span, attrName, nsMode, uri, op, text, modifier);
        }

        private void ParseNth(CssComponentValueList values, bool allowOf, int endOffset, out BigInteger a,
            out BigInteger b, out CssComponentValueList? ofValues)
        {
            var of = -1;
            for (var i = 0; i < values.Count; i++)
            {
                Poll();
                if (IsIdent(values[i]) && EqualsAscii(values[i].Token.Text, "of"))
                {
                    of = i;
                    break;
                }
            }
            if (of >= 0 && !allowOf) throw Error("selector/invalid-syntax", values[of].Span.Start);
            var end = of >= 0 ? of : values.Count;
            ParseAnB(values, end, endOffset, out a, out b);
            if (of < 0) { ofValues = null; return; }
            var trailing = new CssComponentValue[values.Count - of - 1];
            for (var i = 0; i < trailing.Length; i++) { Poll(); trailing[i] = values[of + 1 + i]; }
            ofValues = new CssComponentValueList(trailing);
        }

        private void ParseAnB(CssComponentValueList values, int end, int endOffset,
            out BigInteger a, out BigInteger b)
        {
            var i = 0;
            SkipSpace(values, ref i, end);
            if (i == end) throw Error("selector/invalid-syntax", endOffset);
            var first = values[i++];
            a = default; b = default;
            if (IsIdent(first))
            {
                var text = first.Token.Text;
                if (EqualsAscii(text, "odd")) { a = 2; b = 1; }
                else if (EqualsAscii(text, "even")) { a = 2; }
                else if (!TryNIdent(text, out a, out b, out var needsB))
                    throw Error("selector/invalid-syntax", first.Span.Start);
                else if (needsB) ParseTrailingB(values, ref i, end, endOffset, ref b, text.EndsWith('-'));
            }
            else if (IsToken(first, CssTokenKind.Dimension) &&
                     TryNUnit(first.Token.Unit, out var unitB, out var unitNeedsB))
            {
                a = Number(first.Token.NumberText, first.Span.Start);
                b = unitB;
                if (first.Token.Unit.Length == 1 || unitNeedsB)
                    ParseTrailingB(values, ref i, end, endOffset, ref b, unitNeedsB);
            }
            else if (IsToken(first, CssTokenKind.Number) && first.Token.IsInteger)
            {
                b = Number(first.Token.NumberText, first.Span.Start);
            }
            else if (IsDelim(first, '+'))
            {
                if (i >= end || !IsIdent(values[i]) || values[i].Token.Text.StartsWith('-') ||
                    !TryNIdent(values[i].Token.Text, out a, out b, out var needsB))
                    throw Error("selector/invalid-syntax", i < end ? values[i].Span.Start : endOffset);
                i++;
                if (needsB) ParseTrailingB(values, ref i, end, endOffset, ref b,
                    values[i - 1].Token.Text.EndsWith('-'));
            }
            else throw Error("selector/invalid-syntax", first.Span.Start);
            SkipSpace(values, ref i, end);
            if (i != end) throw Error("selector/invalid-syntax", values[i].Span.Start);
        }

        private void ParseTrailingB(CssComponentValueList values, ref int i, int end, int endOffset,
            ref BigInteger b, bool required = false)
        {
            SkipSpace(values, ref i, end);
            if (i >= end)
            {
                if (required) throw Error("selector/invalid-syntax", endOffset);
                return;
            }
            var signValue = values[i++];
            if (required)
            {
                if (!IsToken(signValue, CssTokenKind.Number) || !signValue.Token.IsInteger ||
                    signValue.Token.NumberText.StartsWith('+') || signValue.Token.NumberText.StartsWith('-'))
                    throw Error("selector/invalid-syntax", signValue.Span.Start);
                b -= Number(signValue.Token.NumberText, signValue.Span.Start);
                return;
            }
            if (IsToken(signValue, CssTokenKind.Number) && signValue.Token.IsInteger &&
                (signValue.Token.NumberText.StartsWith('+') || signValue.Token.NumberText.StartsWith('-')))
            {
                b += Number(signValue.Token.NumberText, signValue.Span.Start);
                return;
            }
            if (!IsDelim(signValue, '+') && !IsDelim(signValue, '-'))
                throw Error("selector/invalid-syntax", signValue.Span.Start);
            var sign = IsDelim(signValue, '-') ? -1 : 1;
            SkipSpace(values, ref i, end);
            if (i >= end || !IsToken(values[i], CssTokenKind.Number) || !values[i].Token.IsInteger ||
                values[i].Token.NumberText.StartsWith('+') || values[i].Token.NumberText.StartsWith('-'))
                throw Error("selector/invalid-syntax", i < end ? values[i].Span.Start : endOffset);
            b += sign * Number(values[i].Token.NumberText, values[i].Span.Start);
            i++;
        }

        private bool TryNIdent(string text, out BigInteger a, out BigInteger b, out bool needsB)
        {
            a = default; b = default; needsB = false;
            var sign = 1;
            if (text.Length > 0 && text[0] == '-') { sign = -1; text = text[1..]; }
            if (text.Length == 0 || text[0] is not ('n' or 'N')) return false;
            a = sign;
            if (text.Length == 1) { needsB = true; return true; }
            if (text.Length == 2 && text[1] == '-') { needsB = true; return true; }
            if (text[1] != '-') return false;
            if (!TryUnsigned(text.AsSpan(2), out var number)) return false;
            b = -number;
            return true;
        }

        private bool TryNUnit(string text, out BigInteger b, out bool needsB)
        {
            b = default;
            needsB = false;
            if (text.Length == 0 || text[0] is not ('n' or 'N')) return false;
            if (text.Length == 1) return true;
            if (text[1] != '-') return false;
            if (text.Length == 2) { needsB = true; return true; }
            if (!TryUnsigned(text.AsSpan(2), out var number)) return false;
            b = -number;
            return true;
        }

        private BigInteger Number(string text, int offset)
        {
            var start = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
            if (!TryUnsigned(text.AsSpan(start), out var number))
                throw Error("selector/invalid-syntax", offset);
            return start == 1 && text[0] == '-' ? -number : number;
        }

        internal bool TryUnsigned(ReadOnlySpan<char> digits, out BigInteger number,
            Action? afterFirstConvertedChunk = null, Action<int>? observeArithmeticOperandBytes = null)
        {
            number = default;
            if (digits.Length == 0) return false;
            for (var i = 0; i < digits.Length; i++)
            {
                Poll();
                if (digits[i] is < '0' or > '9') return false;
            }

            if (digits.Length <= 9)
            {
                // A single chunk needs none of the combining machinery below.
                uint value = 0;
                foreach (var digit in digits)
                {
                    Poll();
                    value = value * 10 + (uint) (digit - '0');
                }
                afterFirstConvertedChunk?.Invoke();
                _cancellation.ThrowIfCancellationRequested();
                number = value;
                return true;
            }

            // Combine base-10^9 chunks as a balanced binary tree. A left fold would
            // rebuild the entire growing BigInteger for every nine input digits.
            var groups = new List<DigitGroup>();
            var powers = new List<BigInteger> { new(1_000_000_000) };
            var firstLength = digits.Length % 9;
            if (firstLength == 0) firstLength = 9;
            for (var offset = 0; offset < digits.Length;)
            {
                var length = offset == 0 ? firstLength : 9;
                uint chunk = 0;
                for (var i = 0; i < length; i++)
                {
                    Poll();
                    chunk = chunk * 10 + (uint) (digits[offset + i] - '0');
                }
                var group = new DigitGroup(new BigInteger(chunk), 0);
                if (offset == 0)
                {
                    afterFirstConvertedChunk?.Invoke();
                    _cancellation.ThrowIfCancellationRequested();
                }
                while (groups.Count > 0 && groups[^1].Level == group.Level)
                {
                    Poll();
                    _cancellation.ThrowIfCancellationRequested();
                    var left = groups[^1];
                    groups.RemoveAt(groups.Count - 1);
                    group = new DigitGroup(
                        Multiply(left.Value, PowerForLevel(group.Level, powers, observeArithmeticOperandBytes),
                            observeArithmeticOperandBytes) + group.Value, group.Level + 1);
                    _cancellation.ThrowIfCancellationRequested();
                }
                groups.Add(group);
                offset += length;
            }

            number = groups[0].Value;
            for (var i = 1; i < groups.Count; i++)
            {
                Poll();
                _cancellation.ThrowIfCancellationRequested();
                var right = groups[i];
                number = Multiply(number, PowerForLevel(right.Level, powers, observeArithmeticOperandBytes),
                    observeArithmeticOperandBytes) + right.Value;
                _cancellation.ThrowIfCancellationRequested();
            }
            return true;
        }

        private BigInteger PowerForLevel(int level, List<BigInteger> powers,
            Action<int>? observeArithmeticOperandBytes)
        {
            while (powers.Count <= level)
            {
                Poll();
                _cancellation.ThrowIfCancellationRequested();
                var previous = powers[^1];
                powers.Add(Multiply(previous, previous, observeArithmeticOperandBytes));
                _cancellation.ThrowIfCancellationRequested();
            }
            return powers[level];
        }

        private BigInteger Multiply(BigInteger left, BigInteger right,
            Action<int>? observeArithmeticOperandBytes)
        {
            if (observeArithmeticOperandBytes is not null)
            {
                observeArithmeticOperandBytes(left.ToByteArray().Length + right.ToByteArray().Length);
                _cancellation.ThrowIfCancellationRequested();
            }
            return left * right;
        }

        private readonly record struct DigitGroup(BigInteger Value, int Level);

        private IReadOnlyList<string> ParseTextArguments(CssComponentValueList values, int endOffset)
        {
            var strings = new List<string>();
            var i = 0;
            while (true)
            {
                Poll();
                SkipSpace(values, ref i);
                if (i >= values.Count) break;
                var v = values[i];
                if (!IsIdent(v) && !IsToken(v, CssTokenKind.String))
                    throw Error("selector/invalid-syntax", v.Span.Start);
                strings.Add(v.Token.Text);
                i++;
                SkipSpace(values, ref i);
                if (i >= values.Count) break;
                if (!IsToken(values[i], CssTokenKind.Comma))
                    throw Error("selector/invalid-syntax", values[i].Span.Start);
                i++;
                SkipSpace(values, ref i);
                if (i >= values.Count) throw Error("selector/invalid-syntax", endOffset);
            }
            if (strings.Count == 0) throw Error("selector/invalid-syntax", endOffset);
            return Freeze(strings);
        }

        private string ParseSingleIdent(CssComponentValueList values, int endOffset)
        {
            var i = 0;
            SkipSpace(values, ref i);
            if (i >= values.Count || !IsIdent(values[i]))
                throw Error("selector/invalid-syntax", i < values.Count ? values[i].Span.Start : endOffset);
            var text = values[i++].Token.Text;
            SkipSpace(values, ref i);
            if (i != values.Count) throw Error("selector/invalid-syntax", values[i].Span.Start);
            return text;
        }

        private void SetDefaultNamespace(CompoundBuilder compound)
        {
            if (_context.NamespaceBindings.TryGetValue(string.Empty, out var uri))
            {
                compound.NamespaceMode = uri.Length == 0 ? NamespaceMode.None : NamespaceMode.Exact;
                compound.NamespaceUri = uri.Length == 0 ? null : uri;
            }
        }

        private void SetNamespace(CompoundBuilder compound, string? prefix, int offset) =>
            ResolveNamespace(prefix, offset, out compound.NamespaceMode, out compound.NamespaceUri);

        private void ResolveNamespace(string? prefix, int offset, out NamespaceMode mode, out string? uri)
        {
            uri = null;
            if (prefix is null) { mode = NamespaceMode.Any; return; }
            if (prefix.Length == 0) { mode = NamespaceMode.None; return; }
            if (!_context.NamespaceBindings.TryGetValue(prefix, out uri))
                throw Error("selector/undeclared-prefix", offset);
            if (uri.Length == 0)
            {
                mode = NamespaceMode.None;
                uri = null;
            }
            else mode = NamespaceMode.Exact;
        }

        private PredicateKind Identify(string name, bool element, bool function, int offset)
        {
            if (element)
            {
                if (function) return SelectorElementFunctionLookup.Match(AsciiLower(name)) ?? throw Error("selector/unsupported-construct", offset);
                if (name.Length >= 8 && CssAscii.EqualsIgnoreCase(name[..8], "-webkit-"))
                    return PredicateKind.WebkitUnknownPseudoElement;
                return SelectorElementLookup.Match(AsciiLower(name)) ?? throw Error("selector/unsupported-construct", offset);
            }
            if (!function)
            {
                return SelectorPseudoClassLookup.Match(AsciiLower(name)) ?? throw Error("selector/unsupported-construct", offset);
            }
            return SelectorPseudoFunctionLookup.Match(AsciiLower(name)) ?? throw Error("selector/unsupported-construct", offset);
        }

        private SelectorParseException Error(string code, int offset)
        {
            _cancellation.ThrowIfCancellationRequested();
            return new SelectorParseException(code, offset);
        }

        private void Poll()
        {
            _supportsWork?.Charge(1);
            if ((++_work & 255) == 0) Check();
        }

        private void Check()
        {
            _cancellation.ThrowIfCancellationRequested();
            _checkpoint?.Invoke();
            _cancellation.ThrowIfCancellationRequested();
        }

        private int ContainerEndOffset(CssComponentValue container, char closing)
        {
            var end = container.Span.Start + container.Span.Length;
            if (end == 0 || _source[end - 1] != closing) return end;
            var children = container.Values;
            if (children.Count != 0)
            {
                var last = children[^1].Span;
                if (last.Start + last.Length == end) return end;
            }
            return end - 1;
        }

        private void SkipSpace(CssComponentValueList values, ref int i, int end = int.MaxValue)
        {
            end = Math.Min(end, values.Count);
            while (i < end && IsSpace(values[i])) { Poll(); i++; }
        }

        private static bool TryCombinator(CssComponentValueList values, int i, out Combinator combinator, out int width)
        {
            combinator = Combinator.Descendant; width = 1;
            if (IsDelim(values[i], '>')) { combinator = Combinator.Child; return true; }
            if (IsDelim(values[i], '+')) { combinator = Combinator.NextSibling; return true; }
            if (IsDelim(values[i], '~')) { combinator = Combinator.SubsequentSibling; return true; }
            if (IsDelim(values[i], '|') && i + 1 < values.Count && IsDelim(values[i + 1], '|'))
            { combinator = Combinator.Column; width = 2; return true; }
            return false;
        }

        private static bool IsSpace(in CssComponentValue v) => v.TokenKind == CssTokenKind.Whitespace;
        private static bool IsIdent(in CssComponentValue v) => v.TokenKind == CssTokenKind.Ident;
        private static bool IsToken(in CssComponentValue v, CssTokenKind kind) => v.TokenKind == kind;
        private static bool IsDelim(in CssComponentValue v, char delimiter) =>
            v.TokenKind == CssTokenKind.Delim && v.Token.Delimiter == delimiter;
        private static bool EqualsAscii(string a, string b) => CssAscii.EqualsIgnoreCase(a, b);

        private string AsciiLower(string text)
        {
            var firstUpper = -1;
            for (var i = 0; i < text.Length; i++)
            {
                Poll();
                if (text[i] is >= 'A' and <= 'Z') { firstUpper = i; break; }
            }
            if (firstUpper < 0) return text;
            var chars = text.ToCharArray();
            for (var i = firstUpper; i < chars.Length; i++)
            {
                Poll();
                if (chars[i] is >= 'A' and <= 'Z') chars[i] = (char) (chars[i] + ('a' - 'A'));
            }
            return new string(chars);
        }
        private static CssSourceSpan Span(int start, int end) => new(start, end - start);

        private sealed class Frame
        {
            internal readonly CssComponentValueList Values;
            internal readonly bool Forgiving;
            internal readonly bool Relative;
            internal readonly bool NestingRoot;
            internal bool BranchContainsNesting;
            internal bool ContainsNesting;
            internal bool InsideHas;
            internal readonly bool AllowPseudoElements;
            internal readonly bool CompoundOnly;
            internal readonly bool SingleBranch;
            internal readonly bool PseudoElementContext;
            internal readonly int EndOffset;
            internal readonly List<ComplexSelector> Branches = new();
            internal readonly List<Compound> Compounds = new();
            internal readonly List<Combinator> Combinators = new();
            internal int Index;
            internal int BranchStart = -1;
            internal bool HadSpace;
            internal bool Done;
            internal Combinator? Leading;
            internal int LeadingStart = -1;
            internal Combinator? PendingCombinator;
            internal CompoundBuilder? Compound;
            private CompoundBuilder? _spareCompound;
            internal Pending? Pending;
            internal SelectorSpecificity Specificity;
            internal SelectorSpecificity MaximumSpecificity;

            internal Frame(CssComponentValueList values, bool forgiving, bool relative, bool insideHas,
                bool allowPseudoElements, int endOffset, bool compoundOnly = false,
                bool singleBranch = false, bool pseudoElementContext = false, bool nestingRoot = false)
            {
                Values = values; Forgiving = forgiving; Relative = relative; NestingRoot = nestingRoot; InsideHas = insideHas;
                AllowPseudoElements = allowPseudoElements; EndOffset = endOffset;
                CompoundOnly = compoundOnly; SingleBranch = singleBranch;
                PseudoElementContext = pseudoElementContext;
            }

            internal void Accept(CompiledSelector child)
            {
                var pending = Pending ?? throw new InvalidOperationException();
                Pending = null;
                BranchContainsNesting |= child.ContainsNesting;
                ContainsNesting |= child.ContainsNesting;
                Compound!.Predicates.Add(new Predicate(pending.Kind, pending.Span,
                    arguments: child, a: pending.A, b: pending.B));
            }

            internal void End(Worker worker, bool eof)
            {
                if (Done) return;
                if (Compound is not null) FinishCompound(worker);
                if (Compounds.Count == 0 || PendingCombinator is not null)
                {
                    if (!Forgiving) throw worker.Error("selector/invalid-syntax", eof ? EndOffset : Values[Index].Span.Start);
                    Reset();
                }
                else
                {
                    var start = LeadingStart >= 0 ? LeadingStart :
                        BranchStart >= 0 ? BranchStart : Compounds[0].Span.Start;
                    var end = Compounds[^1].Span.Start + Compounds[^1].Span.Length;
                    // CSS Nesting §3.1: prepend a typed parent reference for relative branches.
                    if (NestingRoot && (!BranchContainsNesting || Leading is not null))
                    {
                        var parent = worker._context.NestingParent!;
                        Compounds.Insert(0, new Compound(NamespaceMode.Any, null, null, false,
                            new[] { new Predicate(PredicateKind.Is, Span(start, start), arguments: parent, nestingReference: true) }, Span(start, start)));
                        Combinators.Insert(0, Leading ?? Combinator.Descendant);
                        Specificity = SelectorSpecificity.Add(Specificity, parent.MaximumSpecificity);
                        Leading = null;
                    }
                    Branches.Add(new ComplexSelector(Freeze(Compounds), Freeze(Combinators), Leading,
                        Span(start, end), Specificity));
                    if (Specificity.CompareTo(MaximumSpecificity) > 0)
                        MaximumSpecificity = Specificity;
                    Reset();
                }
                if (eof)
                {
                    if (Branches.Count == 0 && !Forgiving)
                        throw worker.Error("selector/invalid-syntax", EndOffset);
                    Done = true;
                }
            }

            internal void FinishCompound(Worker worker)
            {
                var builder = Compound!;
                if (!builder.ExplicitType && builder.Predicates.Count == 0)
                    throw worker.Error("selector/invalid-syntax", builder.Start);
                if (!builder.ExplicitType) worker.SetDefaultNamespace(builder);
                if (CompoundOnly && Compounds.Count != 0)
                    throw worker.Error("selector/invalid-syntax", builder.Start);
                if (Compounds.Count == 0) BranchStart = builder.Start;
                else Combinators.Add(PendingCombinator ?? Combinator.Descendant);
                var compound = new Compound(builder.NamespaceMode, builder.NamespaceUri, builder.TypeName,
                    builder.ExplicitType, Freeze(builder.Predicates), Span(builder.Start, builder.End));
                Compounds.Add(compound);
                if (builder.ExplicitType && builder.TypeName is not null)
                    Specificity = SelectorSpecificity.Add(Specificity, new SelectorSpecificity(0, 0, 1));
                foreach (var p in builder.Predicates)
                {
                    worker.Poll();
                    var baseSpecificity = p.Kind switch
                    {
                        PredicateKind.Id => new SelectorSpecificity(1, 0, 0),
                        PredicateKind.PseudoElement or PredicateKind.WebkitUnknownPseudoElement or PredicateKind.Picker or
                            PredicateKind.Slotted => new SelectorSpecificity(0, 0, 1),
                        PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or PredicateKind.Has => default,
                        _ => new SelectorSpecificity(0, 1, 0)
                    };
                    if (p.Kind is PredicateKind.Is or PredicateKind.Not or PredicateKind.Has or
                        PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted or
                        PredicateKind.NthChild or PredicateKind.NthLastChild)
                        baseSpecificity = SelectorSpecificity.Add(baseSpecificity,
                            p.Arguments?.MaximumSpecificity ?? default);
                    Specificity = SelectorSpecificity.Add(Specificity, baseSpecificity);
                }
                Compound = null;
                PendingCombinator = null;
                builder.Reset(0);
                _spareCompound = builder;
            }

            // A finished builder's predicates are copied out, so the next compound reuses it.
            internal CompoundBuilder StartCompound(int start)
            {
                var builder = _spareCompound;
                _spareCompound = null;
                if (builder is null) builder = new CompoundBuilder(start);
                else builder.Reset(start);
                return Compound = builder;
            }

            internal void Recover(Worker worker)
            {
                while (Index < Values.Count && !IsToken(Values[Index], CssTokenKind.Comma))
                { worker.Poll(); Index++; }
                if (Index < Values.Count) Index++;
                Reset();
                if (Index == Values.Count) Done = true;
            }

            private void Reset()
            {
                Compounds.Clear(); Combinators.Clear(); Compound = null; Pending = null;
                BranchStart = -1; HadSpace = false; Leading = null; LeadingStart = -1;
                PendingCombinator = null;
                Specificity = default;
                BranchContainsNesting = false;
            }
        }

        private sealed class CompoundBuilder
        {
            internal CompoundBuilder(int start) { Start = start; End = start; }

            internal void Reset(int start)
            {
                Start = End = start;
                ExplicitType = PseudoElement = false;
                LastPseudoElement = null;
                NamespaceMode = NamespaceMode.Any;
                NamespaceUri = TypeName = null;
                Predicates.Clear();
            }
            internal int Start;
            internal int End;
            internal bool ExplicitType;
            internal bool PseudoElement;
            internal PredicateKind? LastPseudoElement;
            internal NamespaceMode NamespaceMode = NamespaceMode.Any;
            internal string? NamespaceUri;
            internal string? TypeName;
            internal readonly List<Predicate> Predicates = new();
        }

        private sealed class Pending
        {
            internal Pending(PredicateKind kind, CssSourceSpan span, BigInteger a = default, BigInteger b = default)
            { Kind = kind; Span = span; A = a; B = b; }
            internal PredicateKind Kind { get; }
            internal CssSourceSpan Span { get; }
            internal BigInteger A { get; }
            internal BigInteger B { get; }
        }
    }
}
