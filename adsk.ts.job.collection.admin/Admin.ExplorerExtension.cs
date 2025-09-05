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


namespace adsk.ts.job.collection.admin
{
    class ExplorerExtension : IExplorerExtension
    {
        public IEnumerable<CommandSite> CommandSites()
        {
            List<CommandSite> mJobSelectCmdSites = new List<CommandSite>();

            // Describe admin command item
            CommandItem mJobAdminCmdItem = new CommandItem("Command.JobCollectionAdmin", "Job Collection Sample - Administration...")
            {
                //Image = Properties.Resources.iLogicConfigurationImg
            };
            mJobAdminCmdItem.Execute += mJobAdminCmdHndlr;

            // deploy admin on tools menu
            CommandSite mJobAdminCmdSite = new CommandSite("Menu.ToolsMenu", "Job Collection Administration")
            {
                Location = CommandSiteLocation.ToolsMenu,
                DeployAsPulldownMenu = false
            };
            mJobAdminCmdSite.AddCommand(mJobAdminCmdItem);
            mJobSelectCmdSites.Add(mJobAdminCmdSite);

            return mJobSelectCmdSites;
        }


        private void mJobAdminCmdHndlr(object sender, CommandItemEventArgs e)
        {
            
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
