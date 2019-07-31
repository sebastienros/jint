using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Iterator;

namespace Jint
{
    internal static class IteratorExtensions
    {
        internal static JsValue[] CopyToArray(this IteratorInstance iterator)
        {
            var items = new List<JsValue>();

            var item = iterator.Next();

            while (item.GetProperty("done").Value.AsBoolean() == false)
            {
                items.Add(item.GetProperty("value").Value);
                item = iterator.Next();
            }

            return items.ToArray();
        }
    }
}
