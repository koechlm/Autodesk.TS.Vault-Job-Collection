namespace adsk.ts.job.admin
{
    /// <summary>A job of the collection: its settings file, Vault option and the tab page showing it.</summary>
    internal sealed record JobDefinition(string TabPageName, string AssemblyName, string SettingsFileName)
    {
        public string OptionName => JobSettingsStore.OptionPrefix + AssemblyName;

        /// <summary>Option that selects the engine; shown as pulldown above the grid instead of as grid row.</summary>
        public string? EngineKey { get; init; }

        /// <summary>Engine name to the options only that engine uses; all other options are general.</summary>
        public IReadOnlyDictionary<string, string[]>? EngineOptions { get; init; }

        /// <summary>Options that take a list of values chosen with check boxes (option name to the allowed values).</summary>
        public IReadOnlyDictionary<string, string[]>? ListOptions { get; init; }

        public static readonly IReadOnlyList<JobDefinition> All = new[]
        {
            new JobDefinition("acaddwg2DCreateInventor", "adsk.ts.acad.dwg2d.create.inventor", "adsk.ts.acad.dwg2d.create.inventor.settings.xml"),
            new JobDefinition("export3DCreateInventor", "adsk.ts.export3d.create.inventor", "adsk.ts.export3d.create.inventor.settings.xml")
            {
                ListOptions = new Dictionary<string, string[]> { ["ExportFormats"] = new[] { "3DDWG", "STEP", "JT" } },
            },
            new JobDefinition("imageCreateInventor", "adsk.ts.image.create.inventor", "adsk.ts.image.create.inventor.settings.xml"),
            new JobDefinition("rvtCreateInventor", "adsk.ts.rvt.create.inventor", "adsk.ts.rvt.create.inventor.settings.xml"),
            new JobDefinition("assignUpdateItem", "adsk.ts.assignupdateitem", "adsk.ts.assignupdateitem.xml"),
            new JobDefinition("nwdCreateNavisworks", "adsk.ts.nwd.create.navisworks", "adsk.ts.nwd.create.navisworks.settings.xml")
            {
                ListOptions = new Dictionary<string, string[]> { ["ExportFormats"] = new[] { "NWD", "DWF" } },
            },
            new JobDefinition("pdfCreateOffice", "adsk.ts.pdf.create.office", "adsk.ts.pdf.create.office.settings.xml")
            {
                EngineKey = "ConversionEngine",
                EngineOptions = new Dictionary<string, string[]>
                {
                    ["LibreOffice"] = new[] { "LibreOfficePath", "LibreOfficeProfileRoot", "ConversionTimeoutSeconds" },
                    ["MicrosoftOffice"] = new[] { "MicrosoftOfficeConversionTimeoutSeconds", "OfficeVisible", "PdfExportQuality" },
                },
            },
            new JobDefinition("pdfCreateSldDrw", "adsk.ts.pdf.create.slddrw", "adsk.ts.pdf.create.slddrw.settings.xml"),
        };
    }
}
