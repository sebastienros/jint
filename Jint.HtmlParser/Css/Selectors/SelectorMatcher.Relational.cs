using static Jint.HtmlParser.Css.Selectors.CompiledSelector;
using ComplexSelector = Jint.HtmlParser.Css.Selectors.CompiledSelector.Complex;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors §4, §13.3 and §17.3: https://drafts.csswg.org/selectors/#match-a-selector-against-an-element
// Evaluation frames are local to one call. Nested functions and combinator backtracking
// share one work counter, and neither compiled programs nor DOM nodes retain them.
internal static partial class SelectorMatcher
{
    private enum EvaluationKind { Program, Branch, Compound, Predicate, Has, Nth }

    private sealed class EvaluationFrame(EvaluationKind kind, Node node, Node? scope)
    {
        internal EvaluationKind Kind = kind;
        internal Node Node = node;
        internal Node? Scope = scope;
        internal CompiledSelector? Program;
        internal ComplexSelector? Branch;
        internal Compound? Compound;
        internal Predicate? Predicate;
        internal Node? Anchor;
        internal bool SuppressSubjectNamespace;
        internal bool Waiting;
        internal bool Found;
        internal bool NeedSpecificity;
        internal int Index;
        internal int Position;
        internal SelectorSpecificity Best;
        internal List<BranchPosition>? Positions;
        internal Node? RegionRoot;
        internal Node? Cursor;
        internal bool RegionStarted;
        internal int NthIndex;
        internal List<ForwardState>? Forward;
        internal HashSet<(int Position, Node Node)>? ForwardVisited;
        internal bool? RelativeHasColumn;
    }

    private sealed class BranchPosition(Node node, int part)
    {
        internal Node Node = node;
        internal int Part = part;
        internal bool Checked;
        internal Node? Next;
        internal IEnumerator<Element>? Columns;
    }

    private sealed class ForwardState(Node source, int position)
    {
        internal Node Source = source;
        internal int Position = position;
        internal Node? Cursor;
        internal bool Started;
        internal IEnumerator<Element>? Columns;
    }

    private sealed class FeaturelessFrame(CompiledSelector program)
    {
        internal CompiledSelector Program = program;
        internal int BranchIndex;
        internal int PredicateIndex;
        internal bool InBranch;
        internal bool Waiting;
        internal bool HasCompanion;
        internal bool HasHas;
    }

    private static bool Evaluate(CompiledSelector program, Element element, Node? scope,
        ref Work work, out SelectorSpecificity specificity)
    {
        var root = new EvaluationFrame(EvaluationKind.Program, element, scope)
        {
            Program = program,
            NeedSpecificity = true
        };
        var stack = new List<EvaluationFrame> { root };
        var result = false;
        while (stack.Count != 0)
        {
            work.Step();
            var frame = stack[^1];
            switch (frame.Kind)
            {
                case EvaluationKind.Program:
                    if (frame.Waiting)
                    {
                        frame.Waiting = false;
                        if (result)
                        {
                            if (!frame.NeedSpecificity)
                            {
                                stack.RemoveAt(stack.Count - 1);
                                continue;
                            }
                            frame.Found = true;
                            var matched = frame.Program!.Branches[frame.Index - 1].Specificity;
                            if (matched.CompareTo(frame.Best) > 0) frame.Best = matched;
                        }
                    }
                    if (frame.Index == frame.Program!.Branches.Count)
                    {
                        result = frame.Found;
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                    var branch = frame.Program.Branches[frame.Index++];
                    frame.Waiting = true;
                    stack.Add(new EvaluationFrame(EvaluationKind.Branch, frame.Node, frame.Scope)
                    {
                        Branch = branch,
                        SuppressSubjectNamespace = frame.SuppressSubjectNamespace,
                        Positions = new List<BranchPosition> { new(frame.Node, branch.Compounds.Count - 1) }
                    });
                    break;

                case EvaluationKind.Branch:
                    var positions = frame.Positions!;
                    if (frame.Waiting)
                    {
                        frame.Waiting = false;
                        var checkedPosition = positions[^1];
                        if (!result)
                        {
                            checkedPosition.Columns?.Dispose();
                            positions.RemoveAt(positions.Count - 1);
                        }
                        else if (checkedPosition.Part == 0)
                        {
                            if (frame.Anchor is null || LeadingMatches(checkedPosition.Node,
                                    frame.Anchor, frame.Branch!.LeadingCombinator ?? Combinator.Descendant,
                                    ref work))
                            {
                                result = true;
                                stack.RemoveAt(stack.Count - 1);
                                continue;
                            }
                            // Another predecessor may satisfy the anchor relationship.
                            checkedPosition.Columns?.Dispose();
                            positions.RemoveAt(positions.Count - 1);
                        }
                        else
                        {
                            var edge = frame.Branch!.Combinators[checkedPosition.Part - 1];
                            if (edge == Combinator.Column)
                                checkedPosition.Columns = ColumnPredecessors(checkedPosition.Node, ref work);
                            else
                                checkedPosition.Next = InitialPredecessor(checkedPosition.Node, edge, ref work);
                        }
                    }
                    if (positions.Count == 0)
                    {
                        result = false;
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                    var position = positions[^1];
                    if (!position.Checked)
                    {
                        position.Checked = true;
                        frame.Waiting = true;
                        stack.Add(new EvaluationFrame(EvaluationKind.Compound, position.Node, frame.Scope)
                        {
                            Compound = frame.Branch!.Compounds[position.Part],
                            SuppressSubjectNamespace = frame.SuppressSubjectNamespace &&
                                position.Part == frame.Branch.Compounds.Count - 1
                        });
                        break;
                    }
                    var relation = frame.Branch!.Combinators[position.Part - 1];
                    Node? predecessor;
                    if (relation == Combinator.Column)
                        predecessor = position.Columns is not null && position.Columns.MoveNext()
                            ? position.Columns.Current : null;
                    else predecessor = position.Next;
                    if (predecessor is null)
                    {
                        position.Columns?.Dispose();
                        positions.RemoveAt(positions.Count - 1);
                        break;
                    }
                    work.Step();
                    if (relation != Combinator.Column)
                        position.Next = NextPredecessor(predecessor, relation, ref work);
                    if (predecessor is Element || ReferenceEquals(predecessor, frame.Scope) &&
                        predecessor is DocumentFragment)
                        positions.Add(new BranchPosition(predecessor, position.Part - 1));
                    break;

                case EvaluationKind.Compound:
                    if (frame.Position == 0)
                    {
                        var compound = frame.Compound!;
                        if (frame.Node is Element current)
                        {
                            if ((!frame.SuppressSubjectNamespace || compound.HasExplicitType) &&
                                !NamespaceMatches(compound.NamespaceMode, compound.NamespaceUri, current.NamespaceUri) ||
                                compound.TypeName is not null && !SelectorNameMatches(compound.TypeName,
                                    current.LocalName, IsHtmlElement(current), ref work))
                            {
                                result = false;
                                stack.RemoveAt(stack.Count - 1);
                                continue;
                            }
                        }
                        else if (!ReferenceEquals(frame.Node, frame.Scope) ||
                                 frame.Node is not DocumentFragment || compound.HasExplicitType ||
                                 compound.Predicates.Count == 0)
                        {
                            result = false;
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                        frame.Position = 1;
                    }
                    if (frame.Waiting)
                    {
                        frame.Waiting = false;
                        if (!result)
                        {
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                    }
                    if (frame.Index == frame.Compound!.Predicates.Count)
                    {
                        result = true;
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                    var predicate = frame.Compound.Predicates[frame.Index++];
                    work.Step();
                    if (frame.Node is DocumentFragment && predicate.Kind == PredicateKind.Has &&
                        !HasFeaturelessCompanion(frame.Compound, ref work))
                    {
                        result = false;
                        stack.RemoveAt(stack.Count - 1);
                        break;
                    }
                    if (predicate.Kind is not (PredicateKind.Is or PredicateKind.Where or
                            PredicateKind.Not or PredicateKind.Has) &&
                        !(predicate.Kind is PredicateKind.NthChild or PredicateKind.NthLastChild &&
                          predicate.Arguments is not null))
                    {
                        result = frame.Node is Element ordinary
                            ? MatchPredicate(predicate, ordinary, frame.Scope, ref work)
                            : predicate.Kind == PredicateKind.Scope && ReferenceEquals(frame.Node, frame.Scope);
                        if (!result) stack.RemoveAt(stack.Count - 1);
                        break;
                    }
                    frame.Waiting = true;
                    stack.Add(new EvaluationFrame(EvaluationKind.Predicate, frame.Node, frame.Scope)
                    {
                        Predicate = predicate
                    });
                    break;

                case EvaluationKind.Predicate:
                    var currentPredicate = frame.Predicate!;
                    if (frame.Waiting)
                    {
                        result = currentPredicate.Kind == PredicateKind.Not ? !result : result;
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                    if (currentPredicate.Kind is PredicateKind.Is or PredicateKind.Where or PredicateKind.Not)
                    {
                        // Logical combinations can match a featureless scope only
                        // when an argument selector is itself eligible for it.
                        if (frame.Node is DocumentFragment &&
                            !IsFeaturelessEligible(currentPredicate.Arguments!, ref work))
                        {
                            result = false;
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                        frame.Waiting = true;
                        stack.Add(new EvaluationFrame(EvaluationKind.Program, frame.Node, frame.Scope)
                        {
                            Program = currentPredicate.Arguments,
                            SuppressSubjectNamespace = true
                        });
                        break;
                    }
                    if (currentPredicate.Kind == PredicateKind.Has)
                    {
                        if (frame.Node is not Element && frame.Node is not DocumentFragment)
                        {
                            result = false;
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                        frame.Waiting = true;
                        stack.Add(new EvaluationFrame(EvaluationKind.Has, frame.Node, frame.Scope)
                        {
                            Program = currentPredicate.Arguments
                        });
                        break;
                    }
                    if (currentPredicate.Kind is PredicateKind.NthChild or PredicateKind.NthLastChild &&
                        currentPredicate.Arguments is not null)
                    {
                        if (frame.Node is not Element)
                        {
                            result = false;
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                        frame.Waiting = true;
                        stack.Add(new EvaluationFrame(EvaluationKind.Nth, frame.Node, frame.Scope)
                        {
                            Predicate = currentPredicate,
                            NthIndex = 1
                        });
                        break;
                    }
                    throw Unsupported(currentPredicate.Kind.ToString());

                case EvaluationKind.Has:
                    if (frame.Waiting)
                    {
                        frame.Waiting = false;
                        if (result)
                        {
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                    }
                    var relative = frame.Program!.Branches[frame.Index];
                    var candidate = NextRelativeCandidate(frame, relative, ref work);
                    if (candidate is null)
                    {
                        frame.Index++;
                        if (frame.Index == frame.Program.Branches.Count)
                        {
                            result = false;
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                        frame.RegionRoot = null;
                        frame.Cursor = null;
                        frame.RegionStarted = false;
                        frame.Forward = null;
                        frame.ForwardVisited = null;
                        frame.RelativeHasColumn = null;
                        break;
                    }
                    frame.Waiting = true;
                    stack.Add(new EvaluationFrame(EvaluationKind.Branch, candidate, frame.Scope)
                    {
                        Branch = relative,
                        Anchor = frame.Node,
                        Positions = new List<BranchPosition> { new(candidate, relative.Compounds.Count - 1) }
                    });
                    break;

                case EvaluationKind.Nth:
                    var nth = frame.Predicate!;
                    if (frame.Position == 0)
                    {
                        frame.Position = 1;
                        frame.Waiting = true;
                        stack.Add(new EvaluationFrame(EvaluationKind.Program, frame.Node, frame.Scope)
                        {
                            Program = nth.Arguments
                        });
                        break;
                    }
                    if (frame.Waiting)
                    {
                        frame.Waiting = false;
                        if (frame.Position == 1 && !result)
                        {
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                        if (frame.Position == 2 && result) frame.NthIndex++;
                    }
                    frame.Position = 2;
                    var fromEnd = nth.Kind == PredicateKind.NthLastChild;
                    var sibling = frame.Cursor is null
                        ? fromEnd ? frame.Node.NextSibling : frame.Node.PreviousSibling
                        : fromEnd ? frame.Cursor.NextSibling : frame.Cursor.PreviousSibling;
                    while (sibling is not null && sibling is not Element)
                    {
                        work.Step();
                        sibling = fromEnd ? sibling.NextSibling : sibling.PreviousSibling;
                    }
                    if (sibling is null)
                    {
                        result = MatchAnPlusB(frame.NthIndex, nth.A, nth.B, ref work);
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                    work.Step();
                    frame.Cursor = sibling;
                    frame.Waiting = true;
                    stack.Add(new EvaluationFrame(EvaluationKind.Program, sibling, frame.Scope)
                    {
                        Program = nth.Arguments
                    });
                    break;
            }
        }
        specificity = root.Best;
        return result;
    }

    private static bool LeadingMatches(Node first, Node anchor, Combinator combinator, ref Work work)
    {
        switch (combinator)
        {
            case Combinator.Column:
                return ColumnLeadingMatches(first, anchor, ref work);
            case Combinator.Child:
                return ReferenceEquals(first.ParentNode, anchor);
            case Combinator.Descendant:
                for (var parent = first.ParentNode; parent is not null; parent = parent.ParentNode)
                {
                    work.Step();
                    if (ReferenceEquals(parent, anchor)) return true;
                }
                return false;
            case Combinator.NextSibling:
            case Combinator.SubsequentSibling:
                for (var previous = first.PreviousSibling; previous is not null; previous = previous.PreviousSibling)
                {
                    work.Step();
                    if (previous is not Element) continue;
                    if (ReferenceEquals(previous, anchor)) return true;
                    if (combinator == Combinator.NextSibling) return false;
                }
                return false;
            default:
                throw Unsupported(combinator.ToString());
        }
    }

    // Selectors §3.2.1 permits :has() on a featureless subject only when its
    // compound also contains another simple selector allowed to match it.
    private static bool HasFeaturelessCompanion(Compound compound, ref Work work)
    {
        foreach (var predicate in compound.Predicates)
        {
            work.Step();
            if (predicate.Kind != PredicateKind.Has) return true;
        }
        return false;
    }

    private static bool IsFeaturelessEligible(CompiledSelector program, ref Work work)
    {
        if (work.TryGetFeaturelessEligibility(program, out var cached)) return cached;
        var stack = new List<FeaturelessFrame> { new(program) };
        var result = false;
        while (stack.Count != 0)
        {
            work.Step();
            var frame = stack[^1];
            if (frame.Waiting)
            {
                frame.Waiting = false;
                if (!result)
                {
                    frame.InBranch = false;
                    frame.BranchIndex++;
                    continue;
                }
                frame.HasCompanion = true;
            }
            if (!frame.InBranch)
            {
                if (frame.BranchIndex == frame.Program.Branches.Count)
                {
                    result = false;
                    work.SetFeaturelessEligibility(frame.Program, false);
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                var subject = frame.Program.Branches[frame.BranchIndex].Compounds[^1];
                if (subject.HasExplicitType || subject.Predicates.Count == 0)
                {
                    frame.BranchIndex++;
                    continue;
                }
                frame.InBranch = true;
                frame.PredicateIndex = 0;
                frame.HasCompanion = false;
                frame.HasHas = false;
            }
            var predicates = frame.Program.Branches[frame.BranchIndex].Compounds[^1].Predicates;
            if (frame.PredicateIndex == predicates.Count)
            {
                if (!frame.HasHas || frame.HasCompanion)
                {
                    result = true;
                    work.SetFeaturelessEligibility(frame.Program, true);
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                frame.InBranch = false;
                frame.BranchIndex++;
                continue;
            }
            var predicate = predicates[frame.PredicateIndex++];
            work.Step();
            if (predicate.Kind == PredicateKind.Scope)
            {
                frame.HasCompanion = true;
            }
            else if (predicate.Kind == PredicateKind.Has)
            {
                frame.HasHas = true;
            }
            else if (predicate.Kind is PredicateKind.Is or PredicateKind.Where or PredicateKind.Not)
            {
                if (work.TryGetFeaturelessEligibility(predicate.Arguments!, out var childEligible))
                {
                    if (childEligible) frame.HasCompanion = true;
                    else
                    {
                        frame.InBranch = false;
                        frame.BranchIndex++;
                    }
                }
                else
                {
                    frame.Waiting = true;
                    stack.Add(new FeaturelessFrame(predicate.Arguments!));
                }
            }
            else
            {
                frame.InBranch = false;
                frame.BranchIndex++;
            }
        }
        return result;
    }

    private static Element? NextRelativeCandidate(EvaluationFrame frame, ComplexSelector branch, ref Work work)
    {
        frame.RelativeHasColumn ??= HasColumnEdge(branch, ref work);
        if (frame.RelativeHasColumn.Value)
            return NextColumnRelativeCandidate(frame, branch, ref work);
        if (branch.Compounds.Count == 1 && branch.LeadingCombinator == Combinator.NextSibling)
        {
            if (frame.RegionStarted) return null;
            frame.RegionStarted = true;
            return NextElementSibling(frame.Node, ref work);
        }
        if (branch.Compounds.Count == 1 && branch.LeadingCombinator == Combinator.SubsequentSibling)
        {
            var next = NextElementSibling(frame.Cursor ?? frame.Node, ref work);
            frame.Cursor = next;
            return next;
        }
        if (branch.Compounds.Count == 1 && branch.LeadingCombinator == Combinator.Child)
        {
            var nextChild = frame.RegionStarted ? frame.Cursor?.NextSibling : frame.Node.FirstChild;
            frame.RegionStarted = true;
            while (nextChild is not null)
            {
                work.Step();
                frame.Cursor = nextChild;
                if (nextChild is Element child) return child;
                nextChild = nextChild.NextSibling;
            }
            return null;
        }
        var siblingMode = branch.LeadingCombinator is Combinator.NextSibling or Combinator.SubsequentSibling;
        if (!frame.RegionStarted)
        {
            frame.RegionStarted = true;
            frame.RegionRoot = siblingMode ? NextElementSibling(frame.Node, ref work) : frame.Node;
            frame.Cursor = siblingMode ? null : frame.Node;
        }
        while (frame.RegionRoot is not null)
        {
            Node? next;
            if (frame.Cursor is null)
            {
                next = frame.RegionRoot;
            }
            else
            {
                next = NextWithin(frame.RegionRoot, frame.Cursor, ref work);
            }
            if (next is null)
            {
                // A leading + constrains the first compound, not the subject.
                // A later sibling can be the subject of a complex relative selector.
                if (!siblingMode) return null;
                frame.RegionRoot = NextElementSibling(frame.RegionRoot, ref work);
                frame.Cursor = null;
                continue;
            }
            frame.Cursor = next;
            work.Step();
            if (next is Element candidate) return candidate;
        }
        return null;
    }

    private static Node? NextWithin(Node root, Node current, ref Work work)
    {
        if (current.FirstChild is not null) return current.FirstChild;
        while (!ReferenceEquals(current, root))
        {
            work.Step();
            if (current.NextSibling is not null) return current.NextSibling;
            current = current.ParentNode!;
        }
        return null;
    }

    private static Element? NextElementSibling(Node node, ref Work work)
    {
        for (var next = node.NextSibling; next is not null; next = next.NextSibling)
        {
            work.Step();
            if (next is Element element) return element;
        }
        return null;
    }
}
