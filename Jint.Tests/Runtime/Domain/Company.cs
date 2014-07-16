using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Tests.Runtime.Domain
{
    public class Company : ICompany, IComparable<ICompany>
    {
        private string _name;
        private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>();

        public Company(string name)
        {
            _name = name;
        }

        string ICompany.Name
        {
            get { return _name; }
            set { _name = value; }
        }

        string ICompany.this[string key]
        {
            get { return _dictionary[key]; }
            set { _dictionary[key] = value; }
        }

        int IComparable<ICompany>.CompareTo(ICompany other)
        {
            return string.Compare(_name, other.Name, StringComparison.CurrentCulture);
        }
    }
}
