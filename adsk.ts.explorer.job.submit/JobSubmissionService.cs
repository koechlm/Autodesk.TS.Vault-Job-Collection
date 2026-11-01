using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.Explorer.Extensibility;
using VDF = Autodesk.DataManagement.Client.Framework;
using ButtonConfiguration = Autodesk.DataManagement.Client.Framework.Forms.Currency.ButtonConfiguration;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionService
    {
        private const int DefaultJobPriority = 1;
        private const string EntityIdParamName = "EntityId";
        private const string EntityClassIdParamName = "EntityClassId";
        public static void QueueForSelection(ICommandContext context, JobSubmissionDefinition definition)
        {
            if (context?.Application?.Connection == null)
            {
                VDF.Forms.Library.ShowError("Vault connection is not available.", "TS Job Submission");
                return;
            }

            if (!JobSubmissionSelectionValidator.CanQueue(context, definition))
            {
                VDF.Forms.Library.ShowWarning(
                    "The current selection is not valid for " + definition.MenuLabel + ".",
                    "TS Job Submission",
                    ButtonConfiguration.Ok);
                return;
            }

            VDF.Vault.Currency.Connections.Connection connection = context.Application.Connection;
            ACW.JobService jobService = connection.WebServiceManager.JobService;
            ACW.DocumentService documentService = connection.WebServiceManager.DocumentService;

            int queuedCount = 0;
            int duplicateCount = 0;
            List<string> failures = new List<string>();

            foreach (ISelection selection in context.CurrentSelectionSet.Where(s => s.TypeId == SelectionTypeId.File))
            {
                if (!JobSubmissionEntityResolver.TryResolveFileIterationId(
                        documentService,
                        selection,
                        out long fileIterationId,
                        out string? resolveError))
                {
                    failures.Add(selection.Label + ": " + (resolveError ?? "Could not resolve file id."));
                    continue;
                }

                try
                {
                    ACW.JobParam[] parameters =
                    {
                        new ACW.JobParam
                        {
                            Name = EntityIdParamName,
                            Val = fileIterationId.ToString(),
                        },
                        new ACW.JobParam
                        {
                            Name = EntityClassIdParamName,
                            Val = "FILE",
                        },
                    };

                    string description = string.Format(
                        "Manually queued {0} for job {1}.",
                        selection.Label,
                        definition.JobTypeId);

                    jobService.AddJob(definition.JobTypeId, description, parameters, DefaultJobPriority);
                    queuedCount++;
                }
                catch (Exception ex)
                {
                    if (JobSubmissionExceptionHelper.IsDuplicateJobError(ex))
                    {
                        duplicateCount++;
                        continue;
                    }

                    failures.Add(selection.Label + ": " + JobSubmissionExceptionHelper.FormatForDisplay(ex));
                }
            }

            ShowSubmissionSummary(definition.MenuLabel, queuedCount, duplicateCount, failures);
        }

        private static void ShowSubmissionSummary(
            string menuLabel,
            int queuedCount,
            int duplicateCount,
            IReadOnlyList<string> failures)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Job: " + menuLabel);
            message.AppendLine("Queued: " + queuedCount);

            if (duplicateCount > 0)
            {
                message.AppendLine("Skipped duplicates: " + duplicateCount);
            }

            if (failures.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Failures:");
                foreach (string failure in failures)
                {
                    message.AppendLine(" - " + failure);
                }
            }

            string summary = message.ToString().TrimEnd();
            if (failures.Count > 0 && queuedCount < 1)
            {
                VDF.Forms.Library.ShowError(summary, "TS Job Submission");
                return;
            }

            if (failures.Count > 0 || duplicateCount > 0)
            {
                VDF.Forms.Library.ShowWarning(summary, "TS Job Submission", ButtonConfiguration.Ok);
                return;
            }

            VDF.Forms.Library.ShowMessage(summary, "TS Job Submission", ButtonConfiguration.Ok);
        }
    }
}
