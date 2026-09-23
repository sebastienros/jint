using System.Globalization;
using System.Numerics;
using Jint.HtmlParser.Css.Syntax;
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
        var values = new CssSyntaxParser(source, new CssParseOptions { Limits = context.Limits },
            cancellationToken).ParseComponentValues();
        var compiler = new Worker(source, context, cancellationToken);
        return compiler.Compile(values);
    }

    private sealed class Worker
    {
        private readonly int _sourceLength;
        private readonly string _source;
        private readonly SelectorParseContext _context;
        private readonly CancellationToken _cancellation;
        private int _work;

        internal Worker(string source, SelectorParseContext context, CancellationToken cancellation)
        {
            _source = source;
            _sourceLength = source.Length;
            _context = context;
            _cancellation = cancellation;
        }

        internal CompiledSelector Compile(CssComponentValueList values)
        {
            var stack = new List<Frame> { new(values, false, false, false, true, _sourceLength) };
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
                    var result = new CompiledSelector(Freeze(frame.Branches));
                    stack.RemoveAt(stack.Count - 1);
                    if (stack.Count == 0)
                    {
                        _cancellation.ThrowIfCancellationRequested();
                        return result;
                    }
                    stack[^1].Accept(result);
                }
                catch (SelectorParseException)
                {
                    var forgiving = stack.FindLastIndex(f => f.Forgiving);
                    if (forgiving < 0) throw;
                    stack.RemoveRange(forgiving + 1, stack.Count - forgiving - 1);
                    stack[forgiving].Recover(this);
                }
            }
            throw new InvalidOperationException();
        }

        private Frame? Step(Frame f)
        {
            if (f.Index >= f.Values.Count)
            {
                f.End(this, true);
                return null;
            }
            var value = f.Values[f.Index];
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
                var compound = new CompoundBuilder(value.Span.Start);
                f.Compound = compound;
                if (TryType(f, compound)) return null;
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
                if (prefix) throw Error("selector/invalid-syntax", nameIndex < values.Count ? values[nameIndex].Span.Start : _sourceLength);
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
            if (compound.PseudoElement && !IsToken(value, CssTokenKind.Colon))
                throw Error("selector/invalid-syntax", value.Span.Start);
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
                    throw Error("selector/invalid-syntax", f.Index + 1 < values.Count ? values[f.Index + 1].Span.Start : _sourceLength);
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
            if (nameIndex >= values.Count) throw Error("selector/invalid-syntax", _sourceLength);
            var nameValue = values[nameIndex];
            var name = nameValue.Kind == CssComponentKind.Function ? nameValue.FunctionName :
                IsIdent(nameValue) ? nameValue.Token.Text : null;
            if (name is null) throw Error("selector/invalid-syntax", nameValue.Span.Start);
            var function = nameValue.Kind == CssComponentKind.Function;
            var kind = Identify(name, pseudoElement, function, nameValue.Span.Start);
            if ((pseudoElement || kind == PredicateKind.PseudoElement) && !f.AllowPseudoElements)
                throw Error("selector/invalid-syntax", start);
            if (compound.PseudoElement &&
                kind is not (PredicateKind.Hover or PredicateKind.Active or PredicateKind.Focus or
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
            if (kind is PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or PredicateKind.Has or
                PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted)
            {
                f.Pending = new Pending(kind, span);
                child = new Frame(args, kind is PredicateKind.Is or PredicateKind.Where,
                    kind == PredicateKind.Has, kind != PredicateKind.Has && f.InsideHas,
                    false, argumentEnd,
                    compoundOnly: kind is PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted,
                    singleBranch: kind is PredicateKind.Host or PredicateKind.HostContext or PredicateKind.Slotted);
                // The relative list itself is inside :has for nested-has rejection.
                if (kind == PredicateKind.Has) child.InsideHas = true;
                return true;
            }
            if (kind is PredicateKind.NthChild or PredicateKind.NthLastChild or PredicateKind.NthOfType or
                PredicateKind.NthLastOfType or PredicateKind.NthCol or PredicateKind.NthLastCol)
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
            if (kind is PredicateKind.Lang or PredicateKind.Dir)
            {
                var text = kind == PredicateKind.Lang ? ParseTextArguments(args, argumentEnd) :
                    Freeze(new List<string> { ParseSingleIdent(args, argumentEnd) });
                compound.Predicates.Add(new Predicate(kind, span, textArguments: text));
                return true;
            }
            if (kind == PredicateKind.Picker)
            {
                var text = ParseSingleIdent(args, argumentEnd);
                if (!EqualsAscii(text, "select"))
                    throw Error("selector/invalid-syntax", nameValue.Span.Start);
                compound.Predicates.Add(new Predicate(kind, span, name: "select"));
                return true;
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

        private bool TryUnsigned(ReadOnlySpan<char> digits, out BigInteger number)
        {
            number = default;
            if (digits.Length == 0) return false;
            for (var i = 0; i < digits.Length; i++)
            {
                Poll();
                if (digits[i] is < '0' or > '9') return false;
            }
            return BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

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
                if (function) return AsciiLower(name) switch
                {
                    "picker" => PredicateKind.Picker,
                    "slotted" => PredicateKind.Slotted,
                    _ => throw Error("selector/unsupported-construct", offset)
                };
                if (name.Length >= 8 && CssAscii.EqualsIgnoreCase(name[..8], "-webkit-"))
                    return PredicateKind.WebkitUnknownPseudoElement;
                return AsciiLower(name) switch
                {
                    "before" or "after" or "selection" or "footnote-call" or "footnote-marker" or
                    "first-line" or "first-letter" or "content" or "checkmark" or "picker-icon" => PredicateKind.PseudoElement,
                    _ => throw Error("selector/unsupported-construct", offset)
                };
            }
            if (!function)
            {
                return AsciiLower(name) switch
                {
                    "before" or "after" or "first-line" or "first-letter" => PredicateKind.PseudoElement,
                    "scope" => PredicateKind.Scope,
                    "root" => PredicateKind.Root,
                    "empty" => PredicateKind.Empty,
                    "first-child" => PredicateKind.FirstChild,
                    "last-child" => PredicateKind.LastChild,
                    "only-child" => PredicateKind.OnlyChild,
                    "first-of-type" => PredicateKind.FirstOfType,
                    "last-of-type" => PredicateKind.LastOfType,
                    "only-of-type" => PredicateKind.OnlyOfType,
                    "any-link" => PredicateKind.AnyLink,
                    "link" => PredicateKind.Link,
                    "visited" => PredicateKind.Visited,
                    "checked" => PredicateKind.Checked,
                    "unchecked" => PredicateKind.Unchecked,
                    "indeterminate" => PredicateKind.Indeterminate,
                    "default" => PredicateKind.Default,
                    "enabled" => PredicateKind.Enabled,
                    "disabled" => PredicateKind.Disabled,
                    "required" => PredicateKind.Required,
                    "optional" => PredicateKind.Optional,
                    "valid" => PredicateKind.Valid,
                    "invalid" => PredicateKind.Invalid,
                    "in-range" => PredicateKind.InRange,
                    "out-of-range" => PredicateKind.OutOfRange,
                    "read-only" => PredicateKind.ReadOnly,
                    "read-write" => PredicateKind.ReadWrite,
                    "placeholder-shown" => PredicateKind.PlaceholderShown,
                    "open" => PredicateKind.Open,
                    "closed" => PredicateKind.Closed,
                    "hover" => PredicateKind.Hover,
                    "active" => PredicateKind.Active,
                    "focus" => PredicateKind.Focus,
                    "focus-within" => PredicateKind.FocusWithin,
                    "focus-visible" => PredicateKind.FocusVisible,
                    "target" => PredicateKind.Target,
                    "autofill" or "-webkit-autofill" => PredicateKind.Autofill,
                    "host" => PredicateKind.Host,
                    _ => throw Error("selector/unsupported-construct", offset)
                };
            }
            return AsciiLower(name) switch
            {
                "is" or "matches" => PredicateKind.Is,
                "where" => PredicateKind.Where,
                "not" => PredicateKind.Not,
                "has" => PredicateKind.Has,
                "nth-child" => PredicateKind.NthChild,
                "nth-last-child" => PredicateKind.NthLastChild,
                "nth-of-type" => PredicateKind.NthOfType,
                "nth-last-of-type" => PredicateKind.NthLastOfType,
                "nth-col" => PredicateKind.NthCol,
                "nth-last-col" => PredicateKind.NthLastCol,
                "lang" => PredicateKind.Lang,
                "dir" => PredicateKind.Dir,
                "host" => PredicateKind.Host,
                "host-context" => PredicateKind.HostContext,
                _ => throw Error("selector/unsupported-construct", offset)
            };
        }

        private SelectorParseException Error(string code, int offset)
        {
            _cancellation.ThrowIfCancellationRequested();
            return new SelectorParseException(code, offset);
        }

        private void Poll()
        {
            if ((++_work & 255) == 0) _cancellation.ThrowIfCancellationRequested();
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

        private static void SkipSpace(CssComponentValueList values, ref int i, int end = int.MaxValue)
        {
            end = Math.Min(end, values.Count);
            while (i < end && IsSpace(values[i])) i++;
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

        private static bool IsSpace(CssComponentValue v) => IsToken(v, CssTokenKind.Whitespace);
        private static bool IsIdent(CssComponentValue v) => IsToken(v, CssTokenKind.Ident);
        private static bool IsToken(CssComponentValue v, CssTokenKind kind) =>
            v.Kind == CssComponentKind.Token && v.Token.Kind == kind;
        private static bool IsDelim(CssComponentValue v, char delimiter) =>
            IsToken(v, CssTokenKind.Delim) && v.Token.Delimiter == delimiter;
        private static bool EqualsAscii(string a, string b) => CssAscii.EqualsIgnoreCase(a, b);

        private static string AsciiLower(string text)
        {
            var firstUpper = -1;
            for (var i = 0; i < text.Length; i++)
                if (text[i] is >= 'A' and <= 'Z') { firstUpper = i; break; }
            if (firstUpper < 0) return text;
            var chars = text.ToCharArray();
            for (var i = firstUpper; i < chars.Length; i++)
                if (chars[i] is >= 'A' and <= 'Z') chars[i] = (char) (chars[i] + ('a' - 'A'));
            return new string(chars);
        }
        private static CssSourceSpan Span(int start, int end) => new(start, end - start);

        private sealed class Frame
        {
            internal readonly CssComponentValueList Values;
            internal readonly bool Forgiving;
            internal readonly bool Relative;
            internal bool InsideHas;
            internal readonly bool AllowPseudoElements;
            internal readonly bool CompoundOnly;
            internal readonly bool SingleBranch;
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
            internal Pending? Pending;
            internal SelectorSpecificity Specificity;

            internal Frame(CssComponentValueList values, bool forgiving, bool relative, bool insideHas,
                bool allowPseudoElements, int endOffset, bool compoundOnly = false,
                bool singleBranch = false)
            {
                Values = values; Forgiving = forgiving; Relative = relative; InsideHas = insideHas;
                AllowPseudoElements = allowPseudoElements; EndOffset = endOffset;
                CompoundOnly = compoundOnly; SingleBranch = singleBranch;
            }

            internal void Accept(CompiledSelector child)
            {
                var pending = Pending ?? throw new InvalidOperationException();
                Pending = null;
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
                    Branches.Add(new ComplexSelector(Freeze(Compounds), Freeze(Combinators), Leading,
                        Span(start, end), Specificity));
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
            }
        }

        private sealed class CompoundBuilder
        {
            internal CompoundBuilder(int start) { Start = start; End = start; }
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
