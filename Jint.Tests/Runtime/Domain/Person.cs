namespace Jint.Tests.Runtime.Domain;

public class Person : IPerson
{
    public string Name { get; set; }
    public int Age { get; set; }

    public Type TypeProperty { get; set; } = typeof(Person);

    public override string ToString()
    {
        return Name;
    }

    protected bool Equals(Person other)
    {
        return Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((Person) obj);
    }

    public override int GetHashCode()
    {
        return (Name != null ? Name.GetHashCode() : 0);
    }
}