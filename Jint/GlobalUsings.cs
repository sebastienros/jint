global using PropertyDictionary = Jint.Collections.HybridDictionary<Jint.Runtime.Descriptors.PropertyDescriptor>;
global using SymbolDictionary = Jint.Collections.DictionarySlim<Jint.Native.JsSymbol, Jint.Runtime.Descriptors.PropertyDescriptor>;

global using JsCallDelegate = System.Func<Jint.Native.JsValue, Jint.Native.JsValue[], Jint.Native.JsValue>;
global using JsCallArguments = Jint.Native.JsValue[];
