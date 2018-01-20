using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace sce
{
    class SelfCompilingExecutable
    {
        static int Main(string[] args)
        {
            try
            {
                sce.SelfCompilingExecutable.Update();
                return Program.Main(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        static void Concat(ref string s, char c)
        {
            if (s == null)
            {
                s = String.Empty;
            }
            s = s + c;
        }

        static IEnumerable<string> SplitCommandLine(string commandLine)
        {
            bool inQuotes = false;
            string p = null;

            foreach (var c in commandLine)
            {
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        inQuotes = false;
                        Concat(ref p, c);
                    }
                    else
                    {
                        Concat(ref p, c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                        Concat(ref p, c);
                    }
                    else
                    {
                        if (Char.IsWhiteSpace(c))
                        {
                            if (p != null)
                            {
                                yield return p;
                                p = null;
                            }
                        }
                        else
                        {
                            Concat(ref p, c);
                        }
                    }
                }
            }

            if (p != null)
            {
                yield return p;
            }
        }

        static void log(string f, params object[] args)
        {
            Trace.WriteLine(String.Format(f, args), "sce");
        }

        static Process StartProcess(string commandline)
        {
            var args = SplitCommandLine(commandline).ToList();
            var fileName = args[0];
            var arguments = commandline.Substring(fileName.Length);
            log("restart: commandline={2}, fileName={0}, arguments={1}", fileName, arguments, commandline);
            return Process.Start(new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false
            });
        }

        static void EnsureDirectoryExists(string d)
        {
            if (!Directory.Exists(d))
            {
                Directory.CreateDirectory(d);
            }
        }

        static string StripPrefix(string prefix, string text)
        {
            if (text.StartsWith(prefix))
            {
                return text.Substring(prefix.Length);
            }
            else
            {
                throw new ArgumentOutOfRangeException("text", text, String.Format("{0} does not start with prefix {1}", text, prefix));
            }
        }

        static void EnsureSourceDirectoryExists(string sourceDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                Directory.CreateDirectory(sourceDir);

                var executable = new FileInfo(GetExecutable());

                // write source files
                var a = Assembly.GetExecutingAssembly();
                var embeddedResourcePrefix = a.GetName().Name + ".";
                foreach (var i in a.GetManifestResourceNames())
                {
                    var destination = Path.Combine(sourceDir, StripPrefix(embeddedResourcePrefix, i));
                    using (var w = File.OpenWrite(destination))
                    {
                        using (var r = a.GetManifestResourceStream(i))
                        {
                            r.CopyTo(w);
                        }
                    }
                    new FileInfo(destination).LastWriteTimeUtc = executable.LastWriteTimeUtc;
                }
            }
        }

        static void EnsureFileNotExists(string f)
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }

        static void TryDelete(string f)
        {
            if (File.Exists(f))
            {
                try
                {
                    File.Delete(f);
                }
                catch (System.UnauthorizedAccessException)
                {
                }
            }
        }

        static string GetBinDir()
        {
            return Path.GetDirectoryName(GetExecutable());
        }

        static string GetExecutable()
        {
            var b = Assembly.GetEntryAssembly().Location;
            return b;
        }

        static string GetSourceDir()
        {
            return GetExecutable() + ".src";
        }
        internal static void Update()
        {
            const string newExePrefix = "new-sce-";
            const string oldExePrefix = "old-sce-";

            var sourceDir = GetSourceDir();

            var outputFile = new FileInfo(GetExecutable());
            var oldExecutable = Path.Combine(outputFile.Directory.FullName, oldExePrefix + outputFile.Name);

            TryDelete(oldExecutable);

            EnsureSourceDirectoryExists(sourceDir);

            var sourceFiles = new DirectoryInfo(sourceDir).GetFiles("*.*", SearchOption.AllDirectories);
            var changed = sourceFiles.Where(_ => _.LastWriteTimeUtc > outputFile.LastWriteTimeUtc);

            if (changed.Any())
            {
                log("Source files have changed: {0}", String.Join(", ", changed.Cast<object>().ToArray()));

                // nuget restore

                // compile
                var csharpProvider = new Microsoft.CSharp.CSharpCodeProvider();
                var newExecutable = Path.Combine(outputFile.Directory.FullName, newExePrefix + outputFile.Name);


                var options = new CompilerParameters()
                {
                    OutputAssembly = newExecutable,
                    GenerateExecutable = true,
                    IncludeDebugInformation = false,
                    MainClass = "sce.SelfCompilingExecutable",
                    GenerateInMemory = false,
                };

                options.ReferencedAssemblies.AddRange(new[]
                {
                    "System.dll",
                    "Microsoft.CSharp.dll",
                    // "System.Xml.Linq.dll",
                    "System.Core.dll"
                });

                options.EmbeddedResources.AddRange(sourceFiles.Select(_ => _.FullName).ToArray());

                log(Assembly.GetAssembly(csharpProvider.GetType()).Location);

                var results = csharpProvider.CompileAssemblyFromFile(options, sourceFiles.Select(_ => _.FullName).ToArray());
                if (results.Errors.Cast<object>().Any())
                {
                    Console.Error.WriteLine(String.Join("\r\n", results.Errors.Cast<object>().ToArray()));
                    Environment.Exit(-1);
                }

                File.Move(outputFile.FullName, oldExecutable);
                File.Move(newExecutable, outputFile.FullName);
                var currentProcess = Process.GetCurrentProcess();
                var newProcess = StartProcess(Environment.CommandLine);

                newProcess.WaitForExit();
                Environment.Exit(newProcess.ExitCode);
            }
        }
    }
}
