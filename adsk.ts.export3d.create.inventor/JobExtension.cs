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

using Inventor;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
[assembly: ExtensionId("952d1405-bbd5-452c-9d85-e64cab7bb48e")]


namespace adsk.ts.export3d.create.inventor
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
                mCreateInventor3dExport(context, job);

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
                // close the log file
                if (mTrace != null)
                {
                    mTrace.Flush();
                    mTrace.Close();
                }
            }
        }

        private void mCreateInventor3dExport(IJobProcessorServices context, IJob job)
        {

            List<string> mExpFrmts = new List<string>();
            List<string> mValidExpFrmts = new List<string> { "3DDWG", "CATPart", "glTF", "IGES", "JT", "OBJ", "X_B", "X_T", "ProE_G", "ProE_N", "QIF", "SAT", "SMT", "STEP", "STL", "USDz" };
            List<string> mFilesToUpload = new List<string>();

            // read target export formats from settings file
            Settings settings = Settings.Load();

            // the job must not run, if the source file or target export formats are not supported
            #region validate execution rules

            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Translator Job validates execution rules...");

            // only run the job for 3D source file types, supported by exports (as of today)
            List<string> mFileExtensions = new List<string> { ".ipt", ".iam" }; //ipn is not supported by InventorServer

            if (!mFileExtensions.Any(n => mFile.Name.ToLower().EndsWith(n)))
            {
                mTrace.WriteLine("Translator job exits: file extension is not supported.");
                return;
            }

            // apply execution filters, e.g., exclude files of classification "DesignDocumentation" etc.            
            List<string> mFileClassific = new List<string> { "ConfigurationFactory", "DesignDocumentation" };
            //add "DesignSubstitute" if enabled in the options
            if (settings.ExcludeDesignSubstitute.ToLower() == "false") mFileClassific.Add("DesignSubstitute");
            if (mFileClassific.Any(n => mFile.FileClass.ToString().Contains(n)))
            {
                mTrace.WriteLine("Translator job exits: file classification 'ConfigurationFactory' or 'DesignSubstitute' are not supported.");
                return;
            }

            // you may add addtional execution filters, e.g., category name == "Sheet Metal Part"

            //only run the job for implemented/available combinations of source file and export file formats
            List<string> mIptExpFrmts = new List<string> { "3DDWG", "CATPart", "glTF", "IGES", "JT", "OBJ", "X_B", "X_T", "ProE_G", "ProE_N", "QIF", "SAT", "SMT", "STEP", "STL", "USDz" };
            List<string> mIamExpFrmts = new List<string> { "3DDWG", "CATProduct", "glTF", "IGES", "JT", "OBJ", "X_B", "X_T", "ProE_G", "ProE_N", "SAT", "SMT", "STEP", "STL", "USDz" };

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

            //filter export formats according source file type
            if (mFile.Name.ToLower().EndsWith(".ipt"))
            {
                // remove assembly only formats
                mValidExpFrmts.Remove("CATProduct");
                mValidExpFrmts.Remove("QIF");

                if (!mExpFrmts.Any(n => mValidExpFrmts.Contains(n)))
                {
                    mTrace.WriteLine("Translator job exits: no export file format matches the source file type IPT.");
                    return;
                }
            }
            if (mFile.Name.ToLower().EndsWith(".iam"))
            {
                // remove part only formats
                mValidExpFrmts.Remove("CATPart");
                if (!mExpFrmts.Any(n => mIamExpFrmts.Contains(n)))
                {
                    mTrace.WriteLine("Translator job exits: no export file format matches the source file type IAM.");
                    return;
                }
            }

            //validate that at least one export format is in the list
            if (mExpFrmts.Count < 1)
            {
                mTrace.WriteLine("Translator job exits: no matching source file type/export type found.");
                return;
            }

            mTrace.WriteLine("Job execution rules validated.");

            #endregion validate execution rules

            // InventorServer must have a project file activated; we enforce using the Vault stored IPJ
            #region VaultInventorServer IPJ activation

            //establish InventorServer environment including translator addins; differentiate her in case full Inventor.exe is used
            Inventor.InventorServer mInv = context.InventorObject as InventorServer;
            ApplicationAddIns mInvSrvAddIns = mInv.ApplicationAddIns;

            //override InventorServer default project settings by your Vault specific ones
            Inventor.DesignProjectManager projectManager;
            Inventor.DesignProject mSaveProject = null, mProject = null;

            String mIpjLocalPath = "";

            //download and activate the Inventor Project file in VaultInventorServer
            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Job tries activating Inventor project file as enforced in Vault behavior configurations.");

            adsktsshared.InventorJob mJobInventor = new(connection, mWsMgr);
            bool settingsAcceptLocalIpj = false;
            if (settings.AcceptLocalIpj.ToLower() == "true") settingsAcceptLocalIpj = true;
            mIpjLocalPath = mJobInventor.mGetIpj(settingsAcceptLocalIpj);

            //activate the given project file for this job only
            projectManager = mInv.DesignProjectManager;
            //VaultInventorServer might fail with unhandled exeption on fresh installed machines, if no IPJ had been used before
            try
            {
                if (projectManager.ActiveDesignProject != null && projectManager.ActiveDesignProject.FullFileName != mIpjLocalPath)
                {
                    mSaveProject = projectManager.ActiveDesignProject;
                }
            }
            catch (Exception)
            { }
            mProject = projectManager.DesignProjects.AddExisting(mIpjLocalPath);
            mProject.Activate();

            //[Optionally:] get Inventor Design Data settings and download all related files ---------

            mTrace.WriteLine("Job successfully activated Inventor IPJ.");

            #endregion VaultInventorServer IPJ activation

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

            // export the file into the requested formats
            #region VaultInventorServer CAD Export

            mTrace.WriteLine("Job starts task for each export format listed.");
            //use Inventor to open document
            Inventor.Document mDoc = mInv.Documents.Open(mDocPath);

            if (mDoc == null)
            {
                mJobInventor.mResetIpj(mSaveProject);
                throw new Exception("Job could not open the source file " + mDocPath + " in Inventor.");
            }

            //use the matching export addin and export options
            foreach (string item in mExpFrmts)
            {
                if (item == "3DDWG")
                {
                    try
                    {
                        //activate DXF/DWG translator
                        TranslatorAddIn mDwgTrans = mInvSrvAddIns.ItemById["{C24E3AC2-122E-11D5-8E91-0010B541CD80}"] as TranslatorAddIn;
                        mDwgTrans.Activate();
                        if (mDwgTrans == null)
                        {
                            mTrace.WriteLine("Dwg Translator not found.");
                            break;
                        }

                        //delete existing export file; note the resulting file name is e.g. "Drawing.idw.dwg
                        string mExpFileName = mDocPath + ".dwg";
                        if (System.IO.File.Exists(mExpFileName))
                        {
                            System.IO.FileInfo fileInfo = new FileInfo(mExpFileName);
                            fileInfo.IsReadOnly = false;
                            fileInfo.Delete();
                        }

                        mTrace.IndentLevel += 1;
                        mTrace.WriteLine("DWG Export starts...");

                        //create the TranslationContext
                        Inventor.TranslationContext mTranslationContext = mInv.TransientObjects.CreateTranslationContext();
                        mTranslationContext.Type = IOMechanismEnum.kFileBrowseIOMechanism;

                        //create NameValueMap object
                        NameValueMap mOptions = mInv.TransientObjects.CreateNameValueMap();

                        //Create a DataMedium object
                        DataMedium mDataMedium = mInv.TransientObjects.CreateDataMedium();

                        //Check whether the translator has 'SaveCopyAs' options and add the export configuration *.ini file
                        if (mDwgTrans.HasSaveCopyAsOptions[mDoc, mTranslationContext, mOptions] == true)
                        {
                            mOptions.Value["DwgVersion"] = 32;
                            mOptions.Value["Solid"] = true;
                            mOptions.Value["Surface"] = false;
                            mOptions.Value["Sketch"] = false;
                        }

                        //create the export file
                        mDataMedium.FileName = mExpFileName;
                        try
                        {
                            mDwgTrans.SaveCopyAs(mDoc, mTranslationContext, mOptions, mDataMedium);
                        }
                        catch (Exception ex)
                        {
                            mJobInventor.mResetIpj(mSaveProject);
                            throw new Exception("DWG Translator Add-In failed to export DWG file (TransAddIn.SaveCopyAs()): " + ex.Message);
                        }
                        //collect all export files for later upload                        
                        System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mFilesToUpload.LastOrDefault());
                        if (mExportFileInfo.Exists)
                        {
                            mFilesToUpload.Add(mExpFileName);
                            mTrace.WriteLine("DWG Translator created file: " + mFilesToUpload.LastOrDefault());
                            mTrace.IndentLevel -= 1;
                        }
                        else
                        {
                            mJobInventor.mResetIpj(mSaveProject);
                            throw new Exception("Validating the export file " + mExpFileName + " before upload failed.");
                        }

                    }
                    catch (Exception ex)
                    {
                        mJobInventor.mResetIpj(mSaveProject);
                        throw new Exception("Failed to activate DWG Translator Add-in or prepairing the export options: " + ex.Message);
                    }

                    mDoc.Close(true);
                    mTrace.WriteLine("Source file closed");
                }

                if (item == "STEP")
                {
                    //use Inventor to open document
                    mDoc = mInv.Documents.Open(mDocPath);
                    //activate STEP Translator environment,
                    try
                    {
                        TranslatorAddIn mStepTrans = mInvSrvAddIns.ItemById["{90AF7F40-0C01-11D5-8E83-0010B541CD80}"] as TranslatorAddIn;
                        if (mStepTrans == null)
                        {
                            //switch temporarily used project file back to original one
                            mJobInventor.mResetIpj(mSaveProject);
                            throw new Exception("Job stopped execution, because indicated translator addin is not available.");
                        }
                        TranslationContext mTransContext = mInv.TransientObjects.CreateTranslationContext();
                        NameValueMap mTransOptions = mInv.TransientObjects.CreateNameValueMap();
                        if (mStepTrans.HasSaveCopyAsOptions[mDoc, mTransContext, mTransOptions] == true)
                        {
                            //open, and translate the source file
                            mTrace.IndentLevel += 1;
                            mTrace.WriteLine("Job opens source file.");
                            mTransOptions.Value["ApplicationProtocolType"] = 3; //AP 2014, Automotive Design
                            mTransOptions.Value["Description"] = "Sample-Job Step Translator using VaultInventorServer";
                            mTransContext.Type = IOMechanismEnum.kFileBrowseIOMechanism;
                            //delete local file if exists, as the export wouldn't overwrite
                            string mExpFileName = mDocPath + ".stp";
                            if (System.IO.File.Exists(mExpFileName))
                            {
                                System.IO.File.SetAttributes(mExpFileName, System.IO.FileAttributes.Normal);
                                System.IO.File.Delete(mExpFileName);
                            }
                            ;
                            DataMedium mData = mInv.TransientObjects.CreateDataMedium();
                            mData.FileName = mExpFileName;
                            mStepTrans.SaveCopyAs(mDoc, mTransContext, mTransOptions, mData);
                            //collect all export files for later upload
                            mFilesToUpload.Add(mExpFileName);
                            System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mFilesToUpload.LastOrDefault());
                            if (mExportFileInfo.Exists)
                            {
                                mTrace.WriteLine("STEP Translator created file: " + mFilesToUpload.LastOrDefault());
                                mTrace.IndentLevel -= 1;
                            }
                            else
                            {
                                mJobInventor.mResetIpj(mSaveProject);
                                throw new Exception("Validating the export file " + mExpFileName + " before upload failed.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        mJobInventor.mResetIpj(mSaveProject);
                        mTrace.WriteLine("STEP Export Failed: " + ex.Message);
                    }

                    mDoc.Close(true);
                    mTrace.WriteLine("Source file closed");
                }

                if (item == "JT")
                {
                    //use Inventor to open document
                    mDoc = mInv.Documents.Open(mDocPath);
                    //activate JT Translator environment,
                    try
                    {
                        TranslatorAddIn mJtTrans = mInvSrvAddIns.ItemById["{16625A0E-F58C-4488-A969-E7EC4F99CACD}"] as TranslatorAddIn;
                        if (mJtTrans == null)
                        {
                            //switch temporarily used project file back to original one
                            mTrace.WriteLine("JT Translator not found.");
                            break;
                        }
                        TranslationContext mTransContext = mInv.TransientObjects.CreateTranslationContext();
                        NameValueMap mTransOptions = mInv.TransientObjects.CreateNameValueMap();
                        if (mJtTrans.HasSaveCopyAsOptions[mDoc, mTransContext, mTransOptions] == true)
                        {
                            //open, and translate the source file
                            mTrace.IndentLevel += 1;

                            mTransOptions.Value["Version"] = 102; //default
                            mTransContext.Type = IOMechanismEnum.kFileBrowseIOMechanism;
                            //delete local file if exists, as the export wouldn't overwrite
                            string mExpFileName = mDocPath + ".jt";
                            if (System.IO.File.Exists(mExpFileName))
                            {
                                System.IO.File.SetAttributes(mExpFileName, System.IO.FileAttributes.Normal);
                                System.IO.File.Delete(mExpFileName);
                            }
                            ;
                            DataMedium mData = mInv.TransientObjects.CreateDataMedium();
                            mData.FileName = mExpFileName;
                            mJtTrans.SaveCopyAs(mDoc, mTransContext, mTransOptions, mData);
                            //collect all export files for later upload
                            mFilesToUpload.Add(mExpFileName);
                            System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mFilesToUpload.LastOrDefault());
                            if (mExportFileInfo.Exists)
                            {
                                mTrace.WriteLine("JT Translator created file: " + mFilesToUpload.LastOrDefault());
                                mTrace.IndentLevel -= 1;
                            }
                            else
                            {
                                mJobInventor.mResetIpj(mSaveProject);
                                throw new Exception("Validating the export file " + mExpFileName + " before upload failed.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        mJobInventor.mResetIpj(mSaveProject);
                        mTrace.WriteLine("JT Export Failed: " + ex.Message);
                    }

                    mDoc.Close(true);
                    mTrace.WriteLine("Source file closed");
                }

            }

            //switch temporarily used project file back to original one
            mJobInventor.mResetIpj(mSaveProject);

            mTrace.WriteLine("Job exported file(s); continues uploading.");
            mTrace.IndentLevel -= 1;

            #endregion VaultInventorServer CAD Export


            // process the upload of the created files
            adsktsshared.JobCommon mJobCommon = new(connection, mWsMgr, mTrace);
            mJobCommon.mUploadFiles(mFile, mFilesToUpload, settings.OutPutPath);

            // finalize log output
            mTrace.IndentLevel = 1;
            mTrace.WriteLine("Job finished all steps.");

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
