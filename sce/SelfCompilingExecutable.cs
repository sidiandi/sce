using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace sce
{
    public class SelfCompilingExecutable
    {
        static int Main(string[] args)
        {
            try
            {
                var sce = new SelfCompilingExecutable(FS.GetExecutable());

                if (args.Length >= 1 && string.Equals(args[0], "watch"))
                {
                    sce.Watch();
                    return 0;
                }
                else
                {
                    return sce.Run().Result;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        readonly string executable;
        readonly string sourceDir;
        readonly string cacheDir;
        readonly Nuget.Repository repository;

        public SelfCompilingExecutable(string executable, string cacheRoot = null)
        {
            if (cacheRoot == null)
            {
                cacheRoot = Path.Combine(Path.GetTempPath(), "sce");
            }

            this.executable = Path.GetFullPath(executable);
            this.cacheDir = Path.Combine(cacheRoot, Util.GetDigest(this.executable));
            this.sourceDir = this.executable + ".src";
            repository = new Nuget.Repository(Path.Combine(cacheRoot, "packages"));
            FS.EnsureDirectoryExists(this.cacheDir);
        }

        string OutputFile => Path.Combine(cacheDir, "a.dll");

        public void Watch()
        {
            var fsw = new FileSystemWatcher(sourceDir);
            fsw.BeginInit();
            fsw.EnableRaisingEvents = true;
            fsw.IncludeSubdirectories = true;
            fsw.EndInit();

            for (; ; )
            {
                if (IsBuildRequired(sourceDir, OutputFile))
                {
                    try
                    {
                        Build(sourceDir, OutputFile, repository).Wait();
                        Console.WriteLine("Build succeeded.");
                        var result = Run().Result;
                        Console.WriteLine("Exit code: {0}", result);
                        Util.StartProcess(Environment.CommandLine);
                        Environment.Exit(0);
                    }
                    catch (AggregateException e)
                    {
                        Console.WriteLine("Build failed.");
                        Console.WriteLine(e.InnerExceptions[0]);
                    }
                }
                Console.WriteLine("Waiting for changes in {0}", sourceDir);
                fsw.WaitForChanged(WatcherChangeTypes.All);
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }
        }

        public async Task<int> Run()
        {
            try
            {
                return await RunImpl();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        async Task<int> RunImpl()
        {
            // locate compiled assembly
            FS.EnsureDirectoryExists(cacheDir);
            log(cacheDir);

            var sourceDir = GetSourceDir();
            EnsureSourceDirectoryExists(sourceDir);

            // still up-to-date ?
            if (IsBuildRequired(sourceDir, OutputFile))
            {
                await Build(sourceDir, OutputFile, repository);
            }

            // run
            var a = Assembly.LoadFrom(OutputFile);
            return Run(a);
        }

        static int Run(Assembly a)
        {
            var result = a.EntryPoint.Invoke(null, new object[] { Environment.GetCommandLineArgs() });
            if (result is int)
            {
                return (int)result;
            }
            else
            {
                return 0;
            }
        }

        static void log(string f, params object[] args)
        {
            Trace.WriteLine(String.Format(f, args), "sce");
        }

        static string StripResourcePrefix(string resourceName)
        {
            return resourceName.Substring(resourceName.IndexOf('.') + 1);
        }

        static string GetSourceDir()
        {
            return FS.GetExecutable() + ".src";
        }

        internal static bool IsBuildRequired(string sourceDir, string outputFile)
        {
            var o = new FileInfo(outputFile);
            var sourceFiles = new DirectoryInfo(sourceDir).GetFiles("*.*", SearchOption.AllDirectories);
            var changed = sourceFiles.Where(_ => _.LastWriteTimeUtc > o.LastWriteTimeUtc);
            if (changed.Any())
            {
                log("Source files have changed: {0}", String.Join(", ", changed.Cast<object>().ToArray()));
            }
            return changed.Any();
        }

        static void EnsureSourceDirectoryExists(string sourceDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                Directory.CreateDirectory(sourceDir);

                // write source files
                var a = Assembly.GetExecutingAssembly();
                const string embeddedResourcePrefix = "source.";
                foreach (var i in a.GetManifestResourceNames().Where(_ => _.StartsWith(embeddedResourcePrefix)))
                {
                    var destination = Path.Combine(sourceDir, Util.StripPrefix(embeddedResourcePrefix, i));
                    using (var w = File.OpenWrite(destination))
                    {
                        using (var r = a.GetManifestResourceStream(i))
                        {
                            r.CopyTo(w);
                        }
                    }
                }
            }
        }

        static IEnumerable<KeyValuePair<string, string>> GetMetaData(FileInfo file)
        {
            using (var r = File.OpenText(file.FullName))
            {
                for (; ; )
                {
                    var line = r.ReadLine();
                    if (line == null) break;
                    var m = Regex.Match(line, @"//\s+(?<key>[\w-]+)\s*:\s*(?<value>.*)");
                    if (m.Success)
                    {
                        yield return new KeyValuePair<string, string>(m.Groups["key"].Value, m.Groups["value"].Value);
                    }
                }
            }
        }

        public static ILookup<string, string> GetMetaData(FileInfo[] files)
        {
            return files.SelectMany(_ => GetMetaData(_)).ToLookup(_ => _.Key, _ => _.Value);
        }

        const string nugetSourceKeyWord = "nuget-source";
        const string nugetPackageKeyWord = "nuget-package";

        internal static async Task Build(string sourceDir, string outputFile, Nuget.Repository repository)
        { 
            string outputDir = Path.GetDirectoryName(outputFile);

            var sourceFiles = new DirectoryInfo(sourceDir).GetFiles("*.*", SearchOption.AllDirectories);

            var metaData = GetMetaData(sourceFiles);

            var nugetSources = metaData[nugetSourceKeyWord].Distinct()
                .Select(Nuget.GetSource)
                .ToList();

            if (!nugetSources.Any())
            {
                nugetSources = new List<Nuget.ISource> { Nuget.GetDefaultSource() };
            }

            var nugetPackages = metaData[nugetPackageKeyWord].Distinct().ToList();

            var installedNugetPackages = await repository.EnsurePackagesAreInstalled(nugetSources, nugetPackages);

            // compile
            var csharpProvider = new Microsoft.CSharp.CSharpCodeProvider();

            var externalReferences = installedNugetPackages.SelectMany(_ => _.GetReferenceAssemblies()).ToArray();

            foreach (var i in externalReferences)
            {
                Copy(i, Path.Combine(outputDir, Path.GetFileName(i)));
            }

            var internalReferences = new[]{
                "System.Runtime.dll",
                "System.dll",
                "System.Xml.dll",
                "Microsoft.CSharp.dll",
                "System.Core.dll",
            };

            var options = new CompilerParameters()
            {
                OutputAssembly = outputFile,
                GenerateExecutable = true,
                IncludeDebugInformation = false,
                GenerateInMemory = false,
                MainClass = "Program",
            };

            options.ReferencedAssemblies.AddRange(internalReferences.Concat(externalReferences).ToArray());

            var results = csharpProvider.CompileAssemblyFromFile(options, sourceFiles.Where(Util.IsCSharpFile).Select(_ => _.FullName).ToArray());
            if (results.Errors.Cast<object>().Any())
            {
                var s = String.Join("\r\n", results.Errors.Cast<object>().ToArray());
                throw new Exception(s);
            }
        }

        private static void Copy(string i, string v)
        {
            log("{0} -> {1}", i, v);
            File.Copy(i, v, true);
        }

        internal static void PrintReadme()
        {
            var a = Assembly.GetExecutingAssembly();
            using (var r = a.GetManifestResourceStream("Readme.md"))
            {
                if (r != null)
                {
                    Console.WriteLine(new StreamReader(r).ReadToEnd());
                }
            }
        }
    }
}
