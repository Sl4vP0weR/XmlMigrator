using System;
using System.Collections;
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
using static XmlMigrator.Extensions;

namespace XmlMigrator
{
    public class UnreferencedXmlObject : ICloneable
    {
        public UnreferencedXmlObject(XmlNode node, object objectBeingDeserialized)
        {
            Node = node;
            ObjectBeingDeserialized = objectBeingDeserialized;
        }
        public XmlNode Node;
        public object ObjectBeingDeserialized;

        public object Clone() => MemberwiseClone();
    }
    public class XmlMemberVisitor
    {
        public XmlMemberVisitor(MemberInfo member)
        {
            if ((Member = member) is FieldInfo fld)
            {
                Field = fld;
                VarType = fld.FieldType;
                Setter = fld.SetValue;
            }
            else if (Member is PropertyInfo prp)
            {
                Property = prp;
                VarType = prp.PropertyType;
                Setter = prp.SetValue;
            }
        }
        public readonly MemberInfo Member;
        public readonly Type VarType;
        public readonly Action<object, object> Setter;
        public readonly FieldInfo Field;
        public readonly PropertyInfo Property;
    }
    /// <summary> Migrator for xml serialized C# data </summary>
    public class XmlMigrator
    {
        public ILogger Logger = new MigratorLogger(new ConsoleLogger());
        public readonly XmlSerializer XmlSerializer;
        public Type Type;
        public XmlMigrator(Type type, XmlRootAttribute rootAttr = null)
        {
            XmlSerializer = new XmlSerializer(Type = type, rootAttr);
            XmlSerializer.UnknownAttribute += XmlSerializer_UnknownAttribute;
            XmlSerializer.UnknownElement += XmlSerializer_UnknownElement;
        }
        public XmlMigrator(Type type, StringBuilder xml) : this(type) { Migrate(xml); }
        public XmlMigrator(Type type, string filePath) : this(type) { Migrate(filePath); }

        public delegate void PostDataTypeConvertation(UnreferencedXmlObject xmlObj, XmlMemberVisitor visitor, ref object result);
        public event PostDataTypeConvertation PostDataTypeConvert;
        public delegate void PreCustomDataTypeConvertation(XmlMigrator newMigrator, UnreferencedXmlObject xmlObj, XmlMemberVisitor visitor);
        public event PreCustomDataTypeConvertation PreCustomDataTypeConvert;

        Queue<UnreferencedXmlObject> UnreferencedQueue = new Queue<UnreferencedXmlObject>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Unreferenced(XmlNode node, object obj) => UnreferencedQueue.Enqueue(new UnreferencedXmlObject(node, obj));
        void XmlSerializer_UnknownElement(object sender, XmlElementEventArgs e) => Unreferenced(e.Element, e.ObjectBeingDeserialized);
        void XmlSerializer_UnknownAttribute(object sender, XmlAttributeEventArgs e) => Unreferenced(e.Attr, e.ObjectBeingDeserialized);
        object ConvertListDataType(UnreferencedXmlObject unreferencedObject, XmlMemberVisitor visitor) => 
            ConvertListDataType(unreferencedObject, visitor.VarType);
        object ConvertListDataType(UnreferencedXmlObject unreferencedObject, Type type)
        {
            var val = Activator.CreateInstance(type) as IList;
            foreach (XmlNode childNode in unreferencedObject.Node.ChildNodes)
            {
                var unrefObj = (UnreferencedXmlObject)unreferencedObject.Clone();
                unrefObj.Node = childNode;
                val.Add(ConvertDataType(unrefObj, type.GenericTypeArguments.FirstOrDefault()));
            }
            return val;
        }
        object ConvertCustomDataType(UnreferencedXmlObject unreferencedObject, XmlMemberVisitor visitor)
        {
            var migrator = CreateNewMigrator(visitor.VarType, Logger);
            PreCustomDataTypeConvert?.Invoke(migrator, unreferencedObject, visitor);
            return ConvertCustomDataType(unreferencedObject, migrator);
        }
        object ConvertCustomDataType(UnreferencedXmlObject unreferencedObject, XmlMigrator migrator)
        {
            var type = migrator.Type;
            if (type.GetInterface(nameof(IList)) != null)
                return ConvertListDataType(unreferencedObject, type);
            return migrator.Migrate(new StringBuilder(NodeToDoc(unreferencedObject.Node, type.Name).ToString())); // parsed custom datatype
        }
        /// <summary>
        /// Creates new migrator
        /// </summary>
        /// <param name="logger">Logger for this</param>
        /// <param name="rootName">Custom root name [default: type.Name]</param>
        public static XmlMigrator CreateNewMigrator(Type type, ILogger logger = null, string rootName = null)
        {
            var migrator = new XmlMigrator(type, new XmlRootAttribute(rootName ?? type.Name));
            migrator.Logger = logger ?? migrator.Logger;
            return migrator;
        }
        object ConvertDataType(UnreferencedXmlObject unreferencedObject, XmlMemberVisitor visitor)
        {
            var val = ConvertDataType(unreferencedObject, visitor.VarType);
            PostDataTypeConvert?.Invoke(unreferencedObject, visitor, ref val);
            return val;
        }
        object ConvertDataType(UnreferencedXmlObject unreferencedObject, Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            var node = unreferencedObject.Node;
            object val = default;
            try
            {
                var nav = node.CreateNavigator(); // XNav
                if (node.FirstChild?.NodeType == XmlNodeType.Text) // if defualt datatype
                    nav.MoveToChild(XPathNodeType.Text); // getting first text node for convertation
                // converting default datatype
                val = nav.ValueAs(type);
            }
            catch // isn't a default datatype, converting custom datatype recursively
            {
                try
                {
                    val = ConvertCustomDataType(unreferencedObject, CreateNewMigrator(type, Logger));
                }
                catch { }
            }
            return val;
        }
        void TryResolveUnreferenced(UnreferencedXmlObject unreferencedObject)
        {
            var node = unreferencedObject.Node;
            var obj = unreferencedObject.ObjectBeingDeserialized;
            var member = FindXmlMember(node, obj.GetType());// getting a member that represents current node
            if (member == null) return;
            var visitor = new XmlMemberVisitor(member);
            visitor.Setter?.Invoke(obj, ConvertDataType(unreferencedObject, visitor)); // set member value
        }

        void ResolveAll()
        {
            while (UnreferencedQueue.Count > 0)
                TryResolveUnreferenced(UnreferencedQueue.Dequeue());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object Migrate(string filePath) => Migrate(new StringBuilder(File.ReadAllText(filePath)));

        public object Migrate(StringBuilder xml)
        {
            using (var reader = new StringReader(xml.ToString()))
                Instance = XmlSerializer.Deserialize(reader);
            ResolveAll();
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
    /// <summary> Generic migrator for xml serialized C# data </summary>
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
