[![Build status](http://teamcity.codebetter.com/app/rest/builds/buildType:(id:Jint_Master)/statusIcon)](http://teamcity.codebetter.com/project.html?projectId=Jint&tab=projectOverview)

# Jint

Jint is a __Javascript interpreter__ for .NET which provides full __ECMA 5.1__ compliance and can run on __any .NET platform__. Because it doesn't generate any .NET bytecode nor use the DLR it runs relatively small scripts faster. It's available as a PCL on Nuget at https://www.nuget.org/packages/Jint.

# Features

- Full support for ECMAScript 5.1 - http://www.ecma-international.org/ecma-262/5.1/
- .NET Portable Class Library - http://msdn.microsoft.com/en-us/library/gg597391(v=vs.110).aspx
- .NET Interoperability 

# Discussion

[![Join the chat at https://gitter.im/sebastienros/jint](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/sebastienros/jint)

Or post your questions with the `jint` tag on [stackoverflow](http://stackoverflow.com/questions/tagged/jint).

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
        .Execute("p.Name === 'Mickey Mouse'")
        ;
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

- ECMAScript 5.1 test suite (http://test262.ecmascript.org/) 
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

Continuous Integration kindly provided by  
[![](http://www.jetbrains.com/img/banners/Codebetter300x250.png)](http://www.jetbrains.com/teamcity)

