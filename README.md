[![Build](https://github.com/sebastienros/jint/actions/workflows/build.yml/badge.svg)](https://github.com/sebastienros/jint/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Jint.svg)](https://www.nuget.org/packages/Jint)
[![NuGet](https://img.shields.io/nuget/vpre/Jint.svg)](https://www.nuget.org/packages/Jint)
<!-- [![MyGet](https://img.shields.io/myget/jint/vpre/jint.svg)](https://www.myget.org/feed/Packages/Jint) -->
[![Join the chat at https://gitter.im/sebastienros/jint](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/sebastienros/jint)

# Jint

Jint is a __Javascript interpreter__ for .NET which can run on __any modern .NET platform__ as it supports .NET Standard 2.0 and .NET 4.6.1 targets (and up). Because Jint neither generates any .NET bytecode nor uses the DLR it runs relatively small scripts really fast.

💡 You should prefer 3.x beta over the 2.x legacy version as all new features and improvements are targeted against version 3.x.

## ECMAScipt Features

### Version 2.x

-  ✔ Full support for [ECMAScript 5.1 (ES5)](http://www.ecma-international.org/ecma-262/5.1/)
- .NET Interoperability 

### Version 3.x

The entire execution engine was rebuild with performance in mind, in many cases at least twice as fast as the old engine.  All the features of 2.x and more:

#### ECMAScript 2015 (ES6)

- ✔ Arrow function expression
- ✔ Class support
- ✔ Enhanced object literals
- ✔ Template strings
- ✔ Destructuring
- ✔ Default, rest and spread
- ✔ Lexical scoping of variables (let and const)
- ✔ `for...of`
- ✔ Map and Set
- ✔ Proxies
- ✔ Symbols
- ✔ Reflect
- ✔ Binary and octal literals
- ❌ Generators
- ❌ Unicode
- ❌ Modules and module loaders
- ✔ Weakmap and Weakset
- ✔ Promises (Experimental, API is unstable)
- ❌ Tail calls

#### ECMAScript 2016

- ✔ Block-scoping of variables and functions
- ✔ Destructuring patterns (of variables)
- ✔ Exponentiation operator `**`
- ✔ `Array.prototype.includes`
- ❌ `await`, `async`

####  ECMAScript 2017

- ✔ `Object.values`, `Object.entries` and `Object.getOwnPropertyDescriptors`

#### ECMAScript 2018

- ✔ Rest/spread operators for object literals (`...identifier`),
- ✔ `Promise.prototype.finally`

#### ECMAScript 2019

- ✔ `Array.prototype.flat`, `Array.prototype.flatMap`

#### ECMAScript 2020

- ✔ Nullish coalescing operator (`??`)
- ✔ `globalThis` object
- ❌ BigInt

#### Other

- Further refined .NET CLR interop capabilities
- Constraints for execution (recursion, memory usage, duration)

> Follow new features as they are being implemented, see https://github.com/sebastienros/jint/issues/343

## Discussion

Join the chat on [Gitter](https://gitter.im/sebastienros/jint) or post your questions with the `jint` tag on [stackoverflow](http://stackoverflow.com/questions/tagged/jint).

## Video

Here is a short video of how Jint works and some sample usage

https://channel9.msdn.com/Shows/Code-Conversations/Sebastien-Ros-on-jint-a-Javascript-Interpreter-for-NET


## Examples

This example defines a new value named `log` pointing to `Console.WriteLine`, then runs
a script calling `log('Hello World!')`. 

```c#
var engine = new Engine()
    .SetValue("log", new Action<object>(Console.WriteLine));
    
engine.Execute(@"
    function hello() { 
        log('Hello World');
    };
 
    hello();
");
```

Here, the variable `x` is set to `3` and `x * x` is evaluated in JavaScript. The result is returned to .NET directly, in this case as a `double` value `9`. 
```c#
var square = new Engine()
    .SetValue("x", 3) // define a new variable
    .Evaluate("x * x") // evaluate a statement
    .ToObject(); // converts the value to .NET
```

You can also directly pass POCOs or anonymous objects and use them from JavaScript. In this example for instance a new `Person` instance is manipulated from JavaScript. 
```c#
var p = new Person {
    Name = "Mickey Mouse"
};

var engine = new Engine()
    .SetValue("p", p)
    .Execute("p.Name = 'Minnie'");

Assert.AreEqual("Minnie", p.Name);
```

You can invoke JavaScript function reference
```c#
var add = new Engine()
    .Execute("function add(a, b) { return a + b; }")
    .GetValue("add");

add.Invoke(1, 2); // -> 3
```
or directly by name 
```c#
var engine = new Engine()
   .Execute("function add(a, b) { return a + b; }");

engine.Invoke("add", 1, 2); // -> 3
```
## Accessing .NET assemblies and classes

You can allow an engine to access any .NET class by configuring the engine instance like this:
```c#
var engine = new Engine(cfg => cfg.AllowClr());
```

Then you have access to the `System` namespace as a global value. Here is how it's used in the context on the command line utility:
```javascript
jint> var file = new System.IO.StreamWriter('log.txt');
jint> file.WriteLine('Hello World !');
jint> file.Dispose();
```
And even create shortcuts to common .NET methods
```javascript
jint> var log = System.Console.WriteLine;
jint> log('Hello World !');
=> "Hello World !"
```

When allowing the CLR, you can optionally pass custom assemblies to load types from. 
```c#
var engine = new Engine(cfg => cfg
    .AllowClr(typeof(Bar).Assembly)
);
```

and then to assign local namespaces the same way `System` does it for you, use `importNamespace`
```javascript
jint> var Foo = importNamespace('Foo');
jint> var bar = new Foo.Bar();
jint> log(bar.ToString());
```    

adding a specific CLR type reference can be done like this
```csharp
engine.SetValue("TheType", TypeReference.CreateTypeReference(engine, typeof(TheType)))
```

and used this way
```javascript
jint> var o = new TheType();
```

Generic types are also supported. Here is how to declare, instantiate and use a `List<string>`:
```javascript
jint> var ListOfString = System.Collections.Generic.List(System.String);
jint> var list = new ListOfString();
jint> list.Add('foo');
jint> list.Add(1); // automatically converted to String
jint> list.Count; // 2
```

## Internationalization

You can enforce what Time Zone or Culture the engine should use when locale JavaScript methods are used if you don't want to use the computer's default values.

This example forces the Time Zone to Pacific Standard Time.
```c#
var PST = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
var engine = new Engine(cfg => cfg.LocalTimeZone(PST));
    
engine.Execute("new Date().toString()"); // Wed Dec 31 1969 16:00:00 GMT-08:00
```

This example is using French as the default culture.
```c#
var FR = CultureInfo.GetCultureInfo("fr-FR");
var engine = new Engine(cfg => cfg.Culture(FR));
    
engine.Execute("new Number(1.23).toString()"); // 1.23
engine.Execute("new Number(1.23).toLocaleString()"); // 1,23
```

## Execution Constraints 

Execution constraints are used during script execution to ensure that requirements around resource consumption are met, for example:

* Scripts should not use more than X memory.
* Scripts should only run for a maximum amount of time.

You can configure them via the options:

```c#
var engine = new Engine(options => {

    // Limit memory allocations to MB
    options.LimitMemory(4_000_000);

    // Set a timeout to 4 seconds.
    options.TimeoutInterval(TimeSpan.FromSeconds(4));

    // Set limit of 1000 executed statements.
    options.MaxStatements(1000);

    // Use a cancellation token.
    options.CancellationToken(cancellationToken);
}
```

You can also write a custom constraint by implementing the `IConstraint` interface:

```c#
public interface IConstraint
{
    /// Called before a script is run and useful when you us an engine object for multiple executions.
    void Reset();

    // Called before each statement to check if your requirements are met.
    void Check();
}
```

For example we can write a constraint that stops scripts when the CPU usage gets too high:

```c#
class MyCPUConstraint : IConstraint
{
    public void Reset()
    {
    }

    public void Check()
    {
        var cpuUsage = GetCPUUsage();

        if (cpuUsage > 0.8) // 80%
        {
            throw new OperationCancelledException();
        }
    }
}

var engine = new Engine(options =>
{
    options.Constraint(new MyCPUConstraint());
});
```

When you reuse the engine you want to use cancellation tokens you have to reset the token before each call of `Execute`:

```c#
var constraint = new CancellationConstraint();

var engine = new Engine(options =>
{
    options.Constraint(constraint);
});

for (var i = 0; i < 10; i++) 
{
    using (var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
    {
        constraint.Reset(tcs.Token);

        engine.SetValue("a", 1);
        engine.Execute("a++");
    }
}
```

## .NET Interoperability

- Manipulate CLR objects from JavaScript, including:
  - Single values
  - Objects
    - Properties
    - Methods
  - Delegates
  - Anonymous objects
- Convert JavaScript values to CLR objects
  - Primitive values
  - Object -> expando objects (`IDictionary<string, object>` and dynamic)
  - Array -> object[]
  - Date -> DateTime
  - number -> double
  - string -> string
  - boolean -> bool
  - Regex -> RegExp
  - Function -> Delegate
- Extensions methods

## Security

The following features provide you with a secure, sand-boxed environment to run user scripts.

- Define memory limits, to prevent allocations from depleting the memory.
- Enable/disable usage of BCL to prevent scripts from invoking .NET code.
- Limit number of statements to prevent infinite loops.
- Limit depth of calls to prevent deep recursion calls.
- Define a timeout, to prevent scripts from taking too long to finish.

Continuous Integration kindly provided by  [AppVeyor](https://www.appveyor.com)

## Branches and releases

- The recommended branch is __dev__, any PR should target this branch
- The __dev__ branch is automatically built and published on [Myget](https://www.myget.org/feed/Packages/jint). Add this feed to your NuGet sources to use it: https://www.myget.org/F/jint/api/v3/index.json
- The __dev__ branch is occasionally merged to __master__ and published on [NuGet](https://www.nuget.org/packages/jint)
- The 3.x releases have more features (from es6) and is faster than the 2.x ones. They run the same test suite so they are as reliable. For instance [RavenDB](https://github.com/ravendb/ravendb) is using the 3.x version.
- The 3.x versions are marked as _beta_ as they might get breaking changes while es6 features are added.
