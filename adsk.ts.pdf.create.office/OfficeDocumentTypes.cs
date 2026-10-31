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

        public static bool TryGetLibreOfficePdfFilter(string sourceExtension, bool exportNavigation, out string pdfFilter)
        {
            if (!TryGetLibreOfficePdfFilterBase(sourceExtension, out string baseFilter))
            {
                pdfFilter = string.Empty;
                return false;
            }

            pdfFilter = FormatLibreOfficePdfFilter(baseFilter, exportNavigation);
            return true;
        }

        private static bool TryGetLibreOfficePdfFilterBase(string sourceExtension, out string baseFilter)
        {
            switch (sourceExtension.ToLowerInvariant())
            {
                case ".docx":
                case ".odt":
                case ".fodt":
                case ".ott":
                    baseFilter = "pdf:writer_pdf_Export";
                    return true;
                case ".xlsx":
                case ".ods":
                case ".fods":
                case ".ots":
                    baseFilter = "pdf:calc_pdf_Export";
                    return true;
                case ".pptx":
                case ".odp":
                case ".fodp":
                case ".otp":
                    baseFilter = "pdf:impress_pdf_Export";
                    return true;
                case ".odg":
                case ".fodg":
                case ".otg":
                    baseFilter = "pdf:draw_pdf_Export";
                    return true;
                default:
                    baseFilter = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Appends LibreOffice 7.4+ PDF filter JSON (ExportBookmarks controls outline/navigation export).
        /// </summary>
        internal static string FormatLibreOfficePdfFilter(string baseFilter, bool exportNavigation)
        {
            string exportBookmarksValue = exportNavigation ? "true" : "false";
            return baseFilter + ":{\"ExportBookmarks\":{\"type\":\"boolean\",\"value\":" + exportBookmarksValue + "}}";
        }
    }
}
