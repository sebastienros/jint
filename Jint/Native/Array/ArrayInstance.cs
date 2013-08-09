using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jint.Native.Object;

namespace Jint.Native.Array
{
    public class ArrayInstance : ObjectInstance
    {
        private readonly List<object> _array = new List<object>();
 
        public ArrayInstance(ObjectInstance prototype) : base(prototype)
        {
        }

        public override string Class
        {
            get
            {
                return "Array";
            }
        }

        public int Length { get { return _array.Count; } }

        public void Push(object o)
        {
            _array.Add(o);
        }
    }
}
