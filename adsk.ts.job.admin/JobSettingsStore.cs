using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Autodesk.Connectivity.WebServicesTools;

namespace adsk.ts.job.admin
{
    /// <summary>
    /// Reads job settings from the local settings file (names, defaults, comments) and reads/writes the edited
    /// values as one Vault option per job (KnowledgeVaultService Get/SetVaultOption). The option holds an envelope
    /// { savedBy, savedUtc, settings }.
    /// </summary>
    internal static class JobSettingsStore
    {
        public const string OptionPrefix = "adsk.ts.job.settings.";

        public sealed record Info(string? SavedBy, string? SavedUtc);

        public sealed record LoadResult(List<JobSettingRow> Rows, string Source, Info Info, string? Warning);

        /// <summary>Local settings file: the Job Collection install folder first, then the folder next to this extension.</summary>
        public static string? FindLocalFile(JobDefinition job)
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string extensions = Path.Combine(programData, "Autodesk", "Vault 2027", "Extensions");
            string ownFolder = Path.GetDirectoryName(typeof(JobSettingsStore).Assembly.Location) ?? "";

            string[] candidates =
            {
                Path.Combine(extensions, "adsk.ts.job.collection", job.SettingsFileName),
                Path.Combine(extensions, job.AssemblyName, job.SettingsFileName),
                Path.Combine(ownFolder, job.SettingsFileName),
                Path.Combine(ownFolder, "..", "adsk.ts.job.collection", job.SettingsFileName),
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        /// <summary>Elements of the settings file in order; the comments preceding an element become its description.</summary>
        public static List<JobSettingRow> ReadLocalFile(string path)
        {
            XDocument doc = XDocument.Load(path);
            List<JobSettingRow> rows = new();
            List<string> comments = new();

            foreach (XNode node in doc.Root!.Nodes())
            {
                if (node is XComment comment)
                {
                    string text = string.Join(" ", comment.Value
                        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    if (text.Length > 0) comments.Add(text);
                }
                else if (node is XElement element)
                {
                    rows.Add(new JobSettingRow(element.Name.LocalName, element.Value, string.Join(Environment.NewLine, comments)));
                    comments.Clear();
                }
            }
            return rows;
        }

        public static LoadResult Load(WebServiceManager wsm, JobDefinition job)
        {
            List<JobSettingRow> rows = new();
            string? warning = null;
            string source = "Local settings file";

            string? localFile = FindLocalFile(job);
            if (localFile != null)
            {
                try { rows = ReadLocalFile(localFile); }
                catch (Exception ex) { warning = $"Could not read '{localFile}': {ex.Message}"; }
            }
            else
            {
                warning = $"Settings file '{job.SettingsFileName}' was not found; only values stored in Vault are listed.";
            }

            string? raw = wsm.KnowledgeVaultService.GetVaultOption(job.OptionName);
            Info info = new(null, null);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    JsonObject envelope = JsonNode.Parse(raw)!.AsObject();
                    info = new Info((string?)envelope["savedBy"], (string?)envelope["savedUtc"]);
                    if (envelope["settings"] is JsonObject stored)
                    {
                        foreach (KeyValuePair<string, JsonNode?> item in stored)
                        {
                            string value = (string?)item.Value ?? "";
                            JobSettingRow? row = rows.FirstOrDefault(r => r.Key == item.Key);
                            if (row == null)
                            {
                                row = new JobSettingRow(item.Key, value, "");
                                rows.Add(row);
                            }
                            row.Value = value;
                            row.OriginalValue = value;
                        }
                    }
                    source = "Vault option";
                }
                catch (Exception ex)
                {
                    warning = $"The Vault option '{job.OptionName}' is invalid and was ignored: {ex.Message}";
                }
            }

            return new LoadResult(rows, source, info, warning);
        }

        public static void Save(WebServiceManager wsm, JobDefinition job, IEnumerable<JobSettingRow> rows, string savedBy)
        {
            var svc = wsm.KnowledgeVaultService;
            string? previous = svc.GetVaultOption(job.OptionName);

            JsonObject settings = new();
            foreach (JobSettingRow row in rows) settings[row.Key] = row.Value;

            JsonObject envelope = new()
            {
                ["savedBy"] = savedBy,
                ["savedUtc"] = DateTime.UtcNow.ToString("o"),
                ["settings"] = settings,
            };

            // replace an existing option instead of updating it
            if (!string.IsNullOrWhiteSpace(previous)) svc.SetVaultOption(job.OptionName, null);

            svc.SetVaultOption(job.OptionName, envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            // never report a save that did not reach Vault
            string? stored = svc.GetVaultOption(job.OptionName);
            if (string.IsNullOrWhiteSpace(stored) || !JsonNode.DeepEquals(JsonNode.Parse(stored), envelope))
                throw new InvalidOperationException(
                    $"Vault did not store the values: reading the option '{job.OptionName}' back returned a different value.");
        }

        /// <summary>Deletes the option; the local settings file is used again.</summary>
        public static void Reset(WebServiceManager wsm, JobDefinition job)
        {
            var svc = wsm.KnowledgeVaultService;
            svc.SetVaultOption(job.OptionName, null);
        }
    }
}
