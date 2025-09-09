using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;

namespace adsk.ts.rvt.create.inventor
{
    internal static class NativeMethods
    {
        private const string OLEAUT32 = "oleaut32.dll";
        private const string OLE32 = "ole32.dll";

        //[DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)]
        [DllImport(OLE32, PreserveSig = false)]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [SecurityCritical]  // auto-generated
        public static extern void CLSIDFromProgIDEx([MarshalAs(UnmanagedType.LPWStr)] String progId, out Guid clsid);

        //[DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)]
        [DllImport(OLE32, PreserveSig = false)]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [SecurityCritical]  // auto-generated
        public static extern void CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] String progId, out Guid clsid);

        //[DllImport(Microsoft.Win32.Win32Native.OLEAUT32, PreserveSig = false)]
        [DllImport(OLEAUT32, PreserveSig = false)]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [SecurityCritical]  // auto-generated
        public static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out Object ppunk);
    }

    // .Net Core does not have Marshal.GetActiveObject so copy its implementation from .Net framework
    internal class MarshalCore
    {
        [SecurityCritical]
        public static object GetActiveObject(String progID)
        {
            Guid clsid;

            // Call CLSIDFromProgIDEx first then fall back on CLSIDFromProgID if
            // CLSIDFromProgIDEx doesn't exist.
            try
            {
                NativeMethods.CLSIDFromProgIDEx(progID, out clsid);
            }
            catch
            {
                NativeMethods.CLSIDFromProgID(progID, out clsid);
            }

            NativeMethods.GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
            return obj;
        }
    }
}