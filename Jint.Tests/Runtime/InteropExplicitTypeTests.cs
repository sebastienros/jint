namespace Jint.Tests.Runtime;

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

public class InteropExplicitTypeTests
{
#if NETCOREAPP
    public interface I0
    {

        string NameI0 { get => "I0.Name"; }
        string OverloadSuperMethod() => "I0.OverloadSuperMethod()";
        string SubPropertySuperMethod() => "I0.SubPropertySuperMethod()";
    }

    public interface I1 : I0
    {
        string Name { get; }
        string OverloadSuperMethod(int x) => $"I1.OverloadSuperMethod(int {x})";
        new string SubPropertySuperMethod => "I1.SubPropertySuperMethod";
    }
    public class Super
    {
        public string Name { get; } = "Super";
    }

    public class CI1 : Super, I1
    {
        public new string Name { get; } = "CI1";

        string I1.Name { get; } = "CI1 as I1";
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

    public InteropExplicitTypeTests()
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
    public void EqualTest()
    {
        Assert.Equal(_engine.Evaluate("holder.I1"), _engine.Evaluate("holder.i1"));
        Assert.NotEqual(_engine.Evaluate("holder.I1"), _engine.Evaluate("holder.ci1"));

        Assert.Equal(_engine.Evaluate("holder.Super"), _engine.Evaluate("holder.super"));
        Assert.NotEqual(_engine.Evaluate("holder.Super"), _engine.Evaluate("holder.ci1"));
    }

    [Fact]
    public void ExplicitInterfaceFromField()
    {
        Assert.Equal(holder.i1.Name, _engine.Evaluate("holder.i1.Name"));
        Assert.NotEqual(holder.i1.Name, _engine.Evaluate("holder.ci1.Name"));
    }

    [Fact]
    public void ExplicitInterfaceFromProperty()
    {
        Assert.Equal(holder.I1.Name, _engine.Evaluate("holder.I1.Name"));
        Assert.NotEqual(holder.I1.Name, _engine.Evaluate("holder.CI1.Name"));
    }

    [Fact]
    public void ExplicitInterfaceFromMethod()
    {
        Assert.Equal(holder.GetI1().Name, _engine.Evaluate("holder.GetI1().Name"));
        Assert.NotEqual(holder.GetI1().Name, _engine.Evaluate("holder.GetCI1().Name"));
    }

    [Fact]
    public void ExplicitInterfaceFromIndexer()
    {
        Assert.Equal(holder.IndexerI1[0].Name, _engine.Evaluate("holder.IndexerI1[0].Name"));
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
    [Fact]
    public void SuperClassFromField()
    {
        Assert.Equal(holder.super.Name, _engine.Evaluate("holder.super.Name"));
        Assert.NotEqual(holder.super.Name, _engine.Evaluate("holder.ci1.Name"));
    }

    [Fact]
    public void SuperClassFromProperty()
    {
        Assert.Equal(holder.Super.Name, _engine.Evaluate("holder.Super.Name"));
        Assert.NotEqual(holder.Super.Name, _engine.Evaluate("holder.CI1.Name"));
    }

    [Fact]
    public void SuperClassFromMethod()
    {
        Assert.Equal(holder.GetSuper().Name, _engine.Evaluate("holder.GetSuper().Name"));
        Assert.NotEqual(holder.GetSuper().Name, _engine.Evaluate("holder.GetCI1().Name"));
    }

    [Fact]
    public void SuperClassFromIndexer()
    {
        Assert.Equal(holder.IndexerSuper[0].Name, _engine.Evaluate("holder.IndexerSuper[0].Name"));
    }

    public struct NullabeStruct : I1
    {
        public NullabeStruct()
        {
        }
        public string name = "NullabeStruct";

        public string Name => name;

        string I1.Name => "NullabeStruct as I1";
    }

    public class NullableHolder
    {
        public I1 I1 { get; set; }
        public NullabeStruct? NullabeStruct { get; set; }
    }

    [Fact]
    public void TypedObjectWrapperForNullableType()
    {
        var nullableHolder = new NullableHolder();
        _engine.SetValue("nullableHolder", nullableHolder);
        _engine.SetValue("nullabeStruct", new NullabeStruct());

        Assert.Equal(_engine.Evaluate("nullableHolder.NullabeStruct"), JsValue.Null);
        _engine.Evaluate("nullableHolder.NullabeStruct = nullabeStruct");
        Assert.Equal(_engine.Evaluate("nullableHolder.NullabeStruct.Name"), nullableHolder.NullabeStruct?.Name);
    }

    [Fact]
    public void ClrHelperUnwrap()
    {
        Assert.NotEqual(holder.CI1.Name, _engine.Evaluate("holder.I1.Name"));
        Assert.Equal(holder.CI1.Name, _engine.Evaluate("clrHelper.unwrap(holder.I1).Name"));
    }

    [Fact]
    public void ClrHelperWrap()
    {
        _engine.Execute("Jint = importNamespace('Jint');");
        Assert.NotEqual(holder.I1.Name, _engine.Evaluate("holder.CI1.Name"));
        Assert.Equal(holder.I1.Name, _engine.Evaluate("clrHelper.wrap(holder.CI1, Jint.Tests.Runtime.InteropExplicitTypeTests.I1).Name"));
    }

    [Fact]
    public void ClrHelperTypeOf()
    {
        Action<Engine> runner = engine =>
        {
            engine.SetValue("clrobj", new object());
            Assert.Equal(engine.Evaluate("System.Object"), engine.Evaluate("clrHelper.typeOf(clrobj)"));
        };

        runner.Invoke(new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.Interop.AllowGetType = true;
        }));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            runner.Invoke(new Engine(cfg =>
            {
                cfg.AllowClr();
            }));
        });
        Assert.Equal("Invalid when Engine.Options.Interop.AllowGetType == false", ex.Message);
    }

    [Fact]
    public void ClrHelperTypeOfForNestedType()
    {
        var engine = new Engine(cfg =>
        {
            cfg.AllowClr(GetType().Assembly);
            cfg.Interop.AllowGetType = true;
        });

        engine.SetValue("holder", holder);
        engine.Execute("Jint = importNamespace('Jint');");
        Assert.Equal(engine.Evaluate("Jint.Tests.Runtime.InteropExplicitTypeTests.CI1"), engine.Evaluate("clrHelper.typeOf(holder.CI1)"));
        Assert.Equal(engine.Evaluate("Jint.Tests.Runtime.InteropExplicitTypeTests.I1"), engine.Evaluate("clrHelper.typeOf(holder.I1)"));
    }

    public class TypeHolder
    {
        public static Type Type => typeof(TypeHolder);
    }

    [Fact]
    public void ClrHelperTypeToObject()
    {
        Action<Engine> runner = engine =>
        {
            engine.SetValue("TypeHolder", typeof(TypeHolder));
            Assert.True(engine.Evaluate("TypeHolder") is TypeReference);
            Assert.True(engine.Evaluate("clrHelper.typeToObject(TypeHolder)") is ObjectWrapper);
        };

        runner.Invoke(new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.Interop.AllowGetType = true;
        }));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            runner.Invoke(new Engine(cfg =>
            {
                cfg.AllowClr();
            }));
        });
        Assert.Equal("Invalid when Engine.Options.Interop.AllowGetType == false", ex.Message);
    }

    [Fact]
    public void ClrHelperObjectToType()
    {
        Action<Engine> runner = engine =>
        {
            engine.SetValue("TypeHolder", typeof(TypeHolder));
            Assert.True(engine.Evaluate("TypeHolder.Type") is ObjectWrapper);
            Assert.True(engine.Evaluate("clrHelper.objectToType(TypeHolder.Type)") is TypeReference);
        };

        runner.Invoke(new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.Interop.AllowGetType = true;
        }));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            runner.Invoke(new Engine(cfg =>
            {
                cfg.AllowClr();
            }));
        });
        Assert.Equal("Invalid when Engine.Options.Interop.AllowGetType == false", ex.Message);
    }

    public interface ICallObjectMethodFromInterface
    {
        ICallObjectMethodFromInterface Instance { get; }
        // hide Object.GetHashCode
        string GetHashCode();
        // overload Object.Equals
        string Equals();
    }
    public class CallObjectMethodFromInterface : ICallObjectMethodFromInterface
    {
        public ICallObjectMethodFromInterface Instance => this;
        public override string ToString() => nameof(CallObjectMethodFromInterface);
        public new string GetHashCode() => "new GetHashCode, hide Object.GetHashCode";
        public string Equals() => "overload Object.Equals";
    }

    // issue#1626 ToString method is now unavailable in some CLR Interop scenarios
    [Fact]
    public void CallObjectMethodFromInterfaceWrapper()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        Assert.Equal(inst.Instance.ToString(), _engine.Evaluate("inst.Instance.ToString()"));
    }

    [Fact]
    public void CallInterfaceMethodWhichHideObjectMethod()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        Assert.Equal(inst.Instance.GetHashCode(), _engine.Evaluate("inst.Instance.GetHashCode()"));
    }

    [Fact(Skip = "TODO, no solution now.")]
    public void CallObjectMethodHiddenByInterface()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        Assert.Equal(
            (inst.Instance as object).GetHashCode(),
            _engine.Evaluate("clrHelper.unwrap(inst.Instance).GetHashCode()")
        );
    }

    [Fact]
    public void CallInterfaceMethodWhichOverloadObjectMethod()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        Assert.Equal(inst.Instance.Equals(), _engine.Evaluate("inst.Instance.Equals()"));
        Assert.Equal(inst.Instance.Equals(inst), _engine.Evaluate("inst.Instance.Equals(inst)"));
    }

#endif
}
