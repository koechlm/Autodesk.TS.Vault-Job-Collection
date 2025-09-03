using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

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

using NavisworksAutomation = Autodesk.Navisworks.Api.Automation;
using Autodesk.Navisworks.Api.Automation;
using Autodesk.Navisworks.Api;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
[assembly: ExtensionId("8a697468-fed8-4719-a575-71225085efaf")]


namespace adsk.ts.nwd.create.navisworks
{
    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "adsk.ts.export3d.create.inventor";
        private static Settings mSettings = Settings.Load();
        private static string mLogDir = JobExtension.mSettings.LogFileLocation;
        private static string mLogFile;
        private TextWriterTraceListener mTrace;
        private Connection connection;
        private WebServiceManager mWsMgr;
        ACW.File mFile;
        private NavisworksAutomation.NavisworksApplication mNavisworksAutomation;

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

                //start the export task
                mCreateNavisworksExport(context, job);

                mTrace.IndentLevel = 0;
                mTrace.WriteLine("... successfully ending Job.");
                mTrace.Flush();
                mTrace.Close();

                return JobOutcome.Success;
            }
            catch (Exception ex)
            {
                context.Log(ex, "Job " + JOB_TYPE + " failed: " + ex.ToString() + " ");
                return JobOutcome.Failure;
            }

        }

        private void mCreateNavisworksExport(IJobProcessorServices context, IJob job)
        {
            List<string> mExpFrmts = new List<string>();
            List<string> mValidExpFrmts = new List<string> { "NWD", "NWD+DWF" };
            List<string> mFilesToUpload = new List<string>();

            // read target export formats from settings file
            Settings settings = Settings.Load();

            // the job must not run, if the source file or target export formats are not supported
            #region validate execution rules

            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Translator Job validates execution rules...");

            // apply execution filters, e.g., exclude files of classification "DesignDocumentation" etc.            
            List<string> mFileClassific = new List<string> { "ConfigurationFactory", "DesignDocumentation" };
            if (mFileClassific.Any(n => mFile.FileClass.ToString().Contains(n)))
            {
                mTrace.WriteLine("Translator job exits: file classification 'ConfigurationFactory' or 'DesignSubstitute' are not supported.");
                return;
            }

            // you may add addtional execution filters, e.g., category name == "Sheet Metal Part"

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

            // validate Navisworks instance for NWD format
            mNavisworksAutomation = mGetNavisworksAutom();
            if (mNavisworksAutomation == null)
            {
                mTrace.WriteLine("Translator job required Navisworks but failed to establish an application instance of Navisworks Manage; exit job with failure.");
                throw new Exception("Translator job's single task creating a Navisworks file failed: could not find or start Navisworks application.");
            }

            //download the source file(s) including its references
            #region download source file(s)
            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Job downloads source file(s) for translation.");

            // use shared code to download the file
            adsktsshared.JobCommon tsJobCommon = new(connection, mWsMgr, mTrace);
            string mDocPath = tsJobCommon.mDownloadFile(mFile);
            string mExt = System.IO.Path.GetExtension(mDocPath);

            mTrace.WriteLine("Job successfully downloaded source file(s) for translation.");
            #endregion download source file(s)

            // export the file into the requested format(s)
            foreach (string item in mExpFrmts)
            {
                if ((item == "NWD" || item == "NWD+DWF") && mNavisworksAutomation != null)
                {
                    //delete existing export files; note the resulting file name is e.g. "Assembly.iam.nwd
                    string mNwdName = mDocPath + ".nwd";
                    if (System.IO.File.Exists(mNwdName))
                    {
                        System.IO.FileInfo fileInfo = new FileInfo(mNwdName);
                        fileInfo.IsReadOnly = false;
                        fileInfo.Delete();
                    }
                    string mNWDWFName = mDocPath + ".dwf";
                    if (item == "NWD+DWF")
                    {
                        if (System.IO.File.Exists(mNWDWFName))
                        {
                            System.IO.FileInfo dwfInfo = new FileInfo(mNWDWFName);
                            dwfInfo.IsReadOnly = false;
                            dwfInfo.Delete();
                        }
                    }

                    mTrace.IndentLevel += 1;
                    mTrace.WriteLine("NWD Export starts...");

                    try
                    {
                        //disable navisworks progress bar whilst we do this procedure
                        //Navisworks.DisableProgress();

                        // check if a NWD template is enabled.
                        if (string.IsNullOrEmpty(mSettings.NwdTemplate))
                        {
                            //open the file with navisworks; opening other file formats creates a new navisworks file appending the import file
                            mNavisworksAutomation.OpenFile(mDocPath);
                        }
                        else
                        {
                            // download the template file
                            ACW.File mNwdTemplateFile = mWsMgr.DocumentService.FindLatestFilesByPaths(new string[] { mSettings.NwdTemplate }).FirstOrDefault();
                            if (mNwdTemplateFile != null)
                            {
                                string mNwdTemplate = tsJobCommon.mDownloadFile(mNwdTemplateFile);

                                //open the template file with Navisworks
                                mNavisworksAutomation.OpenFile(mNwdTemplate);

                                //append the source file to the template file
                                mNavisworksAutomation.AppendFile(mDocPath);
                            }
                            else
                            {
                                throw new Exception("Job stopped execution as the file " + settings.NwdTemplate + " was not found in the vault.");
                            }

                        }

                        //save the new navisworks
                        mNavisworksAutomation.SaveFile(mNwdName);

                        //export DWF
                        if (item == "NWD+DWF")
                        {
                            //zoom to fit extents before exporting
                            mNavisworksAutomation.ExecuteAddInPlugin("NativeViewerPluginAdaptor_LcViewFitPlugin_FitView.Navisworks", "");
                            //export the DWF file
                            mNavisworksAutomation.ExecuteAddInPlugin("NativeExportPluginAdaptor_LcDwfExporterPlugin_Export.Navisworks", @mNWDWFName);
                        }
                        //Navisworks.EnableProgress();

                        //collect all export files for later upload
                        System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mNwdName);
                        if (mExportFileInfo.Exists)
                        {
                            mFilesToUpload.Add(mNwdName);
                            mTrace.WriteLine("Navisworks created file: " + mFilesToUpload.LastOrDefault());
                            mTrace.IndentLevel -= 1;
                        }
                        else
                        {                            
                            throw new Exception("Validating the export file " + mNwdName + " before upload failed.");
                        }
                        if (item == "NWD+DWF")
                        {
                            System.IO.FileInfo mExportDwfFileInfo = new System.IO.FileInfo(mNWDWFName);
                            if (mExportDwfFileInfo.Exists)
                            {
                                mFilesToUpload.Add(mNWDWFName);
                                mTrace.WriteLine("Navisworks created file: " + mFilesToUpload.LastOrDefault());
                                mTrace.IndentLevel -= 1;
                            }
                            else
                            {
                                throw new Exception("Validating the export file " + mNWDWFName + " before upload failed.");
                            }
                        }

                        //check if an NWC file has been created; if so, upload it                        
                        System.IO.FileInfo mDocInfo = new System.IO.FileInfo(mDocPath);
                        string mNWC = mDocPath.Replace(mDocInfo.Extension, ".nwc");
                        System.IO.FileInfo mNwcInfo = new System.IO.FileInfo(mNWC);
                        if (mDocInfo.Exists)
                        {
                            //align the file name according our convention
                            string mNwcUpload = mDocPath + ".nwc";
                            if (System.IO.File.Exists(mNwcUpload))
                            {
                                System.IO.FileInfo fileInfo = new FileInfo(mNwcUpload);
                                fileInfo.IsReadOnly = false;
                                fileInfo.Delete();
                            }
                            System.IO.File.Move(mNWC, mNwcUpload);
                            mNwcInfo = new System.IO.FileInfo(mNwcUpload);
                            if (mNwcInfo.Exists)
                            {
                                mFilesToUpload.Add(mNwcUpload);
                            }
                        }
                    }
                    catch (Autodesk.Navisworks.Api.Automation.AutomationException e)
                    {
                        //return an error message to the job queue
                        throw new Exception("Navisworks Automation Error: " + e.Message);
                    }
                    catch (Autodesk.Navisworks.Api.Automation.AutomationDocumentFileException e)
                    {
                        //return an error message to the job queue
                        throw new Exception("Navisworks File Exception Error: " + e.Message);
                    }
                    finally
                    {
                        mNavisworksDispose();
                    }

                }
            }
        }

        private NavisworksApplication mGetNavisworksAutom()
        {
            try
            {
                //create NavisworksApplication automation object
                mNavisworksAutomation = new NavisworksApplication();
            }
            catch (Exception ex)
            {
                throw new Exception("Job could not start Naviswork Manage with exception: ", ex);
            }

            return mNavisworksAutomation;
        }

        private void mNavisworksDispose()
        {
            if (mNavisworksAutomation != null)
            {
                mNavisworksAutomation.Dispose();
                mNavisworksAutomation = null;
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
