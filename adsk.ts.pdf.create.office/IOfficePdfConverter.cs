#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal interface IOfficePdfConverter
    {
        void ValidateAvailability(string? sourceFileName = null);

        void ConvertToPdf(string sourcePath, string outputPdfPath);
    }
}
