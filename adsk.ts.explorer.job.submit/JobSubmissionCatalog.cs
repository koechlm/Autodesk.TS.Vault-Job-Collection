using System.Collections.Generic;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionCatalog
    {
        private static readonly string[] OfficePdfExtensions =
        {
            ".docx",
            ".xlsx",
            ".pptx",
            ".odt",
            ".fodt",
            ".ott",
            ".ods",
            ".fods",
            ".ots",
            ".odp",
            ".fodp",
            ".otp",
            ".odg",
            ".fodg",
            ".otg",
        };

        private static readonly string[] InventorImageExtensions = { ".ipt", ".iam", ".dwg", ".idw" };

        private static readonly string[] Inventor2DExtensions = { ".idw", ".dwg" };

        private static readonly string[] Inventor3DExtensions = { ".ipt", ".iam" };

        public static IReadOnlyList<JobSubmissionDefinition> All { get; } = new List<JobSubmissionDefinition>
        {
            new JobSubmissionDefinition(
                "Assign / Update Items",
                "adsk.ts.assignupdateitem",
                "adsk.ts.explorer.submit.adsk.ts.assignupdateitem",
                null,
                "assignupdateitem",
                "Promote and assign Vault items; Fusion Manage sync follows Job Processor settings."),
            new JobSubmissionDefinition(
                "Create Inventor 2D DWG",
                "adsk.ts.acad.dwg2d.create.inventor",
                "adsk.ts.explorer.submit.adsk.ts.acad.dwg2d.create.inventor",
                Inventor2DExtensions,
                "dwg_2d",
                "Export 2D DWG from .idw or .dwg (ExportFormats 2DDWG on Job Processor)."),
            new JobSubmissionDefinition(
                "Create Inventor 3D DWG, JT, ...",
                "adsk.ts.export3d.create.inventor",
                "adsk.ts.explorer.submit.adsk.ts.export3d.create.inventor",
                Inventor3DExtensions,
                "export3d_formats",
                "Neutral 3D exports from .ipt or .iam; outputs follow ExportFormats on Job Processor (e.g. 3DDWG, STP, JT)."),
            new JobSubmissionDefinition(
                "Create Inventor PNG",
                "adsk.ts.image.create.inventor",
                "adsk.ts.explorer.submit.adsk.ts.image.create.inventor",
                InventorImageExtensions,
                "inventor_png",
                "Rendered image; file type follows ImgFileType on Job Processor (sample: PNG)."),
            new JobSubmissionDefinition(
                "Create Revit RVT",
                "adsk.ts.rvt.create.inventor",
                "adsk.ts.explorer.submit.adsk.ts.rvt.create.inventor",
                new[] { ".iam" },
                "rvt",
                "Revit simplification export to .rvt from Inventor assemblies."),
            new JobSubmissionDefinition(
                "Create NWD and DWF",
                "adsk.ts.nwd.create.navisworks",
                "adsk.ts.explorer.submit.adsk.ts.nwd.create.navisworks",
                null,
                "nwd",
                "Navisworks publish; sample settings use NWD+DWF (DWF omitted when ExportFormats=NWD only)."),
            new JobSubmissionDefinition(
                "Create SolidWorks PDF and DXF",
                "adsk.ts.pdf.create.slddrw",
                "adsk.ts.explorer.submit.adsk.ts.pdf.create.slddrw",
                new[] { ".slddrw" },
                "pdf_slddrw",
                "Drawing PDF export; separate DXF sheet when PdfIncludeDxfSheet is false on Job Processor."),
            new JobSubmissionDefinition(
                "Create Office PDF",
                "adsk.ts.pdf.create.office",
                "adsk.ts.explorer.submit.adsk.ts.pdf.create.office",
                OfficePdfExtensions,
                "pdf_office",
                "Office document PDF; LibreOffice or Microsoft Office engine on Job Processor."),
        };
    }
}
