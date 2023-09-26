using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class InterfaceTests
{
    public interface I0
    {
        string NameI0 { get; }
        string OverloadSuperMethod();
        string SubPropertySuperMethod();
    }

    public interface I1 : I0
    {
        string Name { get; }
        string OverloadSuperMethod(int x);
        new string SubPropertySuperMethod { get; }
    }

    public class Super
    {
        public string Name { get; } = "Super";
    }

    public class CI1 : Super, I1
    {
        public new string Name { get; } = "CI1";

        public string NameI0 { get; } = "I0.Name";

        string I1.Name { get; } = "CI1 as I1";

        string I1.SubPropertySuperMethod { get; } = "I1.SubPropertySuperMethod";

        public string OverloadSuperMethod()
        {
            return "I0.OverloadSuperMethod()";
        }

        public string OverloadSuperMethod(int x)
        {
            return $"I1.OverloadSuperMethod(int {x})";
        }

        public string SubPropertySuperMethod()
        {
            return "I0.SubPropertySuperMethod()";
        }
    }

    public class Indexer<T>
    {
        private readonly T t;

        public Indexer(T t)
        {
            this.t = t;
        }

        public T this[int index]
        {
            get { return t; }
        }
    }

    public class InterfaceHolder
    {
        public InterfaceHolder()
        {
            var ci1 = new CI1();
            this.ci1 = ci1;
            this.i1 = ci1;
            this.super = ci1;

            this.IndexerCI1 = new Indexer<CI1>(ci1);
            this.IndexerI1 = new Indexer<I1>(ci1);
            this.IndexerSuper = new Indexer<Super>(ci1);
        }

        public readonly CI1 ci1;
        public readonly I1 i1;
        public readonly Super super;

        public CI1 CI1 { get => ci1; }
        public I1 I1 { get => i1; }
        public Super Super { get => super; }

        public CI1 GetCI1() => ci1;
        public I1 GetI1() => i1;
        public Super GetSuper() => super;

        public Indexer<CI1> IndexerCI1 { get; }
        public Indexer<I1> IndexerI1 { get; }
        public Indexer<Super> IndexerSuper { get; }
    }

    private readonly Engine _engine;
    private readonly InterfaceHolder holder;

    public InterfaceTests()
    {
        holder = new InterfaceHolder();
        _engine = new Engine(cfg => cfg.AllowClr(
                    typeof(CI1).Assembly,
                    typeof(Console).Assembly,
                    typeof(File).Assembly))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .SetValue("holder", holder)
            ;
    }

    [Fact]
    public void CallSuperPropertyFromInterface()
    {
        Assert.Equal(holder.I1.NameI0, _engine.Evaluate("holder.I1.NameI0"));
    }

    [Fact]
    public void CallOverloadSuperMethod()
    {
        Assert.Equal(
            holder.I1.OverloadSuperMethod(1),
            _engine.Evaluate("holder.I1.OverloadSuperMethod(1)"));
        Assert.Equal(
            holder.I1.OverloadSuperMethod(),
            _engine.Evaluate("holder.I1.OverloadSuperMethod()"));
    }

    [Fact]
    public void CallSubPropertySuperMethod_SubProperty()
    {
        Assert.Equal(
            holder.I1.SubPropertySuperMethod,
            _engine.Evaluate("holder.I1.SubPropertySuperMethod"));
    }

    [Fact]
    public void CallSubPropertySuperMethod_SuperMethod()
    {
        var ex = Assert.Throws<JavaScriptException>(() =>
        {
            Assert.Equal(
                holder.I1.SubPropertySuperMethod(),
                _engine.Evaluate("holder.I1.SubPropertySuperMethod()"));
        });
        Assert.Equal("Property 'SubPropertySuperMethod' of object is not a function", ex.Message);
    }
}
