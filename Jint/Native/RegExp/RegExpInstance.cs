using System;
using Jint.Native.Object;

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

        TypeCode IPrimitiveType.TypeCode
        {
            get { return TypeCode.Boolean; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public string PrimitiveValue { get; set; }
    }
}
