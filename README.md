[![Build status](https://ci.appveyor.com/api/projects/status?id=c84b8rdswh2w4744)](https://ci.appveyor.com/project/jint)

# Jint

Jint is a Javascript interpreter for .NET. Jint doesn't compile Javascript to .NET bytecode and in this sense might be best suited for projects requiring to run relatively small scripts faster, or which need to run on different platforms.
It's available on nuget at https://www.nuget.org/packages/Jint

# Objectives

- Full support for ECMAScript 5.1 - http://www.ecma-international.org/ecma-262/5.1/
- .NET Portable Class Library - http://msdn.microsoft.com/en-us/library/gg597391(v=vs.110).aspx
- .NET Interoperability 

# Example

This example defines a new value named `log` pointing to `Console.WriteLine`, then executes 
a script calling `log('Hello World!')`. 

    var engine = new Engine()
        .SetValue("log", new Action<object>(Console.WriteLine))
        ;
    
    engine.Execute(@"
      function hello() { 
        log("Hello World");
      };
      
      hello();
    ");

Here, the variable `x` is set to `3` and `x * x` is executed in JavaScript. The result is returned to .NET directly, in this case as a `double` value `9`. 

    var square = new Engine()
        .SetValue("x", 3) // define a new variable
        .Execute("x * x") // execute a statement
        .GetCompletionValue() // get the latest statement completion value
        .ToObject() // converts the value to .NET
        ;

You can also directly pass POCOs or anonymous objects and use them from JavaScript. In this example for instance a new `Person` instance is manipulated from JavaScript. 

    var p = new Person {
        Name = "Mickey Mouse"
    };

    var engine = new Engine()
        .SetValue("p", p)
        .Execute("p.Name === 'Mickey Mouse')
        ;

If you need to pass a JavaScript callback to the CLR, then it will be converted to a `Delegate`. You can also use it as a parameter of a CLR method from JavaScript.

    var function = new Engine()
        .Execute("function add(a, b) { return this + a + b; }");
        .GetGlobalValue("add")
        .ToObject() as Delegate;
        
    var result = function.DynamicInvoke(
        new JsValue("foo") /* thisArg */, 
        new JsValue[] { 1, "bar" } /* arguments */,
        ); // "foo1bar"

## Accessing .NET assemblies and classes

You can allow an engine to access any .NET class by configuring the engine instance like this:

    var engine = new Engine(cfg => cfg.AllowClr())

Then you have access to the `System` namespace as a global value. Here is how it's used in the context on the command line utility:

    jint> var file = new System.IO.File('log.txt');
    jint> file.WriteLine('Hello World !');
    jint> file.Dispose();

And even create shortcuts to commong .NET methods

    jint> var log = System.Console.WriteLine;
    jint> log('Hello World !');
    => "Hello World !"

Loading custom assemblies dynamically can be done by using standard reflection `System.Reflection.Assembly.Load`. You will also need
to assign local namespace the same way `System` does it for you, by using `importNamespace`:

    var Foo = importNamespace('Foo');
    var bar = new Foo.Bar();
    log(bar.ToString());
    
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
