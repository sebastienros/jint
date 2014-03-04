using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shapes
{
    public abstract class Shape
    {
        public abstract double Perimeter();
    }

    public class Circle : Shape
    {
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
            return Math.PI*Math.Pow(Radius, 2);
        }
    }
}
