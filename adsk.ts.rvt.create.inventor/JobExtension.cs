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
using adsk.ts.job.shared;
using System.Xml;
using System.Linq.Expressions;
using System.Data.Common;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using Autodesk.Connectivity.WebServices;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
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
        private Inventor.Application mInv = null;

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
                if (mInv != null)
                {
                    mCloseInventor();
                }

                if (mTrace != null)
                {
                    mTrace.Flush();
                    mTrace.Close();
                }
            }
        }

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

            // only run the job for 3D source file types, supported by exports (as of today)
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

            #region validate Inventor availability
            //validate Inventor instance for RVT Export format
            mInv = mGetInventor();
            if (mInv == null)
            {
                mTrace.WriteLine("Translator job required Inventor Application but failed to establish an application instance; exit job with failure.");
                throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not find or start Inventor Application.");
            }
            else
            {
                // check for the BIM Interoperability addin
                Inventor.ApplicationAddIns addIns = mInv.ApplicationAddIns;
                Inventor.ApplicationAddIn addInBimSimplify = null;
                try
                {
                    addInBimSimplify = addIns.get_ItemById("{71019C12-43F6-4C11-BA7A-AD9BDBC5EA0C}"); // BIM Simplify
                    if (addInBimSimplify != null && addInBimSimplify.Activated == false)
                    {
                        addInBimSimplify.Activate();
                    }
                }
                catch (Exception)
                {
                    mTrace.WriteLine("Translator job required Inventor Application with BIM Simplify addin but failed to find the addin; exit job with failure.");
                    throw new Exception("Translator job's single task creating an RVT export from Inventor file failed: could not activate Inventor BIM Simplify addin.");
                }
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
            projectManager = mInv.DesignProjectManager;
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
            // adding or updating a Revit export feature creates a new file iteration: download and check out
            string mDocPath = tsJobCommon.mDownloadFile(mFile, true);
            if (mDocPath != null) {
                string mExt = System.IO.Path.GetExtension(mDocPath);
                
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
            //use Inventor to open document
            Inventor.Document mDoc = mInv.Documents.Open(mDocPath);

            if (mDoc == null)
            {
                mJobInventor.mResetIpj(mSaveProject);
                throw new Exception("Job could not open the source file " + mDocPath + " in Inventor.");
            }

            Inventor.AssemblyDocument mAsmDoc = null;
            if (mDoc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                mAsmDoc = (Inventor.AssemblyDocument)mDoc;
            }
            else
            {
                mTrace.WriteLine("Job could not create RVT export: source file is not an assembly.");
                mJobInventor.mResetIpj(mSaveProject);
                throw new Exception("Job's single task creating an RVT export from Inventor file failed: source file is not an assembly.");
            }

            // activate the Master model state if the active model state is substitute.
            if (mAsmDoc.ComponentDefinition.ModelStates.ActiveModelState.ModelStateType == ModelStateTypeEnum.kSubstituteModelStateType)
            {
                mAsmDoc.ComponentDefinition.ModelStates[1].Activate();
            }
            // check existing export definition
            Inventor.RevitExport revitExport = null;
            Inventor.RevitExportDefinition revitExportDef = null;
            bool mNewExportDef = false;
            string mExpFileName = mDocPath + ".rvt";
            foreach (Inventor.RevitExport rvtFeature in mAsmDoc.ComponentDefinition.RevitExports)
            {
                if (rvtFeature.Name == mExpFileName)
                {
                    //rvtFeature = rvtFeature;
                    revitExportDef = rvtFeature.Definition;
                    break;
                }
            }

            // create new export definition if not existing
            if (revitExportDef == null)
            {
                mNewExportDef = true;
                revitExportDef = mAsmDoc.ComponentDefinition.RevitExports.CreateDefinition();

                // derive path and file name from source file mDoc
                revitExportDef.Location = System.IO.Path.GetDirectoryName(mDocPath);
                revitExportDef.FileName = mExpFileName;

                // read preset from settings file
                Dictionary<string, Dictionary<string, string>> mPresets = new Dictionary<string, Dictionary<string, string>>();
                Dictionary<string, object> mPresetObjects = new Dictionary<string, object>();

                mPresets = mGetRevitPresets();
                mPresetObjects = mReadPresetMap();

                // apply preset settings
                if (mPresets != null)
                {
                    foreach (var preset in mPresets[settings.InventorPresetName])
                    {
                        // case selection for all known preset settings
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
                                revitExportDef.RemovePartsSize = Convert.ToDouble(preset.Value);
                                break;
                            case "REMOVE_HOLE_SELECTOR":
                                revitExportDef.RemoveHolesStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                break;
                            case "MAX_DIAMETER_RVEC";
                                revitExportDef.RemoveHolesDiameterRange = Convert.ToDouble(preset.Value);
                                break;
                            case "REMOVE_FILLET_SELECTOR":
                                revitExportDef.RemoveFilletsStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                break;
                            case "MAX_RADIUS_RVEC":
                                revitExportDef.RemoveFilletsRadiusRange = Convert.ToDouble(preset.Value);
                                break;
                            case "REMOVE_CHAMFER_SELECTOR":
                                revitExportDef.RemoveChamfersStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                break;
                            case "MAX_DISTANCE_RVEC":
                                revitExportDef.RemoveChamfersDistanceRange = Convert.ToDouble(preset.Value);
                                break;
                            case "REMOVE_POCKET_SELECTOR":
                                revitExportDef.RemovePocketsStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                break;
                            case "MAX_LOOP_RVEC":
                                revitExportDef.RemovePocketsMaxDepthRange = Convert.ToDouble(preset.Value);
                                break;
                            case "REMOVE_EMBOSS_SELECTOR":
                                revitExportDef.RemoveEmbossesStyle = (Inventor.SimplificationRemoveStyleEnum)mPresetObjects[preset.Value];
                                break;
                            case "MAX_HEIGHT_RVEC":
                                revitExportDef.RemoveEmbossMaxHeightRange = Convert.ToDouble(preset.Value);
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
                            case "EMOVE_INTERNAL_PARTS_TOGGLE":
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
                    // Input
                    revitExportDef.IsAssociativeDesignView = false;
                    // Envelopes
                    revitExportDef.EnvelopesReplaceStyle = Inventor.EnvelopesReplaceStyleEnum.kNoneReplaceStyle; //118785 No enveloping
                    // Part removal
                    revitExportDef.RemovePartsBySize = true;
                    revitExportDef.RemovePartsSize = 1.0; // 1 cm
                    // Feature removal
                    revitExportDef.RemoveHolesStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveByRange; //118787 Remove in range
                    revitExportDef.RemoveHolesDiameterRange = 1.0; // 1 cm
                    revitExportDef.RemoveFilletsStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll; //118786 Remove all
                    revitExportDef.RemoveFilletsRadiusRange = 1.0; // 1 cm
                    revitExportDef.RemoveChamfersStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll; //118786 Remove all
                    revitExportDef.RemovePocketsStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll; //118786 Remove all
                    revitExportDef.RemoveEmbossStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll; //118786 Remove all
                    revitExportDef.RemoveTunnelsStyle = Inventor.SimplificationRemoveStyleEnum.kSimplificationRemoveAll; //118786 Remove all
                    // Revit structure
                    revitExportDef.Structure = Inventor.RevitExportStructureTypeEnum.kAllInOneElementStructure; //119041 Everything structured as a single Revit element
                    revitExportDef.EnableUpdating = true;
                    // Advanced Options
                    revitExportDef.RemoveAllInternalVoids = true;
                    revitExportDef.RemoveInternalParts = true;
                    revitExportDef.UseColorOverrideFromSourceComponent = true;
                }


            }

            // create or update the export feature
            if (mNewExportDef == true)
            {
                revitExport = mAsmDoc.ComponentDefinition.RevitExports.Add(revitExportDef);
                mTrace.WriteLine("Job created new RVT export definition and feature.");
            }
            else
            {
                //delete existing export file; note the resulting file name is e.g. <assemblyfile>.iam.rvt
                if (System.IO.File.Exists(mExpFileName))
                {
                    System.IO.FileInfo fileInfo = new FileInfo(mExpFileName);
                    fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                }
                revitExport.Update();
                mTrace.WriteLine("Job updated existing RVT export definition and feature.");
            }
            // save the document to make sure the export feature is stored
            mDoc.Save2(true);
            // close the document
            mDoc.Close(true);

            // add the created file to the upload list            
            System.IO.FileInfo mExportFileInfo = new System.IO.FileInfo(mFilesToUpload.LastOrDefault());
            if (mExportFileInfo.Exists)
            {
                mFilesToUpload.Add(mExpFileName);
                mTrace.WriteLine("RVT Simplification created file: " + mFilesToUpload.LastOrDefault());
                mTrace.IndentLevel -= 1;
            }
            else
            {
                mJobInventor.mResetIpj(mSaveProject);
                throw new Exception("Validating the export file " + mExpFileName + " before upload failed.");
            }

            #endregion create RVT export

            // check in the source file, to add/update the Revit Export feature
            #region check in source file
            VDF.Currency.FilePathAbsolute vdfPath = new VDF.Currency.FilePathAbsolute(mDocPath);            
            FileIteration mUploadedFile = null;
            try
            {
                if (mFileAssocParams.Count > 0)
                {
                    mUploadedFile = connection.FileManager.CheckinFile(mNewFileIteration, "Created by job " + JOB_TYPE,
                                            false, mFileAssocParams.ToArray(), null, true, null, mFileIteration.FileClassification, false, vdfPath);
                }
                else
                {
                    mUploadedFile = connection.FileManager.CheckinFile(mNewFileIteration, "Created by job " + JOB_TYPE,
                                            false, null, null, true, null, mFileIteration.FileClassification, false, vdfPath);
                }
            }
            catch
            {
                context.Log(null, "Job could not check-in updated file: " + mUploadedFile.EntityName + ".");
                throw new Exception("Job's single task creating an RVT export from Inventor file failed: could not check-in updated source file.");
            }
            #endregion check in source file

            // process the upload of the created files
            adsktsshared.JobCommon mJobCommon = new(connection, mWsMgr, mTrace);
            mJobCommon.mUploadFiles(mFile, mFilesToUpload, settings.OutPutPath);

            // finalize log output
            mTrace.IndentLevel = 1;
            mTrace.WriteLine("Job finished all steps.");

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
                XmlNodeList settingNodes = xmlDocument.DocumentElement.SelectNodes("//OUTPUT_TYPE_SELECTOR[@Value='OUTPUT_TYPE_RVT']");
                foreach (XmlNode mNode in settingNodes)
                {
                    Dictionary<string, string> mSettings = new Dictionary<string, string>();
                    XmlNode mParentNode = mNode.ParentNode;
                    foreach (XmlNode mChildNode in mParentNode.ChildNodes)
                    {
                        mSettings.Add(mChildNode.Name, mChildNode.Attributes["Value"].Value);
                    }
                    mPresetSettings.Add(mParentNode.Attributes["Name"].Value, mSettings);
                    mRvtPresets.Add(mParentNode.Attributes["Name"].Value);
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
                mInv = MarshalCore.GetActiveObject("Inventor.Application") as Inventor.Application;
                if (mInv != null)
                {
                    mInv.Visible = true;
                    mTrace.WriteLine("Reusing running Inventor application object.");
                    return mInv;
                }
                else
                {
                    Type inventorAppType = System.Type.GetTypeFromProgID("Inventor.Application");
                    mInv = System.Activator.CreateInstance(inventorAppType) as Inventor.Application;
                    if (mInv != null)
                    {
                        mInv.Visible = false;
                        mTrace.WriteLine("Started new Inventor application object.");
                        return mInv;
                    }
                }

                if (mInv == null)
                {
                    mTrace.WriteLine("Failed to get or create Inventor application object.");
                    throw new Exception("Job failed reuse or create Inventor instance.");
                }
            }
            catch
            {
                mTrace.WriteLine("Failed to get or create Inventor application object.");
                throw new Exception("Job run into unhandled exception trying to reuse or create an Inventor instance.");
            }

            return null;
        }

        private void mCloseInventor()
        {
            if (mInv != null)
            {
                try
                {
                    mInv.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(mInv);
                    mInv = null;
                }
                catch (Exception ex)
                {
                    mTrace.WriteLine("Failed to close Inventor application object: " + ex.ToString());
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
    }
}
