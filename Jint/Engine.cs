using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Errors;
using Jint.Native.Function;
using Jint.Native.Object;
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
        private readonly LexicalEnvironment _globalEnvironment;
        private readonly Stack<ExecutionContext> _executionContexts;

        private object _result;

        public Engine() : this(null)
        {
        }

        public Engine(Action<Options> options)
        {
            _executionContexts = new Stack<ExecutionContext>();

            var rootObject = new ObjectInstance(null);
            var rootFunction = new FunctionShim(rootObject, null, null);

            Object = new ObjectConstructor(rootFunction);
            Global = new ObjectInstance(Object);
            Function = new FunctionConstructor(rootFunction);

            //Object.Prototype.DefineOwnProperty("hasOwnProperty", new DataDescriptor(new BuiltInPropertyWrapper((Func<ObjectInstance, string, bool>)ObjectConstructor.HasOwnProperty, rootObject)), false);
            //Object.Prototype.DefineOwnProperty("toString", new DataDescriptor(new BuiltInPropertyWrapper((Func<ObjectInstance, string>)ObjectConstructor.ToString, rootObject)), false);

            Global.Set("Object", Object);
            Global.Set("Function", Function);

            // create the global environment http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.3
            _globalEnvironment = LexicalEnvironment.NewObjectEnvironment(Global, null);
            
            // create the global execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.1.1
            EnterExecutionContext(_globalEnvironment, _globalEnvironment, Global);

            Options = new Options();

            if (options != null)
            {
                options(Options);
            }

            if (options != null)
            {
                foreach (var entry in Options.GetDelegates())
                {
                    Global.DefineOwnProperty(entry.Key, new DataDescriptor(new DelegateWrapper(entry.Value, rootFunction)), false);
                }
            }

            _statements = new StatementInterpreter(this);
            _expressions = new ExpressionInterpreter(this);
        }

        public ObjectInstance Global { get; private set; }
        public ObjectConstructor Object { get; private set; }
        public FunctionConstructor Function { get; private set; }

        public ExecutionContext CurrentExecutionContext { get { return _executionContexts.Peek(); } }

        public object Result { get { return GetValue(_result); } }
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

        public void Execute(string source)
        {
            var parser = new JavascriptParser();
            Execute(parser.Parse(source));
        }

        public void Execute(Program program)
        {
            ExecuteStatement(program);
        }

        public void ExecuteStatement(Statement statement)
        {
            switch (statement.Type)
            {
                case SyntaxNodes.BlockStatement:
                    _statements.ExecuteBlockStatement(statement.As<BlockStatement>());
                    break;
                case SyntaxNodes.BreakStatement:
                    _statements.ExecuteBreakStatement(statement.As<BreakStatement>());
                    break;
                case SyntaxNodes.ContinueStatement:
                    _statements.ExecuteContinueStatement(statement.As<ContinueStatement>());
                    break;
                case SyntaxNodes.DoWhileStatement:
                    _statements.ExecuteDoWhileStatement(statement.As<DoWhileStatement>());
                    break;
                case SyntaxNodes.DebuggerStatement:
                    _statements.ExecuteDebuggerStatement(statement.As<DebuggerStatement>());
                    break;
                case SyntaxNodes.EmptyStatement:
                    _statements.ExecuteEmptyStatement(statement.As<EmptyStatement>());
                    break;
                case SyntaxNodes.ExpressionStatement:
                    _statements.ExecuteExpressionStatement(statement.As<ExpressionStatement>());
                    break;
                case SyntaxNodes.ForStatement:
                    _statements.ExecuteForStatement(statement.As<ForStatement>());
                    break;
                case SyntaxNodes.ForInStatement:
                    break;
                case SyntaxNodes.FunctionDeclaration:
                    _statements.ExecuteFunctionDeclaration(statement.As<FunctionDeclaration>());
                    break;
                case SyntaxNodes.IfStatement:
                    _statements.ExecuteIfStatement(statement.As<IfStatement>());
                    break;
                case SyntaxNodes.LabeledStatement:
                    break;
                case SyntaxNodes.ReturnStatement:
                    _statements.ExecuteReturnStatement(statement.As<ReturnStatement>());
                    break;
                case SyntaxNodes.SwitchStatement:
                    break;
                case SyntaxNodes.ThrowStatement:
                    break;
                case SyntaxNodes.TryStatement:
                    break;
                case SyntaxNodes.VariableDeclaration:
                    _statements.ExecuteVariableDeclaration(statement.As<VariableDeclaration>());
                    break;
                case SyntaxNodes.WhileStatement:
                    _statements.ExecuteWhileStatement(statement.As<WhileStatement>());
                    break;
                case SyntaxNodes.WithStatement:
                    break;
                case SyntaxNodes.Program:
                    _statements.ExecuteProgram(statement.As<Program>());
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public dynamic EvaluateExpression(Expression expression)
        {
            _result = Undefined.Instance;

            switch (expression.Type)
            {
                case SyntaxNodes.AssignmentExpression:
                    _result = _expressions.EvaluateAssignmentExpression(expression.As<AssignmentExpression>());
                    break;
                case SyntaxNodes.ArrayExpression:
                    break;
                case SyntaxNodes.BinaryExpression:
                    _result = _expressions.EvaluateBinaryExpression(expression.As<BinaryExpression>());
                    break;
                case SyntaxNodes.CallExpression:
                    _result = _expressions.EvaluateCallExpression(expression.As<CallExpression>());
                    break;
                case SyntaxNodes.ConditionalExpression:
                    _result = _expressions.EvaluateConditionalExpression(expression.As<ConditionalExpression>());
                    break;
                case SyntaxNodes.FunctionExpression:
                    _result = _expressions.EvaluateFunctionExpression(expression.As<FunctionExpression>());
                    break;
                case SyntaxNodes.Identifier:
                    _result = _expressions.EvaluateIdentifier(expression.As<Identifier>());
                    break;
                case SyntaxNodes.Literal:
                    _result = _expressions.EvaluateLiteral(expression.As<Literal>());
                    break;
                case SyntaxNodes.LogicalExpression:
                    break;
                case SyntaxNodes.MemberExpression:
                    _result = _expressions.EvaluateMemberExpression(expression.As<MemberExpression>());
                    break;
                case SyntaxNodes.NewExpression:
                    _result = _expressions.EvaluateNewExpression(expression.As<NewExpression>());
                    break;
                case SyntaxNodes.ObjectExpression:
                    _result = _expressions.EvaluateObjectExpression(expression.As<ObjectExpression>());
                    break;
                case SyntaxNodes.SequenceExpression:
                    _result = _expressions.EvaluateSequenceExpression(expression.As<SequenceExpression>());
                    break;
                case SyntaxNodes.ThisExpression:
                    _result = _expressions.EvaluateThisExpression(expression.As<ThisExpression>());
                    break;
                case SyntaxNodes.UpdateExpression:
                    _result = _expressions.EvaluateUpdateExpression(expression.As<UpdateExpression>());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return _result;
        }

        public dynamic GetValue(object value)
        {
            var reference = value as Reference;
            
            if (reference == null)
            {
                return value;
            }

            if (reference.IsUnresolvableReference())
            {
                throw new ReferenceError();
            }

            var baseValue = reference.GetBase();

            var record = baseValue as EnvironmentRecord;

            if (record != null)
            {
                return record.GetBindingValue(reference.GetReferencedName(), reference.IsStrict());
            }

            /// todo: complete implementation http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.1
            return ((ObjectInstance) baseValue).Get(reference.GetReferencedName());
        }

        public void SetValue(Reference reference, object value)
        {
            if (reference.IsUnresolvableReference())
            {
                if (reference.IsStrict())
                {
                    throw new ReferenceError();
                }

                Global.Set(reference.GetReferencedName(), value);
            }
            else 
            {
                var baseValue = reference.GetBase();
                var record = baseValue as EnvironmentRecord;
                
                if (record != null)
                {
                    record.SetMutableBinding(reference.GetReferencedName(), value, reference.IsStrict());
                    return;
                }
                
                ((ObjectInstance)baseValue).Set(reference.GetReferencedName(), value);
            }
        }

    }
}