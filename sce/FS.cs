using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sce
{
    public class FS
    {
        public static string GetExecutable()
        {
            return new FileInfo(Assembly.GetEntryAssembly().Location).FullName;
        }

        static string GetBinDir()
        {
            return Path.GetDirectoryName(GetExecutable());
        }

        public static void EnsureDirectoryExists(string d)
        {
            if (!Directory.Exists(d))
            {
                Directory.CreateDirectory(d);
            }
        }
        public static void EnsureFileNotExists(string f)
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }

        public static void TryDelete(string f)
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

        internal static void EnsureParentDirectoryExists(string path)
        {
            EnsureDirectoryExists(Path.GetDirectoryName(path));
        }

        public static void EnsureDirectoryNotExists(string d)
        {
            if (Directory.Exists(d))
            {
                Directory.Delete(d, true);
            }
        }

        public static void EnsureDirectoryIsEmpty(string d)
        {
            EnsureDirectoryNotExists(d);
            Directory.CreateDirectory(d);
        }
    }
}
