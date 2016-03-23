using System.Collections.Generic;
using Jint.Parser;
using Jint.Parser.Ast;
using Jint.Walker;
using Xunit;

namespace Jint.Tests
{
    public class WalkerTests
    {
        private string _prgText = "";
        private int _idCnt;
        private int _ifCnt;
        private int _forCnt;
        private int _funCnt;
        private int _varDeclCnt;
        
        private void DoWalkerTestEvents(string script)
        {
            _funCnt = 0;
            _varDeclCnt = 0;
            var w = new JintWalker();
            w.ExpressionWalkEvent += DoExpressionWalkEvent;
            w.StatementWalkEvent += DoStatementWalkEvent;
            var prg = Engine.Compile(script, new ParserOptions { Source = "script" });
            w.WalkStatement(prg);
        }
        private void DoWalkerTest(string script)
        {
            var prg = Engine.Compile(script, new ParserOptions { Source = "script" });
            _prgText += JintWalker.GetStatementAsString(prg, true);
        }

        void DoStatementWalkEvent(object sender, StatementWalkEventDelegateArgs args)
        {
            var s = args.Stmt;
            if (s is FunctionDeclaration)
            {
                ++_funCnt;
            }
            if (s is ForStatement)
            {
                ++_forCnt;
            }
            if (s is IfStatement)
            {
                ++_ifCnt;
            }
            //Console.WriteLine(JintWalker.GetStatementAsString(s, true));
            //Console.WriteLine(s);
        }

        void DoExpressionWalkEvent(object sender, ExpressionWalkEventDelegateArgs args)
        {
            var e = args.Expr;
            if (e is FunctionExpression)
            {
                ++_funCnt;
            }
            if (e is VariableDeclarator)
            {
                ++_varDeclCnt;
            }
            if (e is Identifier)
            {
                ++_idCnt;
                //Console.WriteLine((e as Identifier).Name);
            }
            //Console.WriteLine(JintWalker.GetExprAsString(e));
            //Console.WriteLine(e);
        }


        [Fact]
        public void WalkVarDeclLit()
        {
            const string script = @"
var a = 123;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclVar()
        {
            const string script = @"
var a = 123;
var b = a;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclStmt()
        {
            const string script = @"
var a = 123;
var b = a + 10;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclExpr2()
        {
            const string script = @"
var a = 123;
var b = a + (10 * a);
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclMultiple()
        {
            const string script = @"
var a = 123, b, c = 456;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclString()
        {
            const string script = @"
var a = 'hello';
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclNewArray()
        {
            const string script = @"
var a = new Array();
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclNewObjectWithArgs()
        {
            const string script = @"
var a = new MyObject(a, b, c + 10);
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclNewArray2()
        {
            const string script = @"
var a = [];
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkAssignExpr()
        {
            const string script = @"
a = 123;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkAssignExprUnary()
        {
            const string script = @"
a = -123;
a = ++b;
a = b++;
a = !c;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }
        [Fact]
        public void WalkAssignExprLogical()
        {
            const string script = @"
a = true || c;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }


        [Fact]
        public void WalkAssignExpr2()
        {
          const string script = @"
a = 123 + (b / c);
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkAssignExpr3()
        {
          const string script = @"
a = (123 + b) / c;
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkAssignExpr4()
        {
          const string script = @"
a = (((123 + b) / c) + (x / y)) + 55;
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkDeclFunction()
        {
          const string script = @"
function test()
{
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkDeclFunctionWithReturn()
        {
          const string script = @"
function test()
{
return 'test';
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkCallFunction()
        {
            const string script = @"
test(a, b);
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkMemeberExpr()
        {
            const string script = @"
a = c.b + 3;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkThisExpr()
        {
            const string script = @"
a = this.b + 3;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkSequenceExpr()
        {
            const string script = @"
a = bar(), foo();
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarRegEx()
        {
            const string script = @"
var re = /[.*+?^${}()|[\]\\]/g;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarCondExpr()
        {
            const string script = @"
var re = a >= b ? 1 : 2;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarFuncExpr()
        {
            const string script = @"
var re = function () { };
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarObjectExpr()
        {
            const string script = @"
var o = { a: 'foo', b: 42, c: {  } };
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarObjectExpr2()
        {
            const string script = @"
var o = { c: {  } };
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarPostfixUpdate()
        {
            const string script = @"
var o = a++;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarPrefixUpdate()
        {
            const string script = @"
var o = ++a;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkVarDeclExpr()
        {
            const string script = @"
o = 1;
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkIfStmt()
        {
            const string script = @"
if(a > 10)
{
var b = 30;
}
else
{
var c = 10;
}
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkIfStmt2()
        {
            const string script = @"
if(a > 10)
{
var b = 30;
}
else
{
if(d > 5)
{
var c = 10;
}
}
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkForStmt()
        {
            const string script = @"
for(var i = 0;i < 10;i++)
{
var a = 0;
}
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkForInStmt()
        {
            const string script = @"
var obj = { a: 1, b: 2, c: 3 };
for(var prop in obj)
{
console.log((('o.' + prop) + ' = ') + obj[prop]);
}
";
            _prgText = "\r\n";
            DoWalkerTest(script);
            Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkSetDecl()
        {
          const string script = @"
var o = { 
  set current(str) 
  { 
    this.log[this.log.length] = str;
  }, log: [] 
};
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkGetDecl()
        {
          const string script = @"
var log = ['test'];
var obj = {
  get latest () {
    if (log.length == 0) 
    {
      return undefined;
    }
    return log[log.length - 1];
  }
};
console.log (obj.latest);
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkDeletePropety()
        {
          const string script = @"
delete obj.latest;
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkWhileStmt()
        {
          const string script = @"
var n = 0;
var x = 0;

while (n < 3) {
  n++;
  x += n;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkWithStmt()
        {
          const string script = @"
function f(x, o) {
  with (o) { print(x); }
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkWithStmt2()
        {
          const string script = @"
var a, x, y;
var r = 10;

with (Math) {
  a = (PI * r) * r;
  x = r * cos(PI);
  y = r * sin(PI / 2);
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkWhileStmtWhithBreak()
        {
          const string script = @"
var n = 0;
var x = 0;

while (n < 3) {
  n++;
  x += n;
  break;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkWhileStmtWhithBreakLabel()
        {
          const string script = @"
outer_block: {
  inner_block: {
    console.log('1');
    break outer_block;
    console.log(':-(');
  }
  console.log('2');
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkDoWhileStmt()
        {
          const string script = @"
var i = 0;
do {
   i += 1;
   console.log(i);
} while (i < 5);
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkContinueStmt()
        {
          const string script = @"
var i = 0;
var n = 0;

while (i < 5) {
  i++;

  if (i === 3) {
    continue;
  }

  n += i;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkContinueStmtWithLabel()
        {
          const string script = @"
var i = 0;
var j = 8;

checkiandj: { while (i < 4) {
  console.log('i: ' + i);
  i += 1;

  checkj: { while (j > 4) {
    console.log('j: '+ j);
    j -= 1;

    if ((j % 2) == 0) {
      continue checkj;
    }
    console.log(j + ' is odd.');
    }
  }
  }
  console.log('i = ' + i);
  console.log('j = ' + j);
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkDebuggerStmt()
        {
          const string script = @"
function potentiallyBuggyCode() {
    debugger;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkEmptyForStmt()
        {
          const string script = @"
for (i = 0; i < arr.length; arr[i++] = 0);
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkComplexIfStmt()
        {
          const string script = @"
if(one){doOne();}else{if(two){doTwo();}else{if(three){doThree();}else{if(four){doFour();}else{launchRocket();}}}}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkEmptyIfStmt()
        {
          const string script = @"
 if(one){doOne();}else{if(two){doTwo();}else{if(three);else{if(four){doFour();}else{launchRocket();}}}}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkSwitchStmt()
        {
          const string script = @"
switch (expr) {
  case 'Oranges':
    console.log('Oranges are $0.59 a pound.');
    break;
  case 'Apples':
    console.log('Apples are $0.32 a pound.');
    break;
  case 'Bananas':
    console.log('Bananas are $0.48 a pound.');
    break;
  case 'Cherries':
    console.log('Cherries are $3.00 a pound.');
    break;
  case 'Mangoes':
  case 'Papayas':
    console.log('Mangoes and papayas are $2.79 a pound.');
    break;
  default:
    console.log(('Sorry, we are out of ' + expr) + '.');
}

";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkThrowStmt()
        {
          const string script = @"
throw 'Error2';
throw 42;
throw true;
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkTryCatchStmt()
        {
          const string script = @"
try {
   throw 'myException';
}
catch (e) {
   logMyErrors(e);
}
  ";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkTryFinallyStmt()
        {
          const string script = @"
openMyFile();
try {
   writeMyFile(theData);
}
finally {
   closeMyFile();
}  ";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkFunDeclWithVarDecl()
        {
          const string script = @"
function test()
{
var i = 0;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script, _prgText);
        }

        [Fact]
        public void WalkAccessNsieve()
        {
          const string script = @"
function pad(number,width){
   var s = number.toString();
   var prefixWidth = width - s.length;
   if (prefixWidth>0){
      for (var i=1; i<=prefixWidth; i++) { s = ' ' + s; }
   }
   return s;
}

function nsieve(m, isPrime){
   var i, k, count;

   for (i=2; i<=m; i++) { isPrime[i] = true; }
   count = 0;

   for (i=2; i<=m; i++){
      if (isPrime[i]) {
         for (k=i+i; k<=m; k+=i) { isPrime[k] = false; }
         count++;
      }
   }
   return count;
}

function sieve() {
    var sum = 0;
    for (var i = 1; i <= 3; i++ ) {
        var m = (1<<i)*10000;
        var flags = Array(m+1);
        sum += nsieve(m, flags);
    }
    return sum;
}

var result = sieve();

var expected = 14302;
if (result != expected)
{
    throw (('ERROR: bad result: expected ' + expected) + ' but got ') + result;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkControlFlowRecursive()
        {
          const string script = @"
function ack(m,n){
   if (m==0) { return n+1; }
   if (n==0) { return ack(m-1,1); }
   return ack(m-1, ack(m,n-1) );
}

function fib(n) {
    if (n < 2){ return 1; }
    return fib(n-2) + fib(n-1);
}

function tak(x,y,z) {
    if (y >= x) {return z;}
    return tak(tak(x-1,y,z), tak(y-1,z,x), tak(z-1,x,y));
}

var result = 0;

for ( var i = 3; i <= 5; i++ ) {
    result += ack(3,i);
    result += fib(17.0+i);
    result += tak((3*i)+3,(2*i)+2,i+1);
}

var expected = 57775;
if (result != expected)
{
    throw (('ERROR: bad result: expected ' + expected) + ' but got ') + result;
}
";
          _prgText = "\r\n";
          DoWalkerTest(script);
          Assert.Equal(script.RemoveWhitespace(), _prgText.RemoveWhitespace());
        }

        [Fact]
        public void WalkControlFlowRecursiveEvents()
        {
          const string script = @"
function ack(m,n){
   if (m==0) { return n+1; }
   if (n==0) { return ack(m-1,1); }
   return ack(m-1, ack(m,n-1) );
}

function fib(n) {
    if (n < 2){ return 1; }
    return fib(n-2) + fib(n-1);
}

function tak(x,y,z) {
    if (y >= x) {return z;}
    return tak(tak(x-1,y,z), tak(y-1,z,x), tak(z-1,x,y));
}

var result = 0;

for ( var i = 3; i <= 5; i++ ) {
    result += ack(3,i);
    result += fib(17.0+i);
    result += tak((3*i)+3,(2*i)+2,i+1);
}

var expected = 57775;
if (result != expected)
{
    throw (('ERROR: bad result: expected ' + expected) + ' but got ') + result;
}
";
          _prgText = "\r\n";
          DoWalkerTestEvents(script);
          Assert.Equal(3,_funCnt);
          Assert.Equal(3, _varDeclCnt);
          Assert.Equal(1, _forCnt);
          Assert.Equal(5, _ifCnt);
          Assert.Equal(51, _idCnt);

        }

// would pass if we allow return in non function
//        [Fact]
//        public void WalkIdsInIfEvents()
//        {
//            const string script = @"
//   if (m==0) { return n+1; }
//";
//            _prgText = "\r\n";
//            DoWalkerTestEvents(script);
//            Assert.Equal(2, _idCnt);
//            Assert.Equal(1, _ifCnt);

//        }

        [Fact]
        public void WalkIdsInForEvents()
        {
          const string script = @"
for ( var i = 3; i <= 5; i++ ) {
    result += ack(3,i);
    result += fib(17.0+i);
    result += tak((3*i)+3,(2*i)+2,i+1);
}
";
          _prgText = "\r\n";
          DoWalkerTestEvents(script);
          Assert.Equal(14, _idCnt);
          Assert.Equal(1, _forCnt);

        }
        [Fact]
        public void TestGetAstFromOffset()
        {
          const string script = @"
var aaa = 1.01;
var bbb = 2.01;
aaa = bbb + 2;
";
          var w = new JintWalker();
          var prg = Engine.Compile(script, new ParserOptions { Source = "script" });
          Identifier astNode = w.GetAstIdentifierAt(prg, 4, 1);
          Assert.Equal(astNode != null, true);
          Assert.Equal(astNode.Name, "aaa");
          astNode = w.GetAstIdentifierAt(prg, 4, 8);
          Assert.Equal(astNode != null, true);
          Assert.Equal(astNode.Name, "bbb");
        }
    }
}