using System;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Math
{
    public sealed class MathInstance : ObjectInstance
    {
        private readonly Engine _engine;

        public MathInstance(Engine engine, ObjectInstance prototype)
            : base(engine, prototype)
        {
            _engine = engine;
        }

        public override string Class
        {
            get
            {
                return "Math";
            }
        }

        public static MathInstance CreateMathObject(Engine engine, ObjectInstance prototype)
        {
            var math = new MathInstance(engine, prototype);
            math.DefineOwnProperty("abs", new ClrDataDescriptor<object, double>(engine, Abs), false);
            math.DefineOwnProperty("acos", new ClrDataDescriptor<object, double>(engine, Acos), false);
            math.DefineOwnProperty("asin", new ClrDataDescriptor<object, double>(engine, Asin), false);
            math.DefineOwnProperty("atan", new ClrDataDescriptor<object, double>(engine, Atan), false);
            math.DefineOwnProperty("atan2", new ClrDataDescriptor<object, double>(engine, Atan2), false);
            math.DefineOwnProperty("ceil", new ClrDataDescriptor<object, double>(engine, Ceil), false);
            math.DefineOwnProperty("cos", new ClrDataDescriptor<object, double>(engine, Cos), false);
            math.DefineOwnProperty("exp", new ClrDataDescriptor<object, double>(engine, Exp), false);
            math.DefineOwnProperty("floor", new ClrDataDescriptor<object, double>(engine, Floor), false);
            math.DefineOwnProperty("log", new ClrDataDescriptor<object, double>(engine, Log), false);
            math.DefineOwnProperty("max", new ClrDataDescriptor<object, double>(engine, Max), false);
            math.DefineOwnProperty("min", new ClrDataDescriptor<object, double>(engine, Min), false);
            math.DefineOwnProperty("pow", new ClrDataDescriptor<object, double>(engine, Pow), false);
            math.DefineOwnProperty("random", new ClrDataDescriptor<object, double>(engine, Random), false);
            math.DefineOwnProperty("round", new ClrDataDescriptor<object, double>(engine, Round), false);
            math.DefineOwnProperty("sin", new ClrDataDescriptor<object, double>(engine, Sin), false);
            math.DefineOwnProperty("sqrt", new ClrDataDescriptor<object, double>(engine, Sqrt), false);
            math.DefineOwnProperty("tan", new ClrDataDescriptor<object, double>(engine, Tan), false);

            math.FastAddProperty("E", System.Math.E, false, false, false);
            math.FastAddProperty("LN10", System.Math.Log(10), false, false, false);
            math.FastAddProperty("LN2", System.Math.Log(2), false, false, false);
            math.FastAddProperty("LOG2E", System.Math.Log(System.Math.E, 2), false, false, false);
            math.FastAddProperty("LOG10E", System.Math.Log(System.Math.E, 10), false, false, false);
            math.FastAddProperty("PI", System.Math.PI, false, false, false);
            math.FastAddProperty("SQRT1_2", System.Math.Sqrt(0.5), false, false, false);
            math.FastAddProperty("SQRT2", System.Math.Sqrt(2), false, false, false);

            return math;
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
