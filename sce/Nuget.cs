using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace sce
{
    public class PackageIndex
    {
        public string[] versions;
    }

    public class PackageSpec
    {
        public string Id { set; get; }
        public string Version { set; get; }
    }

    public class Nuget
    {
        static void log(string f, params object[] args)
        {
            Trace.WriteLine(String.Format(f, args), "Nuget");
        }

        public interface ISource
        {
            Task Download(PackageSpec package, string nupkgDest);
            Task<PackageSpec> GetLatestVersion(string packageId);
        }

        public class SourceV3 : ISource
        {
            private readonly string baseUrl;

            WebClient wc = new WebClient();

            async Task<T> Get<T>(string url)
            {
                log("get: {0}", url);
                var json = await wc.DownloadStringTaskAsync(new Uri(url));
                return new JavaScriptSerializer().Deserialize<T>(json);
            }

            public SourceV3(string baseUrl)
            {
                var p = baseUrl.Split('/');
                this.baseUrl = String.Join("/", p.Take(p.Length - 2));
            }

            public async Task Download(PackageSpec package, string nupkgDest)
            {
                using (var wc = new WebClient())
                {
                    await wc.DownloadFileTaskAsync(GetNupkgUrl(package), nupkgDest);
                }
            }

            string GetNupkgUrl(PackageSpec spec)
            {
                return $"{baseUrl}/v3-flatcontainer/{spec.Id}/{spec.Version}/{spec.Id}.{spec.Version}.nupkg";
            }

            public async Task<PackageSpec> GetLatestVersion(string packageId)
            {
                var url = $"{baseUrl}/v3-flatcontainer/{packageId}/index.json";
                try
                {
                    var p = await Get<PackageIndex>(url);
                    return new PackageSpec
                    {
                        Id = packageId,
                        Version = p.versions.Last()
                    };
                }
                catch (Exception e)
                {
                    log("Exception: {0}", e);
                    return null;
                }
            }
        }

        public class InstalledPackage
        {
            public InstalledPackage(Repository repo, PackageSpec spec)
            {
                Repo = repo;
                Spec = spec;
                PackageDir = repo.GetPackageDir(spec);
            }

            public Repository Repo { get; }
            public PackageSpec Spec { get; }

            public string PackageDir { get; }

            public string NupkgFile => Path.Combine(PackageDir, String.Join(".", Spec.Id, Spec.Version, "nupkg"));

            public string LibDir => Path.Combine(PackageDir, "lib");

            public class FrameworkDir
            {
                public static FrameworkDir Get(DirectoryInfo dir)
                {
                    var m = Regex.Match(dir.Name, @"([a-z]+)(\d+)(-\w+)?");
                    var f = new FrameworkDir();
                    f.directory = dir;
                    f.framework = m.Groups[1].Value;
                    f.version = m.Groups[2].Value;
                    f.attribute = m.Groups[3].Value;
                    return f;
                }

                public DirectoryInfo directory { get; private set; }
                public string framework { get; private set; }
                public string version { get; private set; }
                public string attribute { get; private set; }
            }

            public IEnumerable<FrameworkDir> GetFrameworkDirs()
            {
                return new DirectoryInfo(LibDir).GetDirectories()
                    .Select(_ => FrameworkDir.Get(_))
                    .Where(_ => _ != null)
                    .ToList();
            }

            public IEnumerable<string> GetReferenceAssemblies()
            {
                var fwd = GetFrameworkDirs()
                    .Where(_ => _.framework.Equals("net"))
                    .OrderByDescending(_ => _.version)
                    .First();

                return fwd.directory.GetFiles("*.dll").Select(_ => _.FullName);
            }
        }

        public class Repository
        {
            private readonly string repositoryDirectory;

            public Repository(string repositoryDirectory)
            {
                this.repositoryDirectory = repositoryDirectory;
            }

            public string GetPackageFileName(PackageSpec package)
            {
                return String.Join(".", package.Id, package.Version);
            }

            public string GetPackageDir(PackageSpec package)
            {
                return Path.Combine(repositoryDirectory, GetPackageFileName(package));
            }

            internal string GetNupkgPath(PackageSpec package)
            {
                return Path.Combine(GetPackageDir(package), GetPackageFileName(package) + nupkgExtension);
            }

            const string nupkgExtension = ".nupkg";

            public async Task<InstalledPackage> Install(ISource source, PackageSpec package)
            {
                var pdir = GetPackageDir(package);
                if (Directory.Exists(pdir))
                {
                    throw new Exception("Package already installed.");
                }

                FS.EnsureDirectoryExists(pdir);

                var nupkgDest = GetNupkgPath(package);
                FS.EnsureParentDirectoryExists(nupkgDest);
                await source.Download(package, nupkgDest);

                ZipFile.ExtractToDirectory(nupkgDest, pdir);
                return new InstalledPackage(this, package);
            }

            public bool Exists(PackageSpec package)
            {
                return Directory.Exists(GetPackageDir(package));
            }

            public async Task<InstalledPackage> EnsureInstalled(ISource source, PackageSpec package)
            {
                if (!Exists(package))
                {
                    return await Install(source, package);
                }
                else
                {
                    return Get(package);
                }
            }

            public InstalledPackage Get(PackageSpec package)
            {
                return new InstalledPackage(this, package);
            }

            public async Task<IEnumerable<InstalledPackage>> EnsurePackagesAreInstalled(IEnumerable<ISource> sources, IEnumerable<string> packageIds)
            {
                var spec = new List<InstalledPackage>();

                foreach (var p in packageIds)
                {
                    foreach (var source in sources)
                    {
                        var package = await source.GetLatestVersion(p);
                        if (package == null)
                        {
                            continue;
                        }
                        var ip = await EnsureInstalled(source, package);
                        spec.Add(ip);
                        break;
                    }
                }
                return spec;
            }


        }

        public static ISource GetSource(string arg)
        {
            return new SourceV3(arg);
        }

        public static ISource GetDefaultSource()
        {
            return GetSource("https://api.nuget.org/v3/index.json");
        }
    }
}
