using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace XmlMigrator
{
    public static class Extensions
    {
        public static bool CheckPredicates<T>(T arg, params Func<T, bool>[] predicates)
        {
            foreach (var predicate in predicates)
                if (!(predicate?.Invoke(arg) ?? true))
                    return false;
            return true;
        }
        public static T2 AttributeValueOrDefault<T1,T2>(MemberInfo member, Func<T1, T2> func) where T1 : Attribute
        {
            var attr = member.GetCustomAttribute<T1>();
            if (attr == null) return default;
            var val = func(attr);
            return !string.IsNullOrWhiteSpace(val.ToString()) ? val : default;
        }
        public static XDocument NodeToDoc(XmlNode node, string rootName = null)
        {
            XDocument doc;
            using (var reader = new StringReader(node.OuterXml))
                doc = XDocument.Load(reader);
            doc.Root.Name = rootName ?? node.Name;
            return doc;
        }
        public static MemberInfo FindXmlMember(XmlNode node, Type type) =>
            type.RetriveXmlMembers()
            .FirstOrDefault(x =>
                GetXmlName(x) == node.Name ||
                (x.GetCustomAttribute<XmlRenamedOrMoved>()?.PreviousNames.Contains(node.Name) ?? false));
        public static string GetXmlName(MemberInfo member)
        {
            string res = null;
            res ??= AttributeValueOrDefault<XmlArrayAttribute, string>(member, x => x.ElementName);
            res ??= AttributeValueOrDefault<XmlRootAttribute, string>(member, x => x.ElementName);
            res ??= AttributeValueOrDefault<XmlElementAttribute, string>(member, x => x.ElementName);
            res ??= AttributeValueOrDefault<XmlAttributeAttribute, string>(member, x => x.AttributeName);
            res ??= AttributeValueOrDefault<XmlEnumAttribute, string>(member, x => x.Name);
            return res ?? member.Name;
        }
        public const BindingFlags XmlMembersFlags = BindingFlags.Public | BindingFlags.Instance;
        public static List<MemberInfo> RetriveXmlMembers(this Type type, Func<MemberInfo, bool> predicate = null) =>
            type.RetriveMembers(
                XmlMembersFlags,
                x =>
                {
                    if (x is FieldInfo fld && (fld.IsLiteral || fld.IsInitOnly))
                        return false;
                    else if (x is PropertyInfo prp && (!prp.CanWrite || !prp.CanRead))
                        return false;
                    return 
                    x.MemberType.EqualsAny(MemberTypes.Field, MemberTypes.Property) &&
                    CheckPredicates(x, predicate) &&
                    x.GetCustomAttribute<XmlIgnoreAttribute>() == null;
                }
            );
        public static List<MemberInfo> RetriveMembers(this Type type, BindingFlags flags = BindingFlags.Default, Func<MemberInfo, bool> predicate = null) => type.GetMembers(flags).Where(x => CheckPredicates(x, predicate)).ToList();
        public static bool EqualsAny<T>(this T @this, params T[] other) => EqualsAny(@this, out _, other);
        public static bool EqualsAny<T>(this T @this, out T match, params T[] other)
        {
            match = default;
            foreach (var x in other)
                if (@this.Equals(match = x))
                    return true;
            return false;
        }
        public static string AsXml(this object obj, string rootName = null)
        {
            var type = obj.GetType();
            rootName ??= type.Name;
            var serializer = new XmlSerializer(type, new XmlRootAttribute(rootName));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                using (var reader = new StringReader(writer.ToString()))
                {
                    var doc = XDocument.Load(reader);
                    doc.Root.Attributes().Where(x => x.Name.ToString().Contains("xmlns")).ToList().ForEach(x => x.Remove());
                    return doc.ToString();
                }
            }
        }
    }
}
