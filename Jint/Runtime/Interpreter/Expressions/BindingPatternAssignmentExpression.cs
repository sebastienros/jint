using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;

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
                return JsValue.Undefined;
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

            private static void HandleArrayPattern(Engine engine, ArrayPattern pattern, JsValue argument)
            {
                var obj = TypeConverter.ToObject(engine, argument);

                ArrayPrototype.ArrayOperations arrayOperations = null;
                IIterator iterator = null;
                if (obj.IsArrayLike)
                {
                    arrayOperations = ArrayPrototype.ArrayOperations.For(obj);
                }
                else
                {
                    if (!obj.TryGetIterator(engine, out iterator))
                    {
                        ExceptionHelper.ThrowTypeError(engine);
                        return;
                    }
                }
                
                for (uint i = 0; i < pattern.Elements.Count; i++)
                {
                    var left = pattern.Elements[(int) i];
                    if (left is Identifier identifier)
                    {
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            value = iterator.Next();
                        }
                        AssignToIdentifier(engine, identifier.Name, value);
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
                            value = iterator.Next();
                        }
                        ProcessPatterns(engine, bindingPattern, value);
                    }
                    else if (left is ArrayPatternElement arrayPatternElement)
                    {
                        if (arrayPatternElement is RestElement restElement)
                        {
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
                        }
                        else if (arrayPatternElement is AssignmentPattern assignmentPattern)
                        {
                            JsValue value;
                            if (arrayOperations != null)
                            {
                                arrayOperations.TryGetValue(i, out value);
                            }
                            else
                            {
                                value = iterator.Next();
                            }

                            if (value.IsUndefined()
                                && assignmentPattern.Right is Expression defaultValueExpression)
                            {
                                var jintExpression = Build(engine, defaultValueExpression);
                                value = jintExpression.GetValue();
                            }

                            if (assignmentPattern.Left is Identifier leftIdentifier)
                            {
                                AssignToIdentifier(engine, leftIdentifier.Name, value);
                            }
                            else if (assignmentPattern.Left is BindingPattern bp)
                            {
                                ProcessPatterns(engine, bp, value);
                            }
                        }
                        else
                        {
                            ExceptionHelper.ThrowArgumentOutOfRangeException("pattern", "Unable to determine how to handle array pattern element");
                            break;
                        }
                    }
                }
            }

            private static void HandleObjectPattern(Engine engine, ObjectPattern pattern, JsValue argument)
            {
                var source = TypeConverter.ToObject(engine, argument);
                for (uint i = 0; i < pattern.Properties.Count; i++)
                {
                    var left = pattern.Properties[(int) i];
                    string sourceKey;
                    Identifier identifier = left.Key as Identifier;
                    if (identifier == null)
                    {
                        var keyExpression = Build(engine, left.Key);
                        sourceKey = TypeConverter.ToPropertyKey(keyExpression.GetValue());
                    }
                    else
                    {
                        sourceKey = identifier.Name;
                    }

                    source.TryGetValue(sourceKey, out var value);
                    if (left.Value is AssignmentPattern assignmentPattern
                        && assignmentPattern.Right is Expression expression)
                    {
                        var jintExpression = Build(engine, expression);
                        value = jintExpression.GetValue();

                        var target = assignmentPattern.Left as Identifier ?? identifier;

                        if (value is FunctionInstance scriptFunctionInstance)
                        {
                            scriptFunctionInstance.SetFunctionName(target.Name);
                        }
                        
                        AssignToIdentifier(engine, target.Name, value);
                    }
                    else if (left.Value is BindingPattern bindingPattern)
                    {
                        ProcessPatterns(engine, bindingPattern, value);
                    }
                    else
                    {
                        var target = left.Value as Identifier ?? identifier;
                        AssignToIdentifier(engine, target.Name, value);
                    }
                }
            }
            
            private static void AssignToIdentifier(Engine engine,
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
                    env._record.CreateMutableBinding(name, rval);
                }
            }
        }
}