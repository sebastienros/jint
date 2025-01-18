[![Build](https://github.com/sebastienros/jint/actions/workflows/build.yml/badge.svg)](https://github.com/sebastienros/jint/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Jint.svg)](https://www.nuget.org/packages/Jint)
[![NuGet](https://img.shields.io/nuget/vpre/Jint.svg)](https://www.nuget.org/packages/Jint)
[![MyGet](https://img.shields.io/myget/jint/vpre/jint.svg?label=MyGet)](https://www.myget.org/feed/jint/package/nuget/Jint)
[![Join the chat at https://gitter.im/sebastienros/jint](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/sebastienros/jint)

# Jint

Jint is a __Javascript interpreter__ for .NET which can run on __any modern .NET platform__ as it supports .NET Standard 2.0 and .NET 4.6.2 targets (and later).

## Use cases and users

- Run JavaScript inside your .NET application in a safe sand-boxed environment
- Expose native .NET objects and functions to your JavaScript code (get database query results as JSON, call .NET methods, etc.)
- Support scripting in your .NET application, allowing users to customize your application using JavaScript (like Unity games) 

Some users of Jint include 
[RavenDB](https://github.com/ravendb/ravendb), 
[EventStore](https://github.com/EventStore/EventStore), 
[OrchardCore](https://github.com/OrchardCMS/OrchardCore), 
[ELSA Workflows](https://github.com/elsa-workflows/elsa-core),
[docfx](https://github.com/dotnet/docfx), 
[JavaScript Engine Switcher](https://github.com/Taritsyn/JavaScriptEngineSwitcher),
and many more.

## Supported features

#### ECMAScript 2015 (ES6)

- ✔ ArrayBuffer
- ✔ Arrow function expression
- ✔ Binary and octal literals
- ✔ Class support
- ✔ DataView
- ✔ Destructuring
- ✔ Default, rest and spread
- ✔ Enhanced object literals
- ✔ `for...of`
- ❌ Generators
- ✔ Template strings
- ✔ Lexical scoping of variables (let and const)
- ✔ Map and Set
- ✔ Modules and module loaders
- ✔ Promises (Experimental, API is unstable)
- ✔ Reflect
- ✔ Proxies
- ✔ Symbols
- ❌ Tail calls
- ✔ Typed arrays
- ✔ Unicode
- ✔ Weakmap and Weakset

#### ECMAScript 2016

- ✔ `Array.prototype.includes`
- ✔ `await`, `async`
- ✔ Block-scoping of variables and functions
- ✔ Exponentiation operator `**`
- ✔ Destructuring patterns (of variables)

####  ECMAScript 2017

- ✔ `Object.values`, `Object.entries` and `Object.getOwnPropertyDescriptors`
- ❌ Shared memory and atomics

#### ECMAScript 2018

- ✔ `Promise.prototype.finally`
- ✔ RegExp named capture groups
- ✔ Rest/spread operators for object literals (`...identifier`)
- ✔ SharedArrayBuffer

#### ECMAScript 2019

- ✔ `Array.prototype.flat`, `Array.prototype.flatMap`
- ✔ `String.prototype.trimStart`, `String.prototype.trimEnd`
- ✔ `Object.fromEntries`
- ✔ `Symbol.description`
- ✔ Optional catch binding

#### ECMAScript 2020

- ✔ `BigInt`
- ✔ `export * as ns from`
- ✔ `for-in` enhancements
- ✔ `globalThis` object
- ✔ `import`
- ✔ `import.meta`
- ✔ Nullish coalescing operator (`??`)
- ✔ Optional chaining
- ✔ `Promise.allSettled`
- ✔ `String.prototype.matchAll`

#### ECMAScript 2021

- ✔ Logical Assignment Operators (`&&=` `||=` `??=`)
- ✔ Numeric Separators (`1_000`)
- ✔ `AggregateError`
- ✔ `Promise.any` 
- ✔ `String.prototype.replaceAll`
- ✔ `WeakRef` 
- ✔ `FinalizationRegistry`

#### ECMAScript 2022

- ✔ Class Fields
- ✔ RegExp Match Indices
- ✔ Top-level await
- ✔ Ergonomic brand checks for Private Fields
- ✔ `.at()`
- ✔ Accessible `Object.prototype.hasOwnProperty` (`Object.hasOwn`)
- ✔ Class Static Block
- ✔ Error Cause

#### ECMAScript 2023

- ✔ Array find from last
- ✔ Change Array by copy
- ✔ Hashbang Grammar
- ✔ Symbols as WeakMap keys

#### ECMAScript 2024

- ✔ ArrayBuffer enhancements - `ArrayBuffer.prototype.resize` and `ArrayBuffer.prototype.transfer`
- ❌ `Atomics.waitAsync` 
- ✔ Ensuring that strings are well-formed - `String.prototype.ensureWellFormed` and `String.prototype.isWellFormed`
- ✔ Grouping synchronous iterables - `Object.groupBy` and `Map.groupBy`
- ✔ `Promise.withResolvers`
- ❌ Regular expression flag `/v`

#### ECMAScript Stage 3 (no version yet)

- ✔ `Error.isError`
- ✔ `Float16Array` (Requires NET 6 or higher)
- ✔ Import attributes
- ✔ JSON modules
- ✔ `Math.sumPrecise`
- ✔ `Promise.try`
- ✔ Set methods (`intersection`, `union`, `difference`, `symmetricDifference`, `isSubsetOf`, `isSupersetOf`, `isDisjointFrom`)
- ✔ `ShadowRealm`
- ✔ `Uint8Array` to/from base64

#### Other

- Further refined .NET CLR interop capabilities
- Constraints for execution (recursion, memory usage, duration)


## Performance

- Because Jint neither generates any .NET bytecode nor uses the DLR it runs relatively small scripts really fast
- If you repeatedly run the same script, you should cache the `Script` or `Module` instance produced by Esprima and feed it to Jint instead of the content string
- You should prefer running engine in strict mode, it improves performance

You can check out [the engine comparison results](Jint.Benchmark), bear in mind that every use case is different and benchmarks might not reflect your real-world usage.

## Discussion

Join the chat on [Gitter](https://gitter.im/sebastienros/jint) or post your questions with the `jint` tag on [stackoverflow](http://stackoverflow.com/questions/tagged/jint).

## Video

Here is a short video of how Jint works and some sample usage

https://docs.microsoft.com/shows/code-conversations/sebastien-ros-on-jint-javascript-interpreter-net

## Thread-safety

Engine instances are not thread-safe and they should not accessed from multiple threads simultaneously. 

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
var result = new Engine()
    .Execute("function add(a, b) { return a + b; }")
    .Invoke("add",1, 2); // -> 3
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
engine.SetValue("TheType", TypeReference.CreateTypeReference<TheType>(engine));
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

    // Limit memory allocations to 4 MB
    options.LimitMemory(4_000_000);

    // Set a timeout to 4 seconds.
    options.TimeoutInterval(TimeSpan.FromSeconds(4));

    // Set limit of 1000 executed statements.
    options.MaxStatements(1000);

    // Use a cancellation token.
    options.CancellationToken(cancellationToken);
}
```

You can also write a custom constraint by deriving from the `Constraint` base class:

```c#
public abstract class Constraint
{
    /// Called before script is run and useful when you use an engine object for multiple executions.
    public abstract void Reset();

    // Called before each statement to check if your requirements are met; if not - throws an exception.
    public abstract void Check();
}
```

For example we can write a constraint that stops scripts when the CPU usage gets too high:

```c#
class MyCPUConstraint : Constraint
{
    public override void Reset()
    {
    }

    public override void Check()
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

When you reuse the engine and want to use cancellation tokens you have to reset the token before each call of `Execute`:

```c#
var engine = new Engine(options =>
{
    options.CancellationToken(new CancellationToken(true));
});

var constraint = engine.Constraints.Find<CancellationConstraint>();

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

## Using Modules

You can use modules to `import` and `export` variables from multiple script files:

```c#
var engine = new Engine(options =>
{
    options.EnableModules(@"C:\Scripts");
})

var ns = engine.Modules.Import("./my-module.js");

var value = ns.Get("value").AsString();
```

By default, the module resolution algorithm will be restricted to the base path specified in `EnableModules`, and there is no package support. However you can provide your own packages in two ways.

Defining modules using JavaScript source code:

```c#
engine.Modules.Add("user", "export const name = 'John';");

var ns = engine.Modules.Import("user");

var name = ns.Get("name").AsString();
```

Defining modules using the module builder, which allows you to export CLR classes and values from .NET:

```c#
// Create the module 'lib' with the class MyClass and the variable version
engine.Modules.Add("lib", builder => builder
    .ExportType<MyClass>()
    .ExportValue("version", 15)
);

// Create a user-defined module and do something with 'lib'
engine.Modules.Add("custom", @"
    import { MyClass, version } from 'lib';
    const x = new MyClass();
    export const result as x.doSomething();
");

// Import the user-defined module; this will execute the import chain
var ns = engine.Modules.Import("custom");

// The result contains "live" bindings to the module
var id = ns.Get("result").AsInteger();
```

Note that you don't need to `EnableModules` if you only use modules created using `Engine.Modules.Add`.

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

## Branches and releases

- The recommended branch is __main__, any PR should target this branch
- The __main__ branch is automatically built and published on [MyGet](https://www.myget.org/feed/Packages/jint). Add this feed to your NuGet sources to use it: https://www.myget.org/F/jint/api/v3/index.json
- The __main__ branch is occasionally published on [NuGet](https://www.nuget.org/packages/jint)
