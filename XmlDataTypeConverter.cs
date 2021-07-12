using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using static XmlMigrator.Extensions;

namespace XmlMigrator
{
    public sealed class XmlDataTypeConverter
    {
        public XmlDataTypeConverter(XmlDataTypeConverter parent = null)
        {
            PreConvert = parent?.PreConvert;
            PostConvert = parent?.PostConvert;
        }
        public delegate void PreConvertation(XmlObject obj, Type convertTo, ref bool allow);
        public event PreConvertation PreConvert;
        public delegate void PostConvertation(XmlObject obj, Type convertTo, ref object result);
        public event PostConvertation PostConvert;
        object ConvertListDataType(XmlObject obj, Type type)
        {
            var val = Activator.CreateInstance(type) as IList;
            foreach (XmlNode childNode in obj.Node.ChildNodes)
            {
                var childObj = (XmlObject)obj.Clone();
                childObj.Node = childNode;
                val.Add(ConvertDataType(childObj, type.GenericTypeArguments.FirstOrDefault()));
            }
            return val;
        }
        /// <summary>
        /// Converts custom datatypes, like user classes/structs
        /// </summary>
        public object ConvertCustomDataType(XmlObject obj, XmlMigrator migrator)
        {
            var type = migrator.Type;
            if (type.GetInterface(nameof(IList)) != null)
                return ConvertListDataType(obj, type);
            return migrator.Migrate(new StringBuilder(NodeToDoc(obj.Node, type.Name).ToString())); // parsed custom datatype
        }
        /// <summary>
        /// Converts any datatype
        /// </summary>
        public object ConvertDataType(XmlObject obj, Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            var allow = true;
            PreConvert?.Invoke(obj, type, ref allow);
            if (!allow) throw new Exception($"Not allowed by {nameof(PreConvert)} event.");
            var node = obj.Node;
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
                    var migrator = XmlMigrator.Create(type);
                    migrator.Converter = new XmlDataTypeConverter(this);
                    val = ConvertCustomDataType(obj, migrator);
                }
                catch { }
            }
            PostConvert?.Invoke(obj, type, ref val);
            return val;
        }
    }
}
