namespace Jint.Native.RegExp
{
    public sealed class RegExpPrototype : RegExpInstance
    {
        private RegExpPrototype(Engine engine)
            : base(engine)
        {
        }

        public static RegExpPrototype CreatePrototypeObject(Engine engine, RegExpConstructor regExpConstructor)
        {
            var obj = new RegExpPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.PrimitiveValue = "";
            obj.Extensible = true;

            obj.FastAddProperty("constructor", regExpConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
        }
    }
}
