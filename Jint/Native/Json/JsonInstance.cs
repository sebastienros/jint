using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public sealed class JsonInstance : ObjectInstance
    {
        private readonly Engine _engine;

        private JsonInstance(Engine engine)
            : base(engine)
        {
            _engine = engine;
            Extensible = true;
        }

        public override string Class
        {
            get
            {
                return "JSON";
            }
        }

        public static JsonInstance CreateJsonObject(Engine engine)
        {
            var json = new JsonInstance(engine);
            return json;
        }

        public void Configure()
        {
            FastAddProperty("parse", new ClrFunctionInstance<JsonInstance, object>(Engine, Parse), false, false, false);
            FastAddProperty("stringify", new ClrFunctionInstance<JsonInstance, object>(Engine, Stringify), false, false, false);
        }

        public object Parse(JsonInstance thisObject, object[] arguments)
        {
            var parser = new JsonParser(_engine);

            return parser.Parse(arguments[0].ToString());
        }

        public object Stringify(JsonInstance thisObject, object[] arguments)
        {
            object 
                value = Undefined.Instance, 
                replacer = Undefined.Instance,
                space = Undefined.Instance;

            if (arguments.Length > 2)
            {
                space = arguments[2];
            }

            if (arguments.Length > 1)
            {
                replacer = arguments[1];
            }

            if (arguments.Length > 0)
            {
                value = arguments[0];
            }

            var serializer = new JsonSerializer(_engine);
            return serializer.Serialize(value, replacer, space);
        }
    }
}
