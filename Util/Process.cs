using System;
using System.Text.RegularExpressions;
using System.Linq;

namespace Util
{
	public static class Process
	{
		public static string QuoteArgumentIfRequired(string arg)
		{
			if (Regex.IsMatch(arg, @"\s"))
			{
				return "\"" + arg + "\"";
			}
			else
			{
				return arg;
			}
		}
		
		public static string GetArgumentString(string[] args)
		{
			return String.Join(" ", args.Select(QuoteArgumentIfRequired).ToArray());
		}
		
		public static int Run(string commandLine, string[] args)
		{
			var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = commandLine,
				Arguments = GetArgumentString(args),
				UseShellExecute = false
			});
			p.WaitForExit();
			return p.ExitCode;
		}
	}
}
