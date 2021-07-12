using System;
using System.Reflection;

namespace XmlMigrator
{
    public sealed class XmlMemberVisitor
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
        public readonly FieldInfo Field;
        public readonly PropertyInfo Property;
        public readonly Type VarType;
        public readonly Action<object, object> Setter;
    }
}
