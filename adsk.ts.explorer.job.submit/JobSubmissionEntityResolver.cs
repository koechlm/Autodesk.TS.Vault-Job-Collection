using System;

using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.Explorer.Extensibility;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionEntityResolver
    {
        /// <summary>
        /// Resolves the file iteration id to store in job param EntityId.
        /// Explorer selections may expose either a file iteration id or a file master id.
        /// </summary>
        public static bool TryResolveFileIterationId(
            ACW.DocumentService documentService,
            ISelection selection,
            out long fileIterationId,
            out string? errorMessage)
        {
            fileIterationId = 0;
            errorMessage = null;

            if (documentService == null || selection == null)
            {
                errorMessage = "Selection is not available.";
                return false;
            }

            if (TryGetFileIterationById(documentService, selection.Id, out fileIterationId))
            {
                return true;
            }

            try
            {
                ACW.File latest = documentService.GetLatestFileByMasterId(selection.Id);
                if (latest != null)
                {
                    fileIterationId = latest.Id;
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = FormatVaultError(ex);
                return false;
            }

            errorMessage = "Could not resolve file id for " + selection.Label + ".";
            return false;
        }

        private static bool TryGetFileIterationById(
            ACW.DocumentService documentService,
            long selectionId,
            out long fileIterationId)
        {
            fileIterationId = 0;

            try
            {
                ACW.File file = documentService.GetFileById(selectionId);
                if (file == null)
                {
                    return false;
                }

                fileIterationId = file.Id;
                return true;
            }
            catch (ACW.VaultServiceErrorException ex) when (ex.ErrorCode == 1013)
            {
                return false;
            }
            catch (ACW.VaultServiceErrorException)
            {
                throw;
            }
        }

        private static string FormatVaultError(Exception ex)
        {
            if (ex is ACW.VaultServiceErrorException vaultError)
            {
                return vaultError.ErrorCode.ToString();
            }

            return ex.Message;
        }
    }
}
