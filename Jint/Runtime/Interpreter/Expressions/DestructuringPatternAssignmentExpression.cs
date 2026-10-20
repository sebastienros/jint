using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// A destructuring assignment, and the destructuring every other construct performs: binding patterns in
/// declarations, <c>for</c>-<c>in</c>/<c>of</c> heads and <c>catch</c> parameters go through
/// <see cref="ProcessPatterns"/> or <see cref="ProcessResumablePattern"/>.
/// </summary>
/// <remarks>
/// There are two lanes, and the caller picks one when it is built. <see cref="ProcessPatterns"/> is for a
/// pattern that cannot suspend the frame it runs in, which is nearly every pattern, and it carries no
/// suspension bookkeeping at all. <see cref="ProcessResumablePattern"/> is for a pattern with an <c>await</c>
/// or a <c>yield</c> of the enclosing frame somewhere inside it (<see cref="MaySuspendVisitor"/>), and it
/// resumes at the element or property that suspended; see
/// <c>DestructuringPatternAssignmentExpression.Resumable.cs</c>.
/// </remarks>
internal sealed partial class DestructuringPatternAssignmentExpression : JintExpression
{
    private readonly DestructuringPattern _pattern;
    private readonly JintExpression _right;

    /// <summary>
    /// Whether the pattern contains an <c>await</c> or a <c>yield</c> of the enclosing frame, in a default, a
    /// computed key, a member target or a nested pattern. Only such a pattern runs the resumable lane.
    /// </summary>
    private readonly bool _resumable;

    public DestructuringPatternAssignmentExpression(AssignmentExpression expression) : base(expression)
    {
        _pattern = (DestructuringPattern) expression.Left;
        _right = Build(expression.Right);
        _resumable = MaySuspendVisitor.MaySuspend(_pattern);
    }

    /// <summary>
    /// AssignmentExpression : LeftHandSideExpression = AssignmentExpression with an ObjectLiteral or
    /// ArrayLiteral target (https://tc39.es/ecma262/#sec-assignment-operators-runtime-semantics-evaluation):
    /// GetValue of the right-hand side first, DestructuringAssignmentEvaluation of the pattern over it
    /// second, and the right-hand side's value is the expression's own.
    /// </summary>
    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (_resumable)
        {
            return EvaluateResumable(context);
        }

        var rightValue = _right.GetValue(context);

        // `({ a } = await p)`: the right-hand side suspended, and what came back is the suspension
        // placeholder - a plain undefined for an await, the yielded operand for a yield - not the value to
        // destructure. Hand it back untouched, as every other consumer of the placeholder does
        // (sebastienros/jint#4086), so the enclosing statement completes normally inside the suspended
        // frame and the statement list records its resume position; the resume re-runs this expression,
        // the await returns its memoized value, and the pattern runs once, over the real one. Destructuring
        // the placeholder opened with ToObject, whose TypeError AsyncBlockStart swallowed but which unwound
        // past the position bookkeeping, so the body replayed from statement 0 - and in a generator it read
        // the yielded operand's properties and wrote the targets before next() had been given the value
        // they were due.
        if (context.IsSuspended())
        {
            return rightValue;
        }

        ProcessPatterns(context, _pattern, rightValue, null);
        return rightValue;
    }

    /// <summary>
    /// Destructures <paramref name="argument"/> with a pattern that cannot suspend: there is no <c>await</c> or
    /// <c>yield</c> of the enclosing frame anywhere inside it, so nothing here tests for a suspension or parks
    /// anything for a resume. A pattern that may suspend goes through <see cref="ProcessResumablePattern"/>.
    /// </summary>
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

    /// <summary>
    /// IteratorStepValue (https://tc39.es/ecma262/#sec-iteratorstepvalue) under the
    /// <c>If iteratorRecord.[[Done]] is false</c> guard that every element form of
    /// IteratorDestructuringAssignmentEvaluation (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation)
    /// and IteratorBindingInitialization (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization)
    /// is wrapped in: an iterator that has already reported done is never stepped again, and the
    /// element takes <c>undefined</c> instead. Every abrupt completion — the <c>next</c> call, the
    /// <c>done</c> read behind <see cref="IteratorInstance.TryIteratorStep"/>, or the <c>value</c>
    /// read — sets <paramref name="done"/>, so the caller's <c>finally</c> does not close an
    /// iterator the spec already considers done.
    /// </summary>
    private static JsValue StepValue(IteratorInstance it, ref bool done)
    {
        if (done)
        {
            return JsValue.Undefined;
        }

        try
        {
            if (!it.TryIteratorStep(out var result))
            {
                done = true;
                return JsValue.Undefined;
            }

            return result.Get(CommonProperties.Value);
        }
        catch
        {
            done = true;
            throw;
        }
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

        var obj = TypeConverter.ToObject(realm, argument);
        ArrayOperations? arrayOperations = null;
        IteratorInstance? iterator = null;

        // optimize for array unless someone has touched the iterator
        if (obj.IsArrayLike && obj.HasOriginalIterator)
        {
            arrayOperations = ArrayOperations.For(obj, forWrite: false);
        }
        else
        {
            // ArrayBindingPattern's step is GetIterator(value, sync) over the *value*, not over
            // the object ToObject built from it (https://tc39.es/ecma262/#sec-runtime-semantics-bindinginitialization).
            // The conversion above exists for the array fast path and for the null/undefined throw.
            iterator = argument.GetIterator(realm);
        }

        // The pattern cannot suspend, so every way out of the loop below ends it: a normal completion closes the
        // iterator with a normal completion and a throw with a throw completion, unless it is already done
        // (DestructuringAssignmentEvaluation / BindingInitialization: "If iteratorRecord.[[Done]] is false, return
        // ? IteratorClose(iteratorRecord, result)").
        var completionType = CompletionType.Normal;
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
                        // An elision consumes a step and discards it; it never ends the pattern, so
                        // the elements after it still bind (undefined, or their initializer).
                        StepValue(iterator!, ref done);
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
                        value = StepValue(iterator!, ref done);
                    }

                    AssignToIdentifier(engine, identifier.Name, value, environment, checkReference);
                }
                else if (left is MemberExpression me)
                {
                    var reference = GetReferenceFromMember(context, me);

                    JsValue value;
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out value);
                    }
                    else
                    {
                        value = StepValue(iterator!, ref done);
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
                        // BindingElement : BindingPattern binds the nested pattern to the value the
                        // step produced, never to the { value, done } result object the step returned.
                        value = StepValue(iterator!, ref done);
                    }
                    ProcessPatterns(context, dp, value, environment);
                }
                else if (left is RestElement restElement)
                {
                    Reference? reference = null;
                    if (restElement.Argument is MemberExpression memberExpression)
                    {
                        reference = GetReferenceFromMember(context, memberExpression);
                    }

                    JsArray array;
                    if (arrayOperations != null)
                    {
                        // This lane is gated on HasOriginalIterator, so the receiver is a JsArray, an
                        // ArrayLikeObject or an array-like ObjectWrapper, and all three count elements in
                        // the array index range. Clamp rather than cast all the same: a wrapper's "length"
                        // is whatever its CLR Count/Length property returns, and nothing constrains that
                        // property's type.
                        var length = (uint) System.Math.Min(arrayOperations.GetLongLength(), ArrayOperations.MaxArrayLength);

                        // A pattern with more leading elements than the source has already drove the
                        // notional iterator to done, so the "Repeat, while iteratorRecord.[[Done]] is
                        // false" of BindingRestElement
                        // (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization)
                        // and AssignmentRestElement
                        // (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation)
                        // adds nothing, and the rest array is empty.
                        // The subtraction is unsigned, so that case has to be clamped rather than left
                        // to wrap: with i past length, an unclamped length - i produced a rest array
                        // claiming close to 2^32 elements, which .length reported and which
                        // JSON.stringify, a spread and concat all went on to consume. The clamp costs
                        // the comparison the loop below already makes on its first iteration; the
                        // subtraction inside that loop needs none, since j starts at i.
                        var restLength = i < length ? length - i : 0;

                        array = engine.Realm.Intrinsics.Array.ArrayCreate(restLength);
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
                        // AssignmentRestElement / BindingRestElement: "Repeat, while
                        // iteratorRecord.[[Done]] is false". An iterator a preceding element already
                        // drove to done contributes nothing and is not stepped at all.
                        while (!done)
                        {
                            JsValue value;
                            try
                            {
                                if (!iterator!.TryIteratorStep(out var item))
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
                    // Per ECMAScript spec (IteratorDestructuringAssignmentEvaluation):
                    // 1. If DestructuringAssignmentTarget is neither ObjectLiteral nor ArrayLiteral, evaluate lref first
                    // 2. Then get the value from iterator
                    // 3. Then apply initializer if value is undefined
                    // 4. Then PutValue(lref, v)

                    Reference? memberReference = null;
                    if (assignmentPattern.Left is MemberExpression memberExpr)
                    {
                        memberReference = GetReferenceFromMember(context, memberExpr);
                    }

                    JsValue value;
                    if (arrayOperations != null)
                    {
                        arrayOperations.TryGetValue(i, out value);
                    }
                    else
                    {
                        value = StepValue(iterator!, ref done);
                    }

                    // Only an initializer the engine actually ran can be the subject of
                    // NamedEvaluation below; a supplied value is bound exactly as it came in.
                    var initializerEvaluated = value.IsUndefined();
                    if (initializerEvaluated)
                    {
                        var jintExpression = Build(assignmentPattern.Right);
                        value = jintExpression.GetValue(context);
                    }

                    if (memberReference is not null)
                    {
                        AssignToReference(engine, memberReference, value, environment);
                    }
                    else if (assignmentPattern.Left is Identifier leftIdentifier)
                    {
                        // IteratorDestructuringAssignmentEvaluation / IteratorBindingInitialization
                        // name the initializer's own closure, so both halves of the spec's condition
                        // have to hold: the initializer was evaluated, and it is an anonymous
                        // function definition.
                        if (initializerEvaluated && assignmentPattern.Right.IsAnonymousFunctionDefinition())
                        {
                            ((Function) value).SetFunctionName(leftIdentifier.Name);
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
            }
        }
        catch
        {
            completionType = CompletionType.Throw;
            throw;
        }
        finally
        {
            if (!done && iterator is not null)
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
                    // Same per-node cache the object-literal and class-body key positions use: this
                    // method is static and has no handler of its own to hang the key expression on. The
                    // resumable lane depends on the handler's identity as well; see HandleResumableObjectPattern.
                    var keyExpression = context.Engine.GetOrBuildPropertyKeyExpression(p.Key);
                    var value = keyExpression.GetValue(context);
                    sourceKey = TypeConverter.ToPropertyKey(value);
                }
                else
                {
                    sourceKey = identifier.Name;
                }

                processedProperties?.Add(sourceKey);
                if (p.Value is AssignmentPattern assignmentPattern)
                {
                    // Per ECMAScript spec (KeyedDestructuringAssignmentEvaluation / KeyedBindingInitialization):
                    // If target is MemberExpression, evaluate lref first, then get value
                    // For binding patterns (environment != null), ResolveBinding before GetV (spec 14.3.3.3 step 2-3)
                    Reference? memberReference = null;
                    Reference? bindingRef = null;
                    if (assignmentPattern.Left is MemberExpression memberExpr)
                    {
                        memberReference = GetReferenceFromMember(context, memberExpr);
                    }
                    else if (assignmentPattern.Left is Identifier leftId)
                    {
                        var target = leftId ?? identifier;
                        bindingRef = context.Engine.ResolveBinding(target!.Name, environment);
                    }

                    var value = source.Get(sourceKey);

                    // Only an initializer the engine actually ran can be the subject of
                    // NamedEvaluation below; a supplied value is bound exactly as it came in.
                    var initializerEvaluated = value.IsUndefined();
                    if (initializerEvaluated)
                    {
                        var jintExpression = Build(assignmentPattern.Right);
                        value = jintExpression.GetValue(context);
                    }

                    if (memberReference is not null)
                    {
                        AssignToReference(context.Engine, memberReference, value, environment);
                    }
                    else if (assignmentPattern.Left is DestructuringPattern bp)
                    {
                        ProcessPatterns(context, bp, value, environment);
                    }
                    else
                    {
                        var target = assignmentPattern.Left as Identifier ?? identifier;

                        // KeyedDestructuringAssignmentEvaluation / KeyedBindingInitialization name
                        // the initializer's own closure, so both halves of the spec's condition have
                        // to hold: the initializer was evaluated, and it is an anonymous function
                        // definition.
                        if (initializerEvaluated && assignmentPattern.Right.IsAnonymousFunctionDefinition())
                        {
                            ((Function) value).SetFunctionName(target!.Name);
                        }

                        if (bindingRef is not null)
                        {
                            if (environment is not null)
                            {
                                bindingRef.InitializeReferencedBinding(value, DisposeHint.Normal);
                            }
                            else
                            {
                                if (checkReference && bindingRef.IsUnresolvableReference && context.Engine.ExecutionContext.Strict)
                                {
                                    Throw.ReferenceError(context.Engine.Realm, bindingRef);
                                }
                                context.Engine.PutValue(bindingRef, value);
                            }
                        }
                        else
                        {
                            AssignToIdentifier(context.Engine, target!.Name, value, environment, checkReference);
                        }
                    }
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

                    // Per spec 14.3.3.3 step 2-3: ResolveBinding before GetV
                    var lhs = context.Engine.ResolveBinding(target!.Name, environment);
                    var value = source.Get(sourceKey);

                    if (environment is not null)
                    {
                        lhs.InitializeReferencedBinding(value, DisposeHint.Normal);
                    }
                    else
                    {
                        if (checkReference && lhs.IsUnresolvableReference && context.Engine.ExecutionContext.Strict)
                        {
                            Throw.ReferenceError(context.Engine.Realm, lhs);
                        }
                        context.Engine.PutValue(lhs, value);
                    }
                }
            }
            else
            {
                var restElement = (RestElement) pattern.Properties[i];
                if (restElement.Argument is Identifier leftIdentifier)
                {
                    // Shape-building target: CopyDataProperties adds flow through CreateDataProperty,
                    // interning the rest object's layout (or deopting on integer-like keys).
                    var rest = context.Engine.Realm.Intrinsics.Object.ConstructShapeBuilding();
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
                    var rest = context.Engine.Realm.Intrinsics.Object.ConstructShapeBuilding();
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
            if (checkReference && lhs.IsUnresolvableReference && engine.ExecutionContext.Strict)
            {
                Throw.ReferenceError(engine.Realm, lhs);
            }
            engine.PutValue(lhs, rval);
        }
    }
}
