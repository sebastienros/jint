namespace Jint.Tests.Runtime;

using Jint.Native;
using Jint.Runtime.Interop;

public class InteropExplicitTypeTests
{
    public interface I1
    {
        string Name { get; }
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

    public class BaseValue
    {
        public string BaseOnly { get; } = "base";
    }

    public class DerivedValue : BaseValue
    {
        public int DerivedOnlyProperty { get; set; }
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

    public class BaseValueHolder
    {
        public BaseValue Value { get; } = new DerivedValue();
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
                .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
                .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
                .SetValue("holder", holder)
        ;
    }
    [Fact]
    public void EqualTest()
    {
        _engine.Evaluate("holder.i1").Should().Be(_engine.Evaluate("holder.I1"));
        _engine.Evaluate("holder.super").Should().Be(_engine.Evaluate("holder.Super"));
    }

    [Fact]
    public void ExplicitInterfaceFromField()
    {
        _engine.Evaluate("holder.i1.Name").Should().Be(holder.i1.Name);
        _engine.Evaluate("holder.ci1.Name").Should().NotBe(holder.i1.Name);
    }

    [Fact]
    public void ExplicitInterfaceFromProperty()
    {
        _engine.Evaluate("holder.I1.Name").Should().Be(holder.I1.Name);
        _engine.Evaluate("holder.CI1.Name").Should().NotBe(holder.I1.Name);
    }

    [Fact]
    public void ExplicitInterfaceFromMethod()
    {
        _engine.Evaluate("holder.GetI1().Name").Should().Be(holder.GetI1().Name);
        _engine.Evaluate("holder.GetCI1().Name").Should().NotBe(holder.GetI1().Name);
    }

    [Fact]
    public void ExplicitInterfaceFromIndexer()
    {
        _engine.Evaluate("holder.IndexerI1[0].Name").Should().Be(holder.IndexerI1[0].Name);
    }

    [Fact]
    public void RecentWrapperCacheKeepsExposedTypeViewsSeparate()
    {
        // the default recent-wrapper cache (see #2729) reuses wrappers by object identity; the same
        // instance exposed under different CLR types must still resolve each view's own members
        _engine.Evaluate("holder.ci1.Name").AsString().Should().Be("CI1");
        _engine.Evaluate("holder.i1.Name").AsString().Should().Be("CI1 as I1");
        _engine.Evaluate("holder.super.Name").AsString().Should().Be("Super");

        // and in reverse order against the now-populated cache slots
        _engine.Evaluate("holder.super.Name").AsString().Should().Be("Super");
        _engine.Evaluate("holder.i1.Name").AsString().Should().Be("CI1 as I1");
        _engine.Evaluate("holder.ci1.Name").AsString().Should().Be("CI1");
    }


    [Fact]
    public void SuperClassFromField()
    {
        _engine.Evaluate("holder.super.Name").Should().Be(holder.super.Name);
        _engine.Evaluate("holder.ci1.Name").Should().NotBe(holder.super.Name);
    }

    [Fact]
    public void SuperClassFromProperty()
    {
        _engine.Evaluate("holder.Super.Name").Should().Be(holder.Super.Name);
        _engine.Evaluate("holder.CI1.Name").Should().NotBe(holder.Super.Name);
    }

    [Fact]
    public void SuperClassFromMethod()
    {
        _engine.Evaluate("holder.GetSuper().Name").Should().Be(holder.GetSuper().Name);
        _engine.Evaluate("holder.GetCI1().Name").Should().NotBe(holder.GetSuper().Name);
    }

    [Fact]
    public void SuperClassFromIndexer()
    {
        _engine.Evaluate("holder.IndexerSuper[0].Name").Should().Be(holder.IndexerSuper[0].Name);
    }

    [Fact]
    public void DerivedRuntimePropertyFromBaseDeclaredProperty()
    {
        var engine = new Engine(options => options.Interop.ThrowOnUnresolvedMember = true);
        var holder = new BaseValueHolder();
        engine.SetValue("holder", holder);

        engine.Evaluate("holder.Value.BaseOnly").Should().Be("base");
        engine.Execute("const obj = holder.Value; obj.DerivedOnlyProperty = 123;");
        ((DerivedValue) holder.Value).DerivedOnlyProperty.Should().Be(123);
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

        _engine.Evaluate("nullableHolder.NullabeStruct").Should().Be(JsValue.Null);
        _engine.Evaluate("nullableHolder.NullabeStruct = nullabeStruct");
        _engine.Evaluate("nullableHolder.NullabeStruct.Name").Should().Be(nullableHolder.NullabeStruct?.Name);
    }

    [Fact]
    public void ClrHelperUnwrap()
    {
        _engine.Evaluate("holder.I1.Name").Should().NotBe(holder.CI1.Name);
        _engine.Evaluate("clrHelper.unwrap(holder.I1).Name").Should().Be(holder.CI1.Name);
    }

    [Fact]
    public void ClrHelperWrap()
    {
        _engine.Execute("Jint = importNamespace('Jint');");
        _engine.Evaluate("holder.CI1.Name").Should().NotBe(holder.I1.Name);
        _engine.Evaluate("clrHelper.wrap(holder.CI1, Jint.Tests.Runtime.InteropExplicitTypeTests.I1).Name").Should().Be(holder.I1.Name);
    }

    [Fact]
    public void ClrHelperTypeOf()
    {
        Action<Engine> runner = engine =>
        {
            engine.SetValue("clrobj", new object());
            engine.Evaluate("clrHelper.typeOf(clrobj)").Should().Be(engine.Evaluate("System.Object"));
        };

        runner.Invoke(new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.Interop.AllowGetType = true;
        }));

        var ex = Invoking(() =>
        {
            runner.Invoke(new Engine(cfg =>
            {
                cfg.AllowClr();
            }));
        }).Should().ThrowExactly<InvalidOperationException>().Which;
        ex.Message.Should().Be("Invalid when Engine.Options.Interop.AllowGetType == false");
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
        engine.Evaluate("clrHelper.typeOf(holder.CI1)").Should().Be(engine.Evaluate("Jint.Tests.Runtime.InteropExplicitTypeTests.CI1"));
        engine.Evaluate("clrHelper.typeOf(holder.I1)").Should().Be(engine.Evaluate("Jint.Tests.Runtime.InteropExplicitTypeTests.I1"));
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
            engine.Evaluate("TypeHolder").Should().BeAssignableTo<TypeReference>();
            engine.Evaluate("clrHelper.typeToObject(TypeHolder)").Should().BeAssignableTo<ObjectWrapper>();
        };

        runner.Invoke(new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.Interop.AllowGetType = true;
        }));

        var ex = Invoking(() =>
        {
            runner.Invoke(new Engine(cfg =>
            {
                cfg.AllowClr();
            }));
        }).Should().ThrowExactly<InvalidOperationException>().Which;
        ex.Message.Should().Be("Invalid when Engine.Options.Interop.AllowGetType == false");
    }

    [Fact]
    public void ClrHelperObjectToType()
    {
        Action<Engine> runner = engine =>
        {
            engine.SetValue("TypeHolder", typeof(TypeHolder));
            engine.Evaluate("TypeHolder.Type").Should().BeAssignableTo<ObjectWrapper>();
            engine.Evaluate("clrHelper.objectToType(TypeHolder.Type)").Should().BeAssignableTo<TypeReference>();
        };

        runner.Invoke(new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.Interop.AllowGetType = true;
        }));

        var ex = Invoking(() =>
        {
            runner.Invoke(new Engine(cfg =>
            {
                cfg.AllowClr();
            }));
        }).Should().ThrowExactly<InvalidOperationException>().Which;
        ex.Message.Should().Be("Invalid when Engine.Options.Interop.AllowGetType == false");
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
        _engine.Evaluate("inst.Instance.ToString()").Should().Be(inst.Instance.ToString());
    }

    [Fact]
    public void CallInterfaceMethodWhichHideObjectMethod()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        _engine.Evaluate("inst.Instance.GetHashCode()").Should().Be(inst.Instance.GetHashCode());
    }

    [Fact(Skip = "TODO, no solution now.")]
    public void CallObjectMethodHiddenByInterface()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        _engine.Evaluate("clrHelper.unwrap(inst.Instance).GetHashCode()").Should().Be((inst.Instance as object).GetHashCode());
    }

    [Fact]
    public void CallInterfaceMethodWhichOverloadObjectMethod()
    {
        var inst = new CallObjectMethodFromInterface();
        _engine.SetValue("inst", inst);
        _engine.Evaluate("inst.Instance.Equals()").Should().Be(inst.Instance.Equals());
        _engine.Evaluate("inst.Instance.Equals(inst)").Should().Be(inst.Instance.Equals(inst));
    }
}
