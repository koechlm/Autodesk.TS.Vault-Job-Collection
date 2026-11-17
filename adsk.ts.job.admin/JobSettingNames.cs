using System.Text.RegularExpressions;

namespace adsk.ts.job.admin
{
    /// <summary>
    /// Readable names for the options of the settings files. The XML element names stay the technical names
    /// (jobs and Vault options match on them); the dialog shows these names instead.
    /// </summary>
    internal static class JobSettingNames
    {
        private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            ["LogFileLocation"] = "Log file location",
            ["EnforceSubmittedFileVersion"] = "Enforce submitted file version",
            ["CopySystemComment"] = "Copy system comment",
            ["ExportFormats"] = "Export formats",
            ["ExportPath"] = "Export path",
            ["OutputPath"] = "Output path",
            ["AcceptLocalIpj"] = "Accept local IPJ",
            ["IncludeSourceFileExtension"] = "Include source file extension",
            ["DwgIniFile2D"] = "2D DWG export INI file",
            ["ExcludeDesignSubstitute"] = "Exclude design substitutes",
            ["ImgFileType"] = "Image file type",
            ["UseInventorExe"] = "Use Inventor.exe",
            ["RvtAssociative"] = "RVT associative updating",
            ["TargetRevitVersion"] = "Target Revit versions",
            ["RevitTemplate"] = "Revit template",
            ["InventorPreset"] = "Inventor preset file",
            ["InventorPresetName"] = "Inventor preset names",
            ["FMSync"] = "Fusion Manage sync",
            ["NwdTemplate"] = "NWD template",
            ["DxfSheetName"] = "DXF sheet name",
            ["PdfIncludeDxfSheet"] = "Include DXF sheet in PDF",
            ["ConversionEngine"] = "Conversion engine",
            ["LibreOfficePath"] = "LibreOffice path",
            ["LibreOfficeProfileRoot"] = "LibreOffice profile folder",
            ["ConversionTimeoutSeconds"] = "LibreOffice timeout (seconds)",
            ["MicrosoftOfficeConversionTimeoutSeconds"] = "Microsoft Office timeout (seconds)",
            ["ValidateEngineOnStartup"] = "Validate engine on startup",
            ["OfficeVisible"] = "Show Office applications",
            ["PdfExportQuality"] = "PDF export quality",
            ["PdfExportNavigation"] = "PDF export bookmarks and links",
        };

        /// <summary>Table entry, or the camel case words of an unknown option in sentence case.</summary>
        public static string Get(string key)
        {
            if (Names.TryGetValue(key, out string? name)) return name;

            string words = Regex.Replace(key, "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant();
            return char.ToUpperInvariant(words[0]) + words[1..];
        }
    }
}
