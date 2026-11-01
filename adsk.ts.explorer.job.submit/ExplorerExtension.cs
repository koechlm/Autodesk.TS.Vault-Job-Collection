using System.Collections.Generic;

using Autodesk.Connectivity.Explorer.Extensibility;
using Autodesk.Connectivity.Extensibility.Framework;

#nullable enable

[assembly: ApiVersion("20.0")]
[assembly: ExtensionId("f1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5d")]

namespace adsk.ts.explorer.job.submit
{
    public class ExplorerExtension : IExplorerExtension
    {
        public IEnumerable<CommandSite> CommandSites()
        {
            return new[]
            {
                JobSubmissionCommandBuilder.CreateActionsMenuSite(),
                JobSubmissionCommandBuilder.CreateFileContextMenuSite(),
            };
        }

        public IEnumerable<CustomEntityHandler> CustomEntityHandlers()
        {
            return null!;
        }

        public IEnumerable<DetailPaneTab> DetailTabs()
        {
            return null!;
        }

        public IEnumerable<DockPanel> DockPanels()
        {
            return null!;
        }

        public IEnumerable<string> HiddenCommands()
        {
            return null!;
        }

        public void OnLogOff(IApplication application)
        {
        }

        public void OnLogOn(IApplication application)
        {
        }

        public void OnShutdown(IApplication application)
        {
        }

        public void OnStartup(IApplication application)
        {
        }
    }
}
