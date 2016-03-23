using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Parser.Ast;

namespace Jint.Walker
{
    public class JintWalker
    {
        private readonly ExpressionWalker _expressionWalker;
        private readonly StatementWalker _statementWalker;
        private static bool _isSet;
      private static bool _isGet;
      private List<SyntaxNode> _astList;

      public JintWalker()
        {
            _statementWalker = new StatementWalker(this);
            _expressionWalker = new ExpressionWalker(this);
        }

        public ExpressionWalker ExpressionWalker
        {
            get { return _expressionWalker; }
        }

        public event StatementWalkEventDelegate StatementWalkEvent;

        protected virtual void OnStatementWalkEvent(StatementWalkEventDelegateArgs args)
        {
            StatementWalkEventDelegate handler = StatementWalkEvent;
            if (handler != null) handler(this, args);
        }

        public event ExpressionWalkEventDelegate ExpressionWalkEvent;

        protected virtual void OnExpressionWalkEvent(ExpressionWalkEventDelegateArgs args)
        {
            ExpressionWalkEventDelegate handler = ExpressionWalkEvent;
            if (handler != null) handler(this, args);
        }

        public void WalkExpression(Expression expression)
        {
            if (expression == null)
                return;

            OnExpressionWalkEvent(new ExpressionWalkEventDelegateArgs { Expr = expression });

            switch (expression.Type)
            {
                case SyntaxNodes.AssignmentExpression:
                    ExpressionWalker.WalkAssignmentExpression(expression.As<AssignmentExpression>());
                    break;
                case SyntaxNodes.ArrayExpression:
                    ExpressionWalker.WalkArrayExpression(expression.As<ArrayExpression>());
                    break;
                case SyntaxNodes.BinaryExpression:
                    ExpressionWalker.WalkBinaryExpression(expression.As<BinaryExpression>());
                    break;

                case SyntaxNodes.CallExpression:
                    ExpressionWalker.WalkCallExpression(expression.As<CallExpression>());
                    break;

                case SyntaxNodes.ConditionalExpression:
                    ExpressionWalker.WalkConditionalExpression(expression.As<ConditionalExpression>());
                    break;

                case SyntaxNodes.FunctionExpression:
                    ExpressionWalker.WalkFunctionExpression(expression.As<FunctionExpression>());
                    break;

                case SyntaxNodes.Identifier:
                    ExpressionWalker.WalkIdentifier(expression.As<Identifier>());
                    break;

                case SyntaxNodes.Literal:
                    ExpressionWalker.WalkLiteral(expression.As<Literal>());
                    break;

                case SyntaxNodes.RegularExpressionLiteral:
                    ExpressionWalker.WalkLiteral(expression.As<Literal>());
                    break;

                case SyntaxNodes.LogicalExpression:
                    ExpressionWalker.WalkLogicalExpression(expression.As<LogicalExpression>());
                    break;

                case SyntaxNodes.MemberExpression:
                    ExpressionWalker.WalkMemberExpression(expression.As<MemberExpression>());
                    break;

                case SyntaxNodes.NewExpression:
                    ExpressionWalker.WalkNewExpression(expression.As<NewExpression>());
                    break;

                case SyntaxNodes.ObjectExpression:
                    ExpressionWalker.WalkObjectExpression(expression.As<ObjectExpression>());
                    break;

                case SyntaxNodes.SequenceExpression:
                    ExpressionWalker.WalkSequenceExpression(expression.As<SequenceExpression>());
                    break;

                case SyntaxNodes.ThisExpression:
                    ExpressionWalker.WalkThisExpression(expression.As<ThisExpression>());
                    break;

                case SyntaxNodes.UpdateExpression:
                    ExpressionWalker.WalkUpdateExpression(expression.As<UpdateExpression>());
                    break;

                case SyntaxNodes.UnaryExpression:
                    ExpressionWalker.WalkUnaryExpression(expression.As<UnaryExpression>());
                    break;

                case SyntaxNodes.VariableDeclarator:
                    ExpressionWalker.WalkVariableDeclaratorExpression(expression.As<VariableDeclarator>());
                    break;

                case SyntaxNodes.VariableDeclaration:
                    ExpressionWalker.WalkVariableDeclarationExpression(expression.As<VariableDeclaration>());
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void WalkStatement(Statement statement)
        {
            if (statement == null)
              return;

            OnStatementWalkEvent(new StatementWalkEventDelegateArgs { Stmt = statement });

            switch (statement.Type)
            {
                case SyntaxNodes.BlockStatement:
                    _statementWalker.WalkBlockStatement(statement.As<BlockStatement>());
                    break;

                case SyntaxNodes.BreakStatement:
                    _statementWalker.WalkBreakStatement(statement.As<BreakStatement>());
                    break;

                case SyntaxNodes.ContinueStatement:
                    _statementWalker.WalkContinueStatement(statement.As<ContinueStatement>());
                    break;

                case SyntaxNodes.DoWhileStatement:
                    _statementWalker.WalkDoWhileStatement(statement.As<DoWhileStatement>());
                    break;

                case SyntaxNodes.DebuggerStatement:
                    _statementWalker.WalkDebuggerStatement(statement.As<DebuggerStatement>());
                    break;

                case SyntaxNodes.EmptyStatement:
                    _statementWalker.WalkEmptyStatement(statement.As<EmptyStatement>());
                    break;

                case SyntaxNodes.ExpressionStatement:
                    _statementWalker.WalkExpressionStatement(statement.As<ExpressionStatement>());
                    break;

                case SyntaxNodes.ForStatement:
                    _statementWalker.WalkForStatement(statement.As<ForStatement>());
                    break;

                case SyntaxNodes.ForInStatement:
                    _statementWalker.WalkForInStatement(statement.As<ForInStatement>());
                    break;

                case SyntaxNodes.FunctionDeclaration:
                    _statementWalker.WalkFunctionDeclaration(statement.As<FunctionDeclaration>());
                    break;

                case SyntaxNodes.IfStatement:
                    _statementWalker.WalkIfStatement(statement.As<IfStatement>());
                    break;

                case SyntaxNodes.LabeledStatement:
                    _statementWalker.WalkLabelledStatement(statement.As<LabelledStatement>());
                    break;

                case SyntaxNodes.ReturnStatement:
                    _statementWalker.WalkReturnStatement(statement.As<ReturnStatement>());
                    break;

                case SyntaxNodes.SwitchStatement:
                    _statementWalker.WalkSwitchStatement(statement.As<SwitchStatement>());
                    break;

                case SyntaxNodes.ThrowStatement:
                    _statementWalker.WalkThrowStatement(statement.As<ThrowStatement>());
                    break;

                case SyntaxNodes.TryStatement:
                    _statementWalker.WalkTryStatement(statement.As<TryStatement>());
                    break;

                case SyntaxNodes.VariableDeclaration:
                    _statementWalker.WalkVariableDeclaration(statement.As<VariableDeclaration>());
                    break;

                case SyntaxNodes.WhileStatement:
                    _statementWalker.WalkWhileStatement(statement.As<WhileStatement>());
                    break;

                case SyntaxNodes.WithStatement:
                    _statementWalker.WalkWithStatement(statement.As<WithStatement>());
                    break;

                case SyntaxNodes.Program:
                    _statementWalker.WalkProgram(statement.As<Program>());
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        public static string GetExprAsString(Expression expr)
        {
            if (expr is ArrayExpression)
            {
                var exp = expr as ArrayExpression;
                return "[" + GetExprAsString(exp.Elements) + "]";
            }
            if (expr is AssignmentExpression)
            {
                var exp = expr as AssignmentExpression;
                return GetExprAsString(exp.Left) + " " + AssignmentExpression.GetAssignOperatorAsString(exp.Operator) + " " + GetExprAsString(exp.Right);
            }
            if (expr is BinaryExpression)
            {
                var exp = expr as BinaryExpression;
                bool leftIsExpr = exp.Left is BinaryExpression;
                bool rightIsExpr = exp.Right is BinaryExpression;
                var res = "";
                res += (leftIsExpr ?"(":"")
                  + GetExprAsString(exp.Left) 
                  + (leftIsExpr?") ":" ");
                res += BinaryExpression.GetBinaryOperatorAsString(exp.Operator);
                res += (rightIsExpr?" (":" " )
                    + GetExprAsString(exp.Right) 
                    + (rightIsExpr?")":"");
                return res;
            }
            if (expr is CallExpression)
            {
                var exp = expr as CallExpression;
                return GetExprAsString(exp.Callee) + "(" + GetExprAsString(exp.Arguments) + ")";
            }
            if (expr is ConditionalExpression)
            {
                var exp = expr as ConditionalExpression;
                return GetExprAsString(exp.Test) + " ? " + GetExprAsString(exp.Consequent) + " : " + GetExprAsString(exp.Alternate);
            }
            if (expr is FunctionExpression)
            {
                string res = "";
                var exp = expr as FunctionExpression;
                if (exp.Id != null)
                {
                  res += "function ";
                  res += GetExprAsString(exp.Id);
                  res += "(";
                  res += GetExprAsString(exp.Parameters);
                  res += ") ";
                  res += "{ ";
                  res += GetStatementAsString(exp.Body, true);
                  res += "}";
                }
                else
                {
                  res += (_isSet || _isGet ? "" : "function ");
                  res += "(";
                  res += GetExprAsString(exp.Parameters);
                  res += ") ";
                  res += "{ ";
                  res += GetStatementAsString(exp.Body, true);
                  res += "}";
                }
                //foreach (FunctionDeclaration d in exp.FunctionDeclarations)
                //{
                //    res += GetStatementAsString(d,true);
                //}
                //foreach (VariableDeclaration d in exp.VariableDeclarations)
                //{
                //    res += GetStatementAsString(d,true);
                //}
                return res;
            }
            if (expr is Identifier)
            {
                var exp = expr as Identifier;
                return exp.Name;
            }
            if (expr is Literal)
            {
                var exp = expr as Literal;
                return exp.Raw;
            }
            if (expr is LogicalExpression)
            {
                var exp = expr as LogicalExpression;
                return GetExprAsString(exp.Left) + " " + LogicalExpression.GetLogicalOperatorAsString(exp.Operator) + " " + GetExprAsString(exp.Right);
            }
            if (expr is MemberExpression)
            {
                var exp = expr as MemberExpression;
                if (exp.Computed)
                {
                    return GetExprAsString(exp.Object) + "[" + GetExprAsString(exp.Property) +"]";
                }
                else
                {
                    return GetExprAsString(exp.Object) + "." + GetExprAsString(exp.Property);
                }

            }
            if (expr is NewExpression)
            {
                var exp = expr as NewExpression;
                return "new " + GetExprAsString(exp.Callee) + "(" + GetExprAsString(exp.Arguments) + ")";
            }
            if (expr is ObjectExpression)
            {
                var res = "{ ";
                var exp = expr as ObjectExpression;
                decimal index = 1;
                var props = exp.Properties;
                if (props != null)
                {
                    var cnt = props.Count();
                    foreach (Property p in props)
                    {
                        res += GetExprAsString(p) + (cnt > 1 && index < cnt ? ", " : "");
                        ++index;
                    }
                }
                return res + " }";
            }
            if (expr is Property)
            {
                var exp = expr as Property;
                switch (exp.Kind)
                {
                    case PropertyKind.Set:
                        {
                            _isSet = true;
                            var res = "set " + GetExprAsString(exp.Key as Expression) + GetExprAsString(exp.Value);
                            _isSet = false;
                            return res;

                        }
                    case PropertyKind.Get:
                        {
                            _isGet = true;
                            var res = "get " + GetExprAsString(exp.Key as Expression) + GetExprAsString(exp.Value);
                            _isGet = false;
                            return res;
                        }
                    case PropertyKind.Data:
                        return GetExprAsString(exp.Key as Expression) + ": " + GetExprAsString(exp.Value);
                }
            }
            if (expr is RegExpLiteral)
            {
                var exp = expr as RegExpLiteral;
                throw new NotImplementedException();
            }
            if (expr is SequenceExpression)
            {
                string res = "";
                var exp = expr as SequenceExpression;
                decimal index = 1;
                foreach (Expression e in exp.Expressions)
                {
                    res += GetExprAsString(e) + (exp.Expressions.Count > 1 && index < exp.Expressions.Count ? ", " : "");
                    ++index;
                }
                return res;
            }
            if (expr is ThisExpression)
            {
                return "this";
            }
            if (expr is UnaryExpression)
            {
                if (expr is UpdateExpression)
                {
                    var exp = expr as UpdateExpression;
                    if (exp.Prefix)
                    {
                        return UnaryExpression.GetUnaryOperatorAsString(exp.Operator) + GetExprAsString(exp.Argument);
                    }
                    return GetExprAsString(exp.Argument) + UnaryExpression.GetUnaryOperatorAsString(exp.Operator);
                }
                else
                {
                    var exp = expr as UnaryExpression;
                    return UnaryExpression.GetUnaryOperatorAsString(exp.Operator) + GetExprAsString(exp.Argument);
                }
            }
            if (expr is VariableDeclarator)
            {
                string res = "";
                var exp = expr as VariableDeclarator;
                if (exp.Init != null)
                {
                    res = GetExprAsString(exp.Id) + " = " + GetExprAsString(exp.Init);
                }
                else
                {
                    res = GetExprAsString(exp.Id);
                }
                return res;
            }
            return "";
        }
        public static string GetExprAsString(IEnumerable<Expression> arguments)
        {
            if (arguments != null)
            {
                var cnt = arguments.Count();
                if (cnt == 0)
                {
                    return "";
                }
                var res = "";
                decimal index = 1;
                foreach (Expression argument in arguments)
                {
                    res += GetExprAsString(argument) + (cnt > 1 && index < cnt ? ", " : "");
                    ++index;
                }
                return res;
            }
            return "";
        }

        private static string GetExprAsString(IEnumerable<Identifier> arguments)
        {
            if (arguments != null)
            {
                var cnt = arguments.Count();

                if (cnt == 0)
                {
                    return "";
                }
                var res = "";
                decimal index = 1;
                foreach (Identifier argument in arguments)
                {
                    res += GetExprAsString(argument) + (cnt > 1 && index < cnt ? ", " : "");
                    ++index;
                }
                return res;
            }
            return "";
        }

        public static string GetStatementAsString(Statement s, bool termWithSemiColon)
        {
            string res = "";
            if (s is TryStatement)
            {
              var stmt = s as TryStatement;
              res += "try ";
              res += "{\r\n";
              res += GetStatementAsString(stmt.Block, termWithSemiColon);
              res += "}\r\n";
              //foreach (Statement g in b.GuardedHandlers)
              //{
              //  res += GetStatementAsString(g,termWithSemiColon);
              //}
              foreach (CatchClause c in stmt.Handlers)
              {
                res += "catch ";
                res += "(";
                res += GetExprAsString(c.Param);
                res += ")";
                res += "{\r\n";
                res += GetStatementAsString(c.Body, termWithSemiColon);
                res += "}\r\n";
              }
              if (stmt.Finalizer != null)
              {
                res += "finally ";
                res += "{\r\n";
                res += GetStatementAsString(stmt.Finalizer, termWithSemiColon);
                res += "}\r\n";
              }
              return res;
            }
            if (s is ThrowStatement)
            {
              var stmt = s as ThrowStatement;
              res += "throw ";
              res += GetExprAsString(stmt.Argument);
              res += ";\r\n";
              return res;
            }
            if (s is SwitchStatement)
            {
              var stmt = s as SwitchStatement;
              res += "switch (";
              res += GetExprAsString(stmt.Discriminant);
              res += ")\r\n";
              res += "{\r\n";
              foreach (SwitchCase c in stmt.Cases)
              {
                if (c.Test == null)
                {
                  res += "default:";
                }
                else
                {
                  res += "case " + GetExprAsString(c.Test) + ":\r\n";
                }
                res += GetStatementAsString(c.Consequent, termWithSemiColon);
              }
              res += "}\r\n";
              return res;
            }
            if (s is EmptyStatement)
            {
              var stmt = s as EmptyStatement;
              res += ";\r\n";
              return res;
            }
            if (s is DebuggerStatement)
            {
              res += "debugger";
              var stmt = s as DebuggerStatement;
              res += ";\r\n";
              return res;
            }
            if (s is ContinueStatement)
            {
              res += "continue ";
              var stmt = s as ContinueStatement;
              res += GetExprAsString(stmt.Label);
              res += ";\r\n";
              return res;
            }
            if (s is LabelledStatement)
            {
              var stmt = s as LabelledStatement;
              res += GetExprAsString(stmt.Label);
              res += ": {\r\n";
              res += GetStatementAsString(stmt.Body, termWithSemiColon);
              res += "}\r\n";
              return res;
            }
            if (s is BreakStatement)
            {
              res += "break ";
              var stmt = s as BreakStatement;
              res += GetExprAsString(stmt.Label);
              res += ";\r\n";
              return res;
            }
            if (s is DoWhileStatement)
            {
              res += "do";
              var stmt = s as DoWhileStatement;
              var bodyIsEmpty = stmt.Body is EmptyStatement;
              res += (bodyIsEmpty?"":"\r\n{");
              res += GetStatementAsString(stmt.Body, termWithSemiColon);
              res += (bodyIsEmpty?"\r\n":"}\r\n");
              res += "while(";
              res += GetExprAsString(stmt.Test);
              res += ");\r\n";
              return res;
            }
            if (s is WithStatement)
            {
              res += "with(";
              var stmt = s as WithStatement;
              res += GetExprAsString(stmt.Object);
              res += ")\r\n";
              var bodyIsEmpty = stmt.Body is EmptyStatement;
              res += (bodyIsEmpty ? "" : "{\r\n");
              res += GetStatementAsString(stmt.Body, termWithSemiColon);
              res += (bodyIsEmpty ? "\r\n" : "}\r\n");
              return res;
            }
            if (s is WhileStatement)
            {
              res += "while(";
              var stmt = s as WhileStatement;
              res += GetExprAsString(stmt.Test);
              var bodyIsEmpty = stmt.Body is EmptyStatement;
              res += ")\r\n";
              res += (bodyIsEmpty ? "" : "{\r\n");
              res += GetStatementAsString(stmt.Body, termWithSemiColon);
              res += (bodyIsEmpty ? "\r\n" : "}\r\n");
              return res;
            }
            if (s is ReturnStatement)
            {
              res += "return ";
              var stmt = s as ReturnStatement;
              res += GetExprAsString(stmt.Argument);
              res += ";\r\n";
              return res;
            }
            if (s is ForInStatement)
            {
              res += "for(";
              var stmt = s as ForInStatement;
              res += GetSyntaxNodeAsString(stmt.Left, false);
              res += " in ";
              res += GetExprAsString(stmt.Right);
              var bodyIsEmpty = stmt.Body is EmptyStatement;
              res += ")\r\n";
              res += (bodyIsEmpty ? "" : "{\r\n");
              res += GetStatementAsString(stmt.Body, termWithSemiColon);
              res += (bodyIsEmpty ? "\r\n" : "}\r\n");
              return res;
            }
            if (s is ForStatement)
            {
                res += "for(";
                var stmt = s as ForStatement;
                res += GetSyntaxNodeAsString(stmt.Init, false);
                res += ";";
                res += GetExprAsString(stmt.Test);
                res += ";";
                res += GetExprAsString(stmt.Update);
                var bodyIsEmpty = stmt.Body is EmptyStatement;
                res += ")\r\n";
                res += (bodyIsEmpty?"":"{\r\n");
                res += GetStatementAsString(stmt.Body, termWithSemiColon);
                res += (bodyIsEmpty?"\r\n":"}\r\n");
                return res;
            }
            if (s is IfStatement)
            {
                res += "if(";
                var stmt = s as IfStatement;
                res += GetExprAsString(stmt.Test);
                var consequentIsEmpty = stmt.Consequent is EmptyStatement;
                res += ")\r\n";
                res+= consequentIsEmpty?"\r\n":"{\r\n";
                res += GetStatementAsString(stmt.Consequent, termWithSemiColon);
                res += consequentIsEmpty ? "\r\n" : "}\r\n";
                if (stmt.Alternate != null)
                {
                  var alternateIsEmpty = stmt.Alternate is EmptyStatement;
                  res += "else\r\n";
                  res += alternateIsEmpty?"\r\n":"{\r\n";
                  res += GetStatementAsString(stmt.Alternate, termWithSemiColon);
                  res += alternateIsEmpty?"\r\n":"}\r\n";
                }
                return res;
            }
            if (s is BlockStatement)
            {
                var stmt = s as BlockStatement;
                res += GetStatementAsString(stmt.Body, termWithSemiColon);
                return res;
            }
            if (s is FunctionDeclaration)
            {
                var stmt = s as FunctionDeclaration;
                res += "function ";
                res += GetExprAsString(stmt.Id);
                res += "(";
                res += GetExprAsString(stmt.Parameters);
                res += ")\r\n";
                res += "{\r\n";
                res += GetStatementAsString(stmt.Body, termWithSemiColon);
                res += "}\r\n";
                //foreach (FunctionDeclaration d in v.FunctionDeclarations)
                //{
                //    res += GetStatementAsString(d, termWithSemiColon);
                //}
                //foreach (VariableDeclaration d in v.VariableDeclarations)
                //{
                //    res += GetStatementAsString(d, termWithSemiColon);
                //}
                return res;

            }
            if (s is VariableDeclaration)
            {
                var stmt = s as VariableDeclaration;
                decimal index = 1;
                var decls = stmt.Declarations as List<VariableDeclarator>;
                if (decls != null)
                {
                    res = "var ";
                    foreach (VariableDeclarator d in decls)
                    {
                        res += GetExprAsString(d) + (decls.Count > 1 && index < decls.Count ? ", " : "");
                        ++index;
                    }
                    return res + (termWithSemiColon ? ";\r\n" : "");
                }
            }
            if (s is ExpressionStatement)
            {
                var stmt = s as ExpressionStatement;
                return GetExprAsString(stmt.Expression) + (termWithSemiColon ? ";\r\n" : "");
            }
            if (s is Program)
            {
                var stmt = s as Program;
                foreach (Statement prgStmt in stmt.Body)
                {
                    res += GetStatementAsString(prgStmt, termWithSemiColon);
                }
                return res;
            }
            return null;
        }

        private static string GetSyntaxNodeAsString(SyntaxNode node, bool termWithSemiColon)
        {
            if (node is Expression)
            {
                return GetExprAsString(node as Expression);
            }
            if (node is Statement)
            {
                return GetStatementAsString(node as Statement, termWithSemiColon);
            }
            return "";
        }

        private static string GetStatementAsString(IEnumerable<Statement> body, bool termWithSemiColon)
        {
            string res = "";
            if (body != null)
            {
                foreach (Statement statement in body)
                {
                    res += GetStatementAsString(statement, termWithSemiColon);
                }
            }
            return res;
        }

      public List<SyntaxNode> GetAstList(Program prg)
      {
        _astList = new List<SyntaxNode>();
        this.ExpressionWalkEvent += GetExpressionListEvent;
        this.StatementWalkEvent += GetStatementListEvent;
        WalkStatement(prg);
        this.ExpressionWalkEvent -= GetExpressionListEvent;
        this.StatementWalkEvent -= GetStatementListEvent;

        return _astList;
      }

      private void GetStatementListEvent(object sender, StatementWalkEventDelegateArgs args)
      {
        _astList.Add(args.Stmt);
      }

      private void GetExpressionListEvent(object sender, ExpressionWalkEventDelegateArgs args)
      {
        _astList.Add(args.Expr);
      }

      public Identifier GetAstIdentifierAt(Program prg, int line, int column)
      {
        if (prg == null)
        {
          return null;
        }
        var astList = GetAstList(prg);
        foreach (var syntaxNode in astList)
        {
          if (syntaxNode.Location.Start.Column <= column
            && syntaxNode.Location.End.Column >= column
            && syntaxNode.Location.Start.Line <= line
            && syntaxNode.Location.End.Line >= line
            && syntaxNode is Identifier
            )
          {
            return syntaxNode as Identifier;
          }
        }
        return null;
      }
    }

    public delegate void ExpressionWalkEventDelegate(object sender, ExpressionWalkEventDelegateArgs args);

    public class ExpressionWalkEventDelegateArgs
    {
        public Expression Expr { get; set; }
    }

    public delegate void StatementWalkEventDelegate(object sender, StatementWalkEventDelegateArgs args);

    public class StatementWalkEventDelegateArgs
    {
        public Statement Stmt { get; set; }
    }
}