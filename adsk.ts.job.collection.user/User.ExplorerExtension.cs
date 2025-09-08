using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

using Autodesk.Connectivity.Explorer.Extensibility;
using ACJE = Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using ACWT = Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using System.IO;

namespace adsk.ts.job.collection.user
{
    class ExplorerExtension : IExplorerExtension
    {
        // Vault themes support
        private string mCurrentTheme;
        // set the default icon for the command item(s)
        private Image mIcon = ConvertByteArrayToImage(Properties.Resources.AddToList_16_light);

        public IEnumerable<CommandSite> CommandSites()
        {
            List<CommandSite> mJobSelectCmdSites = new List<CommandSite>();

            // describe user command item add job to queue
            CommandItem mQueueExportJobCmdItem = new CommandItem("Command.JobCollection_AddJob", "Queue Export Sample Job(s)")
            {
                NavigationTypes = new SelectionTypeId[] { SelectionTypeId.File },
                MultiSelectEnabled = true,
                // set the icon
                Image = mIcon
            };
            mQueueExportJobCmdItem.Execute += mQueueExportJobCmdHndlr;

            // deploy add job to queue command on file context menu
            CommandSite mQueueExportJobContextMenu = new CommandSite("Menu.ActionMenu", "Queue Export Sample Job(s)")
            {
                Location = CommandSiteLocation.ActionsMenu,
                DeployAsPulldownMenu = false
            };
            mQueueExportJobContextMenu.AddCommand(mQueueExportJobCmdItem);
            mJobSelectCmdSites.Add(mQueueExportJobContextMenu);

            return mJobSelectCmdSites;
        }

        private void mQueueExportJobCmdHndlr(object sender, CommandItemEventArgs e)
        {
            XtraForm_JobUser jobUserForm = new();

            // get the selected files from the explorer
            foreach (ISelection vaultObj in e.Context.CurrentSelectionSet)
            {
                ACW.File mFile = (ACW.File)e.Context.Application.Connection.WebServiceManager.DocumentService.GetLatestFileByMasterId(vaultObj.Id);
                string filename = mFile.Name;

                // add the selected file to the job user form list
                jobUserForm.AddFileToList(mFile.Id, filename);
            }


            // show the job user form

            jobUserForm.ShowDialog();

            // get the selected list of jobs from the form

        }

        private void mJobAdminCmdHndlr(object sender, CommandItemEventArgs e)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<CustomEntityHandler> CustomEntityHandlers()
        {
            return null;
        }

        public IEnumerable<DetailPaneTab> DetailTabs()
        {
            return null;
        }

        public IEnumerable<DockPanel> DockPanels()
        {
            return null;
        }

        public IEnumerable<string> HiddenCommands()
        {
            return null;
        }

        public void OnLogOff(IApplication application)
        {
            // do nothing
        }

        public void OnLogOn(IApplication application)
        {
            // do nothing
        }

        public void OnShutdown(IApplication application)
        {
            // do nothing
        }

        public void OnStartup(IApplication application)
        {
            // register the last used theme
            mCurrentTheme = VDF.Forms.SkinUtils.WinFormsTheme.Instance.CurrentTheme.ToString();
            if (mCurrentTheme == VDF.Forms.SkinUtils.Theme.Light.ToString())
            {
                this.mIcon = ConvertByteArrayToImage(Properties.Resources.AddToList_16_light);
            }
            else
            {
                this.mIcon = ConvertByteArrayToImage(Properties.Resources.AddToList_16_dark);
            }
        }

        internal static Image ConvertByteArrayToImage(byte[] byteArray)
        {
            using (MemoryStream ms = new MemoryStream(byteArray))
            {
                return Image.FromStream(ms);
            }
        }
    }
}
