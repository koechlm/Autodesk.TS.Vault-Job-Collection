using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.Serialization;
using Autodesk.Connectivity.WebServicesTools;

namespace adsk.ts.job.shared
{
    /// <summary>
    /// Overlays the settings an administrator saved as Vault option (adsk.ts.job.admin extension) onto a job's
    /// Settings object. The local settings xml is the default and fallback; it is never written by this class.
    /// Option: "adsk.ts.job.settings.&lt;job assembly name&gt;" = { savedBy, savedUtc, settings: { Element: value } }.
    /// </summary>
    public static class JobSettingsOption
    {
        public const string OptionPrefix = "adsk.ts.job.settings.";

        /// <summary>
        /// Sets every public [XmlElement] field of <paramref name="settings"/> that has a value in the Vault option.
        /// Returns true if an option was applied; no option or a failing read leaves the settings unchanged.
        /// </summary>
        public static bool Apply(WebServiceManager wsm, string jobAssemblyName, object settings)
        {
            try
            {
                string? raw = wsm.KnowledgeVaultService.GetVaultOption(OptionPrefix + jobAssemblyName);
                if (string.IsNullOrWhiteSpace(raw)) return false;

                if (JsonNode.Parse(raw) is not JsonObject envelope || envelope["settings"] is not JsonObject stored)
                    return false;

                foreach (FieldInfo field in settings.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType != typeof(string)) continue;

                    string name = field.GetCustomAttribute<XmlElementAttribute>()?.ElementName ?? field.Name;
                    if (stored.TryGetPropertyValue(name, out JsonNode? value) && value != null)
                        field.SetValue(settings, (string?)value);
                }
                return true;
            }
            catch
            {
                // job processor user may lack the permission to read options; the local file stays in effect
                return false;
            }
        }
    }
}
