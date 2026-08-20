using System.Threading;

#nullable enable

namespace adsk.ts.pdf.create.office
{
    /// <summary>
    /// Serializes Office and LibreOffice conversions on the Job Processor machine.
    /// </summary>
    internal static class OfficeConversionSync
    {
        private static readonly SemaphoreSlim ConversionLock = new SemaphoreSlim(1, 1);

        public static void Enter()
        {
            ConversionLock.Wait();
        }

        public static void Exit()
        {
            ConversionLock.Release();
        }
    }
}
