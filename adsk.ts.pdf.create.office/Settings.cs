using System;
using System.IO;
using System.Xml.Serialization;

namespace adsk.ts.pdf.create.office
{
    [XmlRoot("settings")]
    public class Settings
    {
        [XmlElement("LogFileLocation")]
        public string LogFileLocation;

        [XmlElement("EnforceSubmittedFileVersion")]
        public string EnforceSubmittedFileVersion;

        [XmlElement("CopySystemComment")]
        public string CopySystemComment;

        [XmlElement("ExportFormats")]
        public string ExportFormats;

        [XmlElement("IncludeSourceFileExtension")]
        public string IncludeSourceFileExtension;

        [XmlElement("ExportPath")]
        public string ExportPath;

        [XmlElement("OutputPath")]
        public string OutPutPath;

        [XmlElement("ConversionEngine")]
        public string ConversionEngine;

        [XmlElement("LibreOfficePath")]
        public string LibreOfficePath;

        [XmlElement("LibreOfficeProfileRoot")]
        public string LibreOfficeProfileRoot;

        [XmlElement("ConversionTimeoutSeconds")]
        public string ConversionTimeoutSeconds;

        [XmlElement("ValidateEngineOnStartup")]
        public string ValidateEngineOnStartup;

        [XmlElement("OfficeVisible")]
        public string OfficeVisible;

        [XmlElement("PdfExportQuality")]
        public string PdfExportQuality;

        private Settings()
        {
        }

        public void Save()
        {
            try
            {
                string codeFolder = Util.GetAssemblyPath();
                string xmlPath = Path.Combine(codeFolder, "adsk.ts.pdf.create.office.settings.xml");

                using StreamWriter writer = new StreamWriter(xmlPath);
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                serializer.Serialize(writer, this);
            }
            catch
            {
            }
        }

        public static Settings Load()
        {
            string codeFolder = Util.GetAssemblyPath();
            string xmlPath = Path.Combine(codeFolder, "adsk.ts.pdf.create.office.settings.xml");

            using StreamReader reader = new StreamReader(xmlPath);
            XmlSerializer serializer = new XmlSerializer(typeof(Settings));
            Settings retVal = (Settings)serializer.Deserialize(reader);
            retVal.ApplyDefaults();
            return retVal;
        }

        public void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(LogFileLocation))
            {
                LogFileLocation = @"C:\Temp\";
            }

            if (string.IsNullOrWhiteSpace(EnforceSubmittedFileVersion))
            {
                EnforceSubmittedFileVersion = "False";
            }

            if (string.IsNullOrWhiteSpace(ExportFormats))
            {
                ExportFormats = "OFFICE.PDF";
            }

            if (string.IsNullOrWhiteSpace(IncludeSourceFileExtension))
            {
                IncludeSourceFileExtension = "True";
            }

            if (string.IsNullOrWhiteSpace(CopySystemComment))
            {
                CopySystemComment = "False";
            }

            if (string.IsNullOrWhiteSpace(ConversionEngine))
            {
                ConversionEngine = "LibreOffice";
            }

            if (string.IsNullOrWhiteSpace(ConversionTimeoutSeconds))
            {
                ConversionTimeoutSeconds = "180";
            }

            if (string.IsNullOrWhiteSpace(ValidateEngineOnStartup))
            {
                ValidateEngineOnStartup = "True";
            }

            if (string.IsNullOrWhiteSpace(OfficeVisible))
            {
                OfficeVisible = "False";
            }

            if (string.IsNullOrWhiteSpace(PdfExportQuality))
            {
                PdfExportQuality = "Standard";
            }
        }
    }
}
