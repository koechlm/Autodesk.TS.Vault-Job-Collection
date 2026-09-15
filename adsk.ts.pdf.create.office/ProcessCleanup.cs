using System;
using System.Diagnostics;
using System.Linq;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    internal static class ProcessCleanup
    {
        public static int[] CaptureProcessIds(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Select(process => process.Id).ToArray();
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        public static void TerminateNewProcesses(
            string processName,
            int[] processIdsBefore,
            TextWriterTraceListener? trace)
        {
            int[] knownProcessIds = processIdsBefore ?? Array.Empty<int>();
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                foreach (Process process in processes)
                {
                    if (knownProcessIds.Contains(process.Id))
                    {
                        continue;
                    }

                    TerminateProcessTree(process, trace);
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        public static void TerminateProcessTree(Process? process, TextWriterTraceListener? trace)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                trace?.WriteLine("Cleaning up process " + process.ProcessName + " (PID " + process.Id + ").");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                trace?.WriteLine(
                    "Failed to clean up process " + process.ProcessName + " (PID " + process.Id + "): " + ex.Message);
            }
        }
    }
}
