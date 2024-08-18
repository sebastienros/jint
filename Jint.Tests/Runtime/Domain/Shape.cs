using Jint.Tests.Runtime.Domain;

public class ShapeWithoutNameSpace : Shapes.Shape
{
    public override double Perimeter() => 42;
}

namespace Shapes
{
    public abstract class Shape
    {
        public int Id = 123;
        public abstract double Perimeter();
        public Colors Color { get; set; }
    }

    public class Circle : Shape
    {
        public class Meta
        {
            public Meta()
            {
                _description = "descp";
            }

            private string _description;

            public string Description
            {
                get
                {
                    return _description;
                }
                set
                {
                    _description = value;
                }
            }

            public enum Usage
            {
                Public,
                Private,
                Internal = 11
            }
        }

        public enum Kind
        {
            Unit,
            Ellipse,
            Round = 5
        }

        public Circle()
        {
        }

        public Circle(double radius)
        {
            Radius = radius;
        }

        public double Radius { get; set; }

        public override double Perimeter()
        {
            return Math.PI * Math.Pow(Radius, 2);
        }
    }
}
