using System.Drawing;
using System.IO;
using System.Reflection;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace adsk.ts.job.admin
{
    /// <summary>Icons embedded from Icons\dark and Icons\light, picked by the current Vault theme.</summary>
    internal static class JobAdminIcons
    {
        public const string CheckIn = "CheckIn_16";
        public const string Options = "Options_16x16";

        private static readonly Dictionary<string, Image> Cache = new();

        public static Image? Get(string iconName)
        {
            string themeFolder = VDF.Forms.Library.CurrentTheme == VDF.Forms.Library.UITheme.Dark ? "dark" : "light";
            string cacheKey = themeFolder + ":" + iconName;
            if (Cache.TryGetValue(cacheKey, out Image? cached)) return cached;

            string resourceName = $"adsk.ts.job.admin.Icons.{themeFolder}.{iconName}.png";
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            Image image = Image.FromStream(stream);
            Cache[cacheKey] = image;
            return image;
        }
    }
}
