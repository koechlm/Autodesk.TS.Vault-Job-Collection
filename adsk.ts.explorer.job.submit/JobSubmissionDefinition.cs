using System.Collections.Generic;

#nullable enable

namespace adsk.ts.explorer.job.submit
{
    internal sealed class JobSubmissionDefinition
    {
        public JobSubmissionDefinition(
            string menuLabel,
            string jobTypeId,
            string commandId,
            IReadOnlyList<string>? allowedExtensions,
            string menuIconKey,
            string menuHint)
        {
            MenuLabel = menuLabel;
            JobTypeId = jobTypeId;
            CommandId = commandId;
            AllowedExtensions = allowedExtensions;
            MenuIconKey = menuIconKey;
            MenuHint = menuHint;
        }

        public string MenuLabel { get; }

        public string JobTypeId { get; }

        public string CommandId { get; }

        /// <summary>
        /// When null, any file selection is allowed (job handler applies its own rules).
        /// </summary>
        public IReadOnlyList<string>? AllowedExtensions { get; }

        /// <summary>
        /// Embedded PNG resource file name (without extension) under Resources/Icons/{Light|Dark}/.
        /// </summary>
        public string MenuIconKey { get; }

        public string MenuHint { get; }
    }
}
