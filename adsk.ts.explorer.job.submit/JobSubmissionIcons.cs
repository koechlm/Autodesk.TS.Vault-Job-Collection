using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

using VDF = Autodesk.DataManagement.Client.Framework;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionIcons
    {
        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();

        public static Image? GetMenuIcon(string iconKey)
        {
            if (string.IsNullOrEmpty(iconKey))
            {
                return null;
            }

            string themeFolder = GetThemeFolder();
            string cacheKey = themeFolder + ":" + iconKey;
            if (Cache.TryGetValue(cacheKey, out Image? cached))
            {
                return cached;
            }

            string resourceName = "adsk.ts.explorer.job.submit.Resources.Icons."
                + themeFolder + "." + iconKey + ".png";

            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            Image image = Image.FromStream(stream);
            Cache[cacheKey] = image;
            return image;
        }

        private static string GetThemeFolder()
        {
            return VDF.Forms.Library.CurrentTheme == VDF.Forms.Library.UITheme.Dark
                ? "Dark"
                : "Light";
        }
    }
}
