using System.ComponentModel;

namespace adsk.ts.job.admin
{
    internal enum SettingEditor
    {
        Text,
        Check,
        Combo,
        CheckedList,
        Number,
        FolderPath,
        FilePath,
        VaultPath,
    }

    /// <summary>One option of a job: XML element name, current value and the comment shipped in the settings file.</summary>
    internal sealed class JobSettingRow : INotifyPropertyChanged
    {
        private string _value;

        public JobSettingRow(string key, string value, string description)
        {
            Key = key;
            _value = value;
            Description = description;
            OriginalValue = value;
            VaultPath = JobSettingEditors.GetVaultPath(key);
            Editor = VaultPath != null ? SettingEditor.VaultPath : JobSettingEditors.Resolve(key, value, out string[] choices);
            Choices = VaultPath != null ? Array.Empty<string>() : JobSettingEditors.ResolveChoices(key);
        }

        /// <summary>Technical name: the XML element and Vault option key.</summary>
        public string Key { get; }

        /// <summary>Readable name shown in the Option column.</summary>
        public string DisplayName => JobSettingNames.Get(Key);

        public string Description { get; }

        public SettingEditor Editor { get; private set; }

        /// <summary>Set for options that hold a Vault path ($/...): what the Vault browser lets the user select.</summary>
        public VaultPathInfo? VaultPath { get; }

        public string[] Choices { get; private set; }

        /// <summary>
        /// Turns the option into a list with a check box per choice. The value becomes the checked choices in the
        /// order of the choices ("NWD, DWF"); separators other than a comma, such as the former "NWD+DWF", and the token STP (STEP) are read too.
        /// </summary>
        public void UseCheckedList(string[] choices)
        {
            Editor = SettingEditor.CheckedList;
            Choices = choices;

            string[] tokens = Value.Split(new[] { ',', '+', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Equals("STP", StringComparison.OrdinalIgnoreCase) ? "STEP" : t).ToArray();
            string normalized = string.Join(", ", choices.Where(c => tokens.Contains(c, StringComparer.OrdinalIgnoreCase)));
            Value = normalized;
            OriginalValue = normalized;
        }

        /// <summary>Value as loaded or last saved; used to detect edits.</summary>
        public string OriginalValue { get; set; }

        public string Value
        {
            get => _value;
            set
            {
                value ??= "";
                if (_value == value) return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public bool IsModified => Value != OriginalValue;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>A Vault folder, or a Vault file of one extension (for example ".preset" with "Inventor preset").</summary>
    internal sealed record VaultPathInfo(string Caption, string? Extension)
    {
        public bool IsFolder => Extension == null;
    }

    internal static class JobSettingEditors
    {
        private static readonly Dictionary<string, VaultPathInfo> VaultPathKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ExportPath"] = new VaultPathInfo("Export folder", null),
            ["NwdTemplate"] = new VaultPathInfo("Navisworks template (*.nwd)", ".nwd"),
            ["RevitTemplate"] = new VaultPathInfo("Revit template (*.rte)", ".rte"),
            ["InventorPreset"] = new VaultPathInfo("Inventor preset (*.preset)", ".preset"),
        };

        public static VaultPathInfo? GetVaultPath(string key) =>
            VaultPathKeys.TryGetValue(key, out VaultPathInfo? info) ? info : null;

        public static string[] ResolveChoices(string key)
        {
            Resolve(key, "", out string[] choices);
            return choices;
        }

        private static readonly string[] FolderKeys = { "LogFileLocation", "OutputPath", "LibreOfficeProfileRoot" };
        private static readonly string[] FileKeys = { "LibreOfficePath", "DwgIniFile2D" };

        private static readonly Dictionary<string, string[]> ComboKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ConversionEngine"] = new[] { "LibreOffice", "MicrosoftOffice" },
            ["PdfExportQuality"] = new[] { "Standard", "Minimum" },
            ["ImgFileType"] = new[] { "BMP", "PNG", "GIF", "JPG", "TIFF" },
        };

        public static SettingEditor Resolve(string key, string value, out string[] choices)
        {
            choices = Array.Empty<string>();

            if (ComboKeys.TryGetValue(key, out string[]? list))
            {
                choices = list;
                return SettingEditor.Combo;
            }
            if (FolderKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return SettingEditor.FolderPath;
            if (FileKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return SettingEditor.FilePath;
            if (key.EndsWith("Seconds", StringComparison.OrdinalIgnoreCase)) return SettingEditor.Number;
            if (bool.TryParse(value, out _)) return SettingEditor.Check;
            return SettingEditor.Text;
        }
    }
}
