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
            math.DefineOwnProperty("abs", new ClrDataDescriptor<MathInstance, double>(engine, Abs), false);
            math.DefineOwnProperty("acos", new ClrDataDescriptor<MathInstance, double>(engine, Acos), false);
            math.DefineOwnProperty("asin", new ClrDataDescriptor<MathInstance, double>(engine, Asin), false);
            math.DefineOwnProperty("atan", new ClrDataDescriptor<MathInstance, double>(engine, Atan), false);
            math.DefineOwnProperty("atan2", new ClrDataDescriptor<MathInstance, double>(engine, Atan2), false);
            math.DefineOwnProperty("ceil", new ClrDataDescriptor<MathInstance, double>(engine, Ceil), false);
            math.DefineOwnProperty("cos", new ClrDataDescriptor<MathInstance, double>(engine, Cos), false);
            math.DefineOwnProperty("exp", new ClrDataDescriptor<MathInstance, double>(engine, Exp), false);
            math.DefineOwnProperty("floor", new ClrDataDescriptor<MathInstance, double>(engine, Floor), false);
            math.DefineOwnProperty("log", new ClrDataDescriptor<MathInstance, double>(engine, Log), false);
            math.DefineOwnProperty("max", new ClrDataDescriptor<MathInstance, double>(engine, Max), false);
            math.DefineOwnProperty("min", new ClrDataDescriptor<MathInstance, double>(engine, Min), false);
            math.DefineOwnProperty("pow", new ClrDataDescriptor<MathInstance, double>(engine, Pow), false);
            math.DefineOwnProperty("random", new ClrDataDescriptor<MathInstance, double>(engine, Random), false);
            math.DefineOwnProperty("round", new ClrDataDescriptor<MathInstance, double>(engine, Round), false);
            math.DefineOwnProperty("sin", new ClrDataDescriptor<MathInstance, double>(engine, Sin), false);
            math.DefineOwnProperty("sqrt", new ClrDataDescriptor<MathInstance, double>(engine, Sqrt), false);
            math.DefineOwnProperty("tan", new ClrDataDescriptor<MathInstance, double>(engine, Tan), false);

            return math;
        }

        private static double Abs(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Abs(x);
        }

        private static double Acos(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Acos(x);
        }

        private static double Asin(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Asin(x);
        }

        private static double Atan(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Atan(x);
        }

        private static double Atan2(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            var y = TypeConverter.ToNumber(arguments[1]);
            return System.Math.Atan2(x, y);
        }

        private static double Ceil(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Ceiling(x);
        }

        private static double Cos(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Cos(x);
        }

        private static double Exp(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Exp(x);
        }

        private static double Floor(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Floor(x);
        }

        private static double Log(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Log(x);
        }

        private static double Max(MathInstance thisObject, object[] arguments)
        {
            double max = TypeConverter.ToNumber(arguments[0]);
            for (int i = 0; i < arguments.Length; i++)
            {
                max = System.Math.Max(max, TypeConverter.ToNumber(arguments[i]));
            }
            return max;
        }

        private static double Min(MathInstance thisObject, object[] arguments)
        {
            double min = TypeConverter.ToNumber(arguments[0]);
            for (int i = 0; i < arguments.Length; i++)
            {
                min = System.Math.Min(min, TypeConverter.ToNumber(arguments[i]));
            }
            return min;
        }

        private static double Pow(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            var y = TypeConverter.ToNumber(arguments[1]);
            return System.Math.Pow(x, y);
        }

        private static double Random(MathInstance thisObject, object[] arguments)
        {
            return new Random().NextDouble();
        }

        private static double Round(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Round(x);
        }

        private static double Sin(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Sin(x);
        }

        private static double Sqrt(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Sqrt(x);
        }

        private static double Tan(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Tan(x);
        }


    }
}
