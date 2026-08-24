using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal sealed class MicrosoftOfficePdfConverter : IOfficePdfConverter
    {
        private readonly Settings _settings;
        private readonly TextWriterTraceListener _trace;

        public MicrosoftOfficePdfConverter(Settings settings, TextWriterTraceListener trace)
        {
            _settings = settings;
            _trace = trace;
        }

        public void ValidateAvailability(string? sourceFileName = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFileName))
            {
                ValidateWordAvailability();
                ValidateExcelAvailability();
                ValidatePowerPointAvailability();
                return;
            }

            switch (Path.GetExtension(sourceFileName).ToLowerInvariant())
            {
                case ".docx":
                    ValidateWordAvailability();
                    break;
                case ".xlsx":
                    ValidateExcelAvailability();
                    break;
                case ".pptx":
                    ValidatePowerPointAvailability();
                    break;
                default:
                    ValidateWordAvailability();
                    ValidateExcelAvailability();
                    ValidatePowerPointAvailability();
                    break;
            }
        }

        public void ConvertToPdf(string sourcePath, string outputPdfPath)
        {
            OfficeFileHelper.ValidateSourceFileReadable(sourcePath);
            OfficeFileHelper.ThrowIfPasswordProtected(sourcePath);

            string outputDirectory = Path.GetDirectoryName(outputPdfPath)
                ?? throw new Exception("Could not determine the output directory for " + outputPdfPath + ".");
            OfficeFileHelper.EnsureWritableExportDirectory(outputDirectory);
            OfficeFileHelper.DeleteExistingOutputFile(outputPdfPath);

            string sourceExtension = Path.GetExtension(sourcePath);
            OfficeConversionSync.Enter();
            try
            {
                _trace.WriteLine("Microsoft Office conversion starts: " + Path.GetFileName(sourcePath));

                switch (sourceExtension.ToLowerInvariant())
                {
                    case ".docx":
                        ConvertWordDocument(sourcePath, outputPdfPath);
                        break;
                    case ".xlsx":
                        ConvertExcelWorkbook(sourcePath, outputPdfPath);
                        break;
                    case ".pptx":
                        ConvertPowerPointPresentation(sourcePath, outputPdfPath);
                        break;
                    default:
                        throw new Exception("Unsupported source extension for Microsoft Office conversion: " + sourceExtension);
                }

                FileInfo outputInfo = new FileInfo(outputPdfPath);
                if (!outputInfo.Exists || outputInfo.Length <= 0)
                {
                    throw new Exception("Microsoft Office did not create a valid PDF at " + outputPdfPath + ".");
                }

                _trace.WriteLine("Microsoft Office created file: " + outputPdfPath);
            }
            finally
            {
                OfficeConversionSync.Exit();
            }
        }

        private void ConvertWordDocument(string sourcePath, string outputPdfPath)
        {
            int[] processIdsBefore = ProcessCleanup.CaptureProcessIds("WINWORD");
            dynamic? wordApp = null;
            dynamic? document = null;

            try
            {
                wordApp = CreateComApplication("Word.Application", "Microsoft Word");
                wordApp.Visible = IsTrue(_settings.OfficeVisible);
                wordApp.DisplayAlerts = OfficeComConstants.WdAlertsNone;

                document = wordApp.Documents.Open(
                    sourcePath,
                    false,
                    true,
                    false);

                document.ExportAsFixedFormat(
                    outputPdfPath,
                    OfficeComConstants.WdExportFormatPdf,
                    false,
                    GetWordOptimizeFor(),
                    OfficeComConstants.WdExportAllDocument,
                    1,
                    1,
                    OfficeComConstants.WdExportDocumentContent,
                    true,
                    true,
                    OfficeComConstants.WdExportCreateNoBookmarks,
                    true,
                    true,
                    false);
            }
            catch (Exception ex)
            {
                throw WrapOfficeExportException("Microsoft Word", ex);
            }
            finally
            {
                CloseDynamicComObject(document, false);
                QuitDynamicComObject(wordApp, "Quit", false);
                ProcessCleanup.TerminateNewProcesses("WINWORD", processIdsBefore, _trace);
            }
        }

        private void ConvertExcelWorkbook(string sourcePath, string outputPdfPath)
        {
            int[] processIdsBefore = ProcessCleanup.CaptureProcessIds("EXCEL");
            dynamic? excelApp = null;
            dynamic? workbook = null;
            string workingSourcePath = OfficeFileHelper.PrepareWritableSourceCopy(sourcePath, "adsk_office_xlsx_");
            string normalizedOutputPath = Path.GetFullPath(outputPdfPath);
            string tempPdfPath = Path.Combine(
                Path.GetTempPath(),
                "adsk_office_pdf_" + Guid.NewGuid().ToString("N") + ".pdf");
            bool deleteTempSource = OfficeFileHelper.IsDifferentPath(workingSourcePath, sourcePath);
            bool tempPdfMoved = false;

            try
            {
                _trace.WriteLine("Excel source path: " + workingSourcePath);
                _trace.WriteLine("Excel temp PDF path: " + tempPdfPath);
                _trace.WriteLine("Excel final PDF path: " + normalizedOutputPath);

                excelApp = CreateComApplication("Excel.Application", "Microsoft Excel");
                excelApp.Visible = IsTrue(_settings.OfficeVisible);
                excelApp.DisplayAlerts = false;
                excelApp.ScreenUpdating = false;
                excelApp.EnableEvents = false;
                excelApp.Interactive = false;

                workbook = excelApp.Workbooks.Open(
                    workingSourcePath,
                    OfficeComConstants.XlUpdateLinksNever,
                    false);

                ExportExcelWorkbookAsPdf(workbook, tempPdfPath, GetExcelQuality());

                if (!File.Exists(tempPdfPath) || new FileInfo(tempPdfPath).Length <= 0)
                {
                    throw new Exception("Microsoft Excel did not create a PDF at " + tempPdfPath + ".");
                }

                OfficeFileHelper.DeleteExistingOutputFile(normalizedOutputPath);
                File.Move(tempPdfPath, normalizedOutputPath);
                tempPdfMoved = true;
            }
            catch (Exception ex)
            {
                throw WrapOfficeExportException("Microsoft Excel", ex);
            }
            finally
            {
                CloseDynamicComObject(workbook, false);
                QuitDynamicComObject(excelApp, "Quit");
                ProcessCleanup.TerminateNewProcesses("EXCEL", processIdsBefore, _trace);

                if (deleteTempSource)
                {
                    TryDeleteFile(workingSourcePath);
                }

                if (!tempPdfMoved)
                {
                    TryDeleteFile(tempPdfPath);
                }
            }
        }

        private void ConvertPowerPointPresentation(string sourcePath, string outputPdfPath)
        {
            int[] processIdsBefore = ProcessCleanup.CaptureProcessIds("POWERPNT");
            object? pptApp = null;
            object? presentation = null;
            string workingSourcePath = OfficeFileHelper.PrepareWritableSourceCopy(sourcePath, "adsk_office_pptx_");
            string normalizedOutputPath = Path.GetFullPath(outputPdfPath);
            string tempPdfPath = Path.Combine(
                Path.GetTempPath(),
                "adsk_office_pdf_" + Guid.NewGuid().ToString("N") + ".pdf");
            bool deleteTempSource = OfficeFileHelper.IsDifferentPath(workingSourcePath, sourcePath);
            bool tempPdfMoved = false;

            try
            {
                _trace.WriteLine("PowerPoint source path: " + workingSourcePath);
                _trace.WriteLine("PowerPoint temp PDF path: " + tempPdfPath);
                _trace.WriteLine("PowerPoint final PDF path: " + normalizedOutputPath);

                pptApp = CreateComApplication("PowerPoint.Application", "Microsoft PowerPoint");
                ConfigurePowerPointApplication(pptApp);
                SetComProperty(pptApp, "DisplayAlerts", OfficeComConstants.PpAlertsNone);

                object presentations = InvokeComGetProperty(pptApp, "Presentations");
                presentation = InvokeComMethod(
                    presentations,
                    "Open",
                    Path.GetFullPath(workingSourcePath),
                    OfficeComConstants.MsoTrue,
                    OfficeComConstants.MsoFalse,
                    OfficeComConstants.MsoFalse);

                ExportPowerPointPresentationAsPdf(presentation, tempPdfPath);

                if (!File.Exists(tempPdfPath) || new FileInfo(tempPdfPath).Length <= 0)
                {
                    throw new Exception("Microsoft PowerPoint did not create a PDF at " + tempPdfPath + ".");
                }

                OfficeFileHelper.DeleteExistingOutputFile(normalizedOutputPath);
                File.Move(tempPdfPath, normalizedOutputPath);
                tempPdfMoved = true;
            }
            catch (Exception ex)
            {
                throw WrapOfficeExportException("Microsoft PowerPoint", ex);
            }
            finally
            {
                CloseDynamicComObject(presentation);
                QuitDynamicComObject(pptApp, "Quit");
                ProcessCleanup.TerminateNewProcesses("POWERPNT", processIdsBefore, _trace);

                if (deleteTempSource)
                {
                    TryDeleteFile(workingSourcePath);
                }

                if (!tempPdfMoved)
                {
                    TryDeleteFile(tempPdfPath);
                }
            }
        }

        private void ValidateWordAvailability()
        {
            dynamic? wordApp = null;
            try
            {
                wordApp = CreateComApplication("Word.Application", "Microsoft Word");
                _trace.WriteLine("Microsoft Word validated successfully.");
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Microsoft Word is required but could not be started. Install Microsoft Office desktop on the Job Processor machine. Details: " +
                    ex.Message,
                    ex);
            }
            finally
            {
                QuitDynamicComObject(wordApp, "Quit", false);
            }
        }

        private void ValidateExcelAvailability()
        {
            dynamic? excelApp = null;
            try
            {
                excelApp = CreateComApplication("Excel.Application", "Microsoft Excel");
                _trace.WriteLine("Microsoft Excel validated successfully.");
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Microsoft Excel is required but could not be started. Install Microsoft Office desktop on the Job Processor machine. Details: " +
                    ex.Message,
                    ex);
            }
            finally
            {
                QuitDynamicComObject(excelApp, "Quit");
            }
        }

        private void ValidatePowerPointAvailability()
        {
            dynamic? pptApp = null;
            try
            {
                pptApp = CreateComApplication("PowerPoint.Application", "Microsoft PowerPoint");
                _trace.WriteLine("Microsoft PowerPoint validated successfully.");
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Microsoft PowerPoint is required but could not be started. Install Microsoft Office desktop on the Job Processor machine. Details: " +
                    ex.Message,
                    ex);
            }
            finally
            {
                QuitDynamicComObject(pptApp, "Quit");
            }
        }

        private void ConfigurePowerPointApplication(object pptApp)
        {
            // PowerPoint rejects Visible=msoFalse ("Hiding the application window is not allowed").
            SetComProperty(pptApp, "Visible", OfficeComConstants.MsoTrue);

            if (IsTrue(_settings.OfficeVisible))
            {
                return;
            }

            try
            {
                SetComProperty(pptApp, "WindowState", OfficeComConstants.PpWindowMinimized);
            }
            catch (Exception)
            {
            }
        }

        private static void ExportPowerPointPresentationAsPdf(object presentation, string outputPdfPath)
        {
            string fullPath = Path.GetFullPath(outputPdfPath);

            // SaveAs is the most reliable late-bound PowerPoint PDF export (fewer COM parameters).
            try
            {
                InvokeComMethod(
                    presentation,
                    "SaveAs",
                    fullPath,
                    OfficeComConstants.PpSaveAsPdf,
                    OfficeComConstants.MsoFalse);
                return;
            }
            catch (Exception saveAsEx)
            {
                try
                {
                    InvokeComMethod(
                        presentation,
                        "ExportAsFixedFormat",
                        fullPath,
                        OfficeComConstants.PpFixedFormatTypePdf,
                        OfficeComConstants.PpFixedFormatIntentPrint,
                        OfficeComConstants.MsoFalse,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing,
                        Type.Missing);
                }
                catch (Exception exportEx)
                {
                    throw new Exception(
                        "SaveAs PDF failed: " + saveAsEx.Message +
                        "; ExportAsFixedFormat failed: " + exportEx.Message,
                        exportEx);
                }
            }
        }

        private static object InvokeComMethod(object comObject, string methodName, params object[] args)
        {
            return comObject.GetType().InvokeMember(
                methodName,
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                comObject,
                args);
        }

        private static object InvokeComGetProperty(object comObject, string propertyName)
        {
            return comObject.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                comObject,
                null)!;
        }

        private static void SetComProperty(object comObject, string propertyName, object value)
        {
            comObject.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.SetProperty,
                null,
                comObject,
                new object[] { value });
        }

        private static dynamic CreateComApplication(string progId, string displayName)
        {
            Type? appType = Type.GetTypeFromProgID(progId);
            if (appType == null)
            {
                throw new Exception(displayName + " COM registration was not found for '" + progId + "'.");
            }

            object? app = Activator.CreateInstance(appType);
            if (app == null)
            {
                throw new Exception("Failed to start " + displayName + ".");
            }

            return app;
        }

        private int GetWordOptimizeFor()
        {
            if (string.Equals(_settings.PdfExportQuality, "Minimum", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeComConstants.WdExportOptimizeForOnScreen;
            }

            return OfficeComConstants.WdExportOptimizeForPrint;
        }

        private int GetExcelQuality()
        {
            if (string.Equals(_settings.PdfExportQuality, "Minimum", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeComConstants.XlQualityMinimum;
            }

            return OfficeComConstants.XlQualityStandard;
        }

        private static void ExportExcelWorkbookAsPdf(object workbook, string outputPdfPath, int quality)
        {
            // Late-bound dynamic calls can misalign Excel's optional COM parameters and trigger error 1004.
            workbook.GetType().InvokeMember(
                "ExportAsFixedFormat",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                workbook,
                new object[]
                {
                    OfficeComConstants.XlTypePdf,
                    outputPdfPath,
                    quality,
                    true,
                    true,
                    Type.Missing,
                    Type.Missing,
                    false,
                    Type.Missing
                });
        }

        private static Exception WrapOfficeExportException(string applicationName, Exception ex)
        {
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return new Exception(
                    applicationName + " could not convert a password-protected file. Remove encryption before running this job. Details: " +
                    ex.Message,
                    ex);
            }

            return new Exception(applicationName + " export failed: " + ex.Message, ex);
        }

        private static void CloseDynamicComObject(object? comObject, params object[]? closeArgs)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                comObject.GetType().InvokeMember(
                    "Close",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    comObject,
                    closeArgs ?? Array.Empty<object>());
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(comObject);
            }
        }

        private static void QuitDynamicComObject(object? comObject, string quitMethodName, params object[]? quitArgs)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                comObject.GetType().InvokeMember(
                    quitMethodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    comObject,
                    quitArgs ?? Array.Empty<object>());
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(comObject);
            }
        }

        private static void ReleaseComObject(object comObject)
        {
            try
            {
                if (Marshal.IsComObject(comObject))
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool IsTrue(string? value)
        {
            return string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
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
