using System;
using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class BindingPatternAssignmentExpression : JintExpression
    {
        private readonly BindingPattern _pattern;
        private readonly JintExpression _right;

        public BindingPatternAssignmentExpression(
            Engine engine, 
            AssignmentExpression expression) : base(engine, expression)
        {
            _pattern = (BindingPattern) expression.Left;
            _right = Build(engine, expression.Right);
        }

        protected override object EvaluateInternal()
        {
            var rightValue = _right.GetValue();
            ProcessPatterns(_engine, _pattern, rightValue);
            return rightValue;
        }

        internal static void ProcessPatterns(Engine engine, BindingPattern pattern, JsValue argument)
        {
            if (pattern is ArrayPattern ap)
            {
                HandleArrayPattern(engine, ap, argument);
            }
            else if (pattern is ObjectPattern op)
            {
                HandleObjectPattern(engine, op, argument);
            }
        }
        
        private static bool ConsumeFromIterator(IIterator it, out JsValue value, out bool done)
        {
            var item = it.Next();
            value = JsValue.Undefined;
            done = false;

            if (item.TryGetValue(CommonProperties.Done, out var d) && d.AsBoolean())
            {
                done = true;
                return false;
            }

            if (!item.TryGetValue(CommonProperties.Value, out value))
            {
                return false;
            }

            return true;
        }
        
        private static void HandleArrayPattern(Engine engine, ArrayPattern pattern, JsValue argument)
        {
            var obj = TypeConverter.ToObject(engine, argument);
            ArrayOperations arrayOperations = null;
            IIterator iterator = null;
            if (obj.IsArrayLike)
            {
                arrayOperations = ArrayOperations.For(obj);
            }
            else
            {
                if (!obj.TryGetIterator(engine, out iterator))
                {
                    ExceptionHelper.ThrowTypeError(engine);
                    return;
                }
            }

            var done = true;
            try
            {
                for (uint i = 0; i < pattern.Elements.Count; i++)
                {
                    var left = pattern.Elements[(int) i];

                    if (left is null)
                    {
                        // skip
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
                            done = false;
                            if (!ConsumeFromIterator(iterator, out value, out done))
                            {
                                break;
                            }
                        }
                        AssignToIdentifier(engine, identifier.Name, value);
                    }
                    else if (left is MemberExpression me)
                    {
                        var reference = GetReferenceFromMember(engine, me);
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            done = false;
                            if (!ConsumeFromIterator(iterator, out value, out done))
                            {
                                break;
                            }
                        }

                        AssignToReference(engine, reference, value);
                    }
                    else if (left is BindingPattern bindingPattern)
                    {
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            done = false;
                            value = iterator.Next();
                        }
                        ProcessPatterns(engine, bindingPattern, value);
                    }
                    else if (left is RestElement restElement)
                    {
                        Reference reference = null; 
                        if (restElement.Argument is MemberExpression memberExpression)
                        {
                            reference = GetReferenceFromMember(engine, memberExpression);
                        }
                    
                        ArrayInstance array;
                        if (arrayOperations != null)
                        {
                            var length = arrayOperations.GetLength();
                            array = engine.Array.ConstructFast(length - i);
                            for (uint j = i; j < length; ++j)
                            {
                                arrayOperations.TryGetValue(j, out var indexValue);
                                array.SetIndexValue(j - i, indexValue, updateLength: false);
                            }
                        }
                        else
                        {
                            done = false;
                            array = engine.Array.ConstructFast(0);
                            var protocol = new ArrayConstructor.ArrayProtocol(engine, obj, array, iterator, null);
                            protocol.Execute();
                        }

                        if (restElement.Argument is Identifier leftIdentifier)
                        {
                            AssignToIdentifier(engine, leftIdentifier.Name, array);
                        }
                        else if (restElement.Argument is BindingPattern bp)
                        {
                            ProcessPatterns(engine, bp, array);
                        }                    
                        else
                        {
                            AssignToReference(engine, reference,  array);
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
                            ConsumeFromIterator(iterator, out value, out done);
                        }

                        if (value.IsUndefined()
                            && assignmentPattern.Right is Expression expression)
                        {
                            var jintExpression = Build(engine, expression);

                            value = jintExpression.GetValue();
                        }

                        if (assignmentPattern.Left is Identifier leftIdentifier)
                        {
                            if (assignmentPattern.Right.IsFunctionWithName())
                            {
                                ((FunctionInstance) value).SetFunctionName(new JsString(leftIdentifier.Name));
                            }

                            AssignToIdentifier(engine, leftIdentifier.Name, value);
                        }
                        else if (assignmentPattern.Left is BindingPattern bp)
                        {
                            ProcessPatterns(engine, bp, value);
                        }
                    }
                    else
                    {
                        ExceptionHelper.ThrowArgumentOutOfRangeException("pattern",
                            "Unable to determine how to handle array pattern element " + left);
                        break;
                    }
                }
            }
            finally
            {
                if (!done)
                {
                    iterator?.Return();
                }
            }
        }

        private static void HandleObjectPattern(Engine engine, ObjectPattern pattern, JsValue argument)
        {
            var processedProperties = pattern.Properties.Count > 0 && pattern.Properties[pattern.Properties.Count - 1] is RestElement
                ? new HashSet<JsValue>()
                : null;

            var source = TypeConverter.ToObject(engine, argument);
            for (var i = 0; i < pattern.Properties.Count; i++)
            {
                if (pattern.Properties[i] is Property p)
                {
                    JsValue sourceKey;
                    var identifier = p.Key as Identifier;
                    if (identifier == null)
                    {
                        var keyExpression = Build(engine, p.Key);
                        sourceKey = TypeConverter.ToPropertyKey(keyExpression.GetValue());
                    }
                    else
                    {
                        sourceKey = identifier.Name;
                    }

                    processedProperties?.Add(sourceKey.AsStringWithoutTypeCheck());
                    source.TryGetValue(sourceKey, out var value);
                    if (p.Value is AssignmentPattern assignmentPattern)
                    {
                        if (value.IsUndefined() && assignmentPattern.Right is Expression expression)
                        {
                            var jintExpression = Build(engine, expression);
                            value = jintExpression.GetValue();
                        }

                        if (assignmentPattern.Left is BindingPattern bp)
                        {
                            ProcessPatterns(engine, bp, value);
                            continue;
                        }

                        var target = assignmentPattern.Left as Identifier ?? identifier;

                        if (assignmentPattern.Right.IsFunctionWithName())
                        {
                            ((FunctionInstance) value).SetFunctionName(target.Name);
                        }

                        AssignToIdentifier(engine, target.Name, value);
                    }
                    else if (p.Value is BindingPattern bindingPattern)
                    {
                        ProcessPatterns(engine, bindingPattern, value);
                    }
                    else if (p.Value is MemberExpression memberExpression)
                    {
                        var reference = GetReferenceFromMember(engine, memberExpression);
                        AssignToReference(engine, reference, value);
                    }
                    else
                    {
                        var target = p.Value as Identifier ?? identifier;
                        AssignToIdentifier(engine, target.Name, value);
                    }
                }
                else
                {
                    var restElement = (RestElement) pattern.Properties[i];
                    if (restElement.Argument is Identifier leftIdentifier)
                    {
                        var count = Math.Max(0, source.Properties?.Count ?? 0) - processedProperties.Count;
                        var rest = engine.Object.Construct(count);
                        source.CopyDataProperties(rest, processedProperties);
                        AssignToIdentifier(engine, leftIdentifier.Name, rest);
                    }
                    else if (restElement.Argument is BindingPattern bp)
                    {
                        ProcessPatterns(engine, bp, argument);
                    }
                    else if (restElement.Argument is MemberExpression memberExpression)
                    {
                        var left = GetReferenceFromMember(engine, memberExpression);
                        var rest = engine.Object.Construct(0);
                        source.CopyDataProperties(rest, processedProperties);
                        AssignToReference(engine, left, rest);
                    }
                    else
                    {
                        ExceptionHelper.ThrowArgumentException("cannot handle parameter type " + restElement.Argument);
                    }
                }
            }
        }

        private static void AssignToReference(Engine engine,  Reference lref,  JsValue rval)
        {
            engine.PutValue(lref, rval);
            engine._referencePool.Return(lref);
        }

        private static Reference GetReferenceFromMember(Engine engine, MemberExpression memberExpression)
        {
            var expression = new JintMemberExpression(engine, memberExpression);
            var reference = expression.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(engine);
            reference.AssertValid(engine);
            return reference;
        }

        private static void AssignToIdentifier(
            Engine engine,
            string name,
            JsValue rval)
        {
            var env = engine.ExecutionContext.LexicalEnvironment;

            var strict = StrictModeScope.IsStrictModeCode;
            if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                name,
                strict,
                out var environmentRecord,
                out _))
            {
                environmentRecord.SetMutableBinding(name, rval, strict);
            }
            else
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceError<Reference>(engine);
                }
                env._record.CreateMutableBinding(name, rval);
            }
        }
    }
}