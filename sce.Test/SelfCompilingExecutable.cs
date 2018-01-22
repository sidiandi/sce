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
    public class SelfCompilingExecutableTest
    {
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
            var testDir = TestUtil.GetTestDir(CanSelfCompile);
            var exe = Path.Combine(testDir, Path.GetFileName(originalExe));

            // ensure that source directory is deleted
            FS.EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            var p = Run(exe);
            Assert.AreEqual(0, p.Result.process.ExitCode);

            // modify source
            File.WriteAllText(Path.Combine(sourceDir, "Program.cs"), @"
using System;

class Program
{
    internal static int Main(string[] args)
    {
        return 123;
    }
}
");

            // Start second time to recompile and return different exit code
            p = Run(exe);
            Assert.AreEqual(123, p.Result.process.ExitCode);
        }

        [Test]
        public void ReturnsMinus1IfCompilationFails()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = TestUtil.GetTestDir(ReturnsMinus1IfCompilationFails);
            var exe = Path.Combine(testDir, Path.GetFileName(originalExe));

            // ensure that source directory is deleted
            FS.EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            var p = Run(exe);
            Assert.AreEqual(0, p.Result.process.ExitCode);

            // modify source
            File.WriteAllText(Path.Combine(sourceDir, "Program.cs"), @"
using System;

class Program
{
    internal static int Main(string[] args)
    {
        return 0.0;
    }
}
");

            // Start second time to recompile and return different exit code
            p = Run(exe);
            Assert.AreEqual(-1, p.Result.process.ExitCode);
        }

        class ProcessResult
        {
            public string output;
            public string error;
            public Process process;
        }

        async Task<ProcessResult> Run(string executable)
        {
            Console.WriteLine(executable);

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                }
            };

            p.Start();

            var copyOut = p.StandardOutput.ReadToEndAsync();
            var copyErr = p.StandardError.ReadToEndAsync();

            p.WaitForExit();

            Console.WriteLine(await copyOut);
            Console.WriteLine(await copyErr);

            return new ProcessResult
            {
                output = await copyOut,
                error = await copyErr,
                process = p
            };
        }

        [Test]
        public void ProcessesShowTheirOutput()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = TestUtil.GetTestDir(ProcessesShowTheirOutput);
            var exe = Path.Combine(testDir, Path.GetFileName(originalExe));
            // ensure that source directory is deleted
            FS.EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            Console.WriteLine("output from Console.WriteLine");
            Run(exe).Wait();
        }

        [Test]
        public void WorksWhenRenamed()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = TestUtil.GetTestDir(WorksWhenRenamed);
            var exe = Path.Combine(testDir, "hello.exe");

            // ensure that source directory is deleted
            FS.EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            var p = Run(exe);
            Assert.AreEqual(0, p.Result.process.ExitCode);

            // modify source
            var programCs = Path.Combine(sourceDir, "Program.cs");
            File.WriteAllText(programCs, @"
using System;

class Program
{
    internal static int Main(string[] args)
    {
        return 123;
    }
}
");


            // Start second time to recompile and return different exit code
            p = Run(exe);
            Assert.AreEqual(123, p.Result.process.ExitCode);

            // Start third time to remove old-sce-hello.exe
            p = Run(exe);
            Assert.AreEqual(123, p.Result.process.ExitCode);

            // delete the source dir
            FS.EnsureDirectoryNotExists(sourceDir);

            // Start to show that without code the default behaviour is restored again
            p = Run(exe);
            Assert.AreEqual(0, p.Result.process.ExitCode);
            Assert.IsTrue(File.Exists(programCs));

        }

        [Test]
        public void CanLoadEmbeddedNugetReferences()
        {
            var originalExe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sce.exe");
            var testDir = TestUtil.GetTestDir(CanLoadEmbeddedNugetReferences);
            var exe = Path.Combine(testDir, Path.GetFileName(originalExe));

            // ensure that source directory is deleted
            FS.EnsureDirectoryIsEmpty(testDir);

            File.Copy(originalExe, exe);

            var sourceDir = exe + ".src";

            // Start first time to create source dir
            var p = Run(exe);
            Assert.AreEqual(0, p.Result.process.ExitCode);

            // modify source
            var programCs = Path.Combine(sourceDir, "Program.cs");
            File.WriteAllText(programCs, @"
// nuget-package: log4net
// nuget-package: sidi-util
// nuget-package: Nunit

using System;

class Program
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    internal static int Main(string[] args)
    {
        log4net.Config.BasicConfigurator.Configure();
        log.Info(""hello from log4net"");
        log.Info(new Sidi.IO.LPath(@""C:\temp""));
        return 123;
    }
}
");

            // Start second time to recompile and return different exit code
            p = Run(exe);
            Assert.AreEqual(123, p.Result.process.ExitCode);
        }

        [Test]
        public void GetMetaData()
        {
            var testDir = TestUtil.GetTestDir(GetMetaData);
            var sourceDir = testDir;

            // modify source
            var programCs = Path.Combine(sourceDir, "Program.cs");
            File.WriteAllText(programCs, @"
// nuget-source: https://api.nuget.org/
// nuget-package: log4net
// nuget-package: sidi-util
// nuget-package: Nunit

using System;

class Program
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    internal static int Main(string[] args)
    {
        log4net.Config.BasicConfigurator.Configure();
        log.Info(""hello from log4net"");
        return 123;
    }
}
");
            var sourceFiles = new DirectoryInfo(sourceDir).GetFiles("*.cs", SearchOption.AllDirectories);
            var m = SelfCompilingExecutable.GetMetaData(sourceFiles);
            Assert.IsTrue(m["nuget-package"].Contains("log4net"));
            Assert.IsTrue(m["nuget-source"].Contains("https://api.nuget.org/"));
        }

    }
}
