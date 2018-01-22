using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace sce
{
    static class Util
    {
        internal static bool IsCSharpFile(FileInfo f)
        {
            return f.Extension.Equals(".cs", StringComparison.InvariantCultureIgnoreCase);
        }

        static internal string GetDigest(string x)
        {
            var sha256 = new SHA256Managed();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(x));
            return GetHexString(hash);
        }

        static internal string GetHexString(byte[] hash)
        {
            return String.Join(String.Empty, hash.Select(_ => String.Format("{0:x2}", _)));
        }

        internal static string StripPrefix(string prefix, string text)
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

        internal static Process StartProcess(string commandline)
        {
            var args = SplitCommandLine(commandline).ToList();
            var fileName = args[0];
            var arguments = commandline.Substring(fileName.Length);
            return Process.Start(new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false
            });
        }

        static void Concat(ref string s, char c)
        {
            if (s == null)
            {
                s = String.Empty;
            }
            s = s + c;
        }

        internal static IEnumerable<string> SplitCommandLine(string commandLine)
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

    }
}
