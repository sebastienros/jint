#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
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
                return ExceptionHelper.ThrowArgumentException<JsValue>("Unable to extract correct key, node type: " + expression.Type);
            }

            return propertyKey;
        }

        private static bool TryGetComputedPropertyKey<T>(T expression, Engine engine, out JsValue propertyKey)
            where T : Expression
        {
            if (expression.Type == Nodes.Identifier
                || expression.Type == Nodes.CallExpression
                || expression.Type == Nodes.BinaryExpression
                || expression.Type == Nodes.UpdateExpression
                || expression.Type == Nodes.AssignmentExpression
                || expression is StaticMemberExpression)
            {
                propertyKey = TypeConverter.ToPropertyKey(JintExpression.Build(engine, expression).GetValue());
                return true;
            }

            propertyKey = string.Empty;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFunctionWithName<T>(this T node) where T : Node
        {
            var type = node.Type;
            return type == Nodes.FunctionExpression 
                   || type == Nodes.ArrowFunctionExpression 
                   || type == Nodes.ArrowParameterPlaceHolder 
                   || type == Nodes.ClassExpression;
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
                    if (assignmentPattern.Right is ClassExpression classExpression)
                    {
                        // TODO check if there's more generic rule
                        if (classExpression.Id is not null)
                        {
                            target.Add(classExpression.Id.Name!);
                        }
                    }
                    continue;
                }
                break;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-definemethod
        /// </summary>
        internal static Record DefineMethod(this ClassProperty m, ObjectInstance obj, ObjectInstance? functionPrototype = null)
        {
            var engine = obj.Engine;
            var property = TypeConverter.ToPropertyKey(m.GetKey(engine));
            var prototype = functionPrototype ?? engine.Function.PrototypeObject;
            var function = m.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(engine);
            var functionDefinition = new JintFunctionDefinition(engine, function);
            var functionThisMode = functionDefinition.Strict || engine._isStrict
                ? FunctionThisMode.Strict 
                : FunctionThisMode.Global;

            var closure = new ScriptFunctionInstance(
                engine,
                functionDefinition,
                engine.ExecutionContext.LexicalEnvironment,
                functionThisMode,
                prototype);

            closure.MakeMethod(obj);

            return new Record(property, closure);
        }

        internal readonly struct Record
        {
            public Record(JsValue key, ScriptFunctionInstance closure)
            {
                Key = key;
                Closure = closure;
            }

            public readonly JsValue Key;
            public readonly ScriptFunctionInstance Closure;
        }
    }
}