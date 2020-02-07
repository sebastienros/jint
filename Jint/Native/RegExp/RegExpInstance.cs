using System.Collections.Generic;
using System.Text.RegularExpressions;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.RegExp
{
    public class RegExpInstance : ObjectInstance
    {
        internal const string regExpForMatchingAllCharacters = "(?:)";
        internal static readonly JsString PropertyLastIndex = new JsString("lastIndex");

        private string _flags;

        private PropertyDescriptor _prototypeDescriptor;

        public RegExpInstance(Engine engine)
            : base(engine, ObjectClass.RegExp)
        {
            Source = regExpForMatchingAllCharacters;
        }

        public Regex Value { get; set; }
        public string Source { get; set; }

        public string Flags
        {
            get => _flags;
            set
            {
                _flags = value;
                foreach (var c in _flags)
                {
                    switch (c)
                    {
                        case 'i':
                            IgnoreCase = true;
                            break;
                        case 'm':
                            Multiline = true;
                            break;
                        case 'g':
                            Global = true;
                            break;
                        case 's':
                            DotAll = true;
                            break;
                        case 'y':
                            Sticky = true;
                            break;
                        case 'u':
                            FullUnicode = true;
                            break;
                    }
                }
            }
        }

        public bool DotAll { get; private set; }
        public bool Global { get; private set; }
        public bool IgnoreCase { get; private set; }
        public bool Multiline { get; private set; }
        public bool Sticky { get; private set; }
        public bool FullUnicode { get; private set; }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == PropertyLastIndex)
            {
                return _prototypeDescriptor ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(property);
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (property == PropertyLastIndex)
            {
                _prototypeDescriptor = desc;
                return;
            }

            base.SetOwnProperty(property, desc);
        }

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            if (_prototypeDescriptor != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(PropertyLastIndex, _prototypeDescriptor);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types)
        {
            var keys = new List<JsValue>();
            if (_prototypeDescriptor != null)
            {
                keys.Add(PropertyLastIndex);
            }

            keys.AddRange(base.GetOwnPropertyKeys(types));
            return keys;
        }

        public override bool HasOwnProperty(JsValue property)
        {
            if (property == PropertyLastIndex)
            {
                return _prototypeDescriptor != null;
            }

            return base.HasOwnProperty(property);
        }
    }
}