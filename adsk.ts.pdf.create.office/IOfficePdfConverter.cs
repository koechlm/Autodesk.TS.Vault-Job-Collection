namespace adsk.ts.pdf.create.office
{
    internal interface IOfficePdfConverter
    {
        void ValidateAvailability();

        void ConvertToPdf(string sourcePath, string outputPdfPath);
    }
}
