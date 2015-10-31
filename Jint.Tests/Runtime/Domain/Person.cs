using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Tests.Runtime.Domain
{
    public class Person : IPerson
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public void SetValues(int? age = null, string name = null)
        {
            if (age.HasValue) { Age = age.Value; }
            if (name != null) { Name = name; }
        }
    }

}
