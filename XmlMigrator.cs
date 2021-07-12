using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using static XmlMigrator.Extensions;

namespace XmlMigrator
{
    /// <summary> Migrator for xml serialized C# data </summary>
    public class XmlMigrator
    {
        public ILogger Logger = new MigratorLogger(new ConsoleLogger());
        public XmlDataTypeConverter Converter = new XmlDataTypeConverter();
        public readonly XmlSerializer XmlSerializer;
        public readonly Type Type;
        /// <summary>
        /// Creates new migrator
        /// </summary>
        /// <param name="logger">Logger for this</param>
        /// <param name="rootName">Custom root name [default: type.Name]</param>
        public static XmlMigrator Create(Type type, ILogger logger = null, string rootName = null)
        {
            var migrator = new XmlMigrator(type, new XmlRootAttribute(rootName ?? type.Name));
            migrator.Logger = logger ?? migrator.Logger;
            return migrator;
        }
        public XmlMigrator(Type type, XmlRootAttribute rootAttr = null)
        {
            XmlSerializer = new XmlSerializer(Type = type, rootAttr);
            XmlSerializer.UnknownAttribute += XmlSerializer_UnknownAttribute;
            XmlSerializer.UnknownElement += XmlSerializer_UnknownElement;
        }
        public XmlMigrator(Type type, StringBuilder xml) : this(type) { Migrate(xml); }
        public XmlMigrator(Type type, string filePath) : this(type) { Migrate(filePath); }


        Queue<XmlObject> UnreferencedQueue = new Queue<XmlObject>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Unreferenced(XmlNode node, object obj) => UnreferencedQueue.Enqueue(new XmlObject(node, obj));

        void XmlSerializer_UnknownElement(object sender, XmlElementEventArgs e) => Unreferenced(e.Element, e.ObjectBeingDeserialized);
        void XmlSerializer_UnknownAttribute(object sender, XmlAttributeEventArgs e) => Unreferenced(e.Attr, e.ObjectBeingDeserialized);
        
        void TryResolveUnreferenced(XmlObject unreferencedObject)
        {
            var node = unreferencedObject.Node;
            var obj = unreferencedObject.Parent;
            var member = FindXmlMember(node, obj.GetType());// getting a member that represents current node
            if (member == null) return;
            var visitor = new XmlMemberVisitor(member);
            object val = null;
            try { val = Converter.ConvertDataType(unreferencedObject, visitor.VarType); }
            catch { Logger.Log($"Can't convert value of type {visitor.VarType}."); throw; }
            visitor.Setter?.Invoke(obj, val); // set member value
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
        public T GenericInstance => (T)Instance;
    }
}
