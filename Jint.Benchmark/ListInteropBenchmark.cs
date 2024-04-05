using System.Collections;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ListInteropBenchmark
{
    private static bool IsArrayLike(Type type)
    {
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return false;
        }

        if (typeof(ICollection).IsAssignableFrom(type))
        {
            return true;
        }

        foreach (var typeInterface in type.GetInterfaces())
        {
            if (!typeInterface.IsGenericType)
            {
                continue;
            }

            if (typeInterface.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)
                || typeInterface.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                return true;
            }
        }

        return false;
    }

    private const int Count = 10_00;
    private Engine _engine;
    private JsValue[] _properties;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();

        _properties = new JsValue[Count];
        var input = new List<Data>(Count);
        for (var i = 0; i < Count; ++i)
        {
            input.Add(new Data { Category = new Category { Name = i % 2 == 0 ? "Pugal" : "Beagle" } });
            _properties[i] = JsNumber.Create(i);
        }

        _engine.SetValue("input", input);
        _engine.SetValue("CONST", new { category = "Pugal" });
    }

    [Benchmark]
    public void Filter()
    {
        var value = (Data) _engine.Evaluate("input.filter(i => i.category?.name === CONST.category)[0]").ToObject();
        if (value.Category.Name != "Pugal")
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void Indexing()
    {
        _engine.Evaluate("for (var i = 0; i < input.length; ++i) { input[i]; }");
    }

    [Benchmark]
    public void HasProperty()
    {
        var input = (ObjectWrapper) _engine.GetValue("input");
        for (var i = 0; i < _properties.Length; ++i)
        {
            if (!input.HasProperty(_properties[i]))
            {
                throw new InvalidOperationException();
            }
        }
    }

    private class Data
    {
        public Category Category { get; set; }
    }

    private class Category
    {
        public string Name { get; set; }
    }
}
