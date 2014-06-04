using System;
using Jint.Native;

namespace Jint.Tests.Runtime.Domain
{
    public class A
    {
        public int Call1()
        {
            return 0;
        }

        public int Call1(int x)
        {
            return x;
        }

        public string Call2(string x)
        {
            return x;
        }

        public string Call3(object x)
        {
            return x.ToString();
        }

        public string Call4(IPerson x)
        {
            return x.ToString();
        }

        public string Call5(Delegate callback)
        {
            var thisArg = JsValue.Undefined;
            var arguments = new JsValue[] { 1, "foo" };

            return callback.DynamicInvoke(thisArg, arguments).ToString();
        }

        public string Call6(Func<JsValue, JsValue[], JsValue> callback)
        {
            var thisArg = new JsValue("bar");
            var arguments = new JsValue[] { 1, "foo" };

            return callback(thisArg, arguments).ToString();
        }

        public bool Call7(string str, Func<string, bool> predicate)
        {
            return predicate(str);
        }

        public string Call8(Func<string> predicate)
        {
            return predicate();
        }

        public void Call9(Action predicate)
        {
            predicate();
        }
        public void Call10(string str, Action<string> predicate)
        {
            predicate(str);
        }
        public void Call11(string str, string str2, Action<string, string> predicate)
        {
            predicate(str, str2);
        }

    }
}
