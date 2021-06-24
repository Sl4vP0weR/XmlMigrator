using System;

namespace XmlMigrator
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class XmlRenamedOrMoved : Attribute
    {
        public readonly string[] PreviousNames;
        public XmlRenamedOrMoved(params string[] previousNames)
        {
            PreviousNames = previousNames;
        }
    }
}
