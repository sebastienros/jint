using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Symbol
{
    public class SymbolInstance : ObjectInstance, IPrimitiveInstance
    {
        public SymbolInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "Symbol";
            }
        }

        Types IPrimitiveInstance.Type
        {
            get { return Types.Symbol; }
        }

        JsValue IPrimitiveInstance.PrimitiveValue
        {
            get { return SymbolData; }
        }

        public JsSymbol SymbolData { get; set; }
    }
}
