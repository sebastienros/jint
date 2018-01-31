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
    }

}
