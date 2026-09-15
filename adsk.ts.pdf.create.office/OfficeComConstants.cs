namespace adsk.ts.pdf.create.office
{
    /// <summary>
    /// COM constants for Office automation without deploying Office Primary Interop Assemblies.
    /// </summary>
    internal static class OfficeComConstants
    {
        internal const int MsoTrue = -1;
        internal const int MsoFalse = 0;

        internal const int WdAlertsNone = 0;
        internal const int WdExportFormatPdf = 17;
        internal const int WdExportOptimizeForPrint = 0;
        internal const int WdExportOptimizeForOnScreen = 1;
        internal const int WdExportAllDocument = 0;
        internal const int WdExportDocumentContent = 0;
        internal const int WdExportCreateNoBookmarks = 0;

        internal const int XlTypePdf = 0;
        internal const int XlQualityStandard = 0;
        internal const int XlQualityMinimum = 1;
        internal const int XlUpdateLinksNever = 0;

        internal const int PpFixedFormatTypePdf = 2;
        internal const int PpFixedFormatIntentPrint = 2;
        internal const int PpWindowMinimized = 2;
        internal const int PpSaveAsPdf = 32;
        internal const int PpAlertsNone = 1;
    }
}
