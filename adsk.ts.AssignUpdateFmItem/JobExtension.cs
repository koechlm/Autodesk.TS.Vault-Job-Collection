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
                context.Log("Job " + JOB_TYPE + " failed: " + ex.ToString() + " .", MessageType.eError);
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
            using (WebServiceManager serviceManager = context.Connection.WebServiceManager)
            {
                ItemService mItemSvc = serviceManager.ItemService;

                ItemsAndFiles promoteResult = null;
                Item[] updatedItems = null;
                bool mPromoteFailed = false;
                try
                {
                    // in this case - we enforce to create/update an item by checkin; with that we must not cause the item creation "twice" in case an assembly's subcomponent also requires an item creation
                    // with that we have to set ItemAssignAll = No
                    mItemSvc.AddFilesToPromote(new long[] { mFileId }, ItemAssignAll.No, true);
                    DateTime timestamp;
                    GetPromoteOrderResults promoteOrderResults = mItemSvc.GetPromoteComponentOrder(out timestamp);
                    if (promoteOrderResults.PrimaryArray != null && promoteOrderResults.PrimaryArray.Any())
                        try
                        {
                            mItemSvc.PromoteComponents(timestamp, promoteOrderResults.PrimaryArray);
                        }
                        catch (Exception ex)
                        {
                            mPromoteFailed = true;
                            context.Log("Job " + JOB_TYPE + " failed: " + ex.ToString() + " .", MessageType.eError);
                        }
                    if (promoteOrderResults.NonPrimaryArray != null && promoteOrderResults.NonPrimaryArray.Any())
                        try
                        {
                            mItemSvc.PromoteComponentLinks(promoteOrderResults.NonPrimaryArray);
                        }
                        catch (Exception ex)
                        {
                            mPromoteFailed = true;
                            context.Log("Job " + JOB_TYPE + " failed: " + ex.ToString() + " .", MessageType.eError);
                        }
                    try
                    {
                        if (mPromoteFailed != true)
                        {
                            promoteResult = mItemSvc.GetPromoteComponentsResults(timestamp);
                            //check the result for locked root item as we continue to update this
                            if (promoteResult.ItemRevArray[0].Locked != true)
                            {
                                updatedItems = promoteResult.ItemRevArray;
                                Item m_CurrentItem = promoteResult.ItemRevArray[0];
                                Item[] m_ItemToUpdateCommit = new Item[1];
                                m_ItemToUpdateCommit[0] = m_CurrentItem;
                                // commit the changes for the root element only; the reason is as stated before for ItemAssignAll = No
                                mItemSvc.UpdateAndCommitItems(m_ItemToUpdateCommit);

                                // check for FM Sync setting and execute if needed
                                if (mSettings.FMSync.ToLower() == "true")
                                {
                                    //Sync to Fusion Manage
                                    var mExternalSyncService = serviceManager.ExternalSyncService;

                                    if (mExternalSyncService != null)
                                    {
                                        // submit the task to FM for the created/modified item                                       
                                        long mRevId = serviceManager.ItemService.GetLatestItemByItemMasterId(m_CurrentItem.MasterId).Id;
                                        NameValuePair[] taskParamArray = new NameValuePair[] { };
                                        string workflowType = "Adsk.UploadItem";
                                        string description = "Assign/Update Item for file " + mFile.Name;
                                        mExternalSyncService.AddExtSyncTask(mRevId, "ITEM", mFMConfigName,workflowType, description, taskParamArray);
                                    }
                                }
                            }
                            else
                            {
                                //create a restriction for file e and item promoteResult.ItemRevArray[0] Number / Title
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        context.Log("Job " + JOB_TYPE + " failed: " + ex.ToString() + " .", MessageType.eError);
                    }
                }
                catch (Exception ex)
                {
                    if (updatedItems != null && updatedItems.Length > 0)
                    {
                        long[] itemIds = new long[updatedItems.Length];
                        for (int i = 0; i < updatedItems.Length; i++)
                        {
                            itemIds[i] = updatedItems[i].Id;
                        }
                        serviceManager.ItemService.UndoEditItems(itemIds);
                    }
                    else
                    {
                        mPromoteFailed = true;
                        context.Log("Job failed likely due to missing Item Data; Check the property 'Item Assignable'. Details: " + ex.Message, MessageType.eError);
                    }
                }
                finally
                {
                    if (promoteResult != null && mPromoteFailed == true)
                    {
                        // clear out the promoted item
                        serviceManager.ItemService.DeleteUnusedItemNumbers(new long[] { promoteResult.ItemRevArray[0].MasterId });
                        serviceManager.ItemService.UndoEditItems(new long[] { promoteResult.ItemRevArray[0].Id });
                    }
                }

                if (mPromoteFailed)
                    return false;
                else
                    return true;
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
