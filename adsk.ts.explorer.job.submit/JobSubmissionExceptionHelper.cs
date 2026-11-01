using System;

using ACW = Autodesk.Connectivity.WebServices;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal static class JobSubmissionExceptionHelper
    {
        private const int DuplicateJobErrorCode = 237;

        public static bool IsDuplicateJobError(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is ACW.VaultServiceErrorException vaultError
                    && vaultError.ErrorCode == DuplicateJobErrorCode)
                {
                    return true;
                }

                if (string.Equals(current.Message, DuplicateJobErrorCode.ToString(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static string FormatForDisplay(Exception ex)
        {
            if (ex is ACW.VaultServiceErrorException vaultError)
            {
                return vaultError.ErrorCode.ToString();
            }

            return ex.Message;
        }
    }
}
