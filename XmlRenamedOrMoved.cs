using System;

namespace XmlMigrator
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class XmlRenamedOrMoved : Attribute
    {
        public readonly string[] PreviousNames;
        public XmlRenamedOrMoved(params string[] previousNames)
        {
            PreviousNames = previousNames;
        }
    }
}
