using System;
using Generic = System.Collections.Generic;
using System.Linq;

namespace Jint.Runtime
{
    public class NamespaceCacheElement
    {
        #region "Instance"
        // Fields
        String _name;
        NamespaceCacheElement _parent;

        Generic.Dictionary<String, NamespaceCacheElement> _ncChildren = null;

        // Properties
        public String Name {
            get { return _name;  }
        }

        public String AbsoluteName {
            get { return assembleAbsoluleName(this);  }
        }

        public NamespaceCacheRoot Global {
            get { return traverseToRoot(this); }
        }

        public NamespaceCacheElement Parent {
            get { return _parent; }
        }

        public NamespaceCacheElement[] Ancestors {
            get { return enumerateAncestors(this); }
        }
        
        public NamespaceCacheElement[] Children {
            get {
                if (_ncChildren == null) return new NamespaceCacheElement[0];
                return _ncChildren.Values.ToArray();
            }
        }

        // Gah, why doesn't C# support indexed properties yet >:(
        /*public NamespaceCacheElement Children[String ns] {
            get { return _children[ns]; }
        }*/

        // Constructor (public construction not allowed)
        protected NamespaceCacheElement(NamespaceCacheElement parent, String name) {
            _parent = parent;
            _name = name;
        }

        // Instance Indexer
        public NamespaceCacheElement this[String ns] {
            get { return findDescendent(this, ns, true); }
        }

        // Methods
        public bool ContainsNamespace(NamespaceCacheElement ncElem) {
            return _ncChildren != null && _ncChildren.ContainsKey(ncElem.Name);
        }

        public bool ContainsNamespace(String ns) {
            return findDescendent(this, ns) != null;
        }
       
        #endregion

        #region "Static"
        protected static NamespaceCacheRoot traverseToRoot(NamespaceCacheElement ncElem) {
            if (ncElem is NamespaceCacheRoot) return (NamespaceCacheRoot)ncElem;

            NamespaceCacheElement ncOldestAncestor = enumerateAncestors(ncElem).DefaultIfEmpty(null).First();
            if (ncOldestAncestor == null || !(ncOldestAncestor is NamespaceCacheRoot)) return null;

            return (NamespaceCacheRoot)ncOldestAncestor;
        }

        protected static NamespaceCacheElement[] enumerateAncestors(NamespaceCacheElement ncElem) {
            return enumerateAncestors(ncElem, false);
        }

        protected static NamespaceCacheElement[] enumerateAncestors(NamespaceCacheElement ncElem, bool includeSelf) {
            Generic.List<NamespaceCacheElement> ncAncestors = new Generic.List<NamespaceCacheElement>();

            if (includeSelf) ncAncestors.Insert(0, ncElem);

            while (ncElem.Parent != null) {
                ncElem = ncElem.Parent;
                ncAncestors.Insert(0, ncElem);
            }

            return ncAncestors.ToArray();
        }

        protected static String assembleAbsoluleName(NamespaceCacheElement ncElem) {
            if (ncElem is NamespaceCacheRoot) return "[Global]";
            NamespaceCacheElement[] ncAncestors = enumerateAncestors(ncElem, true);

            if (ncAncestors.First() is NamespaceCacheRoot)
                return String.Join(".", ncAncestors.Skip(1).Select(x => x.Name).ToArray());

            throw new Exception("Orphan namespace, cannot resolve full path");
        }

        protected static NamespaceCacheElement findDescendent(NamespaceCacheElement ncElem, String ns) {
            return findDescendent(ncElem, ns, false);
        }

        protected static NamespaceCacheElement findDescendent(NamespaceCacheElement ncElem, String ns, bool throwOnNamespaceNotFound) {
            if (String.IsNullOrWhiteSpace(ns)) throw new ArgumentException("Invalid argument: namespace");

            String[] nsParts = ns.Split('.');

            for (int i = 0; i < nsParts.Length; i++) {
                String nsPart = nsParts[i];

                if (ncElem._ncChildren == null || !ncElem._ncChildren.ContainsKey(nsPart)) {
                    ncElem = null;
                    break;
                }

                ncElem = ncElem._ncChildren[nsPart];
            }

            if (throwOnNamespaceNotFound && ncElem == null)
                throw new System.Collections.Generic.KeyNotFoundException();

            return ncElem;
        }

        protected static NamespaceCacheElement AppendNamespaceElement(NamespaceCacheElement ncParent, string name) {
            if (ncParent == null) throw new ArgumentException("Invalid argument: parent");
            if (String.IsNullOrWhiteSpace(name) || name.Contains(".")) throw new ArgumentException("Invalid argument: name");

            NamespaceCacheElement ncChild = findDescendent(ncParent, name, false);

            if (ncChild == null)
            {
                ncChild = new NamespaceCacheElement(ncParent, name);
                if (ncParent._ncChildren == null) ncParent._ncChildren = new Generic.Dictionary<String, NamespaceCacheElement>(StringComparer.OrdinalIgnoreCase);
                ncParent._ncChildren[name] = ncChild;
            }

            return ncChild;
        }
        #endregion

        public override String ToString() {
            return String.Format("{0}  -  ({1})", _name, AbsoluteName);
        }

    }

    public class NamespaceCacheRoot : NamespaceCacheElement {
        public NamespaceCacheRoot() : base(null, "[Global]") {
        }

        public void populate(params System.Reflection.Assembly[] assemblies) {
            String[] namespaces = (from System.Reflection.Assembly assembly in assemblies.Distinct()
                                   from System.Type type in assembly.GetTypes()
                                   where !System.String.IsNullOrEmpty(type.Namespace)
                                   orderby type.Namespace
                                   select type.Namespace).Distinct().ToArray();
            populate(this, namespaces);
        }

        private static void populate(NamespaceCacheRoot ncRoot, String[] namespaces) {
            foreach (String ns in namespaces)
                populate(ncRoot, ns);
        }

        private static void populate(NamespaceCacheRoot ncRoot, String ns) { 
            String[] nsParts = ns.Split('.');

            NamespaceCacheElement ncElem = ncRoot;
            foreach (String nsPart in nsParts)
                ncElem = AppendNamespaceElement(ncElem, nsPart);
        }

        public override String ToString() {
            return "[Global Scope]";
        }
    }
}