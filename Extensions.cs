using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace XmlMigrator
{
    public static class Extensions
    {
        public static List<MemberInfo> RetriveMembers(this Type type, BindingFlags flags = BindingFlags.Default, Func<MemberInfo, bool> predicate = default) => type.GetMembers(flags).Where(x => predicate?.Invoke(x) ?? true).ToList();
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
