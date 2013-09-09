using System;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Math
{
    public sealed class MathInstance : ObjectInstance
    {
        private MathInstance(Engine engine):base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "Math";
            }
        }

        public static MathInstance CreateMathObject(Engine engine)
        {
            var math = new MathInstance(engine);
            math.Extensible = true;
            math.Prototype = engine.Object.PrototypeObject;

            
            return math;
        }

        public void Configure()
        {
            FastAddProperty("abs", new ClrFunctionInstance<object, double>(Engine, Abs), true, false, true);
            FastAddProperty("acos", new ClrFunctionInstance<object, double>(Engine, Acos), true, false, true);
            FastAddProperty("asin", new ClrFunctionInstance<object, double>(Engine, Asin), true, false, true);
            FastAddProperty("atan", new ClrFunctionInstance<object, double>(Engine, Atan), true, false, true);
            FastAddProperty("atan2", new ClrFunctionInstance<object, double>(Engine, Atan2), true, false, true);
            FastAddProperty("ceil", new ClrFunctionInstance<object, double>(Engine, Ceil), true, false, true);
            FastAddProperty("cos", new ClrFunctionInstance<object, double>(Engine, Cos), true, false, true);
            FastAddProperty("exp", new ClrFunctionInstance<object, double>(Engine, Exp), true, false, true);
            FastAddProperty("floor", new ClrFunctionInstance<object, double>(Engine, Floor), true, false, true);
            FastAddProperty("log", new ClrFunctionInstance<object, double>(Engine, Log), true, false, true);
            FastAddProperty("max", new ClrFunctionInstance<object, double>(Engine, Max), true, false, true);
            FastAddProperty("min", new ClrFunctionInstance<object, double>(Engine, Min), true, false, true);
            FastAddProperty("pow", new ClrFunctionInstance<object, double>(Engine, Pow), true, false, true);
            FastAddProperty("random", new ClrFunctionInstance<object, double>(Engine, Random), true, false, true);
            FastAddProperty("round", new ClrFunctionInstance<object, double>(Engine, Round), true, false, true);
            FastAddProperty("sin", new ClrFunctionInstance<object, double>(Engine, Sin), true, false, true);
            FastAddProperty("sqrt", new ClrFunctionInstance<object, double>(Engine, Sqrt), true, false, true);
            FastAddProperty("tan", new ClrFunctionInstance<object, double>(Engine, Tan), true, false, true);

            FastAddProperty("E", System.Math.E, false, false, false);
            FastAddProperty("LN10", System.Math.Log(10), false, false, false);
            FastAddProperty("LN2", System.Math.Log(2), false, false, false);
            FastAddProperty("LOG2E", System.Math.Log(System.Math.E, 2), false, false, false);
            FastAddProperty("LOG10E", System.Math.Log(System.Math.E, 10), false, false, false);
            FastAddProperty("PI", System.Math.PI, false, false, false);
            FastAddProperty("SQRT1_2", System.Math.Sqrt(0.5), false, false, false);
            FastAddProperty("SQRT2", System.Math.Sqrt(2), false, false, false);

        }

        private static double Abs(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Abs(x);
        }

        private static double Acos(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Acos(x);
        }

        private static double Asin(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Asin(x);
        }

        private static double Atan(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Atan(x);
        }

        private static double Atan2(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            var y = TypeConverter.ToNumber(arguments[1]);
            return System.Math.Atan2(x, y);
        }

        private static double Ceil(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Ceiling(x);
        }

        private static double Cos(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Cos(x);
        }

        private static double Exp(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Exp(x);
        }

        private static double Floor(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Floor(x);
        }

        private static double Log(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Log(x);
        }

        private static double Max(object thisObject, object[] arguments)
        {
            double max = TypeConverter.ToNumber(arguments[0]);
            for (int i = 0; i < arguments.Length; i++)
            {
                max = System.Math.Max(max, TypeConverter.ToNumber(arguments[i]));
            }
            return max;
        }

        private static double Min(object thisObject, object[] arguments)
        {
            double min = TypeConverter.ToNumber(arguments[0]);
            for (int i = 0; i < arguments.Length; i++)
            {
                min = System.Math.Min(min, TypeConverter.ToNumber(arguments[i]));
            }
            return min;
        }

        private static double Pow(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            var y = TypeConverter.ToNumber(arguments[1]);
            return System.Math.Pow(x, y);
        }

        private static double Random(object thisObject, object[] arguments)
        {
            return new Random().NextDouble();
        }

        private static double Round(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Round(x);
        }

        private static double Sin(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Sin(x);
        }

        private static double Sqrt(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Sqrt(x);
        }

        private static double Tan(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Tan(x);
        }


    }
}
