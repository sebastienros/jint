# Jint

Jint is a Javascript interpreter for .NET. Jint doesn't compile Javascript to .NET bytecode and in this sense might be best suited for projects requiring to run relatively small scripts faster, or which need to run on different platforms.

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
        .SetValue("x", 3)
        .Execute("x * x")
        .ToObject()
        ;

You can also directly pass POCOs or anonymous objects and use them from JavaScript. In this example for instance a new `Person` instance is manipulated from JavaScript. 

    var p = new Person {
        Name = "Mickey Mouse"
    };

    var engine = new Engine()
        .SetValue("p", p)
        .Execute("p.Name === 'Mickey Mouse')
        ;

# Roadmap

Status:

- Most of the ECMAScript test suite (http://test262.ecmascript.org/) is passing 
- You can define custom delegates as Javascript functions

TODO:

- Improve C# interoperability
- Fix remaining unit tests
  - Regular expression (15.10)
  -	Error object (15.11)
  -	Object constructor (15.2.3)
  -	Object prototype (15.2.4)


