using System;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class NamespaceTests : IDisposable
    {
        public static class Methods
        {
            public static string DoWork(string s)
            {
                return s;
            }

            public static string DoWork2(string s)
            {
                return "Work: " + s;
            }
        }

        private readonly Engine _engine;

        public NamespaceTests()
        {
            _engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                ;
        }

        void IDisposable.Dispose()
        {
        }

        private void RunTest(string source)
        {
            _engine.Execute(source);
        }

        [Fact]
        public void ShouldAllowMethodNamespacing()
        {
            _engine.SetValue("ixp", new ObjectWrapper(_engine, new {}) { Extensible = true });
            var ixp = _engine.GetValue("ixp").TryCast<ObjectInstance>();
            ixp.FastAddProperty("data", new ObjectWrapper(_engine, new {}) { Extensible = true }, false, false, false);

            var data = _engine.GetValue(ixp, "data").TryCast<ObjectInstance>();
            data.FastAddProperty("doWork", new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork), true, false, false);

            RunTest(@"ixp.data.doWork('hello');");
            var result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("hello", result);
        }

        [Fact]
        public void ShouldAllowMethodNamespacingWithUpdate()
        {
            _engine.SetValue("ixp", new ObjectWrapper(_engine, new { }) { Extensible = true });
            var ixp = _engine.GetValue("ixp").TryCast<ObjectInstance>();
            ixp.FastAddProperty("data", new ObjectWrapper(_engine, new { }) { Extensible = true }, false, false, false);

            var data = _engine.GetValue(ixp, "data").TryCast<ObjectInstance>();
            data.FastAddProperty("doWork", new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork), true, false, false);

            RunTest(@"ixp.data.doWork('hello');");
            var result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("hello", result);

            data.FastSetProperty("doWork", new PropertyDescriptor(new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork2), true, false, false));

            RunTest(@"ixp.data.doWork('hello');");
            result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("Work: hello", result);
        }

        [Fact]
        public void ShouldAllowMethodNamespacingWithAddition()
        {
            _engine.SetValue("ixp", new ObjectWrapper(_engine, new { }) { Extensible = true });
            var ixp = _engine.GetValue("ixp").TryCast<ObjectInstance>();
            ixp.FastAddProperty("data", new ObjectWrapper(_engine, new { }) { Extensible = true }, false, false, false);

            var data = _engine.GetValue(ixp, "data").TryCast<ObjectInstance>();
            data.FastAddProperty("doWork", new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork), true, false, false);

            RunTest(@"ixp.data.doWork('hello');");
            var result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("hello", result);

            data.FastAddProperty("doWork2", new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork2), true, false, false);

            RunTest(@"ixp.data.doWork2('hello');");
            result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("Work: hello", result);
        }

        [Fact]
        public void ShouldAllowMethodNamespacingAnonymousNesting()
        {
            _engine.SetValue("ixp", new
            {
                data = new
                {
                    doWork = (Func<string, string>)Methods.DoWork
                }
            });

            _engine.Execute(@"ixp.data.doWork('hello');");
            var result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("hello", result);
        }

        [Fact]
        public void ShouldAllowMethodNamespacingAnonymousNestingWithUpdate()
        {
            _engine.SetValue("ixp", new
                                    {
                                        data = new
                                               {
                                                   doWork = (Func<string, string>)Methods.DoWork
                                               }
                                    });

            _engine.Execute(@"ixp.data.doWork('hello');");
            var result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("hello", result);

            var ixp = _engine.GetValue("ixp");
            Assert.NotEqual(null, ixp.ToObject());

            var data = _engine.GetValue(ixp, "data");
            Assert.NotEqual(null, data.ToObject());

            var doWork = _engine.GetValue(data, "doWork");
            doWork.TryCast<ObjectInstance>().Extensible = true;
            Assert.NotEqual(null, doWork.ToObject());

            var dataInst = data.TryCast<ObjectInstance>();
            dataInst.Extensible = true;
            dataInst.FastSetProperty("doWork", new PropertyDescriptor(new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork2), true, false, false));

            RunTest(@"ixp.data.doWork('hello');");
            result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("Work: hello", result);
        }


        [Fact]
        public void ShouldAllowMethodNamespacingAnonymousNestingWithAddition()
        {
            _engine.SetValue("ixp", new
                                    {
                                        data = new
                                               {
                                                   doWork = (Func<string, string>)Methods.DoWork
                                               }
                                    });

            _engine.Execute(@"ixp.data.doWork('hello');");
            var result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("hello", result);

            var ixp = _engine.GetValue("ixp");
            Assert.NotEqual(null, ixp.ToObject());

            var data = _engine.GetValue(ixp, "data");
            Assert.NotEqual(null, data.ToObject());

            var doWork = _engine.GetValue(data, "doWork");
            doWork.TryCast<ObjectInstance>().Extensible = true;
            Assert.NotEqual(null, doWork.ToObject());

            var dataInst = data.TryCast<ObjectInstance>();
            dataInst.Extensible = true;
            dataInst.FastAddProperty("doWork2", new DelegateWrapper(_engine, (Func<string, string>)Methods.DoWork2), true, false, false);

            RunTest(@"ixp.data.doWork2('hello');");
            result = _engine.GetCompletionValue().ToObject();
            Assert.Equal("Work: hello", result);
        }
    }
}