using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// The resumable lane: destructuring with a pattern that contains an <c>await</c> or a <c>yield</c> of the
/// enclosing frame.
/// </summary>
/// <remarks>
/// <para>
/// Jint resumes a suspended generator or async function by replaying the statement it suspended in. The awaits
/// and yields that already completed hand back their memoized values, so whatever is not one of them runs again
/// unless its handler parks its own position. A pattern that replayed from its first element stepped its iterator
/// again for every element before the suspended one and bound the rest to the wrong values, read every property
/// before the suspended one a second time, and let the construct around it evaluate the right-hand side again; a
/// <c>throw()</c> at a default could miss its <c>yield</c> altogether, because the re-stepped value no longer
/// needed the default (sebastienros/jint#4313).
/// </para>
/// <para>
/// The spec has each element step the iterator once and each property read once, left to right:
/// DestructuringAssignmentEvaluation (https://tc39.es/ecma262/#sec-runtime-semantics-destructuringassignmentevaluation),
/// IteratorDestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation),
/// KeyedDestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-keyeddestructuringassignmentevaluation) and, for binding
/// patterns, BindingInitialization (https://tc39.es/ecma262/#sec-runtime-semantics-bindinginitialization),
/// IteratorBindingInitialization (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization)
/// and KeyedBindingInitialization (https://tc39.es/ecma262/#sec-runtime-semantics-keyedbindinginitialization).
/// So when the pattern suspends it parks where it is (<see cref="DestructuringSuspendData"/>): the element or
/// property, what that element had already produced — its step, its read, its resolved target, its key — and the
/// iterator or the source object. The resume continues there.
/// </para>
/// <para>
/// Three things make that work. The pattern's state is keyed by its AST node on the suspendable, never in a field:
/// handler trees are shared. A default, a computed key and a member target are evaluated through the engine's
/// per-node handler cache (<see cref="Engine.GetOrBuildPropertyKeyExpression"/>) rather than a handler built for
/// the occasion, because the replay state of whatever suspended inside them is keyed by that handler. And an array
/// pattern always runs the iterator protocol here, with no array fast path, since a lane picked from the array's
/// state at the start would not be the right one to resume in after the suspension.
/// </para>
/// <para>
/// None of this is on the path of a pattern that cannot suspend, which is decided when the construct around it is
/// built, with <see cref="MaySuspendVisitor"/>.
/// </para>
/// </remarks>
internal sealed partial class DestructuringPatternAssignmentExpression
{
    private JsValue EvaluateResumable(EvaluationContext context)
    {
        // A resume inside the pattern finds the right-hand side's value parked with it: GetValue already ran
        // before the pattern started, and running it again would repeat its side effects and destructure a
        // second value.
        if (!TryGetResumedArgument(context, _pattern, out var rightValue))
        {
            rightValue = _right.GetValue(context);

            // The right-hand side suspended; see EvaluateInternal.
            if (context.IsSuspended())
            {
                return rightValue;
            }
        }

        ProcessResumablePattern(context, _pattern, rightValue, null);
        return rightValue;
    }

    /// <summary>
    /// The value <paramref name="pattern"/> was destructuring when its frame suspended inside it, if this is the
    /// resume of that frame. The construct that evaluated the value takes it from here instead of evaluating it
    /// again, and then hands it to <see cref="ProcessResumablePattern"/>.
    /// </summary>
    internal static bool TryGetResumedArgument(
        EvaluationContext context,
        DestructuringPattern pattern,
        [NotNullWhen(true)] out JsValue? argument)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        if (suspendable is { IsResuming: true } && suspendable.Data.TryGet(pattern, out DestructuringSuspendData? data))
        {
            argument = data!.Argument;
            return true;
        }

        argument = null;
        return false;
    }

    /// <summary>
    /// Destructures <paramref name="argument"/> with a pattern that may suspend, resuming at the element or property
    /// that suspended when this is the resume of its frame. Every nested pattern takes this lane too.
    /// </summary>
    internal static JsValue ProcessResumablePattern(
        EvaluationContext context,
        DestructuringPattern pattern,
        JsValue argument,
        Environment? environment,
        bool checkPatternPropertyReference = true)
    {
        if (pattern is ArrayPattern ap)
        {
            return HandleResumableArrayPattern(context, ap, argument, environment, checkPatternPropertyReference);
        }

        if (pattern is ObjectPattern op)
        {
            return HandleResumableObjectPattern(context, op, argument, environment, checkPatternPropertyReference);
        }

        Throw.ArgumentException("Not a pattern");
        return default;
    }

    private static JsValue HandleResumableArrayPattern(
        EvaluationContext context,
        ArrayPattern pattern,
        JsValue argument,
        Environment? environment,
        bool checkReference)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        IteratorInstance iterator;
        var done = false;
        var i = 0;
        JsValue? pendingValue = null;
        var pendingValueIsFinal = false;
        Reference? pendingTarget = null;
        var resumed = false;

        if (suspendable is { IsResuming: true } && suspendable.Data.TryGet(pattern, out DestructuringSuspendData? data))
        {
            resumed = true;
            iterator = data!.Iterator!;
            done = data.Done;
            i = data.Index;
            pendingValue = data.PendingValue;
            pendingValueIsFinal = data.PendingValueIsFinal;
            pendingTarget = data.PendingTarget;
        }
        else
        {
            // The conversion is only for the TypeError a null or undefined value raises, as in the fast lane;
            // GetIterator is over the value itself.
            TypeConverter.ToObject(engine.Realm, argument);
            iterator = argument.GetIterator(engine.Realm);
        }

        // "If iteratorRecord.[[Done]] is false, return ? IteratorClose(iteratorRecord, result)": a normal completion
        // and an abrupt one both close the iterator, a suspension does not, since the pattern is not over.
        var completionType = CompletionType.Normal;
        var close = true;
        try
        {
            ref readonly var elements = ref pattern.Elements;
            for (; i < elements.Count; i++)
            {
                var left = elements[i];

                if (left is null)
                {
                    // An elision consumes a step and discards it.
                    StepValue(iterator, ref done);
                }
                else if (left is Identifier identifier)
                {
                    var value = StepValue(iterator, ref done);
                    AssignToIdentifier(engine, identifier.Name, value, environment, checkReference);
                }
                else if (left is MemberExpression memberTarget)
                {
                    // The target is evaluated before the step, so a suspension inside it has consumed nothing.
                    var reference = EvaluateTarget(context, memberTarget);
                    if (reference is null)
                    {
                        close = false;
                        return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, value: null, valueIsFinal: false, target: null);
                    }

                    var value = StepValue(iterator, ref done);
                    AssignToReference(engine, reference, value, environment);
                }
                else if (left is DestructuringPattern nestedPattern)
                {
                    // BindingElement : BindingPattern binds the nested pattern to the value the step produced; a resume
                    // inside the nested pattern hands it that value again, and the nested pattern resumes itself.
                    var value = pendingValue ?? StepValue(iterator, ref done);
                    ProcessResumablePattern(context, nestedPattern, value, environment);
                    if (context.IsSuspended())
                    {
                        close = false;
                        return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, value, valueIsFinal: true, target: null);
                    }
                }
                else if (left is RestElement restElement)
                {
                    Reference? reference = null;
                    if (restElement.Argument is MemberExpression restTarget)
                    {
                        reference = EvaluateTarget(context, restTarget);
                        if (reference is null)
                        {
                            close = false;
                            return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, value: null, valueIsFinal: false, target: null);
                        }
                    }

                    // A resume inside a nested rest pattern has already drained the iterator into this array.
                    var array = pendingValue ?? CollectRest(engine, iterator, ref done);

                    if (restElement.Argument is Identifier restIdentifier)
                    {
                        AssignToIdentifier(engine, restIdentifier.Name, array, environment, checkReference);
                    }
                    else if (restElement.Argument is DestructuringPattern restPattern)
                    {
                        ProcessResumablePattern(context, restPattern, array, environment);
                        if (context.IsSuspended())
                        {
                            close = false;
                            return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, array, valueIsFinal: true, target: null);
                        }
                    }
                    else
                    {
                        AssignToReference(engine, reference!, array, environment);
                    }
                }
                else if (left is AssignmentPattern assignmentPattern)
                {
                    // IteratorDestructuringAssignmentEvaluation: a target that is not a pattern is evaluated first,
                    // then the iterator is stepped, then the initializer runs if the step produced undefined.
                    var memberReference = pendingTarget;
                    if (memberReference is null && assignmentPattern.Left is MemberExpression defaultTarget)
                    {
                        memberReference = EvaluateTarget(context, defaultTarget);
                        if (memberReference is null)
                        {
                            close = false;
                            return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, value: null, valueIsFinal: false, target: null);
                        }
                    }

                    JsValue value;
                    var initializerEvaluated = false;
                    if (pendingValueIsFinal)
                    {
                        // The initializer had already run; the nested pattern on the left is what suspended.
                        value = pendingValue!;
                    }
                    else
                    {
                        value = pendingValue ?? StepValue(iterator, ref done);
                        if (value.IsUndefined())
                        {
                            initializerEvaluated = true;
                            value = engine.GetOrBuildPropertyKeyExpression(assignmentPattern.Right).GetValue(context);
                            if (context.IsSuspended())
                            {
                                close = false;
                                return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, JsValue.Undefined, valueIsFinal: false, memberReference);
                            }
                        }
                    }

                    if (memberReference is not null)
                    {
                        AssignToReference(engine, memberReference, value, environment);
                    }
                    else if (assignmentPattern.Left is Identifier leftIdentifier)
                    {
                        if (initializerEvaluated && assignmentPattern.Right.IsAnonymousFunctionDefinition())
                        {
                            ((Function) value).SetFunctionName(leftIdentifier.Name);
                        }

                        AssignToIdentifier(engine, leftIdentifier.Name, value, environment, checkReference);
                    }
                    else if (assignmentPattern.Left is DestructuringPattern defaultPattern)
                    {
                        ProcessResumablePattern(context, defaultPattern, value, environment);
                        if (context.IsSuspended())
                        {
                            close = false;
                            return Suspended(suspendable!, pattern, argument, iterator, done, source: null, processedProperties: null, i, key: null, value, valueIsFinal: true, target: null);
                        }
                    }
                }
                else
                {
                    Throw.ArgumentOutOfRangeException(nameof(pattern), $"Unable to determine how to handle array pattern element {left}");
                }

                // Only the element the resume started at had anything parked for it.
                pendingValue = null;
                pendingValueIsFinal = false;
                pendingTarget = null;
            }

            // Inside the try, so that the entry is gone even if closing the iterator throws.
            if (resumed)
            {
                suspendable!.Data.Clear(pattern);
            }
        }
        catch (Exception exception) when (LeavingOnException(suspendable, pattern, resumed, exception, out completionType))
        {
            // Unreachable: the filter always declines.
            throw;
        }
        finally
        {
            if (close && !done)
            {
                iterator.Close(completionType);
            }
        }

        return JsValue.Undefined;
    }

    private static JsValue HandleResumableObjectPattern(
        EvaluationContext context,
        ObjectPattern pattern,
        JsValue argument,
        Environment? environment,
        bool checkReference)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        ref readonly var properties = ref pattern.Properties;

        ObjectInstance source;
        HashSet<JsValue>? processedProperties;
        var i = 0;
        JsValue? pendingKey = null;
        JsValue? pendingValue = null;
        var pendingValueIsFinal = false;
        Reference? pendingTarget = null;
        var resumed = false;

        if (suspendable is { IsResuming: true } && suspendable.Data.TryGet(pattern, out DestructuringSuspendData? data))
        {
            resumed = true;
            source = data!.Source!;
            processedProperties = data.ProcessedProperties;
            i = data.Index;
            pendingKey = data.PendingKey;
            pendingValue = data.PendingValue;
            pendingValueIsFinal = data.PendingValueIsFinal;
            pendingTarget = data.PendingTarget;
        }
        else
        {
            processedProperties = properties.Count > 0 && properties[properties.Count - 1] is RestElement
                ? new HashSet<JsValue>()
                : null;
            source = TypeConverter.ToObject(engine.Realm, argument);
        }

        try
        {
            for (; i < properties.Count; i++)
            {
                if (properties[i] is AssignmentProperty p)
                {
                    var identifier = p.Key as Identifier;
                    JsValue sourceKey;
                    if (pendingKey is not null)
                    {
                        sourceKey = pendingKey;
                    }
                    else if (identifier is null || p.Computed)
                    {
                        var keyValue = engine.GetOrBuildPropertyKeyExpression(p.Key).GetValue(context);
                        if (context.IsSuspended())
                        {
                            return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, key: null, value: null, valueIsFinal: false, target: null);
                        }

                        sourceKey = TypeConverter.ToPropertyKey(keyValue);
                    }
                    else
                    {
                        sourceKey = identifier.Name;
                    }

                    processedProperties?.Add(sourceKey);

                    if (p.Value is AssignmentPattern assignmentPattern)
                    {
                        // KeyedDestructuringAssignmentEvaluation / KeyedBindingInitialization: the target is
                        // resolved before the property is read, and the initializer runs after the read.
                        Reference? memberReference = null;
                        Reference? bindingRef = null;
                        if (assignmentPattern.Left is MemberExpression memberTarget)
                        {
                            memberReference = pendingTarget ?? EvaluateTarget(context, memberTarget);
                            if (memberReference is null)
                            {
                                return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, sourceKey, value: null, valueIsFinal: false, target: null);
                            }
                        }
                        else if (assignmentPattern.Left is Identifier leftId)
                        {
                            bindingRef = pendingTarget ?? engine.ResolveBinding(leftId.Name, environment);
                        }

                        JsValue value;
                        var initializerEvaluated = false;
                        if (pendingValueIsFinal)
                        {
                            // The initializer had already run; the nested pattern on the left is what suspended.
                            value = pendingValue!;
                        }
                        else
                        {
                            value = pendingValue ?? source.Get(sourceKey);
                            if (value.IsUndefined())
                            {
                                initializerEvaluated = true;
                                value = engine.GetOrBuildPropertyKeyExpression(assignmentPattern.Right).GetValue(context);
                                if (context.IsSuspended())
                                {
                                    return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, sourceKey, JsValue.Undefined, valueIsFinal: false, memberReference ?? bindingRef);
                                }
                            }
                        }

                        if (memberReference is not null)
                        {
                            AssignToReference(engine, memberReference, value, environment);
                        }
                        else if (assignmentPattern.Left is DestructuringPattern defaultPattern)
                        {
                            ProcessResumablePattern(context, defaultPattern, value, environment);
                            if (context.IsSuspended())
                            {
                                return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, sourceKey, value, valueIsFinal: true, target: null);
                            }
                        }
                        else
                        {
                            var target = assignmentPattern.Left as Identifier ?? identifier;
                            if (initializerEvaluated && assignmentPattern.Right.IsAnonymousFunctionDefinition())
                            {
                                ((Function) value).SetFunctionName(target!.Name);
                            }

                            if (bindingRef is not null)
                            {
                                InitializeOrPut(engine, bindingRef, value, environment, checkReference);
                            }
                            else
                            {
                                AssignToIdentifier(engine, target!.Name, value, environment, checkReference);
                            }
                        }
                    }
                    else if (p.Value is DestructuringPattern nestedPattern)
                    {
                        var value = pendingValue ?? source.Get(sourceKey);
                        ProcessResumablePattern(context, nestedPattern, value, environment);
                        if (context.IsSuspended())
                        {
                            return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, sourceKey, value, valueIsFinal: true, target: null);
                        }
                    }
                    else if (p.Value is MemberExpression memberExpression)
                    {
                        var reference = EvaluateTarget(context, memberExpression);
                        if (reference is null)
                        {
                            return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, sourceKey, value: null, valueIsFinal: false, target: null);
                        }

                        var value = source.Get(sourceKey);
                        AssignToReference(engine, reference, value, environment);
                    }
                    else
                    {
                        var target = p.Value as Identifier ?? identifier;
                        var lhs = engine.ResolveBinding(target!.Name, environment);
                        var value = source.Get(sourceKey);
                        InitializeOrPut(engine, lhs, value, environment, checkReference);
                    }
                }
                else
                {
                    var restElement = (RestElement) properties[i];
                    if (restElement.Argument is Identifier leftIdentifier)
                    {
                        var rest = engine.Realm.Intrinsics.Object.ConstructShapeBuilding();
                        source.CopyDataProperties(rest, processedProperties);
                        AssignToIdentifier(engine, leftIdentifier.Name, rest, environment, checkReference);
                    }
                    else if (restElement.Argument is DestructuringPattern restPattern)
                    {
                        ProcessResumablePattern(context, restPattern, argument, environment);
                        if (context.IsSuspended())
                        {
                            return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, key: null, value: null, valueIsFinal: false, target: null);
                        }
                    }
                    else if (restElement.Argument is MemberExpression memberExpression)
                    {
                        var reference = EvaluateTarget(context, memberExpression);
                        if (reference is null)
                        {
                            return Suspended(suspendable!, pattern, argument, iterator: null, done: false, source, processedProperties, i, key: null, value: null, valueIsFinal: false, target: null);
                        }

                        var rest = engine.Realm.Intrinsics.Object.ConstructShapeBuilding();
                        source.CopyDataProperties(rest, processedProperties);
                        AssignToReference(engine, reference, rest, environment);
                    }
                    else
                    {
                        Throw.ArgumentException("cannot handle parameter type " + restElement.Argument);
                    }
                }

                // Only the property the resume started at had anything parked for it.
                pendingKey = null;
                pendingValue = null;
                pendingValueIsFinal = false;
                pendingTarget = null;
            }

            if (resumed)
            {
                suspendable!.Data.Clear(pattern);
            }
        }
        catch (Exception exception) when (LeavingOnException(suspendable, pattern, resumed, exception, out _))
        {
            // Unreachable: the filter always declines.
            throw;
        }

        return JsValue.Undefined;
    }

    /// <summary>
    /// Evaluates a member-expression target through the engine's per-node handler, so that a suspension inside it
    /// (<c>[o[await k]] = it</c>) finds its own replay state again. Null when the target suspended: what came back
    /// is the suspension's sentinel, never a reference to write through or to park.
    /// </summary>
    private static Reference? EvaluateTarget(EvaluationContext context, MemberExpression target)
    {
        var engine = context.Engine;
        var result = engine.GetOrBuildPropertyKeyExpression(target).Evaluate(context);
        if (context.IsSuspended())
        {
            engine._referencePool.Return(result as Reference);
            return null;
        }

        if (result is not Reference reference)
        {
            Throw.ReferenceError(engine.Realm, "invalid reference");
            return null;
        }

        reference.AssertValid(engine.Realm);
        return reference;
    }

    /// <summary>
    /// AssignmentRestElement / BindingRestElement: "Repeat, while iteratorRecord.[[Done]] is false". An iterator a
    /// preceding element already drove to done contributes nothing and is not stepped at all.
    /// </summary>
    private static JsArray CollectRest(Engine engine, IteratorInstance iterator, ref bool done)
    {
        var array = engine.Realm.Intrinsics.Array.ArrayCreate(0);
        uint index = 0;
        while (!done)
        {
            JsValue value;
            try
            {
                if (!iterator.TryIteratorStep(out var item))
                {
                    done = true;
                    break;
                }

                value = item.Get(CommonProperties.Value);
            }
            catch
            {
                done = true;
                throw;
            }

            array.SetIndexValue(index++, value, updateLength: false);
        }

        array.SetLength(index);
        return array;
    }

    /// <summary>
    /// Writes a resolved binding target: initializes it for a binding pattern, puts through it for an assignment.
    /// </summary>
    private static void InitializeOrPut(Engine engine, Reference lhs, JsValue value, Environment? environment, bool checkReference)
    {
        if (environment is not null)
        {
            lhs.InitializeReferencedBinding(value, DisposeHint.Normal);
            return;
        }

        if (checkReference && lhs.IsUnresolvableReference && engine.ExecutionContext.Strict)
        {
            Throw.ReferenceError(engine.Realm, lhs);
        }

        engine.PutValue(lhs, value);
    }

    /// <summary>
    /// Parks where the pattern was when its frame suspended, and completes with the placeholder every suspended
    /// consumer hands back. Every value parked here is a finished one: a target or a key that suspended is parked
    /// as absent and evaluated again on the resume.
    /// </summary>
    private static JsValue Suspended(
        ISuspendable suspendable,
        DestructuringPattern pattern,
        JsValue argument,
        IteratorInstance? iterator,
        bool done,
        ObjectInstance? source,
        HashSet<JsValue>? processedProperties,
        int index,
        JsValue? key,
        JsValue? value,
        bool valueIsFinal,
        Reference? target)
    {
        var data = suspendable.Data.GetOrCreate<DestructuringSuspendData>(pattern);
        data.Argument = argument;
        data.Iterator = iterator;
        data.Done = done;
        data.Source = source;
        data.ProcessedProperties = processedProperties;
        data.Index = index;
        data.PendingKey = key;
        data.PendingValue = value;
        data.PendingValueIsFinal = valueIsFinal;
        data.PendingTarget = target;
        return JsValue.Undefined;
    }

    /// <summary>
    /// Exception filter for the resumable lane. An exception that leaves the pattern ends it, so the entry a resume
    /// read is removed; and the iterator is closed with a return completion when the exception is the
    /// <see cref="GeneratorReturnException"/> a <c>yield</c> inside the pattern raised for a <c>return()</c>, with a
    /// throw completion otherwise (https://tc39.es/ecma262/#sec-iteratorclose: a failing <c>return()</c> replaces
    /// a return completion and is swallowed by a throw one). Declining, as
    /// <c>JintForInForOfStatement.LeavingOnException</c> does, keeps the frame out of the second pass.
    /// </summary>
    private static bool LeavingOnException(
        ISuspendable? suspendable,
        DestructuringPattern pattern,
        bool resumed,
        Exception exception,
        out CompletionType completionType)
    {
        completionType = exception is GeneratorReturnException ? CompletionType.Return : CompletionType.Throw;
        if (resumed)
        {
            suspendable!.Data.Clear(pattern);
        }

        return false;
    }
}
