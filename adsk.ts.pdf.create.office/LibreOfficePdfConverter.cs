using System;
using System.Diagnostics;
using System.IO;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal sealed class LibreOfficePdfConverter : IOfficePdfConverter
    {
        private static readonly string[] DefaultSofficePaths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
        };

        private readonly Settings _settings;
        private readonly TextWriterTraceListener _trace;
        private readonly string _sofficePath;

        public LibreOfficePdfConverter(Settings settings, TextWriterTraceListener trace)
        {
            _settings = settings;
            _trace = trace;
            _sofficePath = ResolveSofficePath(settings.LibreOfficePath);
        }

        public void ValidateAvailability()
        {
            if (!File.Exists(_sofficePath))
            {
                throw new Exception(
                    "LibreOffice is required but soffice.exe was not found at '" + _sofficePath +
                    "'. Install LibreOffice or set LibreOfficePath in the job settings.");
            }
        }

        public void ConvertToPdf(string sourcePath, string outputPdfPath)
        {
            ValidateAvailability();
            OfficeFileHelper.ValidateSourceFileReadable(sourcePath);
            OfficeFileHelper.ThrowIfPasswordProtected(sourcePath);

            string sourceExtension = Path.GetExtension(sourcePath);
            if (!TryGetPdfFilter(sourceExtension, out string pdfFilter))
            {
                throw new Exception("Unsupported source extension for LibreOffice conversion: " + sourceExtension);
            }

            string outputDirectory = Path.GetDirectoryName(outputPdfPath)
                ?? throw new Exception("Could not determine the output directory for " + outputPdfPath + ".");
            OfficeFileHelper.EnsureWritableExportDirectory(outputDirectory);

            string profileDirectory = CreateIsolatedProfileDirectory();
            string userInstallation = ToLibreOfficeProfileUri(profileDirectory);

            int[] sofficeBefore = ProcessCleanup.CaptureProcessIds("soffice");
            int[] sofficeBinBefore = ProcessCleanup.CaptureProcessIds("soffice.bin");

            OfficeConversionSync.Enter();
            try
            {
                OfficeFileHelper.DeleteExistingOutputFile(outputPdfPath);

                string arguments =
                    "--headless --nologo --norestore --nolockcheck " +
                    "-env:UserInstallation=" + userInstallation + " " +
                    "--convert-to " + QuoteArgument(pdfFilter) + " " +
                    "--outdir " + QuoteArgument(outputDirectory) + " " +
                    QuoteArgument(sourcePath);

                _trace.WriteLine("LibreOffice conversion starts: " + Path.GetFileName(sourcePath));
                _trace.WriteLine("LibreOffice executable: " + _sofficePath);

                int timeoutSeconds = ParseTimeoutSeconds(_settings.ConversionTimeoutSeconds);
                RunSofficeProcess(arguments, timeoutSeconds);

                string libreOfficeOutputPath = Path.Combine(
                    outputDirectory,
                    Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");

                if (!File.Exists(libreOfficeOutputPath))
                {
                    throw new Exception(
                        "LibreOffice did not create the expected PDF at " + libreOfficeOutputPath + ".");
                }

                if (!string.Equals(
                        Path.GetFullPath(libreOfficeOutputPath),
                        Path.GetFullPath(outputPdfPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    OfficeFileHelper.DeleteExistingOutputFile(outputPdfPath);
                    File.Move(libreOfficeOutputPath, outputPdfPath);
                }

                FileInfo outputInfo = new FileInfo(outputPdfPath);
                if (outputInfo.Length <= 0)
                {
                    throw new Exception("LibreOffice created an empty PDF at " + outputPdfPath + ".");
                }

                _trace.WriteLine("LibreOffice created file: " + outputPdfPath);
            }
            finally
            {
                OfficeConversionSync.Exit();
                ProcessCleanup.TerminateNewProcesses("soffice", sofficeBefore, _trace);
                ProcessCleanup.TerminateNewProcesses("soffice.bin", sofficeBinBefore, _trace);
                TryDeleteDirectory(profileDirectory);
            }
        }

        internal static string ResolveSofficePath(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath.Trim();
            }

            foreach (string candidate in DefaultSofficePaths)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return DefaultSofficePaths[0];
        }

        private static bool TryGetPdfFilter(string sourceExtension, out string pdfFilter)
        {
            switch (sourceExtension.ToLowerInvariant())
            {
                case ".docx":
                    pdfFilter = "pdf:writer_pdf_Export";
                    return true;
                case ".xlsx":
                    pdfFilter = "pdf:calc_pdf_Export";
                    return true;
                case ".pptx":
                    pdfFilter = "pdf:impress_pdf_Export";
                    return true;
                default:
                    pdfFilter = string.Empty;
                    return false;
            }
        }

        private string CreateIsolatedProfileDirectory()
        {
            string profileRoot = _settings.LibreOfficeProfileRoot;
            if (string.IsNullOrWhiteSpace(profileRoot))
            {
                profileRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Autodesk",
                    "Vault 2027",
                    "Extensions",
                    "adsk.ts.job.collection",
                    "LOProfiles");
            }

            string profileDirectory = Path.Combine(profileRoot.Trim(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(profileDirectory);
            return profileDirectory;
        }

        private static string ToLibreOfficeProfileUri(string profileDirectory)
        {
            string normalizedPath = Path.GetFullPath(profileDirectory).Replace('\\', '/');
            return "file:///" + normalizedPath;
        }

        private void RunSofficeProcess(string arguments, int timeoutSeconds)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = _sofficePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using Process process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new Exception("Failed to start LibreOffice process.");
            }

            try
            {
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(timeoutSeconds * 1000))
                {
                    ProcessCleanup.TerminateProcessTree(process, _trace);
                    throw new Exception(
                        "LibreOffice conversion timed out after " + timeoutSeconds +
                        " seconds. stderr: " + standardError);
                }

                if (process.ExitCode != 0)
                {
                    throw CreateLibreOfficeFailureException(process.ExitCode, standardOutput, standardError);
                }

                if (!string.IsNullOrWhiteSpace(standardOutput))
                {
                    _trace.WriteLine("LibreOffice stdout: " + standardOutput.Trim());
                }

                if (!string.IsNullOrWhiteSpace(standardError))
                {
                    _trace.WriteLine("LibreOffice stderr: " + standardError.Trim());
                }
            }
            finally
            {
                ProcessCleanup.TerminateProcessTree(process, _trace);
            }
        }

        private static Exception CreateLibreOfficeFailureException(int exitCode, string standardOutput, string standardError)
        {
            string details = "stdout: " + standardOutput + " stderr: " + standardError;
            if (standardError.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                standardOutput.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return new Exception(
                    "LibreOffice could not convert a password-protected file. Remove encryption before running this job. " +
                    details);
            }

            return new Exception("LibreOffice conversion failed with exit code " + exitCode + ". " + details);
        }

        private static int ParseTimeoutSeconds(string? configuredTimeout)
        {
            if (int.TryParse(configuredTimeout, out int timeoutSeconds) && timeoutSeconds > 0)
            {
                return timeoutSeconds;
            }

            return 180;
        }

        private static string QuoteArgument(string value)
        {
            if (value.Contains(' ') || value.Contains('"'))
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            return value;
        }

        private static void TryDeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
