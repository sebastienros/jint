using System.Collections.Generic;

namespace Jint.Parser
{
    public static class ParserExtensions
    {
        public static T Pop<T>(this List<T> list)
        {
            var lastIndex = list.Count - 1;
            var last = list[lastIndex];
            list.RemoveAt(lastIndex);
            return last;
        }

        public static void Push<T>(this List<T> list, T item)
        {
            list.Add(item);
        }
    }
}