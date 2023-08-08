using System.Reflection;

namespace Jint.Tests.Runtime
{
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

        public struct NullabeStruct: I1
        {
            public NullabeStruct()
            {
            }
            public string name = "NullabeStruct";

            public string Name { get => name; }

            string I1.Name { get => "NullabeStruct as I1"; }
        }

        public class NullableHolder
        {
            public I1? I1 { get; set; }
            public NullabeStruct? NullabeStruct { get; set; }
        }

        [Fact]
        public void TestNullable()
        {
            var nullableHolder = new NullableHolder();
            _engine.SetValue("nullableHolder", nullableHolder);
            _engine.SetValue("nullabeStruct", new NullabeStruct());

            Assert.Equal(_engine.Evaluate("nullableHolder.NullabeStruct"), Native.JsValue.Null);
            _engine.Evaluate("nullableHolder.NullabeStruct = nullabeStruct");
            Assert.Equal(_engine.Evaluate("nullableHolder.NullabeStruct.Name"), nullableHolder.NullabeStruct?.Name);
        }

        [Fact]
        public void TestClrUnwrap()
        {
            Assert.NotEqual(holder.CI1.Name, _engine.Evaluate("holder.I1.Name"));
            Assert.Equal(holder.CI1.Name, _engine.Evaluate("clrUnwrap(holder.I1).Name"));
        }

        [Fact]
        public void TestClrWrap()
        {
            Assert.NotEqual(holder.I1.Name, _engine.Evaluate("holder.CI1.Name"));
            Assert.Equal(holder.I1.Name, _engine.Evaluate("clrWrap(holder.CI1, clrType(holder.I1)).Name"));
        }

        [Fact]
        public void TestClrType()
        {
            _engine.SetValue("clrobj", new object());
            Assert.Equal(_engine.Evaluate("System.Object"), _engine.Evaluate("clrType(clrobj)"));
        }

        [Fact]
        public void TestNestedClrType()
        {
            _engine.Execute("Jint = importNamespace('Jint');");
            Assert.Equal(_engine.Evaluate("Jint.Tests.Runtime.InteropExplicitTypeTests.CI1"), _engine.Evaluate("clrType(holder.CI1)"));
            Assert.Equal(_engine.Evaluate("Jint.Tests.Runtime.InteropExplicitTypeTests.I1"), _engine.Evaluate("clrType(holder.I1)"));
        }
    }
}
