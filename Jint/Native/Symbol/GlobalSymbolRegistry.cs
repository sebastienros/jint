using System.Collections.Generic;

namespace Jint.Native.Symbol
{
    public class GlobalSymbolRegistry : Dictionary<string, JsSymbol>
    {
        public static JsSymbol HasInstance { get; } = new JsSymbol("Symbol.hasInstance");
        public static JsSymbol IsConcatSpreadable { get; } = new JsSymbol("Symbol.isConcatSpreadable");
        public static JsSymbol Iterator { get; } = new JsSymbol("Symbol.iterator");
        public static JsSymbol Match { get; } = new JsSymbol("Symbol.match");
        public static JsSymbol Replace { get; } = new JsSymbol("Symbol.replace");
        public static JsSymbol Search { get; } = new JsSymbol("Symbol.search");
        public static JsSymbol Species { get; } = new JsSymbol("Symbol.species");
        public static JsSymbol Split { get; } = new JsSymbol("Symbol.split");
        public static JsSymbol ToPrimitive { get; } = new JsSymbol("Symbol.toPrimitive");
        public static JsSymbol ToStringTag { get; } = new JsSymbol("Symbol.toStringTag");
        public static JsSymbol Unscopables { get; } = new JsSymbol("Symbol.unscopables");
    }
}
