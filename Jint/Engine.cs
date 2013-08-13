using System;
using System.Collections.Generic;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Errors;
using Jint.Native.Function;
using Jint.Native.Global;
using Jint.Native.Math;
using Jint.Native.Number;
using Jint.Native.Object;
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
        private readonly LexicalEnvironment _globalEnvironment;
        private readonly Stack<ExecutionContext> _executionContexts;

        public Engine() : this(null)
        {
        }

        public Engine(Action<Options> options)
        {
            _executionContexts = new Stack<ExecutionContext>();

            RootObject = new ObjectInstance(null);
            RootFunction = new FunctionShim(this, RootObject, null, null);

            Object = new ObjectConstructor(this);
            Global = GlobalObject.CreateGlobalObject(this, Object);
            Function = new FunctionConstructor(this);
            Array = new ArrayConstructor(this);
            String = new StringConstructor(this);
            Number = new NumberConstructor(this);
            Boolean = new BooleanConstructor(this);
            Date = new DateConstructor(this);
            Math = MathInstance.CreateMathObject(this, RootObject);

            Global.Set("Object", Object);
            Global.Set("Function", Function);
            Global.Set("Array", Array);
            Global.Set("String", String);
            Global.Set("Number", Number);
            Global.Set("Boolean", Boolean);
            Global.Set("Date", Date);
            Global.Set("Math", Math);

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
                    Global.DefineOwnProperty(entry.Key, new DataDescriptor(new DelegateWrapper(this, entry.Value)), false);
                }
            }

            _statements = new StatementInterpreter(this);
            _expressions = new ExpressionInterpreter(this);
        }

        public ObjectInstance RootObject { get; private set; }
        public FunctionInstance RootFunction { get; private set; }

        public ObjectInstance Global { get; private set; }
        public ObjectConstructor Object { get; private set; }
        public FunctionConstructor Function { get; private set; }
        public ArrayConstructor Array { get; private set; }
        public StringConstructor String { get; private set; }
        public BooleanConstructor Boolean { get; private set; }
        public NumberConstructor Number { get; private set; }
        public DateConstructor Date { get; private set; }
        public MathInstance Math { get; private set; }

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

        public Completion Execute(string source)
        {
            var parser = new JavascriptParser();
            return Execute(parser.Parse(source));
        }

        public Completion Execute(Program program)
        {
            return ExecuteStatement(program);
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
                    return _statements.ForInStatement(statement.As<ForInStatement>());

                case SyntaxNodes.FunctionDeclaration:
                    return _statements.ExecuteFunctionDeclaration(statement.As<FunctionDeclaration>());
                    
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
                    return _statements.ExecuteVariableDeclaration(statement.As<VariableDeclaration>(), true);
                    
                case SyntaxNodes.WhileStatement:
                    return _statements.ExecuteWhileStatement(statement.As<WhileStatement>());
                    
                case SyntaxNodes.WithStatement:
                    return _statements.ExecuteWithStatement(statement.As<WithStatement>());

                case SyntaxNodes.Program:
                    return _statements.ExecuteProgram(statement.As<Program>());
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return null;
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
                    return null;

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
                throw new ReferenceError();
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

        public object GetGlobalValue(string propertyName)
        {
            if (System.String.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("propertyName");
            }

            return GetValue(Global.Get(propertyName));
        }
    }
}