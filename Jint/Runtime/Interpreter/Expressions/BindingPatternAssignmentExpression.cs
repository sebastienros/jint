using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Native.Object;
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
                HandleArrayPattern(engine, pattern as ArrayPattern, argument);
                HandleObjectPattern(engine, pattern as ObjectPattern, argument);
            }

            internal static void HandleArrayPattern(Engine engine, ArrayPattern arrayPattern, JsValue argument)
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
                
                for (uint i = 0; i < arrayPattern.Elements.Count; i++)
                {
                    var left = arrayPattern.Elements[(int) i];
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

                            AssignToIdentifier(engine, ((Identifier) restElement.Argument).Name, array);
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
                            AssignToIdentifier(engine, ((Identifier) assignmentPattern.Left).Name, value);
                        }
                        else
                        {
                            ExceptionHelper.ThrowArgumentOutOfRangeException("pattern", "Unable to determine how to handle array pattern element");
                            break;
                        }
                    }
                }
            }

            
            internal static void HandleObjectPattern(Engine engine, ObjectPattern objectPattern, JsValue argument)
            {
                var source = (ObjectInstance) argument;
                for (uint i = 0; i < objectPattern.Properties.Count; i++)
                {
                    var left = objectPattern.Properties[(int) i];
                    if (left.Key is Identifier identifier)
                    {
                        Identifier target;
                        if (!source.TryGetValue(identifier.Name, out var value)
                            && left.Value is AssignmentPattern assignmentPattern
                            && assignmentPattern.Right is Expression defaultValueExpression)
                        {
                            var jintExpression = Build(engine, defaultValueExpression);
                            value = jintExpression.GetValue();
                            target = assignmentPattern.Left as Identifier ?? identifier;
                        }
                        else
                        {
                            target = left.Value as Identifier ?? identifier;
                        }
                        AssignToIdentifier(engine, target.Name, value);
                    }
                    else
                    {
                        ExceptionHelper.ThrowArgumentOutOfRangeException("pattern", "Unable to determine how to handle object pattern element");
                        break;
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