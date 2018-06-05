using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Jint.Native.Object
{
    public class JsExpandoObject :
#if __IOS__
        IDictionary<string, object>
#else
        DynamicObject
        , IDictionary<string, object>
#endif
    {

        private object ToObject(JsValue v)
        {
            //if (v.IsObject())
            //    return v.ToObject();
            //if (v.IsNumber())
            //{
            //    return v.AsNumber();
            //}
            //if (v.IsBoolean())
            //    return v.AsBoolean();
            //if (v.IsDate())
            //    return v.AsDate().ToDateTime();
            //if (v.IsString())
            //    return v.AsString();
            //if (v.IsArray())
            //    return v.AsArray().ToObject();
            return v.ToObject();
        }


#if !__IOS__

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var p = EnumerableValues
                .Where(x => x.Key == binder.Name)
                .Select(x=> ToObject(x.Value.Value))
                .FirstOrDefault();
            if (p != null)
            {
                result = p;
                return true;
            }
            else {
                result = null;
            }
            return false;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var p = EnumerableValues
                .Where(x => x.Key == binder.Name)
                .Select(x => x.Value.Value.AsInstance<FunctionInstance>())
                .FirstOrDefault();
            if (p != null)
            {
                var va = args.Select(x => x is JsValue v ? v : JsValue.FromObject(Instance.Engine, x)).ToArray();
                var r = p.Call(Instance, va);
                result = r != null ? r.ToObject() : null;
            }
            else {
                result = null;
            }
            return false;
        }
#endif


        private WeakReference<ObjectInstance> objectInstance;

        public JsExpandoObject(ObjectInstance objectInstance)
        {
            this.objectInstance = new WeakReference<ObjectInstance>(objectInstance);
        }

        public ObjectInstance Instance {
            get {
                if (objectInstance.TryGetTarget(out var i))
                    return i;
                throw new ObjectDisposedException("JavaScript Object");
            }
        }

        public object this[string key] {
            get => ToObject(Instance.Get(key));
            set => throw new System.NotImplementedException();
        }

        private IEnumerable<KeyValuePair<string, PropertyDescriptor>> EnumerableValues =>
            Instance.GetOwnProperties().Where(x => x.Value.Enumerable == true);

        public ICollection<string> Keys =>
            EnumerableValues.Select(x => x.Key).ToList();

        public ICollection<object> Values =>
            EnumerableValues.Select(x => ToObject(x.Value.Value)).ToList();

        public int Count =>
            EnumerableValues.Count();

        public bool IsReadOnly =>
            true;

        // public event PropertyChangedEventHandler PropertyChanged;

        public void Add(string key, object value)
        {
            throw new System.NotImplementedException();
        }

        public void Add(KeyValuePair<string, object> item)
        {
            throw new System.NotImplementedException();
        }

        public void Clear()
        {
            throw new System.NotImplementedException();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            throw new System.NotImplementedException();
        }

        public bool ContainsKey(string key)
        {
            throw new System.NotImplementedException();
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (var k in this.EnumerableValues) {
                yield return new KeyValuePair<string, object>(k.Key, ToObject(k.Value.Value));
            }
        }

        public bool Remove(string key)
        {
            throw new System.NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            throw new System.NotImplementedException();
        }

        public bool TryGetValue(string key, out object value)
        {
            throw new System.NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this as IEnumerator<KeyValuePair<string, object>>;
        }
    }
}