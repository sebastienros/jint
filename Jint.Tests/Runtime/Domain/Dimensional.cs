namespace Jint.Tests.Runtime.Domain
{
    public class Dimensional : IComparable<Dimensional>
    {
        private readonly MeasureUnit[] PossibleMeasureUnits = new MeasureUnit[] { new MeasureUnit("Mass", "kg", 1.0), new MeasureUnit("Mass", "gr", 0.001), new MeasureUnit("Count", "piece", 1.0) };

        public MeasureUnit MeasureUnit { get; private set; }

        public double Value { get; private set; }

        public double NormalizatedValue
        {
            get
            {
                return Value * MeasureUnit.RelativeValue;
            }
        }

        public Dimensional(string measureUnit, double value)
        {
            MeasureUnit = GetMeasureUnitByName(measureUnit);
            Value = value;
        }

        public static Dimensional operator +(Dimensional left, Dimensional right)
        {
            if (left.MeasureUnit.MeasureType != right.MeasureUnit.MeasureType)
                throw new InvalidOperationException("Dimensionals with different measure types are non-summable");

            return new Dimensional(left.MeasureUnit.RelativeValue <= right.MeasureUnit.RelativeValue ? left.MeasureUnit.Name : right.MeasureUnit.Name,
                                                            left.Value * left.MeasureUnit.RelativeValue + right.Value * right.MeasureUnit.RelativeValue);
        }

        private MeasureUnit GetMeasureUnitByName(string name)
        {
            return PossibleMeasureUnits.FirstOrDefault(mu => mu.Name == name);
        }

        public int CompareTo(Dimensional obj)
        {
            if (MeasureUnit.MeasureType != obj.MeasureUnit.MeasureType)
                throw new InvalidOperationException("Dimensionals with different measure types are non-comparable");
            return NormalizatedValue.CompareTo(obj.NormalizatedValue);
        }

        public override string ToString()
        {
            return Value + " " + MeasureUnit.Name;
        }
    }

    public class MeasureUnit
    {
        public string MeasureType { get; set; }
        public string Name { get; set; }
        public double RelativeValue { get; set; }

        public MeasureUnit(string measureType, string Name, double relativeValue)
        {
            this.MeasureType = measureType;
            this.Name = Name;
            this.RelativeValue = relativeValue;
        }
    }
}
