using System.IO;
using System.Text;
using System.Xml.Serialization;
using XmlMigrator;
using Xunit;
using Xunit.Abstractions;

namespace XmlMigratorTests
{
    public class UnitTest
    {
        public UnitTest(ITestOutputHelper output)
        {
            Logger = new CustomMigratorLogger(output);
        }
        public XmlMigrator<NewT> TryMigrate<NewT>(object old, string pathToSave)
            where NewT : class
        {
            using (var writer = new StringWriter())
            {
                new XmlSerializer(old.GetType()).Serialize(writer, old); // serializing Old to writer
                // new instance of migrator with my custom logger
                var migrator = new XmlMigrator<NewT>() { Logger = Logger };
                migrator.PostDataTypeConvert += Migrator_PostDataTypeConvert;
                migrator.Migrate(new StringBuilder(writer.ToString()));
                migrator.SerializedInstance.Save(pathToSave);
                return migrator;
            }
        }

        void Migrator_PostDataTypeConvert(UnreferencedXmlObject xmlObj, XmlMemberVisitor visitor, ref object result)
        {
            Logger.Log($"Migrating {xmlObj.ObjectBeingDeserialized} - {visitor.Member}");
            Logger.Log($"Result: {result}");
        }

        readonly protected CustomMigratorLogger Logger;
    }
}
