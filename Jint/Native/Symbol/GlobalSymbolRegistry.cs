using System.Threading;
using Jint.Collections;

namespace Jint.Native.Symbol
{
    public class GlobalSymbolRegistry
    {
        public static readonly JsSymbol HasInstance = new JsSymbol("Symbol.hasInstance", 1);
        public static readonly JsSymbol IsConcatSpreadable = new JsSymbol("Symbol.isConcatSpreadable", 2);
        public static readonly JsSymbol Iterator = new JsSymbol("Symbol.iterator", 3);
        public static readonly JsSymbol Match = new JsSymbol("Symbol.match", 4);
        public static readonly JsSymbol Replace = new JsSymbol("Symbol.replace", 5);
        public static readonly JsSymbol Search = new JsSymbol("Symbol.search", 6);
        public static readonly JsSymbol Species = new JsSymbol("Symbol.species", 7);
        public static readonly JsSymbol Split = new JsSymbol("Symbol.split", 8);
        public static readonly JsSymbol ToPrimitive = new JsSymbol("Symbol.toPrimitive", 9);
        public static readonly JsSymbol ToStringTag = new JsSymbol("Symbol.toStringTag", 10);
        public static readonly JsSymbol Unscopables = new JsSymbol("Symbol.unscopables", 11);

        // well-known globals
        private static readonly StringDictionarySlim<JsSymbol> _globalLookup;

        // engine-specific created by scripts
        private StringDictionarySlim<JsSymbol> _customSymbolLookup;

        // custom symbol identity is based on running counter 
        private int _symbolCounter = 100;

        static GlobalSymbolRegistry()
        {
            _globalLookup = new StringDictionarySlim<JsSymbol>
            {
                [HasInstance._value] = HasInstance,
                [IsConcatSpreadable._value] = IsConcatSpreadable,
                [Iterator._value] = Iterator,
                [Match._value] = Match,
                [Replace._value] = Replace,
                [Search._value] = Search,
                [Species._value] = Species,
                [Split._value] = Split,
                [ToPrimitive._value] = ToPrimitive,
                [ToStringTag._value] = ToStringTag,
                [Unscopables._value] = Unscopables
            };
        }

        internal bool TryGetSymbol(string key, out JsSymbol symbol)
        {
            if (_globalLookup.TryGetValue(key, out symbol))
            {
                return true;
            }

            return _customSymbolLookup != null && _customSymbolLookup.TryGetValue(key, out symbol);
        }

        internal void Add(JsSymbol symbol)
        {
            _customSymbolLookup ??= new StringDictionarySlim<JsSymbol>();
            _customSymbolLookup[symbol._value] = symbol;
        }

        internal JsSymbol CreateSymbol(JsValue description)
        {
            var identity = Interlocked.Increment(ref _symbolCounter);
            return new JsSymbol(description, identity);
        }

        internal JsSymbol CreateSymbol(string description)
        {
            var identity = Interlocked.Increment(ref _symbolCounter);
            return new JsSymbol(description, identity);
        }
    }
}