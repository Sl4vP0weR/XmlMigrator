using System.IO;
using System.Text;
using System.Xml.Serialization;
using XmlMigrator;
using System;
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
                migrator.Converter.PostConvert += Converter_PreConvert;
                migrator.Migrate(new StringBuilder(writer.ToString()));
                migrator.SerializedInstance.Save(pathToSave);
                return migrator;
            }
        }

        void Converter_PreConvert(XmlObject obj, Type convertTo, ref object result)
        {
            Logger.Log($"Converted {obj.Parent.GetType().Name} - {obj.Node.Name} (type: {convertTo.FullName})");
            //Logger.Log($"Result: {result}");
        }

        readonly protected CustomMigratorLogger Logger;
    }
}
