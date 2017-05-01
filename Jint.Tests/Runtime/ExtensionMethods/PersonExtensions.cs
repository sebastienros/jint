using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime.ExtensionMethods
{
    public static class PersonExtensions
    {
        public static int GetBirthYear(this Person person)
        {
            return DateTime.Now.Year - person.Age;
        }
    }
}
