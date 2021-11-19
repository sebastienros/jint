#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint
{
    public static class EsprimaExtensions
    {
        public static JsValue GetKey(this ClassProperty property, Engine engine) => GetKey(property.Key, engine, property.Computed);

        public static JsValue GetKey(this Expression expression, Engine engine, bool resolveComputed = false)
        {
            if (expression is Literal literal)
            {
                return LiteralKeyToString(literal);
            }

            if (!resolveComputed && expression is Identifier identifier)
            {
                return identifier.Name;
            }

            if (!resolveComputed || !TryGetComputedPropertyKey(expression, engine, out var propertyKey))
            {
                ExceptionHelper.ThrowArgumentException("Unable to extract correct key, node type: " + expression.Type);
                return null;
            }

            return propertyKey;
        }

        private static bool TryGetComputedPropertyKey<T>(T expression, Engine engine, out JsValue propertyKey)
            where T : Expression
        {
            if (expression.Type is Nodes.Identifier
                or Nodes.CallExpression
                or Nodes.BinaryExpression
                or Nodes.UpdateExpression
                or Nodes.AssignmentExpression
                or Nodes.UnaryExpression
                or Nodes.MemberExpression)
            {
                var context = engine._activeEvaluationContext;
                propertyKey = TypeConverter.ToPropertyKey(JintExpression.Build(engine, expression).GetValue(context).Value);
                return true;
            }

            propertyKey = string.Empty;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFunctionDefinition<T>(this T node) where T : Node
        {
            var type = node.Type;
            return type
                is Nodes.FunctionExpression
                or Nodes.ArrowFunctionExpression
                or Nodes.ArrowParameterPlaceHolder
                or Nodes.ClassExpression;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasName<T>(this T node) where T : Node
        {
            if (!node.IsFunctionDefinition())
            {
                return false;
            }

            if ((node as IFunction)?.Id is not null)
            {
                return true;
            }

            if ((node as ClassExpression)?.Id is not null)
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAnonymousFunctionDefinition<T>(this T node) where T : Node
        {
            if (!node.IsFunctionDefinition())
            {
                return false;
            }

            if (node.HasName())
            {
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsOptional<T>(this T node) where T : Expression
        {
            switch (node)
            {
                case MemberExpression { Optional: true }:
                case CallExpression { Optional: true }:
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string LiteralKeyToString(Literal literal)
        {
            // prevent conversion to scientific notation
            if (literal.Value is double d)
            {
                return TypeConverter.ToString(d);
            }
            return literal.Value as string ?? Convert.ToString(literal.Value, provider: null);
        }

        internal static void GetBoundNames(this VariableDeclaration variableDeclaration, List<string> target)
        {
            ref readonly var declarations = ref variableDeclaration.Declarations;
            for (var i = 0; i < declarations.Count; i++)
            {
                var declaration = declarations[i];
                GetBoundNames(declaration.Id, target);
            }
        }

        internal static void GetBoundNames(this Node? parameter, List<string> target)
        {
            if (parameter is null || parameter.Type == Nodes.Literal)
            {
                return;
            }

            // try to get away without a loop
            if (parameter is Identifier id)
            {
                target.Add(id.Name!);
                return;
            }

            if (parameter is VariableDeclaration variableDeclaration)
            {
                variableDeclaration.GetBoundNames(target);
                return;
            }

            while (true)
            {
                if (parameter is Identifier identifier)
                {
                    target.Add(identifier.Name!);
                    return;
                }

                if (parameter is RestElement restElement)
                {
                    parameter = restElement.Argument;
                    continue;
                }
                else if (parameter is ArrayPattern arrayPattern)
                {
                    ref readonly var arrayPatternElements = ref arrayPattern.Elements;
                    for (var i = 0; i < arrayPatternElements.Count; i++)
                    {
                        var expression = arrayPatternElements[i];
                        GetBoundNames(expression, target);
                    }
                }
                else if (parameter is ObjectPattern objectPattern)
                {
                    ref readonly var objectPatternProperties = ref objectPattern.Properties;
                    for (var i = 0; i < objectPatternProperties.Count; i++)
                    {
                        var property = objectPatternProperties[i];
                        if (property is Property p)
                        {
                            GetBoundNames(p.Value, target);
                        }
                        else
                        {
                            GetBoundNames((RestElement) property, target);
                        }
                    }
                }
                else if (parameter is AssignmentPattern assignmentPattern)
                {
                    parameter = assignmentPattern.Left;
                    continue;
                }
                break;
            }
        }

        internal static void BindingInitialization(
            this Expression? expression,
            EvaluationContext context,
            JsValue value,
            EnvironmentRecord env)
        {
            if (expression is Identifier identifier)
            {
                var catchEnvRecord = (DeclarativeEnvironmentRecord) env;
                catchEnvRecord.CreateMutableBindingAndInitialize(identifier.Name, canBeDeleted: false, value);
            }
            else if (expression is BindingPattern bindingPattern)
            {
                BindingPatternAssignmentExpression.ProcessPatterns(
                    context,
                    bindingPattern,
                    value,
                    env);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-definemethod
        /// </summary>
        internal static Record DefineMethod(this ClassProperty m, ObjectInstance obj, ObjectInstance? functionPrototype = null)
        {
            var engine = obj.Engine;
            var property = TypeConverter.ToPropertyKey(m.GetKey(engine));
            var prototype = functionPrototype ?? engine.Realm.Intrinsics.Function.PrototypeObject;
            var function = m.Value as IFunction;
            if (function is null)
            {
                ExceptionHelper.ThrowSyntaxError(engine.Realm);
            }

            var functionDefinition = new JintFunctionDefinition(engine, function);
            var closure = new ScriptFunctionInstance(
                engine,
                functionDefinition,
                engine.ExecutionContext.LexicalEnvironment,
                functionDefinition.ThisMode,
                prototype);

            closure.MakeMethod(obj);

            return new Record(property, closure);
        }

        internal readonly record struct Record(JsValue Key, ScriptFunctionInstance Closure);
    }
}