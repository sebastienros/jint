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
        public static readonly JsSymbol MatchAll = new JsSymbol("Symbol.matchAll", 5);
        public static readonly JsSymbol Replace = new JsSymbol("Symbol.replace", 6);
        public static readonly JsSymbol Search = new JsSymbol("Symbol.search", 7);
        public static readonly JsSymbol Species = new JsSymbol("Symbol.species", 8);
        public static readonly JsSymbol Split = new JsSymbol("Symbol.split", 9);
        public static readonly JsSymbol ToPrimitive = new JsSymbol("Symbol.toPrimitive", 10);
        public static readonly JsSymbol ToStringTag = new JsSymbol("Symbol.toStringTag", 11);
        public static readonly JsSymbol Unscopables = new JsSymbol("Symbol.unscopables", 12);

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
                [HasInstance.ToPropertyKey().Name] = HasInstance,
                [IsConcatSpreadable.ToPropertyKey().Name] = IsConcatSpreadable,
                [Iterator.ToPropertyKey().Name] = Iterator,
                [Match.ToPropertyKey().Name] = Match,
                [Replace.ToPropertyKey().Name] = Replace,
                [Search.ToPropertyKey().Name] = Search,
                [Species.ToPropertyKey().Name] = Species,
                [Split.ToPropertyKey().Name] = Split,
                [ToPrimitive.ToPropertyKey().Name] = ToPrimitive,
                [ToStringTag.ToPropertyKey().Name] = ToStringTag,
                [Unscopables.ToPropertyKey().Name] = Unscopables
            };
        }

        internal bool TryGetSymbol(Key key, out JsSymbol symbol)
        {
            symbol = null;
            return _customSymbolLookup != null && _customSymbolLookup.TryGetValue(key.Name, out symbol);
        }

        internal void Add(JsSymbol symbol)
        {
            _customSymbolLookup ??= new StringDictionarySlim<JsSymbol>();
            _customSymbolLookup[symbol.ToPropertyKey().Name] = symbol;
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