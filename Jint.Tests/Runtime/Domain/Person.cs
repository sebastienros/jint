using System;

namespace Jint.Tests.Runtime.Domain
{
    public class Person : IPerson
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public Type TypeProperty { get; set; } = typeof(Person);

        public override string ToString()
        {
            return Name;
        }
    }

}
