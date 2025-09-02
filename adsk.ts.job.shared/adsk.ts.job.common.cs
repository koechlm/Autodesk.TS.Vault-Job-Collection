using System;
using System.Collections.Generic;
using System.Text;

using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;
using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using Autodesk.DataManagement.Client.Framework.Currency;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using Autodesk.DataManagement.Client.Framework.Vault.Settings;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

namespace adsk.ts.job.shared
{
    public class JobCommon
    {
        readonly WebServiceManager _WebSrvMgr;
        readonly Connection _connection;
        readonly TextWriterTraceListener _trace;

        public JobCommon(Connection connection, WebServiceManager webServiceManager, TextWriterTraceListener mTrace)
        {
            // Constructor
            _WebSrvMgr = webServiceManager;
            _connection = connection;
            _trace = mTrace;
        }

        public string mDownloadFile(ACW.File mFile)
        {
            //download the source file iteration, enforcing overwrite if local files exist
            VDF.Vault.Settings.AcquireFilesSettings mDownloadSettings = new VDF.Vault.Settings.AcquireFilesSettings(_connection);
            VDF.Vault.Currency.Entities.FileIteration mFileIteration = new VDF.Vault.Currency.Entities.FileIteration(_connection, mFile);
            mDownloadSettings.AddFileToAcquire(mFileIteration, VDF.Vault.Settings.AcquireFilesSettings.AcquisitionOption.Download);
            mDownloadSettings.OrganizeFilesRelativeToCommonVaultRoot = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeChildren = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.RecurseChildren = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeLibraryContents = true;
            mDownloadSettings.OptionsRelationshipGathering.FileRelationshipSettings.ReleaseBiased = true;
            VDF.Vault.Settings.AcquireFilesSettings.AcquireFileResolutionOptions mResOpt = new VDF.Vault.Settings.AcquireFilesSettings.AcquireFileResolutionOptions();
            mResOpt.OverwriteOption = VDF.Vault.Settings.AcquireFilesSettings.AcquireFileResolutionOptions.OverwriteOptions.ForceOverwriteAll;
            mResOpt.SyncWithRemoteSiteSetting = VDF.Vault.Settings.AcquireFilesSettings.SyncWithRemoteSite.Always;

            //execute download
            VDF.Vault.Results.AcquireFilesResults? mDownLoadResult = _connection.FileManager.AcquireFiles(mDownloadSettings);
            //pickup result details
            VDF.Vault.Results.FileAcquisitionResult? fileAcquisitionResult = null;
            if (mDownLoadResult != null)
            {
                fileAcquisitionResult = mDownLoadResult.FileResults.FirstOrDefault(n => n.File.EntityName == mFileIteration.EntityName);
            }

            if (fileAcquisitionResult == null)
            {
                throw new Exception("Job stopped execution as the file " + mFile.Name + " did not download.");
            }
            
            return fileAcquisitionResult.LocalPath.FullPath;
        }

        public void mUploadFiles(ACW.File mFile, List<string> filesToUpload, string outPutPath)
        {
            foreach (string file in filesToUpload)
            {
                ACW.File mExpFile;
                System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(file);
                if (mExportFileInfo.Exists)
                {
                    //copy file to output location
                    if (outPutPath != "")
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
                catch (Exception)
                {
                    //the job will not stop execution in this sample, if revision labels don't synchronize
                }

                //synchronize source file properties to export file properties for UDPs assigned to both
                if (mExpFile.FileClass != ACW.FileClassification.DesignVisualization)
                {
                    try
                    {
                        _trace.WriteLine(mExpFile.Name + ": Job tries synchronizing properties in Vault.");
                        //get the design rep category's user properties
                        ACET.IExplorerUtil mExplUtil = Autodesk.Connectivity.Explorer.ExtensibilityTools.ExplorerLoader.LoadExplorerUtil(
                                    _connection.Server, _connection.Vault, _connection.UserID, _connection.Ticket);
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
                            mExplUtil.UpdateFileProperties(mExpFile, mPropDictonary);
                            mExpFile = (_WebSrvMgr.DocumentService.GetLatestFileByMasterId(mExpFile.MasterId));
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
