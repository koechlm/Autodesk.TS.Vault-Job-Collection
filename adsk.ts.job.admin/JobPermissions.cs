using Autodesk.Connectivity.Explorer.Extensibility;

namespace adsk.ts.job.admin
{
    /// <summary>
    /// Vault system permissions that gate the job settings: 77 = Vault Get Options (open the dialog),
    /// 76 = Vault Set Options (edit and save).
    /// </summary>
    internal readonly record struct JobPermissions(bool CanGet, bool CanSet)
    {
        public const long SetOptionsId = 76;
        public const long GetOptionsId = 77;

        public static readonly JobPermissions None = new(false, false);

        /// <summary>Calls the Vault server. A failure means no permission.</summary>
        public static JobPermissions Read(IApplication app)
        {
            try
            {
                var conn = app.Connection;
                var perms = conn.WebServiceManager.AdminService.GetPermissionsByUserId(conn.UserID)
                            ?? Array.Empty<Autodesk.Connectivity.WebServices.Permis>();
                return new JobPermissions(perms.Any(p => p.Id == GetOptionsId), perms.Any(p => p.Id == SetOptionsId));
            }
            catch
            {
                return None;
            }
        }
    }
}
