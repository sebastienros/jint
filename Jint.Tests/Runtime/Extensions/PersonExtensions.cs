using System;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime.Extensions
{
    public static class PersonExtensions
    {
        public static int GetBirthYear(this Person person)
        {
            return DateTime.Now.Year - person.Age;
        }

        public static string GetFormattedName(this Person person)
        {
            return $"{person.Name} ({person.Age})";
        }
    }
}
