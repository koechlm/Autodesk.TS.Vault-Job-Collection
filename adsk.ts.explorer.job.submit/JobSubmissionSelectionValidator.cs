using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Connectivity.Explorer.Extensibility;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionSelectionValidator
    {
        public static bool CanQueue(ICommandContext context, JobSubmissionDefinition definition)
        {
            if (context?.CurrentSelectionSet == null)
            {
                return false;
            }

            List<ISelection> allSelections = context.CurrentSelectionSet.ToList();
            if (allSelections.Count < 1)
            {
                return false;
            }

            List<ISelection> fileSelections = allSelections
                .Where(IsFileSelection)
                .ToList();

            if (fileSelections.Count < 1 || fileSelections.Count != allSelections.Count)
            {
                return false;
            }

            if (definition.AllowedExtensions == null || definition.AllowedExtensions.Count < 1)
            {
                return true;
            }

            return fileSelections.All(selection => MatchesAllowedExtension(selection, definition.AllowedExtensions));
        }

        private static bool IsFileSelection(ISelection selection)
        {
            return selection != null && selection.TypeId == SelectionTypeId.File;
        }

        private static bool MatchesAllowedExtension(ISelection selection, IReadOnlyList<string> allowedExtensions)
        {
            string extension = Path.GetExtension(selection.Label);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            return allowedExtensions.Any(
                candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
