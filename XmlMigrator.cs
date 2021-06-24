using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace XmlMigrator
{
    public class UnreferencedXmlObject
    {
        public UnreferencedXmlObject(XmlNode node, object objectBeingDeserialized)
        {
            Node = node;
            ObjectBeingDeserialized = objectBeingDeserialized;
        }
        public XmlNode Node;
        public object ObjectBeingDeserialized;
    }
    /// <summary> Migrator for xml data </summary>
    public class XmlMigrator
    {
        public readonly XmlSerializer XmlSerializer;
        public ILogger Logger = new MigratorLogger(new ConsoleLogger());
        const BindingFlags MembersFlags = BindingFlags.Public | BindingFlags.Instance;
        public XmlMigrator(Type t, XmlRootAttribute rootAttr = null)
        {
            XmlSerializer = new XmlSerializer(t, rootAttr);
            XmlSerializer.UnknownAttribute += XmlSerializer_UnknownAttribute;
            XmlSerializer.UnknownElement += XmlSerializer_UnknownElement;
        }
        public XmlMigrator(Type type, StringBuilder xml) : this(type)
        {
            Migrate(xml);
        }
        public XmlMigrator(Type type, string filePath) : this(type)
        {
            Migrate(filePath);
        }

        Queue<UnreferencedXmlObject> UnreferencedQueue = new Queue<UnreferencedXmlObject>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Unreferenced(XmlNode node, object obj) => UnreferencedQueue.Enqueue(new UnreferencedXmlObject(node, obj));

        void XmlSerializer_UnknownElement(object sender, XmlElementEventArgs e) => Unreferenced(e.Element, e.ObjectBeingDeserialized);
        void XmlSerializer_UnknownAttribute(object sender, XmlAttributeEventArgs e) => Unreferenced(e.Attr, e.ObjectBeingDeserialized);
        void ResolveUnreferenced(UnreferencedXmlObject unreferencedObject)
        {
            var node = unreferencedObject.Node;
            var obj = unreferencedObject.ObjectBeingDeserialized;
            var type = obj.GetType();
            var member = // getting a MemberInfo that represents current node
                type.RetriveMembers(
                    MembersFlags,
                    x => x.MemberType.EqualsAny(MemberTypes.Field, MemberTypes.Property) &&
                    x.GetCustomAttribute<XmlIgnoreAttribute>() == null
                ).FirstOrDefault(x =>
                    node.Name == x.Name ||
                    (x.GetCustomAttribute<XmlRenamedOrMoved>()?.PreviousNames.Contains(unreferencedObject.Node.Name) ?? false)
                );
            if (member == null) return;
            Action<object, object> set = null;
            type = typeof(object); // getting type of member
            if (member is FieldInfo fld && !fld.IsLiteral)
            {
                set = fld.SetValue;
                type = fld.FieldType;
            }
            else if (member is PropertyInfo prp && prp.CanWrite)
            {
                set = prp.SetValue;
                type = prp.PropertyType;
            }
            var nav = node.CreateNavigator();
            if (node.FirstChild?.NodeType == XmlNodeType.Text) // if defualt datatype
                nav.MoveToChild(XPathNodeType.Text); // getting first text node for parse
            object val = null;
            try
            {
                // parsing default datatype
                val = nav.ValueAs(type);
            }
            catch // isn't a default datatype, parsing custom datatype recursively
            {
                XDocument doc;
                using (var reader = new StringReader(node.OuterXml))
                    doc = XDocument.Load(reader);
                doc.Root.Name = type.Name;
                var migrator = new XmlMigrator(type, new XmlRootAttribute(type.Name)) { Logger = Logger }; // migrator
                val = migrator.Migrate(new StringBuilder(doc.ToString())); // parsed custom datatype
            }
            set?.Invoke(obj, val); // set member value
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object Migrate(string filePath) => Migrate(new StringBuilder(File.ReadAllText(filePath)));

        public object Migrate(StringBuilder xml)
        {
            using (var reader = new StringReader(xml.ToString()))
                Instance = XmlSerializer.Deserialize(reader);
            while (UnreferencedQueue.Count > 0) // resolving
                ResolveUnreferenced(UnreferencedQueue.Dequeue());
            SerializedInstance.LoadXml(ToString());
            return Instance;
        }
        public override string ToString()
        {
            using(var writer = new StringWriter())
            {
                XmlSerializer.Serialize(writer, Instance);
                return writer.ToString();
            }
        }
        public void LogAsXml(string name = null) => (Logger as MigratorLogger)?.LogAsXml(Instance, name);
        public void LogXmlTree() => (Logger as MigratorLogger)?.LogRecursively(SerializedInstance.DocumentElement);

        public object Instance;
        public readonly XmlDocument SerializedInstance = new XmlDocument();
    }
    /// <summary> Migrator for xml data </summary>
    /// <typeparam name="T">Type that was serialized</typeparam>
    public class XmlMigrator<T> : XmlMigrator where T : class
    {
        public XmlMigrator() : base(typeof(T)) { }
        public XmlMigrator(StringBuilder xml) : base(typeof(T), xml) { }
        public XmlMigrator(string filePath) : base(typeof(T), filePath) { }
        public new T Migrate(StringBuilder xml) => (T)base.Migrate(xml);
        public new T Migrate(string filePath) => (T)base.Migrate(filePath);
        public T Value => (T)Instance;
    }
}
