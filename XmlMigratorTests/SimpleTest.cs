using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using XmlMigrator;
using Xunit;
using Xunit.Abstractions;

namespace XmlMigratorTests
{
    public class SimpleTest : UnitTest
    {
        public SimpleTest(ITestOutputHelper output) : base(output)
        {
        }

        [XmlRoot("XmlClass")]
        public class OldXmlClass
        {
            public int a;
            public string b;
            [XmlArray("D"), XmlArrayItem("str")]
            public List<string> d;
            public OldXmlClass c;
            public bool ShouldSerializec() => true;
        }
        [XmlRoot("XmlClass")]
        public class NewXmlClass
        {
            [XmlAttribute]
            public int a;
            [XmlAttribute]
            public string b;
            [XmlRenamedOrMoved("D"), XmlArrayItem("str")]
            public List<string> d;
            [XmlRenamedOrMoved("c")]
            public NewXmlClass C;
            public bool ShouldSerializeC() => true;
        }
        [Fact]
        public void SimpleMigration()
        {
            var Old = new OldXmlClass { a = 1, b = "text1", c = new OldXmlClass { a = 2, b = "text2", d = new List<string> { "abc2" } }, d = new List<string> { "abc1" } };
            var Migrator = TryMigrate<NewXmlClass>(Old, "migrateSimple.txt");
            var New = Migrator.Value;
            Assert.Equal(Old.a, New.a);
            Assert.Equal(Old.b, New.b);
            Assert.Equal(Old.d.Count, New.d?.Count ?? 0);
            Logger.LogAsXml(Old, "Old");
            Migrator.LogAsXml("New");
            Migrator.LogXmlTree();
        }
    }
}
