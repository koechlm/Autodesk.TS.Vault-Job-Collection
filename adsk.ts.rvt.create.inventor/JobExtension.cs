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
using static System.Windows.Forms.DataFormats;
using System.Xml;
using System.Linq.Expressions;
using System.Data.Common;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using Autodesk.Connectivity.WebServices;

using Microsoft.Win32;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("20.0")]
[assembly: ExtensionId("5f980c92-e275-4d61-a80a-3d733c401818")]


namespace adsk.ts.rvt.create.inventor
{

    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "adsk.ts.rvt.create.inventor";
        private static Settings mSettings = Settings.Load();
        private static string mLogDir = JobExtension.mSettings.LogFileLocation;
        private static string mLogFile;
        adsktsshared.JobCommon tsJobCommon;
        private TextWriterTraceListener mTrace;
        private Connection connection;
        private WebServiceManager mWsMgr;
        ACW.File mFile;
        VDF.Vault.Currency.Entities.FileIteration mFileIteration, mNewFileIteration;
        private Inventor.Application mInvApp = null;
        private Inventor.InventorServer mInvSrv = null;
        private Inventor.ApplicationAddIns addIns = null;

        // list active Inventor addins disabled and reenabled during the job
        private List<Inventor.ApplicationAddIn> mDisabledAddins = new();

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

                // create file iteration object
                mFileIteration = new VDF.Vault.Currency.Entities.FileIteration(connection, mFile);

                // prepare log file and initiate logging
                mLogFile = JOB_TYPE + "_" + mFile.Name + ".log";
                FileInfo mLogFileInfo = new FileInfo(System.IO.Path.Combine(
                    mLogDir, mLogFile));
                if (mLogFileInfo.Exists) mLogFileInfo.Delete();
                mTrace = new TextWriterTraceListener(System.IO.Path.Combine(mLogDir, mLogFile), "mJobTrace");
                mTrace.WriteLine("Starting Job...");

                //start the export task
                mCreateRevitSimplification(context, job);

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
                // InventorServer is managed by the hosting context — never call Quit() on it.
                // If we created an Inventor Application instance on our own, close it; otherwise only re-enable any addins we deactivated.
                if (mInvApp != null)
                {
                    mCloseInventor();
                }
                else if (mInvSrv != null && mDisabledAddins.Count > 0)
                {
                    mReenableAddins();
                }

                if (mTrace != null)
                {
                    mTrace.Flush();
                    mTrace.Close();
                }
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

        private void mCreateRevitSimplification(IJobProcessorServices context, IJob job)
        {
            List<string> mExpFrmts = new List<string>();
            List<string> mValidExpFrmts = new List<string> { "RVT" };
            List<string> mFilesToUpload = new List<string>();

            // read target export formats from settings file
            Settings settings = Settings.Load();

            // the job must not run, if the source file or target export formats are not supported
            #region validate execution rules

            mTrace.IndentLevel += 1;
            mTrace.WriteLine("Translator Job validates execution rules...");

            // only run the job for Inventor assembly file types, supported for Revit simplification (as of today)
            List<string> mFileExtensions = new List<string> { ".iam" }; //ipn is not supported by InventorServer

            if (!mFileExtensions.Any(n => mFile.Name.ToLower().EndsWith(n)))
            {
                mTrace.WriteLine("Translator job exits: file extension is not supported.");
                return;
            }

            // apply execution filters, e.g., exclude files of classification "DesignDocumentation" etc.            
            List<string> mFileClassific = new List<string> { "ConfigurationFactory" };

            if (mFileClassific.Any(n => mFile.FileClass.ToString().Contains(n)))
            {
                mTrace.WriteLine("Translator job exits: file classification 'ConfigurationFactory' is not supported.");
                return;
            }
            #endregion validate execution rules

            // check Revit application availability
            if (IsRevitInstalled() == false)
            {
                mTrace.WriteLine("Translator job required Revit Application but failed to find it installed; exit job with failure.");
                throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not find Revit Application.");
            }

            // check Inventor application availability
            Type invType = Type.GetTypeFromProgID("Inventor.Application");
            if (mSettings.UseInventorExe != null && mSettings.UseInventorExe.ToLower() == "true" && invType == null)
            {
                mTrace.WriteLine("Translator job settings required Inventor Application but failed to find it installed; exit job with failure.");
                throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not find Inventor Application.");
            }

            #region validate Inventor availability
            // evaluate setting to use Inventor executable or Inventor Server for the export creation; default to Inventor Server if not specified
            if (settings.UseInventorExe != null && settings.UseInventorExe.ToLower() == "true")
            {
                mInvApp = mCreateInvInstance();
                addIns = mInvApp.ApplicationAddIns;
            }
            else
            {
                mInvSrv = context.InventorObject as InventorServer;
                addIns = mInvSrv.ApplicationAddIns;
            }

            if (mInvApp == null && mInvSrv == null)
            {
                mTrace.WriteLine("Translator job required Inventor Application or Inventor Server but failed to establish an application instance; exit job with failure.");
                throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not find or start Inventor Application.");
            }
            else
            {
                // Activate the RVT Translator Addin
                Inventor.ApplicationAddIn mRvtTranslator = null;
                try
                {
                    mRvtTranslator = addIns.get_ItemById("{2058EF4F-37A3-4B57-A322-B4E79E7D53E4}"); // RVT Translator functionality
                    if (mRvtTranslator != null)
                    {
                        mRvtTranslator.Activate();
                    }
                }
                catch (Exception)
                {
                    mTrace.WriteLine("Translator job required Inventor Application with RVT Translator addin but failed to find the addin; exit job with failure.");
                    throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not activate Inventor RVT Translator addin.");
                }

                // check the availability of the target Revit file format.
                Inventor.FileManager fileManager = InvFileManager;
                Inventor.NameValueMap formatOptions = InvTransientObjects.CreateNameValueMap();
                if (fileManager != null)
                {
                    formatOptions = fileManager.GetRevitEngineInstallationStatus();
                    // validate every configured Revit version is available before starting the export
                    if (formatOptions != null)
                    {
                        if (settings.TargetRevitVersions.Count > 0)
                        {
                            foreach (string rvtVersion in settings.TargetRevitVersions)
                            {
                                if (formatOptions.Value[rvtVersion] is false)
                                {
                                    mTrace.WriteLine("Job could not find Revit Interoperability version " + rvtVersion + " in the Inventor Revit export engine options; exit job with failure.");
                                    throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not find Revit Interoperability version " + rvtVersion + " in the Inventor Revit export engine options.");
                                }
                            }
                        }
                        else
                        {
                            mTrace.WriteLine("No target Revit version specified in settings file; continue with export creation with default Revit version.");
                        }
                    }
                }

                // disable iLogic if active
                try
                {
                    Inventor.ApplicationAddIn addIniLogic = null;
                    addIniLogic = addIns.ItemById["{3BDD8D79-2179-4B11-8A5A-257B1C0263AC}"]; // iLogic
                    if (addIniLogic != null) //&& addIniLogic.Activated == true
                    {
                        addIniLogic.Deactivate();
                        mDisabledAddins.Add(addIniLogic);
                    }
                }
                catch (Exception)
                {
                    //ignore, iLogic not installed
                }

                // disable Vault Data Standard Addin if active
                try
                {
                    Inventor.ApplicationAddIn addInVDS = null;
                    addInVDS = addIns.ItemById["{B0E8F1C3-9BFD-4B9A-9C8B-7F2E1C025DCD}"]; // Vault Data Standard
                    if (addInVDS != null && addInVDS.Activated == true)
                    {
                        addInVDS.Deactivate();
                        mDisabledAddins.Add(addInVDS);
                    }
                }
                catch (Exception)
                {
                    //ignore, Vault Data Standard Addin not installed
                }

                // disable Vault Addin if active
                try
                {
                    Inventor.ApplicationAddIn addInVault = null;
                    addInVault = addIns.ItemById["{48B682BC-42E6-4953-84C5-3D253B52E77B}"]; // Vault
                    if (addInVault != null && addInVault.Activated == true)
                    {
                        addInVault.Deactivate();
                        mDisabledAddins.Add(addInVault);
                    }
                }
                catch (Exception)
                {
                    //ignore, Vault Addin not installed
                }

            #endregion validate Inventor availability

                // Inventor must have a project file activated; we enforce using the Vault stored IPJ
                #region Inventor IPJ activation

                //override Inventor default project settings by your Vault specific ones
                Inventor.DesignProjectManager projectManager;
                Inventor.DesignProject mSaveProject = null, mProject = null;

                String mIpjLocalPath = "";

                //download and activate the Inventor Project file
                mTrace.IndentLevel += 1;
                mTrace.WriteLine("Job tries activating Inventor project file as enforced in Vault behavior configurations.");

                adsktsshared.InventorJob mJobInventor = new(connection, mWsMgr);
                bool settingsAcceptLocalIpj = false;
                if (settings.AcceptLocalIpj.ToLower() == "true") settingsAcceptLocalIpj = true;
                mIpjLocalPath = mJobInventor.mGetIpj(settingsAcceptLocalIpj);

                //activate the given project file for this job only
                projectManager = InvDesignProjectManager;
                // Inventor might fail with unhandled exeption on fresh installed machines, if no IPJ had been used before
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

                #endregion Inventor IPJ activation

                //download the source file(s) including its references
                #region download source file(s)
                mTrace.IndentLevel += 1;
                mTrace.WriteLine("Job downloads source file(s) for translation.");

                // use shared code to download the file
                tsJobCommon = new(connection, mWsMgr, mTrace);
                string mDocPath = null;
                // download with/without check-out depending on Revit associativity setting; the download method validates and exits the job in case of failures
                if (mSettings.RvtAssociative.ToLower() == "true")
                {
                    mDocPath = tsJobCommon.mDownloadFile(mFile, true);
                    if (mDocPath != null)
                    {
                        string mExt = System.IO.Path.GetExtension(mDocPath);
                    }
                }
                else
                {
                    mDocPath = tsJobCommon.mDownloadFile(mFile, false);
                    if (mDocPath != null)
                    {
                        string mExt = System.IO.Path.GetExtension(mDocPath);
                    }
                }

                ACW.File mDownloadedFile = mWsMgr.DocumentService.GetLatestFileByMasterId(mFile.MasterId);
                mNewFileIteration = new VDF.Vault.Currency.Entities.FileIteration(connection, mDownloadedFile);

                mTrace.WriteLine("Job successfully downloaded source file(s) for translation.");
                #endregion download source file(s)

                // capture dependencies for upload later
                #region capture dependencies
                //we need to return all relationships during later check-in
                List<ACW.FileAssocParam> mFileAssocParams = new List<ACW.FileAssocParam>();
                ACW.FileAssocArray mFileAssocArray = null;
                mFileAssocArray = mWsMgr.DocumentService.GetLatestFileAssociationsByMasterIds(new long[] { mFile.MasterId },
                    ACW.FileAssociationTypeEnum.None, false, ACW.FileAssociationTypeEnum.All, false, false, false, true).FirstOrDefault();
                if (mFileAssocArray.FileAssocs != null)
                {
                    foreach (ACW.FileAssoc item in mFileAssocArray.FileAssocs)
                    {
                        ACW.FileAssocParam mFileAssocParam = new ACW.FileAssocParam();
                        mFileAssocParam.CldFileId = item.CldFile.Id;
                        mFileAssocParam.ExpectedVaultPath = item.ExpectedVaultPath;
                        mFileAssocParam.RefId = item.RefId;
                        mFileAssocParam.Source = item.Source;
                        mFileAssocParam.Typ = item.Typ;
                        mFileAssocParams.Add(mFileAssocParam);
                    }
                }
                #endregion capture dependencies

                // manage RVT export definition and feature
                #region create RVT export
                mTrace.WriteLine("Job starts task for RVT Simplification.");

                // load presets and preset-object map once before the export loop
                Dictionary<string, Dictionary<string, string>> mPresets = mGetRevitPresets();
                Dictionary<string, object> mPresetObjects = mReadPresetMap();

                // download the Revit template from Vault once; reused for all version × preset iterations
                string templateLocalPath = null;
                if (mSettings.RevitTemplate != null && mSettings.RevitTemplate != "")
                {
                    ACW.File mTemplateFile = mWsMgr.DocumentService.FindLatestFilesByPaths([mSettings.RevitTemplate]).FirstOrDefault();
                    if (mTemplateFile != null)
                    {
                        templateLocalPath = tsJobCommon.mDownloadFile(mTemplateFile);
                    }
                    else
                    {
                        mTrace.WriteLine("Job could not find the specified Revit template in Vault; continue without template.");
                    }
                }

                // determine whether multi-value naming is required
                bool isMultiExport = settings.TargetRevitVersions.Count > 1 || settings.InventorPresetNames.Count > 1;

                //use Inventor to open document
                Inventor.Document mDoc = InvDocuments.Open(mDocPath);

                if (mDoc == null)
                {
                    mJobInventor.mResetIpj(mSaveProject);
                    // undo the check-out if the file is checked out
                    if (mDownloadedFile.CheckedOut == true)
                    {
                        mWsMgr.DocumentService.UndoCheckoutFile(mFile.MasterId, out ByteArray downloadTicket);
                    }
                    throw new Exception("Job could not open the source file " + mDocPath + " in Inventor.");
                }

                // validate the assembly context.
                Inventor.AssemblyDocument mAsmDoc = null;
                if (mDoc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    mAsmDoc = (Inventor.AssemblyDocument)mDoc;
                }
                else
                {
                    mTrace.WriteLine("Job could not create RVT export: source file is not an assembly.");
                    mJobInventor.mResetIpj(mSaveProject);
                    // undo the check-out if the file is checked out
                    if (mDownloadedFile.CheckedOut == true)
                    {
                        mWsMgr.DocumentService.UndoCheckoutFile(mFile.MasterId, out ByteArray downloadTicket);
                    }
                    throw new Exception("Job's single task creating an RVT export from Inventor file failed: source file is not an assembly.");
                }

                // activate the Master model state if the active model state is substitute.
                if (mAsmDoc.ComponentDefinition.ModelStates.ActiveModelState.ModelStateType == ModelStateTypeEnum.kSubstituteModelStateType)
                {
                    mAsmDoc.ComponentDefinition.ModelStates[1].Activate();
                }

                // run the version × preset export loop; document stays open for all iterations
                foreach (string rvtVersion in settings.TargetRevitVersions)
                {
                    foreach (string presetName in settings.InventorPresetNames)
                    {
                        // compute unique output filename per iteration
                        string mExpFileName = isMultiExport
                            ? mDocPath + "_" + rvtVersion + "_" + presetName + ".rvt"
                            : mDocPath + ".rvt";

                        mTrace.WriteLine("Job processes export: Revit version=" + rvtVersion + ", preset=" + presetName + ", output=" + System.IO.Path.GetFileName(mExpFileName));

                        // check for an existing export feature (associative mode only)
                        Inventor.RevitExport revitExport = null;
                        Inventor.RevitExportDefinition revitExportDef = null;
                        bool mNewExportDef = false;
                        if (mSettings.RvtAssociative.ToLower() == "true")
                        {
                            foreach (Inventor.RevitExport rvtFeature in mAsmDoc.ComponentDefinition.RevitExports)
                            {
                                if (rvtFeature.Name == mExpFileName)
                                {
                                    revitExportDef = rvtFeature.Definition;
                                    break;
                                }
                            }
                        }

                        // create a new export definition if no existing one was found
                        if (revitExportDef == null)
                        {
                            mNewExportDef = true;
                            revitExportDef = mAsmDoc.ComponentDefinition.RevitExports.CreateDefinition();

                            // derive path and file name from source file mDoc
                            revitExportDef.Location = System.IO.Path.GetDirectoryName(mDocPath);
                            revitExportDef.FileName = mExpFileName;

                            // set the target Revit version for this iteration
                            revitExportDef.RevitVersion = rvtVersion;

                            // apply preset settings
                            if (mPresets != null && mPresets.ContainsKey(presetName))
                            {
                                foreach (var preset in mPresets[presetName])
                                {
                                    switch (preset.Key)
                                    {
                                        case "ENVELOPE_SELECTOR":
                                            if (mPresetObjects.ContainsKey(preset.Value))
                                                revitExportDef.EnvelopesReplaceStyle = (Inventor.EnvelopesReplaceStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "REMOVE_PART_BY_SIZE_TOGGLE":
                                            revitExportDef.RemovePartsBySize = Convert.ToBoolean(preset.Value);
                                            break;
                                        case "MAXIMUM_DIAGONAL_RVEC":
                                            revitExportDef.RemovePartsSize = Convert.ToDouble(preset.Value.Split(' ').FirstOrDefault());
                                            break;
                                        case "REMOVE_HOLE_SELECTOR":
                                            revitExportDef.RemoveHolesStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "MAX_DIAMETER_RVEC":
                                            revitExportDef.RemoveHolesDiameterRange = Convert.ToDouble(preset.Value.Split(' ').FirstOrDefault());
                                            break;
                                        case "REMOVE_FILLET_SELECTOR":
                                            revitExportDef.RemoveFilletsStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "MAX_RADIUS_RVEC":
                                            revitExportDef.RemoveFilletsRadiusRange = Convert.ToDouble(preset.Value.Split(' ').FirstOrDefault());
                                            break;
                                        case "REMOVE_CHAMFER_SELECTOR":
                                            revitExportDef.RemoveChamfersStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "MAX_DISTANCE_RVEC":
                                            revitExportDef.RemoveChamfersDistanceRange = Convert.ToDouble(preset.Value.Split(' ').FirstOrDefault());
                                            break;
                                        case "REMOVE_POCKET_SELECTOR":
                                            revitExportDef.RemovePocketsStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "MAX_LOOP_RVEC":
                                            revitExportDef.RemovePocketsMaxDepthRange = Convert.ToDouble(preset.Value.Split(' ').FirstOrDefault());
                                            break;
                                        case "REMOVE_EMBOSS_SELECTOR":
                                            revitExportDef.RemoveEmbossesStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "MAX_HEIGHT_RVEC":
                                            revitExportDef.RemoveEmbossMaxHeightRange = Convert.ToDouble(preset.Value.Split(' ').FirstOrDefault());
                                            break;
                                        case "REMOVE_TUNNEL_SELECTOR":
                                            revitExportDef.RemoveTunnelsStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "RVT_STRUCTURE_SELECTOR":
                                            if (mPresetObjects.ContainsKey(preset.Value))
                                                revitExportDef.Structure = (Inventor.RevitExportStructureTypeEnum)mPresetObjects[preset.Value];
                                            break;
                                        case "FILL_INTERNAL_VOIDS_TOGGLE":
                                            revitExportDef.RemoveAllInternalVoids = Convert.ToBoolean(preset.Value);
                                            break;
                                        case "REMOVE_INTERNAL_PARTS_TOGGLE":
                                            revitExportDef.RemoveInternalParts = Convert.ToBoolean(preset.Value);
                                            break;
                                        case "USE_COLOR_OVERRIDE_FROM_SOURCE_TOGGLE":
                                            revitExportDef.UseColorOverrideFromSourceComponent = Convert.ToBoolean(preset.Value);
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                // continue with default settings as no preset could be applied
                                revitExportDef.IsAssociativeDesignView = false;
                                revitExportDef.EnvelopesReplaceStyle = Inventor.EnvelopesReplaceStyleEnum.kAllInOneEnvelopeReplaceStyle;
                                revitExportDef.RemovePartsBySize = true;
                                revitExportDef.RemovePartsSize = 1.0; // 1 cm
                                revitExportDef.RemoveHolesStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange;
                                revitExportDef.RemoveHolesDiameterRange = 1.0; // 1 cm
                                revitExportDef.RemoveFilletsStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll;
                                revitExportDef.RemoveFilletsRadiusRange = 1.0; // 1 cm
                                revitExportDef.RemoveChamfersStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll;
                                revitExportDef.RemovePocketsStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll;
                                revitExportDef.RemoveEmbossStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll;
                                revitExportDef.RemoveTunnelsStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll;
                                revitExportDef.Structure = Inventor.RevitExportStructureTypeEnum.kAllInOneElementStructure;
                                revitExportDef.RemoveAllInternalVoids = true;
                                revitExportDef.RemoveInternalParts = true;
                                revitExportDef.UseColorOverrideFromSourceComponent = true;
                            }

                            // enable updating if Revit association feature is required
                            revitExportDef.EnableUpdating = mSettings.RvtAssociative.ToLower() == "true";

                            // assign the template downloaded before the loop (reused for all iterations)
                            if (templateLocalPath != null)
                            {
                                revitExportDef.RevitTemplate = templateLocalPath;
                            }
                        }

                        // delete existing output file before running the export
                        if (System.IO.File.Exists(mExpFileName))
                        {
                            System.IO.FileInfo fileInfo = new FileInfo(mExpFileName);
                            fileInfo.IsReadOnly = false;
                            fileInfo.Delete();
                        }
                        if (mNewExportDef == true)
                        {
                            revitExport = mAsmDoc.ComponentDefinition.RevitExports.Add(revitExportDef);
                            mTrace.WriteLine("Job created new RVT export definition and feature.");
                        }
                        else
                        {
                            revitExport.Update();
                            mTrace.WriteLine("Job updated existing RVT export definition and feature.");
                        }

                        // release export COM objects after each iteration so ATF reference counts
                        // drop to zero while the document is still alive
                        if (revitExport != null)
                        {
                            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(revitExport);
                            revitExport = null;
                        }
                        if (revitExportDef != null)
                        {
                            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(revitExportDef);
                            revitExportDef = null;
                        }

                        // verify output file and add to upload list
                        System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mExpFileName);
                        if (mExportFileInfo.Exists)
                        {
                            mFilesToUpload.Add(mExpFileName);
                            mTrace.WriteLine("RVT Simplification created file: " + mFilesToUpload.LastOrDefault());
                            mTrace.IndentLevel -= 1;
                        }
                        else
                        {
                            mJobInventor.mResetIpj(mSaveProject);
                            if (mDownloadedFile.CheckedOut == true)
                            {
                                mWsMgr.DocumentService.UndoCheckoutFile(mFile.MasterId, out ByteArray downloadTicket);
                            }
                            throw new Exception("Validating the export file " + mExpFileName + " before upload failed.");
                        }
                    } // end foreach presetName
                } // end foreach rvtVersion

                // save the document once after all exports if associative export is enabled
                if (mSettings.RvtAssociative.ToLower() == "true" && mDownloadedFile.CheckedOut == true)
                {
                    mDoc.Save2(false);
                }

                // close the document once after all version × preset exports complete
                mDoc.Close(true);

                // Deactivate the RVT Translator addin so ATF releases the out-of-process
                // Revit engine (ATFRevitBroker), allowing sequential jobs to run cleanly.
                mShutdownRvtTranslator();

                #endregion create RVT export

                // check in the source file, to add/update the Revit Export feature
                #region check in source file

                FileIteration mUploadedFile = null;
                if (mDownloadedFile.CheckedOut == true)
                {
                    VDF.Currency.FilePathAbsolute vdfPath = new VDF.Currency.FilePathAbsolute(mDocPath);

                    try
                    {
                        if (mFileAssocParams.Count > 0)
                        {
                            mUploadedFile = connection.FileManager.CheckinFile(
                                file: mNewFileIteration,
                                comment: "Created by job " + JOB_TYPE,
                                keepCheckedOut: false,
                                associations: mFileAssocParams.ToArray(),
                                bom: null,
                                copyBom: true,
                                newFileName: null,
                                classification: mFileIteration.FileClassification,
                                hidden: false,
                                filePath: vdfPath
                            );
                        }
                        else
                        {
                            mUploadedFile = connection.FileManager.CheckinFile(
                                file: mNewFileIteration,
                                comment: "Created by job " + JOB_TYPE,
                                keepCheckedOut: false,
                                associations: null,
                                bom: null,
                                copyBom: true,
                                newFileName: null,
                                classification: mFileIteration.FileClassification,
                                hidden: false,
                                filePath: vdfPath
                            );
                        }
                    }
                    catch
                    {
                        context.Log(null, "Job could not check-in updated file: " + mUploadedFile.EntityName + ".");
                        throw new Exception("Job's single task creating an RVT export from Inventor file failed: could not check-in updated source file.");
                    }
                }
                else
                {
                    mUploadedFile = mFileIteration;
                }
                #endregion check in source file

                // process the upload of the created files
                adsktsshared.JobCommon mJobCommon = new(connection, mWsMgr, mTrace);
                // the original file iteration mFile is no longer valid, the export created a new version
                mFile = connection.WebServiceManager.DocumentService.GetLatestFileByMasterId(mUploadedFile.EntityMasterId);
                mJobCommon.mUploadFiles(mFile, mFilesToUpload, settings.OutPutPath, settings.CopySystemComment == "True");

                // finalize log output
                mTrace.IndentLevel = 1;
                mTrace.WriteLine("Job finished all steps.");
            }
        }
        private Dictionary<string, object> mReadPresetMap()
        {
            Dictionary<string, object> mMap = new Dictionary<string, object>();

            // replace with envelopes
            mMap.Add("ENVELOPE_REPLACE_NONE_ITEM", Inventor.EnvelopesReplaceStyleEnum.kNoneReplaceStyle); //118785 No enveloping
            mMap.Add("ENVELOPE_REPLACE_TOP_ASSEMBLY_ITEM", Inventor.EnvelopesReplaceStyleEnum.kAllInOneEnvelopeReplaceStyle); //118786 Replace entire assembly with an envelope
            mMap.Add("ENVELOPE_REPLACE_ALL_PARTS_ITEM", Inventor.EnvelopesReplaceStyleEnum.kEachPartReplaceStyle); //118788 Replace each part with an envelope
            mMap.Add("ENVELOPE_REPLACE_TOP_COMPONENTS_ITEM", Inventor.EnvelopesReplaceStyleEnum.kEachTopLevelComponentReplaceStyle); //118787 Replace each top level components with an envelope

            // Simplification
            mMap.Add("REMOVE_HOLE_ALL_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll); //118786 Remove all
            mMap.Add("REMOVE_HOLE_RANGE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange); //118787 Remove in range
            mMap.Add("REMOVE_HOLE_NONE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveNone); //118785 Do not remove any
            mMap.Add("REMOVE_FILLET_ALL_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll); //118786 Remove all
            mMap.Add("REMOVE_FILLET_RANGE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange); //118787 Remove in range
            mMap.Add("REMOVE_FILLET_NONE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveNone); //118785 Do not remove any
            mMap.Add("REMOVE_CHAMFER_ALL_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll); //118786 Remove all
            mMap.Add("REMOVE_CHAMFER_RANGE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange); //118787 Remove in range
            mMap.Add("REMOVE_CHAMFER_NONE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveNone); //118785 Do not remove any
            mMap.Add("REMOVE_POCKET_ALL_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll); //118786 Remove all
            mMap.Add("REMOVE_POCKET_RANGE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange); //118787 Remove in range
            mMap.Add("REMOVE_POCKET_NONE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveNone); //118785 Do not remove any
            mMap.Add("REMOVE_EMBOSS_ALL_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll); //118786 Remove all
            mMap.Add("REMOVE_EMBOSS_RANGE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange); //118787 Remove in range
            mMap.Add("REMOVE_EMBOSS_NONE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveNone); //118785 Do not remove any
            mMap.Add("REMOVE_TUNNEL_ALL_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll); //118786 Remove all
            mMap.Add("REMOVE_TUNNEL_RANGE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange); //118787 Remove in range
            mMap.Add("REMOVE_TUNNEL_NONE_ITEM", Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveNone); //118785 Do not remove any

            // Revit structure
            mMap.Add("RVT_ALL_IN_ONE", Inventor.RevitExportStructureTypeEnum.kAllInOneElementStructure); //119041 Everything structured as a single Revit element
            mMap.Add("RVT_TOPLEVEL_COMPONENTS", Inventor.RevitExportStructureTypeEnum.kEachTopLevelComponentStructure); //119042 Top level components as Revit elements

            return mMap;
        }


        // Unified accessors — null-coalescing between the two Inventor application types; no per-call conditionals needed.
        private Inventor.Documents             InvDocuments             => mInvApp?.Documents             ?? mInvSrv.Documents;
        private Inventor.FileManager           InvFileManager           => mInvApp?.FileManager           ?? mInvSrv.FileManager;
        private Inventor.TransientObjects      InvTransientObjects      => mInvApp?.TransientObjects      ?? mInvSrv.TransientObjects;
        private Inventor.DesignProjectManager  InvDesignProjectManager  => mInvApp?.DesignProjectManager  ?? mInvSrv.DesignProjectManager;

        private Dictionary<string, Dictionary<string, string>> mGetRevitPresets()
        {
            // read the preset XML file and create a name/value map for all simplification options
            ACW.File mPresetFile = mWsMgr.DocumentService.FindLatestFilesByPaths([mSettings.InventorPreset]).FirstOrDefault();
            string presetFile = tsJobCommon.mDownloadFile(mPresetFile);
            List<string> mRvtPresets = new List<string>();
            XmlDocument xmlDocument = new XmlDocument();

            // create a name/value map for all simplification options
            Dictionary<string, Dictionary<string, string>> mPresetSettings = new Dictionary<string, Dictionary<string, string>>();
            if (mPresetFile != null)
            {
                xmlDocument.Load(presetFile);
                // select all Preset nodes regardless of name
                XmlNodeList settingNodes = xmlDocument.SelectNodes("//Preset");

                foreach (XmlNode mNode in settingNodes)
                {
                    Dictionary<string, string> mSettings = new Dictionary<string, string>();
                    foreach (XmlNode mChildNode in mNode.ChildNodes)
                    {
                        mSettings.Add(mChildNode.Name, mChildNode.Attributes["Value"].Value);
                    }
                    mPresetSettings.Add(mNode.Attributes["Name"].Value, mSettings);
                    mRvtPresets.Add(mNode.Attributes["Name"].Value);
                }

                return mPresetSettings;
            }

            return null;
        }

        private Inventor.Application mGetInventor()
        {
            // Try to get an active instance of Inventor
            try
            {
                try
                {
                    mInvApp = MarshalCore.GetActiveObject("Inventor.Application") as Inventor.Application;
                    if (mInvApp != null)
                    {
                        mInvApp.Visible = true;
                        // run Inventor silently
                        mInvApp.SilentOperation = true;
                        mTrace.WriteLine("Reusing running Inventor application object.");
                        return mInvApp;
                    }
                }
                catch (Exception)
                {
                    Type inventorAppType = System.Type.GetTypeFromProgID("Inventor.Application");
                    mInvApp = System.Activator.CreateInstance(inventorAppType) as Inventor.Application;
                    if (mInvApp != null)
                    {
                        mInvApp.Visible = false;
                        // run Inventor silently
                        mInvApp.SilentOperation = true;
                        mTrace.WriteLine("Started new Inventor application object.");
                    }
                }
                return mInvApp;
            }
            catch
            {
                mTrace.WriteLine("Failed to get or create Inventor application object.");
                throw new Exception("Job run into unhandled exception trying to reuse or create an Inventor instance.");
            }
        }

        private Inventor.Application mCreateInvInstance()
        {
            Inventor.Application inventorApp = null;
            try
            {
                Type inventorAppType = System.Type.GetTypeFromProgID("Inventor.Application");
                inventorApp = System.Activator.CreateInstance(inventorAppType) as Inventor.Application;
                if (inventorApp != null)
                {
                    inventorApp.Visible = false;
                    // run Inventor silently
                    inventorApp.SilentOperation = true;
                    mTrace.WriteLine("Started new Inventor application object.");
                }
                return inventorApp;
            }
            catch
            {
                mTrace.WriteLine("Failed to create Inventor application object.");
                throw new Exception("Job run into unhandled exception trying to create an Inventor instance.");
            }
        }

        private void mReenableAddins()
        {
            try
            {
                foreach (var addin in mDisabledAddins)
                {
                    if (addin != null && addin.Activated == false)
                    {
                        addin.Activate();
                    }
                }
            }
            catch (Exception)
            {
                // not a reason to throw an exception
            }
        }

        private void mCloseInventor()
        {
            if (mInvApp == null) return;

            mReenableAddins();

            try
            {
                mInvApp.Quit();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(mInvApp);
                mInvApp = null;
            }
            catch (Exception ex)
            {
                mTrace.WriteLine("Failed to close Inventor application object: " + ex.ToString());
            }
        }

        private void mShutdownRvtTranslator()
        {
            // Deactivating the RVT Translator addin is the designed ATF API signal to release the
            // out-of-process Revit engine. FinalReleaseComObject ensures the addin RCW drops its
            // COM reference to the ATF component immediately rather than waiting for the GC.
            try
            {
                Inventor.ApplicationAddIn rvtTranslator = addIns.get_ItemById("{2058EF4F-37A3-4B57-A322-B4E79E7D53E4}");
                if (rvtTranslator != null && rvtTranslator.Activated)
                {
                    rvtTranslator.Deactivate();
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(rvtTranslator);
                    mTrace.WriteLine("RVT Translator addin deactivated and COM reference released.");
                }
            }
            catch (Exception ex)
            {
                mTrace.WriteLine("Could not deactivate RVT Translator addin: " + ex.Message);
            }

            // ATF process hierarchy: ATFRevitRCEHost is the actual Revit engine host spawned by
            // ATFRevitBroker. The broker only exits after the host releases it, so we must wait
            // for them in dependency order. WaitForExit observes the natural exit — it does not
            // kill anything.
            const int timeoutMs = 15_000;
            foreach (string procName in (string[])["ATFRevitRCEHost", "ATFRevitBroker"])
            {
                try
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName(procName))
                    {
                        using (proc)
                        {
                            if (proc.WaitForExit(timeoutMs))
                                mTrace.WriteLine($"{procName} (PID {proc.Id}) exited cleanly.");
                            else
                                mTrace.WriteLine($"{procName} (PID {proc.Id}) did not exit within {timeoutMs / 1000}s; continuing.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    mTrace.WriteLine($"Could not observe {procName} exit: " + ex.Message);
                }
            }
        }


        public static bool IsRevitInstalled()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\Revit"))
            {
                return key != null && key.GetSubKeyNames().Length > 0;
            }
        }
    }
}