namespace Jint.Native.Date
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.5
    /// </summary>
    public sealed class DatePrototype : DateInstance
    {
        private DatePrototype(Engine engine)
            : base(engine)
        {
        }

        public static DatePrototype CreatePrototypeObject(Engine engine, DateConstructor dateConstructor)
        {
            var obj = new DatePrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;

            obj.FastAddProperty("constructor", dateConstructor, false, false, false);

            return obj;
        }


    }
}
