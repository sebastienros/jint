using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error
{
    public sealed class ErrorInstance : ObjectInstance
    {
        public ErrorInstance(Engine engine, ObjectInstance prototype, string name)
            : base(engine, prototype)
        {
            Name = name;
            Message = "";
        }

        public override string Class
        {
            get
            {
                return "Error";
            }
        }

        public object Message { get; set; }

        public object Name { get; set; }

        public string ToErrorString()
        {
            string name;
            string msg;

            if (Name == null || Name == "" || Name == Undefined.Instance)
            {
                name = "Error";
            }
            else
            {
                name = TypeConverter.ToString(Name);
            }

            if (Message == Undefined.Instance)
            {
                msg = "";
            }
            else
            {
                msg = TypeConverter.ToString(Message);
            }

            if (name == "")
            {
                return msg;
            }

            if (msg == "")
            {
                return name;
            }

            return name + ":" + msg;
        }
    }
}
