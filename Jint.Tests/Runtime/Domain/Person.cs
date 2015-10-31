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

        public void SetOptionalValues(string name = null, int age = 0)
        {
            if (age != 0) { Age = age; }
            if (name != null) { Name = name; }
        }

        public override string ToString()
        {
            return Name;
        }
    }

}
