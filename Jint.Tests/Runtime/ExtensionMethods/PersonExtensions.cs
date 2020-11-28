using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esprima.Ast;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime.ExtensionMethods
{
    public static class PersonExtensions
    {
        public static int MultiplyAge(this Person person, int factor)
        {
            return person.Age * factor;
        }

    }
}
