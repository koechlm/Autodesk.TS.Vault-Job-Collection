using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Word = Microsoft.Office.Interop.Word;

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

        public void ValidateAvailability()
        {
            ValidateWordAvailability();
            ValidateExcelAvailability();
            ValidatePowerPointAvailability();
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
            Word.Application? wordApp = null;
            Word.Document? document = null;
            int[] processIdsBefore = ProcessCleanup.CaptureProcessIds("WINWORD");

            try
            {
                wordApp = new Word.Application
                {
                    Visible = IsTrue(_settings.OfficeVisible),
                    DisplayAlerts = Word.WdAlertLevel.wdAlertsNone,
                };

                document = wordApp.Documents.Open(
                    FileName: sourcePath,
                    ConfirmConversions: false,
                    ReadOnly: true,
                    AddToRecentFiles: false,
                    Visible: false);

                document.ExportAsFixedFormat(
                    OutputFileName: outputPdfPath,
                    ExportFormat: Word.WdExportFormat.wdExportFormatPDF,
                    OpenAfterExport: false,
                    OptimizeFor: GetWordOptimizeFor(),
                    Range: Word.WdExportRange.wdExportAllDocument,
                    Item: Word.WdExportItem.wdExportDocumentContent,
                    IncludeDocProps: true,
                    KeepIRM: true,
                    CreateBookmarks: Word.WdExportCreateBookmarks.wdExportCreateNoBookmarks,
                    DocStructureTags: true,
                    BitmapMissingFonts: true,
                    UseISO19005_1: false);
            }
            catch (Exception ex)
            {
                throw WrapOfficeExportException("Microsoft Word", ex);
            }
            finally
            {
                CloseWordDocument(document);
                QuitWordApplication(wordApp);
                ProcessCleanup.TerminateNewProcesses("WINWORD", processIdsBefore, _trace);
            }
        }

        private void ConvertExcelWorkbook(string sourcePath, string outputPdfPath)
        {
            Excel.Application? excelApp = null;
            Excel.Workbook? workbook = null;
            int[] processIdsBefore = ProcessCleanup.CaptureProcessIds("EXCEL");

            try
            {
                excelApp = new Excel.Application
                {
                    Visible = IsTrue(_settings.OfficeVisible),
                    DisplayAlerts = false,
                    ScreenUpdating = false,
                };

                workbook = excelApp.Workbooks.Open(
                    Filename: sourcePath,
                    UpdateLinks: 0,
                    ReadOnly: true,
                    AddToMru: false);

                workbook.ExportAsFixedFormat(
                    Type: Excel.XlFixedFormatType.xlTypePDF,
                    Filename: outputPdfPath,
                    Quality: GetExcelQuality(),
                    IncludeDocProperties: true,
                    IgnorePrintAreas: false,
                    OpenAfterPublish: false);
            }
            catch (Exception ex)
            {
                throw WrapOfficeExportException("Microsoft Excel", ex);
            }
            finally
            {
                CloseExcelWorkbook(workbook);
                QuitExcelApplication(excelApp);
                ProcessCleanup.TerminateNewProcesses("EXCEL", processIdsBefore, _trace);
            }
        }

        private void ConvertPowerPointPresentation(string sourcePath, string outputPdfPath)
        {
            PowerPoint.Application? pptApp = null;
            PowerPoint.Presentation? presentation = null;
            int[] processIdsBefore = ProcessCleanup.CaptureProcessIds("POWERPNT");

            try
            {
                pptApp = new PowerPoint.Application
                {
                    Visible = IsTrue(_settings.OfficeVisible)
                        ? Microsoft.Office.Core.MsoTriState.msoTrue
                        : Microsoft.Office.Core.MsoTriState.msoFalse,
                };

                presentation = pptApp.Presentations.Open(
                    FileName: sourcePath,
                    ReadOnly: Microsoft.Office.Core.MsoTriState.msoTrue,
                    Untitled: Microsoft.Office.Core.MsoTriState.msoFalse,
                    WithWindow: Microsoft.Office.Core.MsoTriState.msoFalse);

                presentation.ExportAsFixedFormat(
                    Path: outputPdfPath,
                    FixedFormatType: PowerPoint.PpFixedFormatType.ppFixedFormatTypePDF,
                    Intent: PowerPoint.PpFixedFormatIntent.ppFixedFormatIntentPrint,
                    FrameSlides: Microsoft.Office.Core.MsoTriState.msoFalse);
            }
            catch (Exception ex)
            {
                throw WrapOfficeExportException("Microsoft PowerPoint", ex);
            }
            finally
            {
                ClosePowerPointPresentation(presentation);
                QuitPowerPointApplication(pptApp);
                ProcessCleanup.TerminateNewProcesses("POWERPNT", processIdsBefore, _trace);
            }
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

        private void ValidateWordAvailability()
        {
            Word.Application? wordApp = null;
            try
            {
                wordApp = new Word.Application
                {
                    Visible = false,
                    DisplayAlerts = Word.WdAlertLevel.wdAlertsNone,
                };
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
                QuitWordApplication(wordApp);
            }
        }

        private void ValidateExcelAvailability()
        {
            Excel.Application? excelApp = null;
            try
            {
                excelApp = new Excel.Application
                {
                    Visible = false,
                    DisplayAlerts = false,
                };
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
                QuitExcelApplication(excelApp);
            }
        }

        private void ValidatePowerPointAvailability()
        {
            PowerPoint.Application? pptApp = null;
            try
            {
                pptApp = new PowerPoint.Application
                {
                    Visible = Microsoft.Office.Core.MsoTriState.msoFalse,
                };
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
                QuitPowerPointApplication(pptApp);
            }
        }

        private Word.WdExportOptimizeFor GetWordOptimizeFor()
        {
            if (string.Equals(_settings.PdfExportQuality, "Minimum", StringComparison.OrdinalIgnoreCase))
            {
                return Word.WdExportOptimizeFor.wdExportOptimizeForOnScreen;
            }

            return Word.WdExportOptimizeFor.wdExportOptimizeForPrint;
        }

        private Excel.XlFixedFormatQuality GetExcelQuality()
        {
            if (string.Equals(_settings.PdfExportQuality, "Minimum", StringComparison.OrdinalIgnoreCase))
            {
                return Excel.XlFixedFormatQuality.xlQualityMinimum;
            }

            return Excel.XlFixedFormatQuality.xlQualityStandard;
        }

        private static void CloseWordDocument(Word.Document? document)
        {
            if (document == null)
            {
                return;
            }

            try
            {
                document.Close(SaveChanges: false);
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(document);
            }
        }

        private static void QuitWordApplication(Word.Application? wordApp)
        {
            if (wordApp == null)
            {
                return;
            }

            try
            {
                wordApp.Quit(SaveChanges: false);
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(wordApp);
            }
        }

        private static void CloseExcelWorkbook(Excel.Workbook? workbook)
        {
            if (workbook == null)
            {
                return;
            }

            try
            {
                workbook.Close(SaveChanges: false);
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(workbook);
            }
        }

        private static void QuitExcelApplication(Excel.Application? excelApp)
        {
            if (excelApp == null)
            {
                return;
            }

            try
            {
                excelApp.Quit();
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(excelApp);
            }
        }

        private static void ClosePowerPointPresentation(PowerPoint.Presentation? presentation)
        {
            if (presentation == null)
            {
                return;
            }

            try
            {
                presentation.Close();
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(presentation);
            }
        }

        private static void QuitPowerPointApplication(PowerPoint.Application? pptApp)
        {
            if (pptApp == null)
            {
                return;
            }

            try
            {
                pptApp.Quit();
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseComObject(pptApp);
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
    }
}
