using System;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal enum ConversionEngineType
    {
        LibreOffice,
        MicrosoftOffice,
        Unknown,
    }

    internal static class ConversionEngineHelper
    {
        public static ConversionEngineType Parse(string? configuredEngine)
        {
            string normalized = Normalize(configuredEngine);
            if (string.IsNullOrEmpty(normalized))
            {
                return ConversionEngineType.LibreOffice;
            }

            if (normalized.Equals("libreoffice", StringComparison.Ordinal) ||
                normalized.Equals("lo", StringComparison.Ordinal))
            {
                return ConversionEngineType.LibreOffice;
            }

            if (normalized.Equals("microsoftoffice", StringComparison.Ordinal) ||
                normalized.Equals("msoffice", StringComparison.Ordinal) ||
                normalized.Equals("microsoft-office", StringComparison.Ordinal))
            {
                return ConversionEngineType.MicrosoftOffice;
            }

            return ConversionEngineType.Unknown;
        }

        public static string GetDisplayName(ConversionEngineType engineType)
        {
            switch (engineType)
            {
                case ConversionEngineType.MicrosoftOffice:
                    return "MicrosoftOffice";
                default:
                    return "LibreOffice";
            }
        }

        public static string GetSupportedEngineList()
        {
            return "LibreOffice, MicrosoftOffice";
        }

        private static string Normalize(string? configuredEngine)
        {
            if (string.IsNullOrWhiteSpace(configuredEngine))
            {
                return string.Empty;
            }

            return configuredEngine
                .Trim()
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }
    }
}
