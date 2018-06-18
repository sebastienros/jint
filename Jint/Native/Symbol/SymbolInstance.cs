using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Symbol
{
    public class SymbolInstance : ObjectInstance, IPrimitiveInstance
    {
        public SymbolInstance(Engine engine)
            : base(engine, objectClass: "Symbol")
        {
        }

        Types IPrimitiveInstance.Type => Types.Symbol;

        JsValue IPrimitiveInstance.PrimitiveValue => SymbolData;

        public JsSymbol SymbolData { get; set; }
    }
}
