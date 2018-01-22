using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sce.Test
{
    [TestFixture]
    public class NugetTest
    {
        async Task InstallAsync()
        {
            var n = new Nuget();
            var source = Nuget.GetDefaultSource();
            var p = await source.GetLatestVersion("log4net");
            var testDir = TestUtil.GetTestDir(Install);

            var repo = new Nuget.Repository(Path.Combine(testDir, "packages"));

            await repo.Install(source, p);

            var referenceAssemblies = repo.Get(p).GetReferenceAssemblies();
            Assert.IsTrue(referenceAssemblies.Any(_ => _.Contains("log4net.dll")));
        }

        [Test]
        public void Install()
        {
            InstallAsync().Wait();
        }
    }
}
