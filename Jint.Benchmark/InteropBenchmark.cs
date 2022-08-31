using System.Reflection;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Array;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class InteropBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    private Engine _engine;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine(cfg => cfg.AllowClr(
                typeof(Person).GetTypeInfo().Assembly,
                typeof(Console).GetTypeInfo().Assembly,
                typeof(System.IO.File).GetTypeInfo().Assembly))
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(x => { }))
            .SetValue("equal", new Action<object, object>((x, y) => { }));
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void DelegatesCanBeSet()
    {
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("square", new Func<double, double>(x => x * x));
            _engine.Execute("assert(square(10) === 100);");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void ExtraParametersAreIgnored()
    {
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("passNumber", new Func<int, int>(x => x));
            _engine.Execute("assert(passNumber(123,'test',{},[],null) === 123);");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void GetObjectProperties()
    {
        var p = new Person
        {
            Name = "Mickey Mouse"
        };

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("p", p);
            _engine.Execute("assert(p.Name === 'Mickey Mouse');");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void InvokeObjectMethods()
    {
        var p = new Person
        {
            Name = "Mickey Mouse"
        };

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("p", p);
            _engine.Execute(@"assert(p.ToString() === 'Mickey Mouse');");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SetObjectProperties()
    {
        var p = new Person
        {
            Name = "Mickey Mouse"
        };

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("p", p);
            _engine.Execute("p.Name = 'Donald Duck'; assert(p.Name === 'Donald Duck');");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void GetIndexUsingStringKey()
    {
        var dictionary = new Dictionary<string, Person>();
        dictionary.Add("person1", new Person {Name = "Mickey Mouse"});
        dictionary.Add("person2", new Person {Name = "Goofy"});

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("dictionary", dictionary);
            _engine.Execute("assert(dictionary['person1'].Name === 'Mickey Mouse'); assert(dictionary['person2'].Name === 'Goofy');");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void GenericMethods()
    {
        var dictionary = new Dictionary<int, string>
        {
            {1, "Mickey Mouse"}
        };

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("dictionary", dictionary);
            _engine.Execute("dictionary.Clear(); dictionary.Add(2, 'Goofy'); assert(dictionary[2] === 'Goofy');");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MultiGenericTypes()
    {
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.Execute(@"
                var type = System.Collections.Generic.Dictionary(System.Int32, System.String);
                var dictionary = new type();
                dictionary.Add(1, 'Mickey Mouse');
                dictionary.Add(2, 'Goofy');
                assert(dictionary[2] === 'Goofy');
            ");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexOnList()
    {
        var list = new List<object>(2);
        list.Add("Mickey Mouse");
        list.Add("Goofy");

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("list", list);
            _engine.Execute("list[1] = 'Donald Duck'; assert(list[1] === 'Donald Duck');");
        }
    }


    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void EcmaValuesAreAutomaticallyConvertedWhenSetInPoco()
    {
        var p = new Person
        {
            Name = "foo",
        };


        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("p", p);
            _engine.Execute(@"
                assert(p.Name === 'foo');
                assert(p.Age === 0);
                p.Name = 'bar';
                p.Age = 10;
            ");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Trim()
    {
        var p = new Person
        {
            Name = "Mickey Mouse "
        };
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("p", p);
            _engine.Execute(@"
                assert(p.Name === 'Mickey Mouse ');
                p.Name = p.Name.trim();
                assert(p.Name === 'Mickey Mouse');
            ");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MathFloor()
    {
        var p = new Person();
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("p", p);
            _engine.Execute("p.Age = Math.floor(1.6); assert(p.Age === 1);");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void DelegateAsFunction()
    {
        var even = new Func<int, bool>(x => x % 2 == 0);

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("even", even);
            _engine.Execute("assert(even(2) === true);");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void ConvertArrayToArrayInstance()
    {
        var ints = new[] {1, 2, 3, 4, 5, 6};
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine
                .SetValue("values", ints)
                .Execute("values.filter(function(x){ return x % 2 == 0; })");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void ConvertListsToArrayInstance()
    {
        var ints = new List<object> {1, 2, 3, 4, 5, 6};
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine
                .SetValue("values", ints)
                .Execute("new Array(values).filter(function(x){ return x % 2 == 0; })");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void ConvertArrayInstanceToArray()
    {
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.Execute("'foo@bar.com'.split('@');");
        }
    }
        
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void ResolvingConsoleWriteLine()
    {
        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.Execute("System.Console.WriteLine('value to write');");
        }
        Console.SetOut(originalOut);
    }
        
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Setter()
    {
        var p = new Person();
        _engine.SetValue("p", p);
        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.Execute("p.Age = 42;");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void LoopWithNativeEnumerator()
    {
        JsValue Adder(JsValue argValue)
        {
            ArrayInstance args = argValue.AsArray();
            double sum = 0;
            foreach (var item in args)
            {
                if (item.IsNumber())
                {
                    sum += item.AsNumber();
                }
            }

            return sum;
        }

        for (int i = 0; i < OperationsPerInvoke; ++i)
        {
            _engine.SetValue("getSum", new Func<JsValue, JsValue>(Adder));
            _engine.Execute("getSum([1,2,3]);");
        }
    }
}