using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;

using adsktsshared = adsk.ts.job.shared;

#nullable enable

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("20.0")]
[assembly: ExtensionId("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f")]

namespace adsk.ts.pdf.create.office
{
    public class JobExtension : IJobHandler
    {
        private const string JobType = "adsk.ts.pdf.create.office";
        private static readonly Settings DefaultSettings = Settings.Load();
        private static readonly string LogDirectory = DefaultSettings.LogFileLocation;

        private string _logFile = string.Empty;
        private TextWriterTraceListener? _trace;
        private Connection? _connection;
        private WebServiceManager? _wsMgr;
        private ACW.File? _file;
        private IJobProcessorServices? _context;

        public bool CanProcess(string jobType)
        {
            return jobType == JobType;
        }

        public JobOutcome Execute(IJobProcessorServices context, IJob job)
        {
            _context = context;

            try
            {
                _connection = context.Connection;
                _wsMgr = _connection.WebServiceManager;
                long entityId = Convert.ToInt64(job.Params["EntityId"]);
                string entityClassId = job.Params["EntityClassId"];

                if (entityClassId != "FILE")
                {
                    return JobOutcome.Success;
                }

                _file = _wsMgr.DocumentService.GetFileById(entityId);
                if (_file == null)
                {
                    context.Log(
                        "Job " + JobType + " did not start: could not retrieve the file object for id " + entityId,
                        MessageType.eError);
                    return JobOutcome.Failure;
                }

                if (_file.FileRev.MaxFileId != entityId)
                {
                    Settings settings = Settings.Load();
                    if (settings.EnforceSubmittedFileVersion.ToLower() == "false")
                    {
                        _file = _wsMgr.DocumentService.GetFileById(_file.FileRev.MaxFileId);
                    }
                    else
                    {
                        context.Log(
                            "Job " + JobType + " did not start: submitted version (" + entityId +
                            ") is no longer the tip version (" + _file.FileRev.MaxFileId + ").",
                            MessageType.eError);
                        return JobOutcome.Failure;
                    }
                }

                _logFile = JobType + "_" + _file.Name + ".log";
                FileInfo logFileInfo = new FileInfo(Path.Combine(LogDirectory, _logFile));
                if (logFileInfo.Exists)
                {
                    logFileInfo.Delete();
                }

                _trace = new TextWriterTraceListener(Path.Combine(LogDirectory, _logFile), "mJobTrace");
                _trace.WriteLine("Starting Job...");

                CreateOfficePdfExport();

                _trace.IndentLevel = 0;
                _trace.WriteLine("... successfully ending Job.");
                _trace.Flush();
                _trace.Close();

                return JobOutcome.Success;
            }
            catch (Exception ex)
            {
                context.Log(ex, "Job " + JobType + " failed: " + ex.Message);
                if (_trace != null)
                {
                    _trace.IndentLevel = 0;
                    _trace.WriteLine("... ending Job with failure.");
                    _trace.WriteLine(ex.ToString());
                }

                return JobOutcome.Failure;
            }
            finally
            {
                if (_trace != null)
                {
                    _trace.Flush();
                    _trace.Close();
                }

                _context = null;
            }
        }

        private void CreateOfficePdfExport()
        {
            Settings settings = Settings.Load();
            List<string> exportFormats = ParseExportFormats(settings.ExportFormats);
            List<string> validExportFormats = new List<string> { "OFFICE.PDF" };
            List<string> supportedExtensions = new List<string> { ".docx", ".xlsx", ".pptx" };
            List<string> filesToUpload = new List<string>();
            string conversionEngine = ResolveConversionEngineName(settings);

            _trace!.IndentLevel += 1;
            _trace.WriteLine("Translator job validates execution rules...");
            _trace.WriteLine("Conversion engine: " + conversionEngine);

            if (!supportedExtensions.Any(ext => _file!.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                _trace.WriteLine("Translator job exits: file extension is not supported.");
                return;
            }

            if (exportFormats.Count < 1 || !exportFormats.Any(fmt => validExportFormats.Contains(fmt)))
            {
                _trace.WriteLine("Translator job exits: no matching export format found.");
                return;
            }

            IOfficePdfConverter converter = CreateConverter(settings);
            if (IsTrue(settings.ValidateEngineOnStartup))
            {
                converter.ValidateAvailability();
                _trace.WriteLine("Conversion engine validated successfully.");
            }

            _trace.IndentLevel += 1;
            _trace.WriteLine("Job downloads source file(s) for translation.");

            adsktsshared.JobCommon jobCommon = new adsktsshared.JobCommon(_connection!, _wsMgr!, _trace);
            string sourcePath = jobCommon.mDownloadFile(_file!);
            string exportDir = jobCommon.mResolveExportLocalDirectory(settings.ExportPath, _file!, sourcePath);

            OfficeFileHelper.ValidateSourceFileReadable(sourcePath);
            OfficeFileHelper.ThrowIfPasswordProtected(sourcePath);
            OfficeFileHelper.EnsureWritableExportDirectory(exportDir);

            _trace.WriteLine("Job successfully downloaded source file(s) for translation.");
            _trace.WriteLine("Export directory: " + exportDir);

            foreach (string exportFormat in exportFormats)
            {
                if (exportFormat != "OFFICE.PDF")
                {
                    continue;
                }

                string pdfPath = BuildOutputPdfPath(settings, exportDir, sourcePath);
                OfficeFileHelper.DeleteExistingOutputFile(pdfPath);

                _trace.IndentLevel += 1;
                _trace.WriteLine("Office -> PDF export starts using " + conversionEngine + "...");

                try
                {
                    converter.ConvertToPdf(sourcePath, pdfPath);
                }
                catch (Exception ex)
                {
                    _context?.Log(ex, "Job " + JobType + " conversion failed for " + _file!.Name + ": " + ex.Message);
                    throw;
                }

                FileInfo exportFileInfo = new FileInfo(pdfPath);
                if (!exportFileInfo.Exists)
                {
                    throw new Exception("Validating the export file " + pdfPath + " before upload failed.");
                }

                filesToUpload.Add(pdfPath);
                _trace.WriteLine("Validated export file before upload: " + pdfPath + " (" + exportFileInfo.Length + " bytes).");
                _trace.IndentLevel -= 1;
            }

            if (filesToUpload.Count < 1)
            {
                throw new Exception("Job completed conversion but no export files were queued for upload.");
            }

            jobCommon.mUploadFiles(
                _file!,
                filesToUpload,
                settings.OutPutPath,
                settings.CopySystemComment == "True");

            _trace.IndentLevel = 1;
            _trace.WriteLine("Job finished all steps.");
        }

        private IOfficePdfConverter CreateConverter(Settings settings)
        {
            string engine = settings.ConversionEngine?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(engine) ||
                engine.Equals("LibreOffice", StringComparison.OrdinalIgnoreCase))
            {
                return new LibreOfficePdfConverter(settings, _trace!);
            }

            if (engine.Equals("MicrosoftOffice", StringComparison.OrdinalIgnoreCase))
            {
                return new MicrosoftOfficePdfConverter(settings, _trace!);
            }

            throw new Exception(
                "Conversion engine '" + engine + "' is not supported. Use ConversionEngine=LibreOffice or ConversionEngine=MicrosoftOffice.");
        }

        private static string ResolveConversionEngineName(Settings settings)
        {
            string engine = settings.ConversionEngine?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(engine))
            {
                return "LibreOffice";
            }

            return engine;
        }

        private static List<string> ParseExportFormats(string? configuredFormats)
        {
            if (string.IsNullOrWhiteSpace(configuredFormats))
            {
                throw new Exception("Settings expect to list at least one export format.");
            }

            if (configuredFormats.Contains(','))
            {
                return configuredFormats.Replace(" ", string.Empty).Split(',').ToList();
            }

            return new List<string> { configuredFormats.Trim() };
        }

        private static string BuildOutputPdfPath(Settings settings, string exportDir, string sourcePath)
        {
            if (IsTrue(settings.IncludeSourceFileExtension))
            {
                return Path.Combine(exportDir, Path.GetFileName(sourcePath) + ".pdf");
            }

            return Path.Combine(exportDir, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
        }

        private static bool IsTrue(string? value)
        {
            return string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }

        public void OnJobProcessorShutdown(IJobProcessorServices context)
        {
        }

        public void OnJobProcessorSleep(IJobProcessorServices context)
        {
        }

        public void OnJobProcessorStartup(IJobProcessorServices context)
        {
        }

        public void OnJobProcessorWake(IJobProcessorServices context)
        {
        }
    }
}
