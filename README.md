# Jint

Jint is a Javascript interpreter for .NET. Jint doesn't compile Javascript to .NET bytecode and in this sense might be best suited for projects requiring to run relatively small scripts faster, or which need to run on different platforms.

# Objectives

- Full support for ECMAScript 5.1 - http://www.ecma-international.org/ecma-262/5.1/
- .NET Portable Class Library - http://msdn.microsoft.com/en-us/library/gg597391(v=vs.110).aspx
- .NET Interoperability 

# Example


    script= @"
      function hello() { 
        log("Hello World");
      };
    ";
  
    var engine = new Engine(cfg => cfg
        .WithDelegate("log", new Action<object>(Console.WriteLine))
    );
    
    engine.Execute("hello()");


You can also check the actual implemented test suite for more samples.

# Roadmap

Status:

- Most of the ECMAScript test suite (http://test262.ecmascript.org/) is passing 
- You can define custom delegates as Javascript functions

Todo:

- Improve C# interoperability
- Finish up ECMAScript test suite

