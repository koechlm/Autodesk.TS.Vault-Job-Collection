using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
[assembly: ExtensionId("a0574014-3e9a-4276-8dd8-2d54777a68d6")]


namespace adsk.ts.image.create.inventor
{
    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "adsk.ts.image.create.inventor";
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
                    context.Log("Job " + JOB_TYPE + " did not start: " + "Job could not retrieve the file object for id " + mEntId.ToString(), MessageType.eError);
                    return JobOutcome.Failure;
                }

                //get the latest file version, if allowed to execute on tip version
                if (mFile.FileRev.MaxFileId != mEntId)
                {
                    if (mSettings.EnforceSubmittedFileVersion.ToLower() == "false")
                    {
                        mFile = mWsMgr.DocumentService.GetFileById(mFile.FileRev.MaxFileId);
                    }
                    else
                    {
                        context.Log("Job " + JOB_TYPE + " did not start: " + "Job execution is restricted to submitted file version; the submitted version (" + mEntId.ToString() + ") is no longer the tip(latest) version (" + mFile.FileRev.MaxFileId.ToString() + ")", MessageType.eError);
                        return JobOutcome.Failure;
                    }
                }

                // prepare log file and initiate logging
                mLogFile = JOB_TYPE + "_" + mFile.Name + ".log";
                FileInfo mLogFileInfo = new FileInfo(System.IO.Path.Combine(
                    mLogDir, mLogFile));
                if (mLogFileInfo.Exists) mLogFileInfo.Delete();
                mTrace = new TextWriterTraceListener(System.IO.Path.Combine(mLogDir, mLogFile), "mJobTrace");
                mTrace.WriteLine("Starting Job execution...");

                //start the export task
                mCreateInventorImageExport(context, job);

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

        private void mCreateInventorImageExport(IJobProcessorServices context, IJob job)
        {
            throw new NotImplementedException();
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
