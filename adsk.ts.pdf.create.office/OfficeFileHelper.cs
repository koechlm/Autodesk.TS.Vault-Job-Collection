using System;
using System.IO;
using System.IO.Compression;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal static class OfficeFileHelper
    {
        public static bool IsPasswordProtectedOfficeOpenXml(string filePath)
        {
            try
            {
                using FileStream stream = File.OpenRead(filePath);
                using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                return archive.GetEntry("EncryptedPackage") != null;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static void EnsureWritableExportDirectory(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);

            string probeFile = Path.Combine(directoryPath, ".office_pdf_write_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(probeFile, "write-test");
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Export directory is not writable by the Job Processor account: " + directoryPath +
                    ". Details: " + ex.Message,
                    ex);
            }
            finally
            {
                TryDeleteFile(probeFile);
            }
        }

        public static void DeleteExistingOutputFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.IsReadOnly)
            {
                fileInfo.IsReadOnly = false;
            }

            fileInfo.Delete();
        }

        public static void ValidateSourceFileReadable(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception("Source file does not exist: " + filePath);
            }

            FileInfo sourceInfo = new FileInfo(filePath);
            if (sourceInfo.Length <= 0)
            {
                throw new Exception("Source file is empty: " + filePath);
            }

            // Vault working-folder downloads are commonly marked read-only; verify read access only.
            try
            {
                using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception ex)
            {
                throw new Exception("Source file cannot be opened for reading: " + filePath + ". Details: " + ex.Message, ex);
            }
        }

        public static void ThrowIfPasswordProtected(string filePath)
        {
            if (!IsPasswordProtectedOfficeOpenXml(filePath))
            {
                return;
            }

            throw new Exception(
                "The source file appears to be password-protected (" + Path.GetFileName(filePath) +
                "). Remove encryption before running this job.");
        }

        /// <summary>
        /// Excel export can fail when the source workbook is opened from a read-only Vault download path.
        /// Returns a writable temp copy when needed; otherwise returns the original full path.
        /// </summary>
        public static string PrepareWritableSourceCopy(string sourcePath, string tempFilePrefix)
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            FileInfo sourceInfo = new FileInfo(fullSourcePath);
            if (!sourceInfo.IsReadOnly)
            {
                return fullSourcePath;
            }

            string tempSourcePath = Path.Combine(
                Path.GetTempPath(),
                tempFilePrefix + Guid.NewGuid().ToString("N") + sourceInfo.Extension);

            File.Copy(fullSourcePath, tempSourcePath, true);
            File.SetAttributes(tempSourcePath, FileAttributes.Normal);
            return tempSourcePath;
        }

        public static bool IsDifferentPath(string leftPath, string rightPath)
        {
            return !string.Equals(
                Path.GetFullPath(leftPath),
                Path.GetFullPath(rightPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
