using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Global;
using Jint.Native.Json;
using Jint.Native.Math;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Parser;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.References;

namespace Jint
{
    public class Engine
    {
        private readonly ExpressionInterpreter _expressions;
        private readonly StatementInterpreter _statements;
        private readonly Stack<ExecutionContext> _executionContexts;

        public Engine() : this(null)
        {
        }

        public Engine(Action<Options> options)
        {
            _executionContexts = new Stack<ExecutionContext>();

            Global = GlobalObject.CreateGlobalObject(this);

            Object = ObjectConstructor.CreateObjectConstructor(this);
            Function = FunctionConstructor.CreateFunctionConstructor(this);

            Array = ArrayConstructor.CreateArrayConstructor(this);
            String = StringConstructor.CreateStringConstructor(this);
            RegExp = RegExpConstructor.CreateRegExpConstructor(this);
            Number = NumberConstructor.CreateNumberConstructor(this);
            Boolean = BooleanConstructor.CreateBooleanConstructor(this);
            Date = DateConstructor.CreateDateConstructor(this);
            Math = MathInstance.CreateMathObject(this);
            Json = JsonInstance.CreateJsonObject(this);

            Error = ErrorConstructor.CreateErrorConstructor(this, "Error");
            EvalError = ErrorConstructor.CreateErrorConstructor(this, "EvalError");
            RangeError = ErrorConstructor.CreateErrorConstructor(this, "RangeError");
            ReferenceError = ErrorConstructor.CreateErrorConstructor(this, "ReferenceError");
            SyntaxError = ErrorConstructor.CreateErrorConstructor(this, "SyntaxError");
            TypeError = ErrorConstructor.CreateErrorConstructor(this, "TypeError");
            UriError = ErrorConstructor.CreateErrorConstructor(this, "URIError");

            // Because the properties might need some of the built-in object
            // their configuration is delayed to a later step

            Global.Configure();

            Object.Configure();
            Object.PrototypeObject.Configure();

            Function.Configure();
            Function.PrototypeObject.Configure();

            Array.Configure();
            Array.PrototypeObject.Configure();

            String.Configure();
            String.PrototypeObject.Configure();

            RegExp.Configure();
            RegExp.PrototypeObject.Configure();

            Number.Configure();
            Number.PrototypeObject.Configure();

            Boolean.Configure();
            Boolean.PrototypeObject.Configure();

            Date.Configure();
            Date.PrototypeObject.Configure();

            Math.Configure();
            Json.Configure();

            Error.Configure();

            // create the global environment http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.3
            GlobalEnvironment = LexicalEnvironment.NewObjectEnvironment(this, Global, null, true);
            
            // create the global execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.1.1
            EnterExecutionContext(GlobalEnvironment, GlobalEnvironment, Global);

            Options = new Options();

            if (options != null)
            {
                options(Options);
            }

            if (options != null)
            {
                foreach (var entry in Options.GetDelegates())
                {
                    Global.FastAddProperty(entry.Key, new DelegateWrapper(this, entry.Value), true, false, true);
                }
            }

            Eval = new EvalFunctionInstance(this, new ObjectInstance(this), new string[0], LexicalEnvironment.NewDeclarativeEnvironment(this, ExecutionContext.LexicalEnvironment), Options.IsStrict());
            Global.FastAddProperty("eval", Eval, true, false, true);

            _statements = new StatementInterpreter(this);
            _expressions = new ExpressionInterpreter(this);
        }

        public LexicalEnvironment GlobalEnvironment;

        public GlobalObject Global { get; private set; }
        public ObjectConstructor Object { get; private set; }
        public FunctionConstructor Function { get; private set; }
        public ArrayConstructor Array { get; private set; }
        public StringConstructor String { get; private set; }
        public RegExpConstructor RegExp { get; private set; }
        public BooleanConstructor Boolean { get; private set; }
        public NumberConstructor Number { get; private set; }
        public DateConstructor Date { get; private set; }
        public MathInstance Math { get; private set; }
        public JsonInstance Json { get; private set; }
        public EvalFunctionInstance Eval { get; private set; }

        public ErrorConstructor Error { get; private set; }
        public ErrorConstructor EvalError { get; private set; }
        public ErrorConstructor SyntaxError { get; private set; }
        public ErrorConstructor TypeError { get; private set; }
        public ErrorConstructor RangeError { get; private set; }
        public ErrorConstructor ReferenceError { get; private set; }
        public ErrorConstructor UriError { get; private set; }

        public ExecutionContext ExecutionContext { get { return _executionContexts.Peek(); } }

        public Options Options { get; private set; }

        public ExecutionContext EnterExecutionContext(LexicalEnvironment lexicalEnvironment, LexicalEnvironment variableEnvironment, object thisBinding)
        {
            var executionContext = new ExecutionContext
                {
                    LexicalEnvironment = lexicalEnvironment,
                    VariableEnvironment = variableEnvironment,
                    ThisBinding = thisBinding
                };
            _executionContexts.Push(executionContext);

            return executionContext;
        }

        public void LeaveExecutionContext()
        {
            _executionContexts.Pop();
        }

        public object Execute(string source)
        {
            var parser = new JavaScriptParser();
            return Execute(parser.Parse(source));
        }

        public object Execute(Program program)
        {
            var result = _statements.ExecuteProgram(program);
            if (result.Type == Completion.Throw)
            {
                throw new JavaScriptException(result.Value);
            }

            return GetValue(result.Value);
        }

        public Completion ExecuteStatement(Statement statement)
        {
            switch (statement.Type)
            {
                case SyntaxNodes.BlockStatement:
                    return _statements.ExecuteBlockStatement(statement.As<BlockStatement>());
                    
                case SyntaxNodes.BreakStatement:
                    return _statements.ExecuteBreakStatement(statement.As<BreakStatement>());
                    
                case SyntaxNodes.ContinueStatement:
                    return _statements.ExecuteContinueStatement(statement.As<ContinueStatement>());
                    
                case SyntaxNodes.DoWhileStatement:
                    return _statements.ExecuteDoWhileStatement(statement.As<DoWhileStatement>());
                    
                case SyntaxNodes.DebuggerStatement:
                    return _statements.ExecuteDebuggerStatement(statement.As<DebuggerStatement>());
                    
                case SyntaxNodes.EmptyStatement:
                    return _statements.ExecuteEmptyStatement(statement.As<EmptyStatement>());
                    
                case SyntaxNodes.ExpressionStatement:
                    return _statements.ExecuteExpressionStatement(statement.As<ExpressionStatement>());

                case SyntaxNodes.ForStatement:
                    return _statements.ExecuteForStatement(statement.As<ForStatement>());
                    
                case SyntaxNodes.ForInStatement:
                    return _statements.ExecuteForInStatement(statement.As<ForInStatement>());

                case SyntaxNodes.FunctionDeclaration:
                    return new Completion(Completion.Normal, null, null);
                    
                case SyntaxNodes.IfStatement:
                    return _statements.ExecuteIfStatement(statement.As<IfStatement>());
                    
                case SyntaxNodes.LabeledStatement:
                    return null;

                case SyntaxNodes.ReturnStatement:
                    return _statements.ExecuteReturnStatement(statement.As<ReturnStatement>());
                    
                case SyntaxNodes.SwitchStatement:
                    return _statements.ExecuteSwitchStatement(statement.As<SwitchStatement>());
                    
                case SyntaxNodes.ThrowStatement:
                    return _statements.ExecuteThrowStatement(statement.As<ThrowStatement>());

                case SyntaxNodes.TryStatement:
                    return _statements.ExecuteTryStatement(statement.As<TryStatement>());
                    
                case SyntaxNodes.VariableDeclaration:
                    return _statements.ExecuteVariableDeclaration(statement.As<VariableDeclaration>());
                    
                case SyntaxNodes.WhileStatement:
                    return _statements.ExecuteWhileStatement(statement.As<WhileStatement>());
                    
                case SyntaxNodes.WithStatement:
                    return _statements.ExecuteWithStatement(statement.As<WithStatement>());

                case SyntaxNodes.Program:
                    return _statements.ExecuteProgram(statement.As<Program>());
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object EvaluateExpression(Expression expression)
        {
            switch (expression.Type)
            {
                case SyntaxNodes.AssignmentExpression:
                    return _expressions.EvaluateAssignmentExpression(expression.As<AssignmentExpression>());

                case SyntaxNodes.ArrayExpression:
                    return _expressions.EvaluateArrayExpression(expression.As<ArrayExpression>());

                case SyntaxNodes.BinaryExpression:
                    return _expressions.EvaluateBinaryExpression(expression.As<BinaryExpression>());

                case SyntaxNodes.CallExpression:
                    return _expressions.EvaluateCallExpression(expression.As<CallExpression>());

                case SyntaxNodes.ConditionalExpression:
                    return _expressions.EvaluateConditionalExpression(expression.As<ConditionalExpression>());

                case SyntaxNodes.FunctionExpression:
                    return _expressions.EvaluateFunctionExpression(expression.As<FunctionExpression>());

                case SyntaxNodes.Identifier:
                    return _expressions.EvaluateIdentifier(expression.As<Identifier>());

                case SyntaxNodes.Literal:
                    return _expressions.EvaluateLiteral(expression.As<Literal>());

                case SyntaxNodes.LogicalExpression:
                    return _expressions.EvaluateLogicalExpression(expression.As<LogicalExpression>());

                case SyntaxNodes.MemberExpression:
                    return _expressions.EvaluateMemberExpression(expression.As<MemberExpression>());

                case SyntaxNodes.NewExpression:
                    return _expressions.EvaluateNewExpression(expression.As<NewExpression>());

                case SyntaxNodes.ObjectExpression:
                    return _expressions.EvaluateObjectExpression(expression.As<ObjectExpression>());

                case SyntaxNodes.SequenceExpression:
                    return _expressions.EvaluateSequenceExpression(expression.As<SequenceExpression>());

                case SyntaxNodes.ThisExpression:
                    return _expressions.EvaluateThisExpression(expression.As<ThisExpression>());

                case SyntaxNodes.UpdateExpression:
                    return _expressions.EvaluateUpdateExpression(expression.As<UpdateExpression>());

                case SyntaxNodes.UnaryExpression:
                    return _expressions.EvaluateUnaryExpression(expression.As<UnaryExpression>());

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.1
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public object GetValue(object value)
        {
            var reference = value as Reference;

            if (reference == null)
            {
                var completion = value as Completion;

                if (completion != null)
                {
                    return GetValue(completion.Value);
                }

                return value;
            }

            if (reference.IsUnresolvableReference())
            {
                throw new JavaScriptException(ReferenceError);
            }

            var baseValue = reference.GetBase();

            var record = baseValue as EnvironmentRecord;

            if (record != null)
            {
                return record.GetBindingValue(reference.GetReferencedName(), reference.IsStrict());
            }

            var o = TypeConverter.ToObject(this, baseValue);
            return o.Get(reference.GetReferencedName());
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.2
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="value"></param>
        public void PutValue(Reference reference, object value)
        {
            if (reference.IsUnresolvableReference())
            {
                if (reference.IsStrict())
                {
                    throw new JavaScriptException(ReferenceError);
                }

                Global.Put(reference.GetReferencedName(), value, false);
            }
            else if (reference.IsPropertyReference())
            {
                var baseValue = reference.GetBase();
                if (!reference.HasPrimitiveBase())
                {
                    ((ObjectInstance)baseValue).Put(reference.GetReferencedName(), value, reference.IsStrict());
                }
                else
                {
                    PutPrimitiveBase(baseValue, reference.GetReferencedName(), value, reference.IsStrict());
                }
            }
            else
            {
                var baseValue = reference.GetBase();
                var record = baseValue as EnvironmentRecord;

                if (record == null)
                {
                    throw new ArgumentNullException();
                }

                record.SetMutableBinding(reference.GetReferencedName(), value, reference.IsStrict());
            }
        }

        /// <summary>
        /// Used by PutValue when the reference has a primitive base value
        /// </summary>
        /// <param name="b"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="throwOnError"></param>
        public void PutPrimitiveBase(object b, string name, object value, bool throwOnError)
        {
            var o = TypeConverter.ToObject(this, b);
            if (!o.CanPut(name))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(TypeError);
                }

                return;
            }

            var ownDesc = o.GetOwnProperty(name);

            if (ownDesc.IsDataDescriptor())
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(TypeError);
                }

                return;
            }

            var desc = o.GetProperty(name);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.As<AccessorDescriptor>().Set;
                setter.Call(b, new[] { value });
            }
            else
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(TypeError);
                }
            }
        }


        public object GetGlobalValue(string propertyName)
        {
            if (System.String.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("propertyName");
            }

            return GetValue(Global.Get(propertyName));
        }

        public void FunctionDeclarationBindings(IFunctionScope functionScope, LexicalEnvironment localEnv, bool configurableBindings, bool strict)
        {
            // Declaration Binding Instantiation http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
            var env = localEnv.Record;

            // process all function declarations in the current parser scope
            foreach (var functionDeclaration in functionScope.FunctionDeclarations)
            {
                var fn = functionDeclaration.Id.Name;
                var fo = Function.CreateFunctionObject(functionDeclaration);
                var funcAlreadyDeclared = env.HasBinding(fn);
                if (!funcAlreadyDeclared)
                {
                    env.CreateMutableBinding(fn, configurableBindings);
                }
                else
                {
                    if (env == GlobalEnvironment.Record)
                    {
                        var go = Global;
                        var existingProp = go.GetProperty(fn);
                        if (existingProp.Configurable)
                        {
                            go.DefineOwnProperty(fn,
                                                 new DataDescriptor(Undefined.Instance)
                                                 {
                                                     Writable = true,
                                                     Enumerable = true,
                                                     Configurable = configurableBindings
                                                 }, true);
                        }
                        else
                        {
                            if (existingProp.IsAccessorDescriptor() || (!existingProp.Enumerable))
                            {
                                throw new JavaScriptException(TypeError);
                            }
                        }
                    }
                }

                env.SetMutableBinding(fn, fo, strict);
            }
        }

        public void VariableDeclarationBinding(IEnumerable<VariableDeclaration> declarations, EnvironmentRecord env, bool configurableBindings, bool strict)
        {
            // process all variable declarations in the current parser scope
            foreach (var d in declarations.SelectMany(x => x.Declarations))
            {
                var dn = d.Id.Name;
                var varAlreadyDeclared = env.HasBinding(dn);
                if (!varAlreadyDeclared)
                {
                    env.CreateMutableBinding(dn, configurableBindings);
                    env.SetMutableBinding(dn, Undefined.Instance, strict);
                }
            }
        }

    }
}