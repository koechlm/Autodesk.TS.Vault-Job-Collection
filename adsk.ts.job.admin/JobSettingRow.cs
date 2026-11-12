using System.ComponentModel;

namespace adsk.ts.job.admin
{
    internal enum SettingEditor
    {
        Text,
        Check,
        Combo,
        Number,
        FolderPath,
        FilePath,
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
            Editor = JobSettingEditors.Resolve(key, value, out string[] choices);
            Choices = choices;
        }

        /// <summary>Technical name: the XML element and Vault option key.</summary>
        public string Key { get; }

        /// <summary>Readable name shown in the Option column.</summary>
        public string DisplayName => JobSettingNames.Get(Key);

        public string Description { get; }

        public SettingEditor Editor { get; }

        public string[] Choices { get; }

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

    internal static class JobSettingEditors
    {
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
