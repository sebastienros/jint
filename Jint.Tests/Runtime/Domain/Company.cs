namespace Jint.Tests.Runtime.Domain;

public class Company : ICompany, IComparable<ICompany>
{
    private string _name;

    private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>()
    {
        {"key", "value"}
    };

    public Company(string name)
    {
        _name = name;
    }

    string ICompany.Name
    {
        get => _name;
        set => _name = value;
    }

    string ICompany.this[string key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public string Item => "item thingie";

    int IComparable<ICompany>.CompareTo(ICompany other)
    {
        return string.Compare(_name, other.Name, StringComparison.CurrentCulture);
    }

    public IEnumerable<char> GetNameChars()
    {
        foreach (var c in _name)
        {
            yield return c;
        }
    }
}