using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.Explorer.Extensibility;
using ACJE = Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using ACWT = Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace adsk.ts.job.collection.user
{
    class ExplorerExtension : IExplorerExtension
    {
        public IEnumerable<CommandSite> CommandSites()
        {
            List<CommandSite> mJobSelectCmdSites = new List<CommandSite>();

            // describe user command item add job to queue
            CommandItem mQueueExportJobCmdItem = new CommandItem("Command.JobCollection_AddJob", "Queue Export Sample Job(s)")
            {
                NavigationTypes = new SelectionTypeId[] { SelectionTypeId.File },
                MultiSelectEnabled = true,
                //Image = Properties.Resources.iLogic_Browser_transparent
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
            // do nothing
        }
    }
}
