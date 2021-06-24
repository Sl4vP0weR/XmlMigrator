using System.IO;
using System.Text;
using System.Xml.Serialization;
using XmlMigrator;
using Xunit;
using Xunit.Abstractions;

namespace XmlMigratorTests
{
    [XmlRoot("XmlClass")]
    public class OldXmlClass
    {
        public int a;
        public string b;
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
        [XmlRenamedOrMoved("c")]
        public NewXmlClass C;
        public bool ShouldSerializeC() => true;
    }
    class CustomMigratorLogger : MigratorLogger
    {
        ITestOutputHelper Output;
        public CustomMigratorLogger(ITestOutputHelper output) : base() { Output = output; }
        public override void Log(object obj) => Output.WriteLine(obj?.ToString() ?? "");
    }
    public class UnitTests
    {
        public UnitTests(ITestOutputHelper output)
        {
            using (var writer = new StringWriter())
            {
                new XmlSerializer(typeof(OldXmlClass)).Serialize(writer, Old); // serializing Old to writer
                // new instance of migrator with my custom logger
                Migrator = new XmlMigrator<NewXmlClass>(new StringBuilder(writer.ToString())) { Logger = Logger = new CustomMigratorLogger(output) };
                // saving result of migration
                Migrator.SerializedInstance.Save("migrated.xml");
            }
        }
        OldXmlClass Old = new OldXmlClass { a = 1, b = "text1", c = new OldXmlClass { a = 2, b = "text2" } };
        readonly CustomMigratorLogger Logger;
        public XmlMigrator<NewXmlClass> Migrator;
        [Fact]
        public void SimpleMigration()
        {
            var New = Migrator.Value;
            Assert.Equal(Old.a, New.a);
            Assert.Equal(Old.b, New.b);
            Logger.LogAsXml(Old, "Old");
            Migrator.LogAsXml("New");
            Migrator.LogXmlTree();
        }
    }
}
