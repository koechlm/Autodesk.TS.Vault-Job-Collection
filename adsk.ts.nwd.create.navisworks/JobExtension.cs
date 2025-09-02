using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using Autodesk.Connectivity.WebServices;
using System.Diagnostics;
using NavisworksAutomation = Autodesk.Navisworks.Api.Automation;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
[assembly: ExtensionId("8a697468-fed8-4719-a575-71225085efaf")]


namespace adsk.ts.nwd.create.navisworks
{
    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "adsk.ts.nwd.create.navisworks";
        private static Settings mSettings = Settings.Load();
        private static string mLogDir = JobExtension.mSettings.LogFileLocation;
        private static string mLogFile = JOB_TYPE + ".log";
        private TextWriterTraceListener mTrace = new TextWriterTraceListener(System.IO.Path.Combine(
            mLogDir, mLogFile), "mJobTrace");
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
                MessageBox.Show(JOB_TYPE, JOB_TYPE + "-Messenger", MessageBoxButtons.OK);
                return JobOutcome.Success;
            }
            catch (Exception ex)
            {
                context.Log(ex, "Job " + JOB_TYPE + " failed: " + ex.ToString() + " ");
                return JobOutcome.Failure;
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
