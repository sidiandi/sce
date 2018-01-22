using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sce.Test
{
    public class TestUtil
    {
        public static string GetTestDir(Action test)
        {
            var a = Assembly.GetExecutingAssembly();
            var testDir = Path.Combine(Path.GetDirectoryName(a.Location), "test", test.GetMethodInfo().DeclaringType.Name, test.GetMethodInfo().Name);
            FS.EnsureDirectoryIsEmpty(testDir);
            return testDir;
        }
    }
}
