using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using Autodesk.Connectivity.WebServices;
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
[assembly: ApiVersion("20.0")]
[assembly: ExtensionId("e47f92ba-e2c2-4c6c-91c3-9482b732d738")]


namespace adsk.ts.assignupdateitem
{
    /// <summary>
    /// Job handler to assign/update Vault and Fusion Manage items
    /// </summary>
    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "adsk.ts.assignupdateitem";

        #region custom variables
        private static readonly List<string> mExcludedCategories = new List<string>()
        {
            "Reference",
            "Phantom",
            "Substitute"
        };

        private static readonly List<FileClassification> mExcludedFileCls = new()
        {
            FileClassification.DesignVisualization,
            FileClassification.DesignRepresentation,
            FileClassification.ConfigurationFactory
        };

        private static Settings mSettings = Settings.Load();
        private static string mLogDir = JobExtension.mSettings.LogFileLocation;
        private static string mLogFile;
        private TextWriterTraceListener mTrace;
        Connection connection = null;
        WebServiceManager mWsMgr = null;
        Autodesk.Connectivity.WebServices.File mFile = null;

        // Fusion Manage config name
        private const string mFMConfigName = "Adsk.Vault.ExternalSyncTask.FusionManage";

        // PromoteComponents/PromoteComponentLinks batch size for large assemblies
        private const int PromoteBatchSize = 15;


        #endregion custom variables

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

                //get the latest file version
                if (mFile.FileRev.MaxFileId != mEntId)
                {
                    mFile = mWsMgr.DocumentService.GetFileById(mFile.FileRev.MaxFileId);
                }

                // prepare log file and initiate logging
                mLogFile = JOB_TYPE + "_" + mFile.Name + ".log";
                FileInfo mLogFileInfo = new FileInfo(System.IO.Path.Combine(
                    mLogDir, mLogFile));
                if (mLogFileInfo.Exists) mLogFileInfo.Delete();
                mTrace = new TextWriterTraceListener(System.IO.Path.Combine(mLogDir, mLogFile), "mJobTrace");
                mTrace.WriteLine("Starting Job...");

                // assign or update FM item for this file
                bool success = mAssignUpdateItem(context, mFile);
                if (!success)
                {
                    mTrace.IndentLevel = 0;
                    mTrace.WriteLine("... ending Job with failure");

                    return JobOutcome.Failure;
                }
                else
                {
                    mTrace.IndentLevel = 0;
                    mTrace.WriteLine("... successfully ending Job.");

                    return JobOutcome.Success;
                }
            }

            catch (Exception ex)
            {
                context.Log("Job " + JOB_TYPE + " failed: " + mFormatExceptionForLog(ex) + " .", MessageType.eError);
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

        private bool mAssignUpdateItem(IJobProcessorServices context, Autodesk.Connectivity.WebServices.File file)
        {
            // exclude categories that must not get an item assigned and would fail
            if (mExcludedCategories.Contains(file.Cat.CatName))
            {
                return true;
            }

            // exclude file classifications
            if (mExcludedFileCls.Contains(file.FileClass))
            {
                return true;
            }

            // retrieve the primary referenced file for files of classification "Design Document"
            if (file.FileClass == FileClassification.DesignDocument)
            {
                WebServiceManager serviceManager = context.Connection.WebServiceManager;

                Autodesk.Connectivity.WebServices.File parent = null;

                DocumentService docService = serviceManager.DocumentService;
                // get the associated references
                List<TreeNode> children = new List<TreeNode>();
                FileAssocArray[] fileAssociations = serviceManager.DocumentService.GetLatestFileAssociationsByMasterIds(
                    new long[] { file.MasterId },
                    FileAssociationTypeEnum.None,
                    false,
                    FileAssociationTypeEnum.Dependency,
                    false,
                    false,
                    false,
                    false);

                if (fileAssociations.FirstOrDefault()?.FileAssocs != null)
                {
                    foreach (var fileAssociation in fileAssociations.First().FileAssocs)
                    {
                        parent = fileAssociation.CldFile;
                    }
                }

                if (parent != null)
                {
                    // use the parent file for item assignment
                    file = parent;
                }
                else
                {
                    // no valid parent found - exit
                    return true;
                }
            }

            // call promote file to assign or update item on FM
            return mPromoteFileToItem(context, file.Id);

        }

        private bool mPromoteFileToItem(IJobProcessorServices context, long mFileId)
        {
            WebServiceManager serviceManager = context.Connection.WebServiceManager;
            ItemService mItemSvc = serviceManager.ItemService;

            ItemsAndFiles promoteResult = null;
            DateTime? promoteTimestamp = null;
            bool mPromoteFailed = false;
            bool mItemsUndone = false;

            try
            {
                // Match UI behavior: respect server "Assign all" setting
                mWriteLog("Adding file id " + mFileId + " to promote (ItemAssignAll.Default, autoAssignDuplicates=true)");
                mItemSvc.AddFilesToPromote(new long[] { mFileId }, ItemAssignAll.Default, true);

                DateTime timestamp;
                GetPromoteOrderResults promoteOrderResults = mItemSvc.GetPromoteComponentOrder(out timestamp);
                promoteTimestamp = timestamp;

                int primaryCount = promoteOrderResults.PrimaryArray?.Length ?? 0;
                int nonPrimaryCount = promoteOrderResults.NonPrimaryArray?.Length ?? 0;
                mWriteLog("Promote order: " + primaryCount + " primary, " + nonPrimaryCount + " non-primary component(s)");

                if (primaryCount > 0)
                {
                    try
                    {
                        mPromoteComponentsInBatches(mItemSvc, timestamp, promoteOrderResults.PrimaryArray);
                    }
                    catch (Exception ex)
                    {
                        mPromoteFailed = true;
                        mLogPromoteError(context, "PromoteComponents", ex);
                    }
                }

                if (!mPromoteFailed && nonPrimaryCount > 0)
                {
                    try
                    {
                        mPromoteComponentLinksInBatches(mItemSvc, promoteOrderResults.NonPrimaryArray);
                    }
                    catch (Exception ex)
                    {
                        mPromoteFailed = true;
                        mLogPromoteError(context, "PromoteComponentLinks", ex);
                    }
                }

                if (!mPromoteFailed)
                {
                    promoteResult = mItemSvc.GetPromoteComponentsResults(timestamp);

                    // StatusArray: 1=unchanged, 2=new item, 4=updated item — only commit changed items
                    List<Item> itemsToCommit = new List<Item>();
                    for (int i = 0; i < promoteResult.ItemRevArray.Length; i++)
                    {
                        if (promoteResult.StatusArray[i] > 1 && promoteResult.ItemRevArray[i].Locked != true)
                        {
                            itemsToCommit.Add(promoteResult.ItemRevArray[i]);
                        }
                    }

                    mWriteLog("Promote results: " + promoteResult.ItemRevArray.Length + " item(s), "
                        + itemsToCommit.Count + " to commit");

                    if (itemsToCommit.Count > 0)
                    {
                        mItemSvc.UpdateAndCommitItems(itemsToCommit.ToArray());
                        mWriteLog("Committed " + itemsToCommit.Count + " item(s)");

                        if (mSettings.FMSync.ToLower() == "true")
                        {
                            var mExternalSyncService = serviceManager.ExternalSyncService;

                            if (mExternalSyncService != null)
                            {
                                foreach (Item mItem in itemsToCommit)
                                {
                                    long mRevId = serviceManager.ItemService.GetLatestItemByItemMasterId(mItem.MasterId).Id;
                                    NameValuePair[] taskParamArray = new NameValuePair[] { };
                                    string workflowType = "Adsk.UploadItem";
                                    string description = "Assign/Update Item for file " + mFile.Name;
                                    mExternalSyncService.AddExtSyncTask(mRevId, "ITEM", mFMConfigName, workflowType, description, taskParamArray);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mPromoteFailed = true;
                context.Log("Job failed likely due to missing Item Data; Check the property 'Item Assignable'. Details: "
                    + mFormatExceptionForLog(ex), MessageType.eError);
                mWriteLog("Promote failed: " + mFormatExceptionForLog(ex));
            }
            finally
            {
                mCleanupPromoteOnFailure(serviceManager, promoteTimestamp, promoteResult, mPromoteFailed, ref mItemsUndone);
            }

            return !mPromoteFailed;
        }

        private void mPromoteComponentsInBatches(ItemService itemService, DateTime timestamp, long[] componentIds)
        {
            int batchIndex = 0;
            foreach (long[] batch in mChunkArray(componentIds, PromoteBatchSize))
            {
                batchIndex++;
                mWriteLog("PromoteComponents batch " + batchIndex + ": " + batch.Length + " component(s)");
                itemService.PromoteComponents(timestamp, batch);
            }
        }

        private void mPromoteComponentLinksInBatches(ItemService itemService, long[] componentIds)
        {
            int batchIndex = 0;
            foreach (long[] batch in mChunkArray(componentIds, PromoteBatchSize))
            {
                batchIndex++;
                mWriteLog("PromoteComponentLinks batch " + batchIndex + ": " + batch.Length + " component(s)");
                itemService.PromoteComponentLinks(batch);
            }
        }

        private static IEnumerable<long[]> mChunkArray(long[] array, int chunkSize)
        {
            if (array == null || array.Length == 0)
                yield break;

            for (int i = 0; i < array.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, array.Length - i);
                long[] chunk = new long[length];
                Array.Copy(array, i, chunk, 0, length);
                yield return chunk;
            }
        }

        private void mCleanupPromoteOnFailure(WebServiceManager serviceManager, DateTime? timestamp,
            ItemsAndFiles promoteResult, bool promoteFailed, ref bool itemsUndone)
        {
            if (!promoteFailed || itemsUndone)
                return;

            try
            {
                ItemsAndFiles result = promoteResult;

                // Always attempt to retrieve partial promote state when cleanup is needed
                if (result == null && timestamp.HasValue)
                {
                    try
                    {
                        result = serviceManager.ItemService.GetPromoteComponentsResults(timestamp.Value);
                        mWriteLog("Retrieved promote results for cleanup: "
                            + (result?.ItemRevArray?.Length ?? 0) + " item(s)");
                    }
                    catch (Exception ex)
                    {
                        mWriteLog("Could not retrieve promote results for cleanup: " + mFormatExceptionForLog(ex));
                    }
                }

                if (result?.ItemRevArray != null && result.ItemRevArray.Length > 0)
                {
                    long[] masterIds = result.ItemRevArray.Select(i => i.MasterId).ToArray();
                    long[] itemIds = result.ItemRevArray.Select(i => i.Id).ToArray();
                    serviceManager.ItemService.DeleteUnusedItemNumbers(masterIds);
                    serviceManager.ItemService.UndoEditItems(itemIds);
                    itemsUndone = true;
                    mWriteLog("Cleaned up " + itemIds.Length + " locked item(s) after promote failure");
                }
            }
            catch (Exception ex)
            {
                mWriteLog("Cleanup after promote failure failed: " + mFormatExceptionForLog(ex));
            }
        }

        private void mWriteLog(string message)
        {
            mTrace?.WriteLine(message);
        }

        private void mLogPromoteError(IJobProcessorServices context, string operation, Exception ex)
        {
            string logMessage = operation + " failed: " + mFormatExceptionForLog(ex);
            context.Log("Job " + JOB_TYPE + " failed: " + logMessage + " .", MessageType.eError);
            mWriteLog(logMessage);
        }

        private static void GetErrorAndRestrictionDetails(Exception e,
            out string errorCode, out List<string> restrictionDetails)
        {
            VaultServiceErrorException vse = e as VaultServiceErrorException;
            errorCode = null;
            restrictionDetails = new List<string>();
            string[] restrictionErrors = new string[]
            { "1092", "1387", "1633" };

            if (vse != null)
            {
                try
                {
                    errorCode = vse.ErrorCode.ToString();

                    if (restrictionErrors.Contains(errorCode) && vse.Restrictions != null)
                    {
                        foreach (var restriction in vse.Restrictions)
                        {
                            string detail = restriction.Code.ToString();
                            if (restriction.EntityId > 0)
                                detail += " entityId=" + restriction.EntityId;
                            if (restriction.Parameters != null)
                            {
                                string[] parameters = restriction.Parameters.ToArray();
                                if (parameters.Length > 0)
                                    detail += " [" + string.Join(", ", parameters) + "]";
                            }
                            restrictionDetails.Add(detail);
                        }
                    }
                }
                catch
                { }
            }
        }

        /// <summary>
        /// Builds a log-friendly message for an exception, appending the Vault error code
        /// and any restriction codes/entities if the exception is a VaultServiceErrorException.
        /// </summary>
        private static string mFormatExceptionForLog(Exception ex)
        {
            GetErrorAndRestrictionDetails(ex, out string errorCode, out List<string> restrictionDetails);

            if (string.IsNullOrEmpty(errorCode))
                return ex.ToString();

            string message = ex.ToString() + " (Vault error code: " + errorCode + ")";

            if (restrictionDetails.Count > 0)
                message += " Restrictions: " + string.Join("; ", restrictionDetails);

            return message;
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
