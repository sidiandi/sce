using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sce.Test
{
    [TestFixture]
    public class SelfCompilingExecutable
    {
        static void EnsureDirectoryNotExists(string d)
        {
            if (Directory.Exists(d))
            {
                Directory.Delete(d, true);
            }
        }

        static void EnsureDirectoryIsEmpty(string d)
        {
            EnsureDirectoryNotExists(d);
            var newDir = Directory.CreateDirectory(d);
            Assert.IsTrue(newDir.Exists);

        }

        static void AssertOk(Process p)
        {
            p.WaitForExit();
            Assert.AreEqual(0, p.ExitCode);
        }

        static void ReplaceInFile(string filePath, string oldValue, string newValue)
        {
            var text = File.ReadAllText(filePath);
            text = text.Replace(oldValue, newValue);
            File.WriteAllText(filePath, text);
        }

        [Test]
        public void CanSelfCompile()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = Path.Combine(Path.GetDirectoryName(originalExe), @".test\CanSelfCompile");
            var exe = Path.Combine(testDir, Path.GetFileName(originalExe));

            // ensure that source directory is deleted
            EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            AssertOk(Process.Start(exe));

            // modify source
            ReplaceInFile(Path.Combine(sourceDir, "Program.cs"), "return 0;", "return 123;");

            // Start second time to recompile and return different exit code
            var p = Process.Start(exe);
            p.WaitForExit();
            Assert.AreEqual(123, p.ExitCode);
        }

        [Test]
        public void ReturnsMinus1IfCompilationFails()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = Path.Combine(Path.GetDirectoryName(originalExe), @".test\ReturnsMinus1IfCompilationFails");
            var exe = Path.Combine(testDir, Path.GetFileName(originalExe));

            // ensure that source directory is deleted
            EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            AssertOk(Process.Start(exe));

            // modify source
            ReplaceInFile(Path.Combine(sourceDir, "Program.cs"), "return 0;", "return 0.0;");

            // Start second time to recompile and return different exit code
            var p = Process.Start(exe);
            p.WaitForExit();
            Assert.AreEqual(-1, p.ExitCode);
        }

        [Test]
        public void WorksWhenRenamed()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = Path.Combine(Path.GetDirectoryName(originalExe), @".test\WorksWhenRenamed");
            var exe = Path.Combine(testDir, "hello.exe");

            // ensure that source directory is deleted
            EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            AssertOk(Process.Start(exe));

            // modify source
            ReplaceInFile(Path.Combine(sourceDir, "Program.cs"), "return 0;", "return 123;");

            // Start second time to recompile and return different exit code
            var p = Process.Start(exe);
            p.WaitForExit();
            Assert.AreEqual(123, p.ExitCode);

            // Start third time to remove old-sce-hello.exe
            p = Process.Start(exe);
            p.WaitForExit();
            Assert.AreEqual(123, p.ExitCode);
        }
    }
}
