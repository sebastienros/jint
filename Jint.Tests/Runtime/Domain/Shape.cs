using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jint.Tests.Runtime.Domain;

namespace Shapes
{
    public abstract class Shape
    {
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
            return Math.PI*Math.Pow(Radius, 2);
        }
    }
}
