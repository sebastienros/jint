using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Extensions
{
    internal static class IteratorExtensions
    {
        internal static List<JsValue> CopyToList(this IIterator iterator)
        {
            var items = new List<JsValue>();

            iterator.TryIteratorStep(out var item);

            int i = 0;

            while (!TypeConverter.ToBoolean(item.Get("done")))
            {
                try
                {
                    var jsValue = item.Get("value");
                    items.Add(jsValue);
                }
                catch
                {
                    break;
                }

                iterator.TryIteratorStep(out item);

                if (i++ > 1000)
                {
                    throw new Exception("TODO this logic is still flawed");
                }
            }

            return items;
        }
    }
}