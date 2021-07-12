using System;
using System.Xml;

namespace XmlMigrator
{
    public sealed class XmlObject : ICloneable
    {
        public XmlObject(XmlNode node, object parent)
        {
            Node = node;
            Parent = parent;
        }
        public XmlNode Node;
        public object Parent;

        public object Clone() => MemberwiseClone();
    }
}
