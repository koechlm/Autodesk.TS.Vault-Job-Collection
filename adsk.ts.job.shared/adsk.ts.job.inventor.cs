using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

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

using Inventor;

namespace adsk.ts.job.shared
{
    public class InventorJob
    {
        readonly WebServiceManager _WebSrvMgr;
        readonly Connection _connection;

        public InventorJob(Connection connection, WebServiceManager webServiceManager)
        {
            // Constructor
            _WebSrvMgr = webServiceManager;
            _connection = connection;
        }


        public string mGetIpj(bool acceptLocalIpj = true)
        {
            String mIpjPath = "";
            String mWfPath = "";
            String mIpjLocalPath = "";
            ACW.File mProjFile;
            VDF.Vault.Currency.Entities.FileIteration? mIpjFileIter = null;

            try
            {
                //Download enforced ipj file
                if (_WebSrvMgr.DocumentService.GetEnforceWorkingFolder() && _WebSrvMgr.DocumentService.GetEnforceInventorProjectFile())
                {
                    mIpjPath = _WebSrvMgr.DocumentService.GetInventorProjectFileLocation();
                    mWfPath = _WebSrvMgr.DocumentService.GetRequiredWorkingFolderLocation();
                }
                else
                {
                    throw new Exception("Job requires both settings enabled: 'Enforce Workingfolder' and 'Enforce Inventor Project'.");
                }

                String[]? mIpjFullFileName = mIpjPath.Split(new string[] { "/" }, StringSplitOptions.None);
                String mIpjFileName = mIpjFullFileName?.LastOrDefault() ?? string.Empty;

                //get the projects file object for download
                ACW.PropDef[] filePropDefs = _WebSrvMgr.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
                ACW.PropDef mNamePropDef = filePropDefs.Single(n => n.SysName == "ClientFileName");
                ACW.SrchCond mSrchCond = new ACW.SrchCond()
                {
                    PropDefId = mNamePropDef.Id,
                    PropTyp = ACW.PropertySearchType.SingleProperty,
                    SrchOper = 3, // is equal
                    SrchRule = ACW.SearchRuleType.Must,
                    SrchTxt = mIpjFileName
                };
                string bookmark = string.Empty;
                ACW.SrchStatus? status = null;
                List<ACW.File> totalResults = new List<ACW.File>();
                while (status == null || totalResults.Count < status.TotalHits)
                {
                    ACW.File[] results = _WebSrvMgr.DocumentService.FindFilesBySearchConditions(new ACW.SrchCond[] { mSrchCond },
                        null, null, false, true, ref bookmark, out status);
                    if (results != null)
                        totalResults.AddRange(results);
                    else
                        break;
                }
                if (totalResults.Count == 1)
                {
                    mProjFile = totalResults[0];
                }
                else
                {
                    throw new Exception("Job execution stopped due to ambigous project file definitions; single project file per Vault expected");
                }

                //define download settings for the project file
                VDF.Vault.Settings.AcquireFilesSettings mDownloadSettings_IPJ = new VDF.Vault.Settings.AcquireFilesSettings(_connection);
                mDownloadSettings_IPJ.LocalPath = new VDF.Currency.FolderPathAbsolute(mWfPath);
                mIpjFileIter = new VDF.Vault.Currency.Entities.FileIteration(_connection, mProjFile);
                mDownloadSettings_IPJ.AddFileToAcquire(mIpjFileIter, VDF.Vault.Settings.AcquireFilesSettings.AcquisitionOption.Download);

                //download project file and get local path
                VDF.Vault.Results.AcquireFilesResults mDownLoadResult;
                VDF.Vault.Results.FileAcquisitionResult? fileAcquisitionResult;
                mDownLoadResult = _connection.FileManager.AcquireFiles(mDownloadSettings_IPJ);
                fileAcquisitionResult = mDownLoadResult?.FileResults.FirstOrDefault();
                if (fileAcquisitionResult != null)
                {
                    mIpjLocalPath = fileAcquisitionResult.LocalPath.FullPath;
                }
                else
                {
                    //let's check for allowance that an existing to be consumed
                    if (acceptLocalIpj == true && System.IO.File.Exists(mDownloadSettings_IPJ.LocalPath.ToString()))
                    {
                        mIpjLocalPath = mDownloadSettings_IPJ.LocalPath.ToString() + mIpjFileName;
                    }
                    else
                    {
                        throw new Exception("Job stopped execution as the project file to translate did not download");
                    }
                }
                return mIpjLocalPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Job was not able to activate Inventor project file. - Note: The ipj must not be checked out by another user.", ex.InnerException);
            }
        }

        public void mResetIpj(DesignProject previousIpj)
        {
            if (previousIpj != null)
            {
                previousIpj.Activate();
            }
        }
    }
}
