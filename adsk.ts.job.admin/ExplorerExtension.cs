using Autodesk.Connectivity.Explorer.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ACW = Autodesk.Connectivity.WebServices;
using VDF = Autodesk.DataManagement.Client.Framework;
using VDFV = Autodesk.DataManagement.Client.Framework.Vault;

// These 5 assembly attributes must be specified or your extension will not load. 
// [assembly: AssemblyCompany("MyCompany")] // moved to .NET project file
// [assembly: AssemblyProduct("Command Extension Template")] // moved to .NET project file
[assembly: AssemblyDescription("Sample App Template")]

// The extension ID needs to be unique for each extension.  
// Make sure to generate your own ID when writing your own extension. 
[assembly: Autodesk.Connectivity.Extensibility.Framework.ExtensionId("3C180252-E353-4037-8AF7-4634DA5F7E5B")]

// This number gets incremented for each Vault release.
// *ComponentUpgradeEveryRelease-Client*
[assembly: Autodesk.Connectivity.Extensibility.Framework.ApiVersion("20.0")]


namespace adsk.ts.job.admin
{
    /// <summary>The Vault Explorer main window as owner of the dialog.</summary>
    internal sealed class ExplorerWindow : IWin32Window
    {
        public IntPtr Handle { get; } = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    }

    public class ExplorerExtension : IExplorerExtension
    {
        /// <summary>
        /// This function tells Vault Explorer what custom commands this extension provides.
        /// Part of the IExtension interface.
        /// </summary>
        /// <returns>A collection of CommandSites, which are collections of custom commands.</returns>
        public IEnumerable<CommandSite>? CommandSites()
        {
            CommandItem adminCommand = new CommandItem("adsk.ts.job.admin.ShowJobAdmin", "Job Collection Configuration...")
            {
                MultiSelectEnabled = false,
                Hint = "Configure the settings of the Technical Sales Job Collection.",
            };

            adminCommand.Execute += (sender, args) =>
            {
                JobPermissions permissions = JobPermissions.Read(args.Context.Application);
                if (!permissions.CanGet)
                {
                    VDF.Forms.Library.ShowMessage("Viewing the job settings requires the Vault permission 'Vault Get Options'.",
                        "Job Collection", VDF.Forms.Currency.ButtonConfiguration.Ok);
                    return;
                }

                using (XtraFrmJobAdmin dialog = new XtraFrmJobAdmin(args.Context.Application, permissions.CanSet))
                {
                    // owned by the Vault Explorer main window, so it is not a separate taskbar entry
                    dialog.ShowDialog(new ExplorerWindow());
                }
            };

            CommandSite toolsSite = new CommandSite("adsk.ts.job.admin.ToolsMenu", "Job Collection Configuration...")
            {
                Location = CommandSiteLocation.ToolsMenu,
                DeployAsPulldownMenu = false,
            };
            toolsSite.AddCommand(adminCommand);

            return new[] { toolsSite };
        }

        /// <summary>
        /// This function allows the extension to define special behavior for Custom Entity types.
        /// Part of the IExtension interface.
        /// </summary>
        /// <returns>A collection of CustomEntityHandler objects.  Each object defines special behavior
        /// for a specific Custom Entity type.</returns>
        public IEnumerable<CustomEntityHandler>? CustomEntityHandlers()
        {
            // return the list of your custom CustomEntityHandler objects;
            return null;
        }

        /// <summary>
        /// This function tells Vault Explorer what custom tabs this extension provides.
        /// Part of the IExtension interface.
        /// </summary>
        /// <returns>A collection of DetailTabs, each object represents a custom tab.</returns>
        public IEnumerable<DetailPaneTab>? DetailTabs()
        {
            // return the list of your custom DetailPaneTab objects;
            return null;
        }

        /// <summary>
        /// This function tells Vault Explorer what custom dock panels this extension provides.
        /// Part of the IExtension interface.
        /// </summary>
        /// <returns>A collection of DockPanel(s), each object represents a custom dock panel.</returns>
        public IEnumerable<DockPanel>? DockPanels()
        {
            // return the list of your custom DockPanel objects;
            return null;
        }

        /// <summary>
        /// This function tells Vault Exlorer which default commands should be hidden.
        /// Part of the IExtension interface.
        /// </summary>
        /// <returns>A collection of command names.</returns>
        public IEnumerable<string>? HiddenCommands()
        {
            // return the list of command names to be hidden;
            return null;
        }

        /// <summary>
        /// This function is called after the user is logged out of the Vault Server.
        /// Part of the IExtension interface.
        /// </summary>
        /// <param name="application">Provides information about the running application.</param>
        public void OnLogOff(IApplication application)
        {

        }

        /// <summary>
        /// This function is called after the user logs in to the Vault Server.
        /// Part of the IExtension interface.
        /// </summary>
        /// <param name="application">Provides information about the running application.</param>
        public void OnLogOn(IApplication application)
        {
            // MessageBox.Show("Welcome to adsk.ts.job.admin!");
        }

        /// <summary>
        /// This function is called before the application is closed.
        /// Part of the IExtension interface.
        /// </summary>
        /// <param name="application">Provides information about the running application.</param>
        public void OnShutdown(IApplication application)
        {

        }

        /// <summary>
        /// This function is called after the application starts up.
        /// Part of the IExtension interface.
        /// </summary>
        /// <param name="application">Provides information about the running application.</param>
        public void OnStartup(IApplication application)
        {

        }
    }
}
