using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class DestructuringPatternAssignmentExpression : JintExpression
{
    private readonly DestructuringPattern _pattern;
    private JintExpression _right = null!;
    private bool _initialized;

    public DestructuringPatternAssignmentExpression(AssignmentExpression expression) : base(expression)
    {
        _pattern = (DestructuringPattern) expression.Left;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            _right = Build(((AssignmentExpression) _expression).Right);
            _initialized = true;
        }

        var rightValue = _right.GetValue(context);
        if (context.IsAbrupt())
        {
            return rightValue;
        }

        var completion = ProcessPatterns(context, _pattern, rightValue, null);
        if (context.IsAbrupt())
        {
            return completion;
        }

        return rightValue;
    }

    internal static JsValue ProcessPatterns(
        EvaluationContext context,
        DestructuringPattern pattern,
        JsValue argument,
        Environment? environment,
        bool checkPatternPropertyReference = true)
    {
        if (pattern is ArrayPattern ap)
        {
            return HandleArrayPattern(context, ap, argument, environment, checkPatternPropertyReference);
        }

        if (pattern is ObjectPattern op)
        {
            return HandleObjectPattern(context, op, argument, environment, checkPatternPropertyReference);
        }

        Throw.ArgumentException("Not a pattern");
        return default;
    }

    private static bool ConsumeFromIterator(IteratorInstance it, out JsValue value, out bool done)
    {
        value = JsValue.Undefined;
        done = false;

        if (!it.TryIteratorStep(out var d))
        {
            done = true;
            return false;
        }

        value = d.Get(CommonProperties.Value);
        return true;
    }

    private static JsValue HandleArrayPattern(
        EvaluationContext context,
        ArrayPattern pattern,
        JsValue argument,
        Environment? environment,
        bool checkReference)
    {
        var engine = context.Engine;
        var realm = engine.Realm;
        var generator = engine.ExecutionContext.Generator;

        // Check if we're resuming from a yield inside this destructuring pattern
        DestructuringSuspendData? suspendData = null;
        var resuming = false;
        if (generator is not null && generator._isResuming)
        {
            if (generator.SuspendData.TryGet(pattern, out suspendData))
            {
                resuming = true;
            }
        }

        var obj = TypeConverter.ToObject(realm, argument);
        ArrayOperations? arrayOperations = null;
        IteratorInstance? iterator = null;

        if (resuming && suspendData is not null)
        {
            // Reuse the saved iterator
            iterator = suspendData.Iterator;

            // If resuming with Return completion, close the iterator immediately
            // This handles the case where generator.return() was called while
            // suspended inside this destructuring pattern
            if (generator!._resumeCompletionType == CompletionType.Return)
            {
                if (!suspendData.Done)
                {
                    suspendData.Done = true;
                    iterator?.Close(CompletionType.Return);
                }
                generator.SuspendData.Clear(pattern);
                // Signal return request - callers check _returnRequested flag
                generator._returnRequested = true;
                generator._suspendedValue = generator._nextValue ?? JsValue.Undefined;
                return JsValue.Undefined;
            }
        }
        else
        {
            // optimize for array unless someone has touched the iterator
            if (obj.IsArrayLike && obj.HasOriginalIterator)
            {
                arrayOperations = ArrayOperations.For(obj, forWrite: false);
            }
            else
            {
                iterator = obj.GetIterator(realm);

                // Save the iterator for potential yield inside this pattern
                if (generator is not null && iterator is not null)
                {
                    suspendData = generator.SuspendData.GetOrCreate<DestructuringSuspendData>(pattern, iterator);
                }
            }
        }

        var completionType = CompletionType.Normal;
        var close = false;
        var done = false;
        uint i = 0;
        try
        {
            ref readonly var elements = ref pattern.Elements;
            for (; i < elements.Count; i++)
            {
                var left = elements[(int) i];

                if (left is null)
                {
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out _);
                    }
                    else
                    {
                        if (!ConsumeFromIterator(iterator!, out _, out done))
                        {
                            break;
                        }
                    }
                    // skip assignment
                    continue;
                }

                if (left is Identifier identifier)
                {
                    JsValue value;
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out value);
                    }
                    else
                    {
                        ConsumeFromIterator(iterator!, out value, out done);
                    }

                    AssignToIdentifier(engine, identifier.Name, value, environment, checkReference);
                }
                else if (left is MemberExpression me)
                {
                    close = true;
                    var reference = GetReferenceFromMember(context, me);

                    // Check for generator suspension after evaluating member expression
                    if (context.IsSuspended())
                    {
                        close = false; // Don't close iterator, we'll resume later
                        return JsValue.Undefined;
                    }

                    // Check for generator return request
                    if (generator?._returnRequested == true)
                    {
                        if (!done && iterator is not null)
                        {
                            done = true;
                            iterator.Close(CompletionType.Return);
                        }
                        generator.SuspendData.Clear(pattern);
                        close = false; // Already closed
                        return JsValue.Undefined;
                    }

                    JsValue value;
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out value);
                    }
                    else
                    {
                        ConsumeFromIterator(iterator!, out value, out done);
                    }

                    AssignToReference(engine, reference, value, environment);
                }
                else if (left is DestructuringPattern dp)
                {
                    JsValue value;
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out value);
                    }
                    else
                    {
                        iterator!.TryIteratorStep(out var temp);
                        value = temp;
                    }
                    ProcessPatterns(context, dp, value, environment);
                }
                else if (left is RestElement restElement)
                {
                    close = true;
                    Reference? reference = null;
                    if (restElement.Argument is MemberExpression memberExpression)
                    {
                        reference = GetReferenceFromMember(context, memberExpression);

                        // Check for generator suspension after evaluating member expression
                        if (context.IsSuspended())
                        {
                            close = false; // Don't close iterator, we'll resume later
                            return JsValue.Undefined;
                        }

                        // Check for generator return request
                        if (generator?._returnRequested == true)
                        {
                            if (!done && iterator is not null)
                            {
                                done = true;
                                iterator.Close(CompletionType.Return);
                            }
                            generator.SuspendData.Clear(pattern);
                            close = false; // Already closed
                            return JsValue.Undefined;
                        }
                    }

                    JsArray array;
                    if (arrayOperations != null)
                    {
                        var length = arrayOperations.GetLength();
                        array = engine.Realm.Intrinsics.Array.ArrayCreate(length - i);
                        for (uint j = i; j < length; ++j)
                        {
                            arrayOperations.TryGetValue(j, out var indexValue);
                            array.SetIndexValue(j - i, indexValue, updateLength: false);
                        }
                    }
                    else
                    {
                        array = engine.Realm.Intrinsics.Array.ArrayCreate(0);
                        uint index = 0;
                        done = true;
                        do
                        {
                            if (!iterator!.TryIteratorStep(out var item))
                            {
                                done = true;
                                break;
                            }

                            var value = item.Get(CommonProperties.Value);
                            array.SetIndexValue(index++, value, updateLength: false);
                        } while (true);

                        array.SetLength(index);
                    }

                    if (restElement.Argument is Identifier leftIdentifier)
                    {
                        AssignToIdentifier(engine, leftIdentifier.Name, array, environment, checkReference);
                    }
                    else if (restElement.Argument is DestructuringPattern bp)
                    {
                        ProcessPatterns(context, bp, array, environment);
                    }
                    else
                    {
                        AssignToReference(engine, reference!, array, environment);
                    }
                }
                else if (left is AssignmentPattern assignmentPattern)
                {
                    JsValue value;
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out value);
                    }
                    else
                    {
                        ConsumeFromIterator(iterator!, out value, out done);
                    }

                    if (value.IsUndefined())
                    {
                        var jintExpression = Build(assignmentPattern.Right);
                        value = jintExpression.GetValue(context);

                        // Check for generator suspension after evaluating default value
                        if (context.IsSuspended())
                        {
                            close = false; // Don't close iterator, we'll resume later
                            return JsValue.Undefined;
                        }

                        // Check for generator return request after evaluating default value
                        if (generator?._returnRequested == true)
                        {
                            if (!done && iterator is not null)
                            {
                                done = true;
                                iterator.Close(CompletionType.Return);
                            }
                            generator.SuspendData.Clear(pattern);
                            close = false; // Already closed
                            return JsValue.Undefined;
                        }
                    }

                    if (assignmentPattern.Left is Identifier leftIdentifier)
                    {
                        if (assignmentPattern.Right.IsFunctionDefinition())
                        {
                            ((Function) value).SetFunctionName(new JsString(leftIdentifier.Name));
                        }

                        AssignToIdentifier(engine, leftIdentifier.Name, value, environment, checkReference);
                    }
                    else if (assignmentPattern.Left is DestructuringPattern bp)
                    {
                        ProcessPatterns(context, bp, value, environment);
                    }
                }
                else
                {
                    Throw.ArgumentOutOfRangeException(nameof(pattern), $"Unable to determine how to handle array pattern element {left}");
                    break;
                }

                // Check for generator suspension after processing each element
                if (context.IsSuspended())
                {
                    // Generator yield - don't close the iterator, we'll resume later
                    close = false;
                    return JsValue.Undefined;
                }

                // Check for generator return request
                if (generator?._returnRequested == true)
                {
                    // Generator return() was called - close iterator with Return completion
                    if (!done && iterator is not null)
                    {
                        done = true; // Prevent double-close in finally
                        iterator.Close(CompletionType.Return);
                    }
                    generator.SuspendData.Clear(pattern);
                    close = false; // Prevent double-close in finally
                    return JsValue.Undefined;
                }
            }

            close = true;
            // Clear suspend data on normal completion
            generator?.SuspendData.Clear(pattern);
        }
        catch
        {
            completionType = CompletionType.Throw;
            // Clear suspend data on error
            generator?.SuspendData.Clear(pattern);
            throw;
        }
        finally
        {
            if (close && !done && iterator is not null)
            {
                iterator.Close(completionType);
            }
        }

        return JsValue.Undefined;
    }

    private static JsValue HandleObjectPattern(
        EvaluationContext context,
        ObjectPattern pattern,
        JsValue argument,
        Environment? environment,
        bool checkReference)
    {
        var processedProperties = pattern.Properties.Count > 0 && pattern.Properties[pattern.Properties.Count - 1] is RestElement
            ? new HashSet<JsValue>()
            : null;

        var source = TypeConverter.ToObject(context.Engine.Realm, argument);
        for (var i = 0; i < pattern.Properties.Count; i++)
        {
            if (pattern.Properties[i] is AssignmentProperty p)
            {
                JsValue sourceKey;
                var identifier = p.Key as Identifier;
                if (identifier == null || p.Computed)
                {
                    var keyExpression = Build(p.Key);
                    var value = keyExpression.GetValue(context);
                    if (context.IsAbrupt())
                    {
                        return value;
                    }
                    sourceKey = TypeConverter.ToPropertyKey(value);
                }
                else
                {
                    sourceKey = identifier.Name;
                }

                processedProperties?.Add(sourceKey);
                if (p.Value is AssignmentPattern assignmentPattern)
                {
                    var value = source.Get(sourceKey);
                    if (value.IsUndefined())
                    {
                        var jintExpression = Build(assignmentPattern.Right);
                        var completion = jintExpression.GetValue(context);
                        if (context.IsAbrupt())
                        {
                            return completion;
                        }
                        // Check for async/generator suspension after evaluating default value
                        if (context.IsSuspended())
                        {
                            return JsValue.Undefined;
                        }
                        value = completion;
                    }

                    if (assignmentPattern.Left is DestructuringPattern bp)
                    {
                        ProcessPatterns(context, bp, value, environment);
                        continue;
                    }

                    var target = assignmentPattern.Left as Identifier ?? identifier;

                    if (assignmentPattern.Right.IsFunctionDefinition())
                    {
                        ((Function) value).SetFunctionName(target!.Name);
                    }

                    AssignToIdentifier(context.Engine, target!.Name, value, environment, checkReference);
                }
                else if (p.Value is DestructuringPattern dp)
                {
                    var value = source.Get(sourceKey);
                    ProcessPatterns(context, dp, value, environment);
                }
                else if (p.Value is MemberExpression memberExpression)
                {
                    var reference = GetReferenceFromMember(context, memberExpression);
                    var value = source.Get(sourceKey);
                    AssignToReference(context.Engine, reference, value, environment);
                }
                else
                {
                    var identifierReference = p.Value as Identifier;
                    var target = identifierReference ?? identifier;
                    var value = source.Get(sourceKey);
                    AssignToIdentifier(context.Engine, target!.Name, value, environment, checkReference);
                }
            }
            else
            {
                var restElement = (RestElement) pattern.Properties[i];
                if (restElement.Argument is Identifier leftIdentifier)
                {
                    var count = Math.Max(0, source.Properties?.Count ?? 0) - processedProperties!.Count;
                    var rest = context.Engine.Realm.Intrinsics.Object.Construct(count);
                    source.CopyDataProperties(rest, processedProperties);
                    AssignToIdentifier(context.Engine, leftIdentifier.Name, rest, environment, checkReference);
                }
                else if (restElement.Argument is DestructuringPattern bp)
                {
                    ProcessPatterns(context, bp, argument, environment);
                }
                else if (restElement.Argument is MemberExpression memberExpression)
                {
                    var left = GetReferenceFromMember(context, memberExpression);
                    var rest = context.Engine.Realm.Intrinsics.Object.Construct(0);
                    source.CopyDataProperties(rest, processedProperties);
                    AssignToReference(context.Engine, left, rest, environment);
                }
                else
                {
                    Throw.ArgumentException("cannot handle parameter type " + restElement.Argument);
                }
            }
        }

        return JsValue.Undefined;
    }

    private static void AssignToReference(
        Engine engine,
        Reference lhs,
        JsValue v,
        Environment? environment)
    {
        if (environment is null)
        {
            engine.PutValue(lhs, v);
        }
        else
        {
            lhs.InitializeReferencedBinding(v, DisposeHint.Normal);
        }
        engine._referencePool.Return(lhs);
    }

    private static Reference GetReferenceFromMember(EvaluationContext context, MemberExpression memberExpression)
    {
        var expression = new JintMemberExpression(memberExpression);
        var reference = expression.Evaluate(context) as Reference;
        if (reference is null)
        {
            Throw.ReferenceError(context.Engine.Realm, "invalid reference");
        }
        reference.AssertValid(context.Engine.Realm);
        return reference;
    }

    private static void AssignToIdentifier(
        Engine engine,
        string name,
        JsValue rval,
        Environment? environment,
        bool checkReference = true)
    {
        var lhs = engine.ResolveBinding(name, environment);
        if (environment is not null)
        {
            lhs.InitializeReferencedBinding(rval, DisposeHint.Normal);
        }
        else
        {
            if (checkReference && lhs.IsUnresolvableReference && StrictModeScope.IsStrictModeCode)
            {
                Throw.ReferenceError(engine.Realm, lhs);
            }
            engine.PutValue(lhs, rval);
        }
    }
}
