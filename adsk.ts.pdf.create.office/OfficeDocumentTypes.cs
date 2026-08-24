using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal static class OfficeDocumentTypes
    {
        private static readonly string[] MicrosoftOfficeExtensions =
        {
            ".docx",
            ".xlsx",
            ".pptx",
        };

        private static readonly string[] LibreOfficeExtensions =
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

        public static IReadOnlyList<string> GetSupportedExtensions(ConversionEngineType engine)
        {
            return engine == ConversionEngineType.MicrosoftOffice
                ? MicrosoftOfficeExtensions
                : LibreOfficeExtensions;
        }

        public static bool IsSupported(string fileName, ConversionEngineType engine)
        {
            string extension = Path.GetExtension(fileName);
            return GetSupportedExtensions(engine).Any(
                candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryGetLibreOfficePdfFilter(string sourceExtension, out string pdfFilter)
        {
            switch (sourceExtension.ToLowerInvariant())
            {
                case ".docx":
                case ".odt":
                case ".fodt":
                case ".ott":
                    pdfFilter = "pdf:writer_pdf_Export";
                    return true;
                case ".xlsx":
                case ".ods":
                case ".fods":
                case ".ots":
                    pdfFilter = "pdf:calc_pdf_Export";
                    return true;
                case ".pptx":
                case ".odp":
                case ".fodp":
                case ".otp":
                    pdfFilter = "pdf:impress_pdf_Export";
                    return true;
                case ".odg":
                case ".fodg":
                case ".otg":
                    pdfFilter = "pdf:draw_pdf_Export";
                    return true;
                default:
                    pdfFilter = string.Empty;
                    return false;
            }
        }
    }
}
