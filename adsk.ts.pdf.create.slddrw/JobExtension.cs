using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using Autodesk.DataManagement.Client.Framework.Currency;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using Autodesk.DataManagement.Client.Framework.Vault.Settings;

using adsktsshared = adsk.ts.job.shared;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Diagnostics;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using static System.Net.WebRequestMethods;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
[assembly: ExtensionId("8be623d4-cc6e-4f68-ada6-e307ee73b80f")]


namespace adsk.ts.pdf.create.slddrw
{
    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "adsk.ts.pdf.create.slddrw";
        private static Settings mSettings = Settings.Load();
        private static string mLogDir = JobExtension.mSettings.LogFileLocation;
        private static string mLogFile;
        private TextWriterTraceListener mTrace;
        private Connection connection;
        private WebServiceManager mWsMgr;
        ACW.File mFile;
        private SldWorks sldWorks;

        #region IJobHandler Implementation
        public bool CanProcess(string jobType)
        {
            return jobType == JOB_TYPE;
        }

        public JobOutcome Execute(IJobProcessorServices context, IJob job)
        {
            try
            {
                //pick up this job's context
                connection = context.Connection;
                mWsMgr = connection.WebServiceManager;
                long mEntId = Convert.ToInt64(job.Params["EntityId"]);
                string mEntClsId = job.Params["EntityClassId"];

                // only run the job for files
                if (mEntClsId != "FILE")
                    return JobOutcome.Success;

                // get the file object for this job
                mFile = mWsMgr.DocumentService.GetFileById(mEntId);
                if (mFile == null)
                {
                    throw new Exception("The file version is no longer available!");
                }

                // prepare log file and initiate logging
                mLogFile = JOB_TYPE + "_" + mFile.Name + ".log";
                FileInfo mLogFileInfo = new FileInfo(System.IO.Path.Combine(
                    mLogDir, mLogFile));
                if (mLogFileInfo.Exists) mLogFileInfo.Delete();
                mTrace = new TextWriterTraceListener(System.IO.Path.Combine(mLogDir, mLogFile), "mJobTrace");
                mTrace.WriteLine("Starting Job...");

                //start step export
                mCreateSldDrwPdfExport(context, job);

                mTrace.IndentLevel = 0;
                mTrace.WriteLine("... successfully ending Job.");
                mTrace.Flush();
                mTrace.Close();

                return JobOutcome.Success;
            }
            catch (Exception ex)
            {
                context.Log(ex, "Job " + JOB_TYPE + " failed: " + ex.ToString() + " ");
                mTrace.IndentLevel = 0;
                mTrace.WriteLine("... ending Job with failure.");
                return JobOutcome.Failure;
            }
            finally
            {
                // clean up navisworks instance, if any
                if (sldWorks != null)
                {
                    mSldworksDispose();
                }

                // close the log file
                if (mTrace != null)
                {
                    mTrace.Flush();
                    mTrace.Close();
                }
            }

        }

        private void mCreateSldDrwPdfExport(IJobProcessorServices context, IJob job)
        {
            List<string> mExpFrmts = new List<string>();
            List<string> mValidExpFrmts = new List<string> { "SLDDRW.PDF" };
            List<string> mFilesToUpload = new List<string>();

            // read target export formats from settings file
            Settings settings = Settings.Load();

            // the job must not run, if the source file or target export formats are not supported
            #region validate execution rules

            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Translator Job validates execution rules...");

            // only run the job for Solidworks 2D drawing source file types, supported by exports (as of today)
            List<string> mFileExtensions = new List<string> { ".slddrw" };

            if (!mFileExtensions.Any(n => mFile.Name.ToLower().EndsWith(n)))
            {
                mTrace.WriteLine("Translator job exits: file extension is not supported.");
                return;
            }

            // read configured export format(s)
            if (settings.ExportFormats == null)
                throw new Exception("Settings expect to list at least one export format!");
            if (settings.ExportFormats.Contains(","))
            {
                mExpFrmts = settings.ExportFormats.Replace(" ", "").Split(',').ToList();
            }
            else
            {
                mExpFrmts.Add(settings.ExportFormats);
            }

            // validate that at least one export format is in the list and that it matches supported formats
            if (mExpFrmts.Count < 1 && mExpFrmts.Any(fmt => mValidExpFrmts.Contains(fmt)))
            {
                mTrace.WriteLine("Translator job exits: no matching source file type/export type found.");
                return;
            }
            #endregion validate execution rules

            #region validate SolidWorks availability
            //validate Solidworks instance for SLDDRW.PDF format
            if (mExpFrmts.Contains("SLDDRW.PDF"))
            {
                sldWorks = mGetSldworks();
                if (sldWorks == null)
                {
                    //the job might continue successful for other formats than SLDDRW.PDF; terminate only if SLDDRW.PDF is the only target format
                    if (mExpFrmts.Count == 1 && mExpFrmts.FirstOrDefault().Contains("SLDDRW.PDF"))
                    {
                        mTrace.WriteLine("Translator job required Solidworks but failed to establish an application instance of Solidworks; exit job with failure.");
                        throw new Exception("Translator job's single task creating a Solidworks PDF file failed: could not find or start Solidworks application.");
                    }
                    else
                    {
                        mTrace.WriteLine("Translator job required Solidworks, but failed to get an instance of the application; job continues to export other formats.");
                    }
                }
            }
            #endregion validate SolidWorks availability

            //download the source file(s) including its references
            #region download source file(s)
            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Job downloads source file(s) for translation.");

            // use shared code to download the file
            adsktsshared.JobCommon tsJobCommon = new adsktsshared.JobCommon(connection, mWsMgr, mTrace);
            string mDocPath = tsJobCommon.mDownloadFile(mFile);
            string mExt = System.IO.Path.GetExtension(mDocPath);

            mTrace.WriteLine("Job successfully downloaded source file(s) for translation.");
            #endregion download source file(s)

            // export the file into the requested format(s)
            foreach (string item in mExpFrmts)
            {
                if (item == "SLDDRW.PDF")
                {
                    string mPDFName = mDocPath + ".pdf";
                    if (System.IO.File.Exists(mPDFName))
                    {
                        System.IO.FileInfo fileInfo = new FileInfo(mPDFName);
                        fileInfo.IsReadOnly = false;
                        fileInfo.Delete();
                    }

                    mTrace.IndentLevel += 1;
                    mTrace.WriteLine("SLDDRW -> PDF Export starts...");

                    try
                    {
                        //open the file with Solidworks
                        sldWorks.OpenDoc6(mDocPath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", 0, 0);
                        //sldWorks.OpenDoc(mDocPath, (int)swDocumentTypes_e.swDocDRAWING);

                        //export the file as PDF
                        ModelDoc2 swModel = (ModelDoc2)sldWorks.ActiveDoc;
                        DrawingDoc swDraw = (DrawingDoc)swModel;

                        // Save as PDF                        
                        int errors = 0;
                        int warnings = 0;
                        bool status = swModel.Extension.SaveAs(mPDFName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                        if (status)
                        {
                            Console.WriteLine("File saved successfully as PDF.");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to save file as PDF. Errors: {errors}, Warnings: {warnings}");
                        }

                        //collect all export files for later upload
                        System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mPDFName);
                        if (mExportFileInfo.Exists)
                        {
                            mFilesToUpload.Add(mPDFName);
                            mTrace.WriteLine("Solidworks created file: " + mFilesToUpload.LastOrDefault());
                            mTrace.IndentLevel -= 1;
                        }
                        else
                        {
                            throw new Exception("Validating the export file " + mPDFName + " before upload failed.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Solidworks Export Failed: " + ex.Message);
                    }
                    finally
                    {
                        sldWorks.CloseAllDocuments(true);
                        //todo: check if other Solidworks jobs are in the queue and keep the application open
                        mSldworksDispose();
                    }

                }
            }

            // process the upload of the created files
            adsktsshared.JobCommon mJobCommon = new(connection, mWsMgr, mTrace);
            mJobCommon.mUploadFiles(mFile, mFilesToUpload, settings.OutPutPath);

            // finalize log output
            mTrace.IndentLevel = 1;
            mTrace.WriteLine("Job finished all steps.");

        }

        private SldWorks mGetSldworks()
        {
            sldWorks = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")) as SldWorks;
            if (sldWorks != null)
            {
                return sldWorks;
            }
            else
            {
                throw new Exception("Job could not start a SolidWorks instance.");
            }
        }

        private void mSldworksDispose()
        {
            if (sldWorks != null)
            {
                sldWorks.ExitApp();
                sldWorks = null;
            }
        }

        public void OnJobProcessorShutdown(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }

        public void OnJobProcessorSleep(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }

        public void OnJobProcessorStartup(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }

        public void OnJobProcessorWake(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }
        #endregion IJobHandler Implementation
    }
}
