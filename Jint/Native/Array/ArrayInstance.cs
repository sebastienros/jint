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
        private readonly Stack<object> _array = new Stack<object>();
 
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

        public double Length { get { return _array.Count; } }

        public void Push(object o)
        {
            _array.Push(o);
        }

        public object Pop()
        {
            return _array.Pop();
        }
    }
}
