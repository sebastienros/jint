[![Build status](https://ci.appveyor.com/api/projects/status/xh2lsliy6usk60o5?svg=true)](https://ci.appveyor.com/project/SebastienRos/jint)
[![NuGet](https://img.shields.io/nuget/v/Jint.svg)](https://www.nuget.org/packages/Jint)
[![MyGet](https://img.shields.io/myget/jint/v/jint.svg)](https://www.myget.org/feed/Packages/jint)
[![Join the chat at https://gitter.im/sebastienros/jint](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/sebastienros/jint)

# Jint

Jint is a __Javascript interpreter__ for .NET which provides full __ECMA 5.1__ compliance and can run on __any .NET platform__. Because it doesn't generate any .NET bytecode nor use the DLR it runs relatively small scripts faster. It's available as a PCL on Nuget at https://www.nuget.org/packages/Jint.

# Features

- Full support for ECMAScript 5.1 - http://www.ecma-international.org/ecma-262/5.1/
- .NET Portable Class Library - http://msdn.microsoft.com/en-us/library/gg597391(v=vs.110).aspx
- .NET Interoperability 

> ECMAScript 6.0 currently being implemeted, see https://github.com/sebastienros/jint/issues/343

# Discussion

Join the chat on [Gitter](https://gitter.im/sebastienros/jint) or post your questions with the `jint` tag on [stackoverflow](http://stackoverflow.com/questions/tagged/jint).

# Examples

This example defines a new value named `log` pointing to `Console.WriteLine`, then executes 
a script calling `log('Hello World!')`. 
```c#
    var engine = new Engine()
        .SetValue("log", new Action<object>(Console.WriteLine))
        ;
    
    engine.Execute(@"
      function hello() { 
        log('Hello World');
      };
      
      hello();
    ");
```
Here, the variable `x` is set to `3` and `x * x` is executed in JavaScript. The result is returned to .NET directly, in this case as a `double` value `9`. 
```c#
    var square = new Engine()
        .SetValue("x", 3) // define a new variable
        .Execute("x * x") // execute a statement
        .GetCompletionValue() // get the latest statement completion value
        .ToObject() // converts the value to .NET
        ;
```
You can also directly pass POCOs or anonymous objects and use them from JavaScript. In this example for instance a new `Person` instance is manipulated from JavaScript. 
```c#
    var p = new Person {
        Name = "Mickey Mouse"
    };

    var engine = new Engine()
        .SetValue("p", p)
        .Execute("p.Name = 'Minnie'")
        ;
    Assert.AreEqual("Minnie", p.Name);
```
You can invoke JavaScript function reference
```c#
    var add = new Engine()
        .Execute("function add(a, b) { return a + b; }")
        .GetValue("add")
        ;

    add.Invoke(1, 2); // -> 3
```
or directly by name 
```c#
    var engine = new Engine()
        .Execute("function add(a, b) { return a + b; }")
        ;

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

## Implemented features:

### ECMAScript 5.1

- Complete implementation
  - ECMAScript 5.1 test suite (http://test262.ecmascript.org/) 

### ECMAScript 6.0

ES6 features which are being implemented:
- [x] [arrows](https://github.com/lukehoban/es6features/blob/master/README.md#arrows)
- [ ] [classes](https://github.com/lukehoban/es6features/blob/master/README.md#classes)
- [x] [enhanced object literals](https://github.com/lukehoban/es6features/blob/master/README.md#enhanced-object-literals)
- [x] [template strings](https://github.com/lukehoban/es6features/blob/master/README.md#template-strings)
- [x] [destructuring](https://github.com/lukehoban/es6features/blob/master/README.md#destructuring)
- [x] [default + rest + spread](https://github.com/lukehoban/es6features/blob/master/README.md#default--rest--spread)
- [ ] [let + const](https://github.com/lukehoban/es6features/blob/master/README.md#let--const)
- [x] [iterators + for..of](https://github.com/lukehoban/es6features/blob/master/README.md#iterators--forof)
- [ ] [generators](https://github.com/lukehoban/es6features/blob/master/README.md#generators)
- [ ] [unicode](https://github.com/lukehoban/es6features/blob/master/README.md#unicode)
- [ ] [modules](https://github.com/lukehoban/es6features/blob/master/README.md#modules)
- [ ] [module loaders](https://github.com/lukehoban/es6features/blob/master/README.md#module-loaders)
- [x] [map + set](https://github.com/lukehoban/es6features/blob/master/README.md#map--set--weakmap--weakset)
- [ ] [weakmap + weakset](https://github.com/lukehoban/es6features/blob/master/README.md#map--set--weakmap--weakset)
- [ ] [proxies](https://github.com/lukehoban/es6features/blob/master/README.md#proxies)
- [x] [symbols](https://github.com/lukehoban/es6features/blob/master/README.md#symbols)
- [ ] [subclassable built-ins](https://github.com/lukehoban/es6features/blob/master/README.md#subclassable-built-ins)
- [ ] [promises](https://github.com/lukehoban/es6features/blob/master/README.md#promises)
- [x] [math APIs](https://github.com/lukehoban/es6features/blob/master/README.md#math--number--string--array--object-apis)
- [x] [number APIs](https://github.com/lukehoban/es6features/blob/master/README.md#math--number--string--array--object-apis)
- [x] [string APIs](https://github.com/lukehoban/es6features/blob/master/README.md#math--number--string--array--object-apis)
- [x] [array APIs](https://github.com/lukehoban/es6features/blob/master/README.md#math--number--string--array--object-apis)
- [ ] [object APIs](https://github.com/lukehoban/es6features/blob/master/README.md#math--number--string--array--object-apis)
- [x] [binary and octal literals](https://github.com/lukehoban/es6features/blob/master/README.md#binary-and-octal-literals)
- [ ] [reflect api](https://github.com/lukehoban/es6features/blob/master/README.md#reflect-api)
- [ ] [tail calls](https://github.com/lukehoban/es6features/blob/master/README.md#tail-calls)

### .NET Interoperability

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

### Security

The following features provide you with a secure, sand-boxed environment to run user scripts.

- Define memory limits, to prevent allocations from depleting the memory.
- Enable/disable usage of BCL to prevent scripts from invoking .NET code.
- Limit number of statements to prevent infinite loops.
- Limit depth of calls to prevent deep recursion calls.
- Define a timeout, to prevent scripts from taking too long to finish.

Continuous Integration kindly provided by  [AppVeyor](https://www.appveyor.com)

### Branches and releases

- The recommended branch is __dev__, any PR should target this branch
- The __dev__ branch is automatically built and published on [Myget](https://www.myget.org/feed/Packages/jint)
- The __dev__ branch is occasionally merged to __master__ and published on [NuGet](https://www.nuget.org/feed/Packages/jint)
- The 3.x releases have more features (from es6) and is faster than the 2.x ones. They run the same test suite so they are as reliable. For instance [RavenDB](https://github.com/ravendb/ravendb) is using the 3.x version.
- The 3.x versions are marked as _beta_ as they might get breaking changes while es6 features are added. 
