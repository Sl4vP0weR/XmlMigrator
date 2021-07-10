using XmlMigrator;
using Xunit.Abstractions;

namespace XmlMigratorTests
{
    public class CustomMigratorLogger : MigratorLogger
    {
        ITestOutputHelper Output;
        public CustomMigratorLogger(ITestOutputHelper output) : base() { Output = output; }
        public override void Log(object obj) => Output.WriteLine(obj?.ToString() ?? "");
    }
}
