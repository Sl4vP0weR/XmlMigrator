using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace XmlMigrator
{
    public interface ILogger
    {
        void Log(object obj);
    }
    public class ConsoleLogger : ILogger
    {
        public void Log(object obj) => Console.WriteLine(obj);
    }
    public class MigratorLogger : ILogger
    {
        ILogger Parent;
        public MigratorLogger(ILogger parent = null)
        {
            Parent = parent ?? this;
        }

        /// <param name="prepend">whitespace before text length</param>
        public void FormatLog(XmlNode node, int prepend = 0)
        {
            var isText = node.NodeType == XmlNodeType.Text && (node.ParentNode?.NodeType.EqualsAny(XmlNodeType.Element, XmlNodeType.Attribute) ?? false);
            var hasChilds = node.HasChildNodes;
            var type = isText ? "Value" : node.NodeType + ""; // current node type
            var name = isText ? $" {node.ParentNode.Name} = " : ""; // name of parent node (text node name)
            var value = isText ? $"{node.InnerText}" : ""; // value of text node
            value = isText ? double.TryParse(value.Replace('.', ','), out var _) ? value : $"\"{value}\"" : ""; // format if text is numeric
            var arrow = (hasChilds ? "˯" : "");
            Parent.Log($"{Extensions.Whitespace(prepend)}{arrow}{type}{name}{value}");
        }
        /// <param name="prepend">whitespace before text length</param>
        public void LogRecursively(XmlNode node, int prepend = 0, bool debug = false)
        {
            FormatLog(node, prepend);
            if (node.Attributes?.Count > 0)
            {
                foreach (var attr in node.Attributes.OfType<XmlAttribute>())
                    LogRecursively(attr, prepend + 1, debug);
                if (debug)
                    Parent.Log("");
            }
            foreach (var child in node.ChildNodes.OfType<XmlNode>())
                LogRecursively(child, prepend + 2, debug);
        }
        public void LogAsXml(object obj, string rootName = null)
        {
            Log(obj.AsXml(rootName)+"\n");
        }
        public virtual void Log(object obj) => Parent.Log(obj);
    }
}
