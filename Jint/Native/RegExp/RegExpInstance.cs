using System;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.RegExp
{
    public class RegExpInstance : ObjectInstance, IPrimitiveType
    {
        private readonly Engine _engine;

        public RegExpInstance(Engine engine)
            : base(engine)
        {
            _engine = engine;
        }

        public override string Class
        {
            get
            {
                return "RegExp";
            }
        }

        Types IPrimitiveType.Type
        {
            get { return Types.Boolean; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public string PrimitiveValue { get; set; }
    }
}
