using System.Collections.Generic;
using System.Drawing;

using Autodesk.Connectivity.Explorer.Extensibility;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionCommandBuilder
    {
        public static IEnumerable<CommandItem> CreateJobCommands()
        {
            List<CommandItem> commands = new List<CommandItem>();

            foreach (JobSubmissionDefinition definition in JobSubmissionCatalog.All)
            {
                CommandItem commandItem = new CommandItem(definition.CommandId, definition.MenuLabel)
                {
                    NavigationTypes = new[] { SelectionTypeId.File },
                    MultiSelectEnabled = true,
                    Hint = BuildHint(definition),
                };

                Image? menuIcon = JobSubmissionIcons.GetMenuIcon(definition.MenuIconKey);
                if (menuIcon != null)
                {
                    commandItem.Image = menuIcon;
                }

                commandItem.Execute += (sender, args) =>
                {
                    JobSubmissionService.QueueForSelection(args.Context, definition);
                };

                commands.Add(commandItem);
            }

            return commands;
        }

        public static CommandSite CreateActionsMenuSite()
        {
            CommandSite site = new CommandSite("Menu.ActionMenu.TSJobSubmission", "TS Job Submission")
            {
                Location = CommandSiteLocation.ActionsMenu,
                DeployAsPulldownMenu = true,
            };

            foreach (CommandItem commandItem in CreateJobCommands())
            {
                site.AddCommand(commandItem);
            }

            return site;
        }

        public static CommandSite CreateFileContextMenuSite()
        {
            CommandSite site = new CommandSite("Menu.File.ContextMenu.TSJobSubmission", "TS Job Submission")
            {
                Location = CommandSiteLocation.FileContextMenu,
                DeployAsPulldownMenu = true,
            };

            foreach (CommandItem commandItem in CreateJobCommands())
            {
                site.AddCommand(commandItem);
            }

            return site;
        }

        private static string BuildHint(JobSubmissionDefinition definition)
        {
            if (definition.AllowedExtensions == null || definition.AllowedExtensions.Count < 1)
            {
                return definition.MenuHint;
            }

            return definition.MenuHint + " Required extensions: "
                + string.Join(", ", definition.AllowedExtensions) + ".";
        }
    }
}
