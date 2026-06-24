using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Currency;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using Autodesk.DataManagement.Client.Framework.Vault.Settings;
using Inventor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;
using ACW = Autodesk.Connectivity.WebServices;
using VDF = Autodesk.DataManagement.Client.Framework;
using ManagePropsHelper = Vault_API_Sample_ManageProperties.ManageProperties;

namespace adsk.ts.job.shared
{
    /// <summary>
    /// Common functionality for jobs
    /// </summary>
    public class JobCommon
    {
        readonly WebServiceManager _WebSrvMgr;
        readonly Connection _connection;
        readonly TextWriterTraceListener _trace;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="webServiceManager"></param>
        /// <param name="mTrace"></param>
        public JobCommon(Connection connection, WebServiceManager webServiceManager, TextWriterTraceListener mTrace)
        {
            // Constructor
            _WebSrvMgr = webServiceManager;
            _connection = connection;
            _trace = mTrace;
        }

        /// <summary>
        /// Download a file from Vault to the local working folder, optionally checking it out
        /// </summary>
        /// <param name="mFile"></param>
        /// <param name="checkout"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string mDownloadFile(ACW.File mFile, bool checkout = false)
        {
            //download the source file iteration, enforcing overwrite if local files exist
            VDF.Vault.Currency.Entities.FileIteration mFileIteration = new VDF.Vault.Currency.Entities.FileIteration(_connection, mFile);

            AcquireFilesSettings mDownloadSettings = new AcquireFilesSettings(_connection);
            // set the default acquisition option to download only; this will apply to all files added to the settings unless specified otherwise
            mDownloadSettings.DefaultAcquisitionOption = AcquireFilesSettings.AcquisitionOption.Download;

            // set the acquisition option for this specific file according the parameter checkout
            if (checkout)
            {   // download and checkout
                mDownloadSettings.AddFileToAcquire(mFileIteration, AcquireFilesSettings.AcquisitionOption.Checkout | AcquireFilesSettings.AcquisitionOption.Download);
            }
            else
            {  // download only
                mDownloadSettings.AddFileToAcquire(mFileIteration, AcquireFilesSettings.AcquisitionOption.Download);
            }

            mDownloadSettings.OrganizeFilesRelativeToCommonVaultRoot = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeChildren = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.RecurseChildren = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeLibraryContents = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.VersionGatheringOption = VDF.Vault.Currency.VersionGatheringOption.Revision;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.ReleaseBiased = true;
            // set overwrite options
            AcquireFilesSettings.AcquireFileResolutionOptions mResOpt = new AcquireFilesSettings.AcquireFileResolutionOptions();
            mResOpt.OverwriteOption = AcquireFilesSettings.AcquireFileResolutionOptions.OverwriteOptions.ForceOverwriteAll;
            mResOpt.SyncWithRemoteSiteSetting = AcquireFilesSettings.SyncWithRemoteSite.Always;
            mDownloadSettings.OptionsResolution.OverwriteOption = mResOpt.OverwriteOption;
            mDownloadSettings.OptionsResolution.SyncWithRemoteSiteSetting = mResOpt.SyncWithRemoteSiteSetting;

            //execute download
            VDF.Vault.Results.AcquireFilesResults? mDownLoadResult = _connection.FileManager.AcquireFiles(mDownloadSettings);

            // find the result for the requested file iteration
            VDF.Vault.Results.FileAcquisitionResult? fileAcquisitionResult = null;
            if (mDownLoadResult != null)
            {
                fileAcquisitionResult = mDownLoadResult.FileResults.FirstOrDefault(n => n.File.EntityName == mFileIteration.EntityName);
            }
            // the download cancelled if the file already existed locally
            if (mDownLoadResult?.IsCancelled == true)
            {
                // check that the file is consumable for the job user
                PropertyDefinitionDictionary mProps = _connection.PropertyManager.GetPropertyDefinitions(VDF.Vault.Currency.Entities.EntityClassIds.Files, null, PropertyDefinitionFilter.IncludeAll);

                PropertyDefinition mVaultStatus = mProps[PropertyDefinitionIds.Client.VaultStatus];

                EntityStatusImageInfo? mStatus = _connection.PropertyManager.GetPropertyValue(mFileIteration, mVaultStatus, null) as EntityStatusImageInfo;
                if (mStatus?.Status.ConsumableState == EntityStatus.ConsumableStateEnum.LatestConsumable)
                {
                    return (_connection.WorkingFoldersManager.GetPathOfFileInWorkingFolder(mFileIteration).FullPath.ToString());
                }
            }

            if (fileAcquisitionResult == null || fileAcquisitionResult.LocalPath == null)
            {
                throw new Exception("Job could not download file " + mFile.Name + " from Vault.");
            }
            return fileAcquisitionResult.LocalPath.FullPath;
        }

        /// <summary>
        /// Resolves the system comment to use when updating the export file's properties.
        /// If the source file is in a consumable lifecycle state the comment of the first iteration
        /// that entered the current revision+state combination is returned, so that downstream
        /// property-update iterations (whose Comm would be e.g. "Property Update") are skipped.
        /// Falls back to mFile.Comm for non-consumable states, and to "Property Update" when Comm is empty.
        /// </summary>
        private string mGetSourceComment(ACW.File mFile)
        {
            try
            {
                if (!mFile.FileLfCyc.Consume)
                {
                    // non-consumable state: the current iteration's comment is authoritative
                    return !string.IsNullOrEmpty(mFile.Comm) ? mFile.Comm : "Property Update";
                }

                // consumable state: find the first iteration that entered this revision+state combination.
                // Filter by LfCycStateId  — same lifecycle state (e.g. "Released")
                //         FileRev.MaxFileId — same revision (all iterations in a revision share this value;
                //                             using the DB id avoids ambiguity from repeated label strings)
                // Order by Id ascending   — lowest Id is the oldest iteration = the one that triggered
                //                           the lifecycle transition into this state
                ACW.File[] allIterations = _WebSrvMgr.DocumentService.GetFilesByMasterId(mFile.MasterId);
                ACW.File? firstInState = allIterations
                    .Where(f => f.FileLfCyc.LfCycStateId == mFile.FileLfCyc.LfCycStateId
                             && f.FileRev.MaxFileId == mFile.FileRev.MaxFileId)
                    .OrderBy(f => f.Id)
                    .FirstOrDefault();

                string comment = firstInState?.Comm ?? string.Empty;
                if (!string.IsNullOrEmpty(comment))
                    return comment;

                // fall back to the current iteration's comment
                return !string.IsNullOrEmpty(mFile.Comm) ? mFile.Comm : "Property Update";
            }
            catch (Exception ex)
            {
                _trace.WriteLine("Job could not resolve source comment for " + mFile.Name + "; falling back to current iteration comment. Details: " + ex.Message);
                return !string.IsNullOrEmpty(mFile.Comm) ? mFile.Comm : "Property Update";
            }
        }

        /// <summary>
        /// Upload files to Vault, optionally copying them to a local output folder; the files are added as new files or new versions of existing files
        /// </summary>
        /// <param name="mFile"></param>
        /// <param name="filesToUpload"></param>
        /// <param name="outPutPath"></param>
        /// <exception cref="Exception"></exception>
        public void mUploadFiles(ACW.File mFile, List<string> filesToUpload, string? outPutPath = null, bool copySourceComment = false)
        {
            foreach (string file in filesToUpload)
            {
                ACW.File mExpFile;
                System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(file);
                if (mExportFileInfo.Exists)
                {
                    //copy file to output location
                    if (outPutPath != null)
                    {
                        System.IO.FileInfo fileInfo = new FileInfo(outPutPath + "\\" + mExportFileInfo.Name);
                        if (fileInfo.Exists)
                        {
                            fileInfo.IsReadOnly = false;
                            fileInfo.Delete();
                        }
                        System.IO.File.Copy(mExportFileInfo.FullName, outPutPath + "\\" + mExportFileInfo.Name, true);
                    }

                    //add resulting export file to Vault if it doesn't exist, otherwise update the existing one

                    ACW.Folder? mFolder = _WebSrvMgr.DocumentService.FindFoldersByIds([mFile.FolderId]).FirstOrDefault();
                    if (mFolder == null || mFolder.Id == -1)
                        throw new Exception("Vault folder with Id=" + mFile.FolderId + " not found");
                    string vaultFilePath = System.IO.Path.Combine(mFolder.FullName, mExportFileInfo.Name).Replace("\\", "/");

                    ACW.File wsFile = _WebSrvMgr.DocumentService.FindLatestFilesByPaths(new string[] { vaultFilePath }).First();
                    VDF.Currency.FilePathAbsolute vdfPath = new VDF.Currency.FilePathAbsolute(mExportFileInfo.FullName);
                    VDF.Vault.Currency.Entities.FileIteration? vdfFile = null;
                    VDF.Vault.Currency.Entities.FileIteration? addedFile = null;
                    VDF.Vault.Currency.Entities.FileIteration? mUploadedFile = null;
                    if (wsFile == null || wsFile.Id < 0)
                    {
                        // add new file to Vault
                        _trace.WriteLine("Job adds " + mExportFileInfo.Name + " as new file.");

                        var folderEntity = new Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities.Folder(_connection, mFolder);
                        try
                        {
                            //check if the file is a DWF file to upload as a hidden file
                            if (mExportFileInfo.Extension.ToLower() == ".dwf")
                            {
                                addedFile = _connection.FileManager.AddFile(folderEntity, "Created by ExportSampleJob", null, null, ACW.FileClassification.DesignVisualization, true, vdfPath);
                                mExpFile = addedFile;
                            }
                            else
                            {
                                addedFile = _connection.FileManager.AddFile(folderEntity, "Created by ExportSampleJob", null, null, ACW.FileClassification.DesignRepresentation, false, vdfPath);
                                mExpFile = addedFile;
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Job could not add export file " + vdfPath + "Exception: ", ex);
                        }

                    }
                    else
                    {
                        // checkin new file version
                        _trace.WriteLine("Job uploads " + mExportFileInfo.Name + " as new file version.");

                        VDF.Vault.Settings.AcquireFilesSettings aqSettings = new VDF.Vault.Settings.AcquireFilesSettings(_connection)
                        {
                            DefaultAcquisitionOption = VDF.Vault.Settings.AcquireFilesSettings.AcquisitionOption.Checkout
                        };
                        vdfFile = new VDF.Vault.Currency.Entities.FileIteration(_connection, wsFile);
                        aqSettings.AddEntityToAcquire(vdfFile);
                        var results = _connection.FileManager.AcquireFiles(aqSettings);
                        try
                        {
                            //check if the file is a DWF file to upload as a hidden file
                            if (vdfFile.FileClassification == ACW.FileClassification.DesignVisualization)
                            {
                                mUploadedFile = _connection.FileManager.CheckinFile(results.FileResults.First().File, "Created by ExportSampleJob", false, null, null, false, null, ACW.FileClassification.DesignVisualization, true, vdfPath);
                                mExpFile = mUploadedFile;
                            }
                            else
                            {
                                mUploadedFile = _connection.FileManager.CheckinFile(results.FileResults.First().File, "Created by ExportSampleJob", false, null, null, false, null, ACW.FileClassification.DesignRepresentation, false, vdfPath);
                                mExpFile = mUploadedFile;
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Job could not update existing export file " + vdfFile + "Exception: ", ex);
                        }
                    }
                }
                else
                {
                    throw new Exception("Job could not find the export result file: " + mExportFileInfo.Name);
                }

                _trace.IndentLevel += 1;

                //update the new file's revision
                try
                {
                    _trace.WriteLine("Job tries synchronizing " + mExpFile.Name + "'s revision in Vault.");
                    _WebSrvMgr.DocumentServiceExtensions.UpdateFileRevisionNumbers(new long[] { mExpFile.Id }, new string[] { mFile.FileRev.Label }, "Rev Index synchronized by ExportSampleJob");
                    mExpFile = (_WebSrvMgr.DocumentService.GetLatestFileByMasterId(mExpFile.MasterId));
                }
                catch (Exception ex)
                {
                    _trace.WriteLine("Job failed synchronizing the revision label of " + mFile.Name + " with export file: " + mExpFile.Name + " . Exception details: " + ex);
                    //you may uncomment the action below if the job should abort executing due to failures copying property values
                    //throw new Exception("Job failed synchronizing the revision label of " + mFile.Name + " with export file: " + mExpFile.Name + " . Exception details: " + ex.ToString() + " ");

                }

                //synchronize source file properties to export file properties for UDPs assigned to both
                if (mExpFile.FileClass != ACW.FileClassification.DesignVisualization)
                {
                    try
                    {
                        _trace.WriteLine(mExpFile.Name + ": Job tries synchronizing properties in Vault.");

                        // initialize helper class
                        // Read date and bool conversion options from Vault settings
                        bool dateOnly = _connection.WebServiceManager.KnowledgeVaultService
                            .GetVaultOption("Autodesk.EDM.UpdateProperties.DateMappingOption") == "1";
                        bool boolAsInt = _connection.WebServiceManager.KnowledgeVaultService
                            .GetVaultOption("Autodesk.EDM.UpdateProperties.WriteBoolPropertyAsN") == "1";

                        // Initialize ManageProperties helper
                        ManagePropsHelper? manageProps = new ManagePropsHelper(_connection, dateOnly, boolAsInt);
                        // initialize dictionary for properties to be updated 
                        Dictionary<ACW.PropDef, object> mPropDictonary = new Dictionary<ACW.PropDef, object>();

                        //get property definitions filtered to UDPs
                        VDF.Vault.Currency.Properties.PropertyDefinitionDictionary mPropDefDic = _connection.PropertyManager.GetPropertyDefinitions(
                            VDF.Vault.Currency.Entities.EntityClassIds.Files, null, VDF.Vault.Currency.Properties.PropertyDefinitionFilter.IncludeUserDefined);

                        VDF.Vault.Currency.Properties.PropertyDefinition mPropDef = new PropertyDefinition();
                        ACW.PropInst[] mSourcePropInsts = _WebSrvMgr.PropertyService.GetProperties("FILE", new long[] { mFile.Id }, new long[] { mPropDef.Id });

                        //get property definitions assigned to Design Representation category
                        ACW.CatCfg catCfg1 = _WebSrvMgr.CategoryService.GetCategoryConfigurationById(mExpFile.Cat.CatId, new string[] { "UserDefinedProperty" });
                        List<long> mFilePropDefs = new List<long>();

                        foreach (ACW.Bhv bhv in catCfg1.BhvCfgArray.First().BhvArray)
                        {
                            mFilePropDefs.Add(bhv.Id);
                        }

                        //get properties assigned to source file and add definition/value pair to dictionary
                        mSourcePropInsts = _WebSrvMgr.PropertyService.GetProperties("FILE", new long[] { mFile.Id }, mFilePropDefs.ToArray());
                        if (mSourcePropInsts != null && mFilePropDefs != null)
                        {
                            ACW.PropDef[]? propDefs = _connection.WebServiceManager.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
                            foreach (ACW.PropInst item in mSourcePropInsts)
                            {
                                mPropDef = _connection.PropertyManager.GetPropertyDefinitionById(item.PropDefId);
                                ACW.PropDef? propDef = propDefs?.SingleOrDefault(n => n.Id == item.PropDefId);
                                if (propDef != null)
                                    mPropDictonary.Add(propDef, item.Val);
                            }

                            //update export file using the property dictionary; note this the IExplorerUtil method bumps file iteration and requires no check out
                            PropWriteResults propWriteResults = new PropWriteResults();
                            string[] cloakedEntityClasses;
                            string mComment = copySourceComment ? mGetSourceComment(mFile) : "Created by ExportSampleJob";
                            manageProps.UpdateFileProperties(
                                mExpFile, comment: mComment, allowSync: true, 
                                mPropDictonary, 
                                out propWriteResults, out cloakedEntityClasses);
                            mExpFile = (_WebSrvMgr.DocumentService.GetLatestFileByMasterId(mExpFile.MasterId));
                            manageProps = null; // release manageProps to avoid keeping reference to potentially large property objects in memory
                        }
                    }

                    catch (Exception ex)
                    {
                        _trace.WriteLine("Job failed copying properties from source file " + mFile.Name + " to export file: " + mExpFile.Name + " . Exception details: " + ex);
                        //you may uncomment the action below if the job should abort executing due to failures copying property values
                        //throw new Exception("Job failed copying properties from source file " + mFile.Name + " to export file: " + mExpFile.Name + " . Exception details: " + ex.ToString() + " ");
                    }
                }

                //align lifecycle states of export to source file's state name
                if (mExpFile.FileClass == ACW.FileClassification.DesignVisualization)
                {
                    try
                    {
                        _trace.WriteLine(mExpFile.Name + ": Job tries synchronizing lifecycle state in Vault.");
                        Dictionary<string, long> mTargetStateNames = new Dictionary<string, long>();
                        ACW.LfCycDef? mTargetLfcDef = (_WebSrvMgr.LifeCycleService.GetLifeCycleDefinitionsByIds(new long[] { mExpFile.FileLfCyc.LfCycDefId })).FirstOrDefault();
                        if (mTargetLfcDef != null)
                        {
                            foreach (var item in mTargetLfcDef.StateArray)
                            {
                                mTargetStateNames.Add(item.DispName, item.Id);
                            }
                            mTargetStateNames.TryGetValue(mFile.FileLfCyc.LfCycStateName, out long mTargetLfcStateId);
                            _WebSrvMgr.DocumentServiceExtensions.UpdateFileLifeCycleStates(new long[] { mExpFile.MasterId }, new long[] { mTargetLfcStateId }, "Lifecycle state synchronized ExportSampleJob");
                        }
                    }
                    catch (Exception ex)
                    {
                        _trace.WriteLine("Job failed aligning lifecycle states of source file " + mFile.Name + " and export file: " + mExpFile.Name + " . Exception details: " + ex);
                    }
                }

                //attach export file to source file leveraging design representation attachment type; for DWF files use visualization attachment type
                try
                {
                    _trace.WriteLine(mExpFile.Name + ": Job tries to attach to its source in Vault.");
                    ACW.FileAssocParam mAssocParam = new ACW.FileAssocParam();
                    mAssocParam.CldFileId = (_WebSrvMgr.DocumentService.GetLatestFileByMasterId(mExpFile.MasterId)).Id;
                    mAssocParam.ExpectedVaultPath = _WebSrvMgr.DocumentService.FindFoldersByIds(new long[] { mFile.FolderId }).First().FullName;
                    mAssocParam.RefId = null;
                    mAssocParam.Source = null;
                    mAssocParam.Typ = ACW.AssociationType.Attachment;
                    //refresh the parent file to the latest version id; default jobs like sync props or update rev.table might have updated the parent already
                    mFile = (_WebSrvMgr.DocumentService.GetLatestFileByMasterId(mFile.MasterId));
                    if (mExpFile.FileClass == ACW.FileClassification.DesignVisualization)
                    {
                        _WebSrvMgr.DocumentService.AddDesignVisualizationFileAttachment(mFile.Id, mAssocParam);
                        mFile = (_WebSrvMgr.DocumentService.GetLatestFileByMasterId(mFile.MasterId));
                        _WebSrvMgr.DocumentService.SetDesignVisualizationAttachmentStatusById(mFile.Id, ACW.DesignVisualizationAttachmentStatus.Syncronized);
                    }
                    else
                    {
                        //before attaching the design representation, remove any existing attachment with the same MasterId
                        FileAssocArray[] fileAssocArray = _WebSrvMgr.DocumentService.GetFileAssociationsByIds(new long[] { mFile.Id }, FileAssociationTypeEnum.None, false, FileAssociationTypeEnum.Attachment, false, false, false);
                        foreach (FileAssocArray fileAssoc in fileAssocArray)
                        {
                            if (fileAssoc.FileAssocs == null)
                                continue;
                            foreach (FileAssoc assoc in fileAssoc.FileAssocs)
                            {
                                ACW.File assocFile = assoc.CldFile;
                                if (assocFile.MasterId == mExpFile.MasterId)
                                {
                                    _WebSrvMgr.DocumentService.RemoveDesignRepresentationFileAttachment(mFile.Id, assoc.CldFile.Id);
                                }
                            }
                        }

                        _WebSrvMgr.DocumentService.AddDesignRepresentationFileAttachment(mFile.Id, mAssocParam);
                    }
                }
                catch (Exception ex)
                {
                    _trace.WriteLine("Job failed attaching the exported file " + mExpFile.Name + " to the source file: " + mFile.Name + " . Exception details: " + ex);
                }

                _trace.IndentLevel -= 1;

            }
        }
    }
}
