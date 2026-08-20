using System;
using System.IO;

namespace adsk.ts.pdf.create.office
{
    public class Util
    {
        public static string GetAssemblyPath()
        {
            string prefix = "file:///";
            string codebase = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (codebase.StartsWith(prefix, StringComparison.Ordinal))
            {
                codebase = codebase.Substring(prefix.Length);
            }

            return Path.GetDirectoryName(codebase) ?? string.Empty;
        }
    }
}
