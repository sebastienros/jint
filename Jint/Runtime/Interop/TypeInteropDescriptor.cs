using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Jint.Runtime.Interop
{
    internal class TypeInteropDescriptor
    {
        public Type WrappedType;

        public PropertyProxy[] TypeProperties;
        public FieldInfo[] TypeFields;
        public MethodInfo[] TypeMethods;
        public Type[] TypeInterfaces;

        public IDictionary<string, MatchedMember> MatchedMembersCache = new Dictionary<string, MatchedMember>();
        public IDictionary<string, PropertyDescriptor> Properties = new Dictionary<string, PropertyDescriptor>();

        public TypeInteropDescriptor(Type type)
        {
            WrappedType = type;

            CacheProperties();
        }

        public PropertyDescriptor GetProperty(Engine engine, string propertyName)
        {
            PropertyDescriptor x;
            if (Properties.TryGetValue(propertyName, out x))
                return x;

            var member = FindMember(propertyName);

            switch (member.Type)
            {
                case MatchedMemberType.Property:
                    {
                        var descriptor = new PropertyInfoDescriptor(engine, member.Property);
                        Properties.Add(propertyName, descriptor);
                        return descriptor;
                    }

                case MatchedMemberType.Field:
                    {
                        var descriptor = new FieldInfoDescriptor(engine, member.Field);
                        Properties.Add(propertyName, descriptor);
                        return descriptor;
                    }

                case MatchedMemberType.Method:
                    {
                        var descriptor = new PropertyDescriptor(new MethodInfoFunctionInstance(engine, member.Methods), false, true, false);
                        Properties.Add(propertyName, descriptor);
                        return descriptor;
                    }

                case MatchedMemberType.Indexer:
                    {
                        if (member.IndexerType != null)
                        {
                            return new IndexDescriptor(engine, member.IndexerType, propertyName);
                        }
                        else
                        {
                            return new IndexDescriptor(engine, WrappedType, propertyName);
                        }
                    }
            }

            return PropertyDescriptor.Undefined;
        }

        public MatchedMember FindMember(string propertyName)
        {
            MatchedMember member;
            if (MatchedMembersCache.TryGetValue(propertyName, out member))
            {
                return member;
            }
            
            // look for a property
            var property = TypeProperties
                .Where(p => EqualsIgnoreCasing(p.Name, propertyName))
                .FirstOrDefault();
            if (property != null)
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Property,
                    Property = property
                });
                return member;
            }

            // look for a field
            var field = TypeFields
                .Where(f => EqualsIgnoreCasing(f.Name, propertyName))
                .FirstOrDefault();
            if (field != null)
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Field,
                    Field = new FieldProxy(field)
                });
                return member;
            }

            // if no properties were found then look for a method 
            var methods = TypeMethods
                .Where(m => EqualsIgnoreCasing(m.Name, propertyName))
                .ToArray();

            if (methods.Any())
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Method,
                    Methods = new MethodGroup(methods)
                });
                return member;
            }

            // if no methods are found check if target implemented indexing
            if (TypeProperties.Where(p => p.HasIndexParameters).FirstOrDefault() != null)
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Indexer
                });
                return member;
            }

            // try to find a single explicit property implementation
            var explicitProperties = 
                (from iface in TypeInterfaces
                from iprop in iface.GetProperties()
                where EqualsIgnoreCasing(iprop.Name, propertyName)
                select iprop).ToArray();

            if (explicitProperties.Length == 1)
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Property,
                    Property = new PropertyProxy(explicitProperties[0], explicitProperties[0].GetGetMethod(), explicitProperties[0].GetSetMethod())
                });
               
                return member;
            }

            // try to find explicit method implementations
            var explicitMethods = 
                (from iface in TypeInterfaces
                from imethod in iface.GetMethods()
                where EqualsIgnoreCasing(imethod.Name, propertyName)
                select imethod).ToArray();

            if (explicitMethods.Length > 0)
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Method,
                    Methods = new MethodGroup(explicitMethods)
                });
                return member;
            }

            // try to find explicit indexer implementations
            var explicitIndexers =
                (from iface in TypeInterfaces
                from iprop in iface.GetProperties()
                where iprop.GetIndexParameters().Length != 0
                select iprop).ToArray();

            if (explicitIndexers.Length == 1)
            {
                MatchedMembersCache[propertyName] = (member = new MatchedMember()
                {
                    Type = MatchedMemberType.Indexer,
                    IndexerType = explicitIndexers[0].DeclaringType

                });
                return member;
            }

            return new MatchedMember()
            {
                Type = MatchedMemberType.NotFound
            };
        }

        private void CacheProperties()
        {
            TypeProperties =
                WrappedType.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public).
                Select(property => new PropertyProxy(property, property.GetGetMethod(), property.GetSetMethod())).ToArray();
            TypeFields = WrappedType.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
            TypeMethods = WrappedType.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);

            TypeInterfaces = WrappedType.GetInterfaces();
        }

        private bool EqualsIgnoreCasing(string s1, string s2)
        {
            bool equals = false;

            if (s1.Length == s2.Length)
            {
                if (s1.Length > 0 && s2.Length > 0)
                {
                    equals = (Char.ToLowerInvariant(s1[0]) == Char.ToLowerInvariant(s2[0]));
                }
                if (s1.Length > 1 && s2.Length > 1)
                {
                    equals = equals && (s1.Substring(1) == s2.Substring(1));
                }
            }
            return equals;
        }
    }

    internal enum MatchedMemberType
    {
        Property,
        Field,
        Method,
        Indexer,
        NotFound	
    }

    internal class MatchedMember
    {
        public MatchedMemberType Type;

        public PropertyProxy Property;
        public FieldProxy Field;
        public MethodGroup Methods;
        public Type IndexerType;
    }
}
