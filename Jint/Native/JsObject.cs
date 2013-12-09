using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native
{
    public struct JsObject
    {
        public JsObject(bool value)
        {
            _bool = value;
            _double = null;
            _object = null;
            _string = null;
            _type = Types.Boolean;
        }

        public JsObject(double value)
        {
            _bool = null;
            _double = value;
            _object = null;
            _string = null;
            _type = Types.Number;
        }

        public JsObject(ObjectInstance value)
        {
            _bool = null;
            _double = null;
            _object = value;
            _string = null;
            _type = Types.Object;
        }

        private bool? _bool;

        private double? _double;

        private ObjectInstance _object;

        private string _string;

        private Types _type;
    };
}
