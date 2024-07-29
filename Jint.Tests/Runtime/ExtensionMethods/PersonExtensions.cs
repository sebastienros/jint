using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime.ExtensionMethods;

public static class PersonExtensions
{
    public static int MultiplyAge(this Person person, int factor)
    {
        return person.Age * factor;
    }
}