using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MixerMemory
{
    static class Extensions
    {
        private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int SYNCHRONIZE = 0x00100000;

        [DllImport("Kernel32.dll")]
        private static extern IntPtr OpenProcess(int access, bool inheritHandle, int processID);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

        public static string GetMainModuleFileName(this Process process, int buffer = 1024)
        {
            var fileNameBuilder = new StringBuilder(buffer);
            uint bufferLength = (uint)fileNameBuilder.Capacity + 1;
            IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, false, process.Id);
            return QueryFullProcessImageName(handle, 0, fileNameBuilder, ref bufferLength) ? fileNameBuilder.ToString() : null;
        }

        public static string GetProductName(this Process process)
        {

            string fileName = process.GetMainModuleFileName();
            if (string.IsNullOrEmpty(fileName)) return null;
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(fileName);
                return versionInfo.ProductName;
            }
            catch
            {
                return "";
            }
        }

        public static string GetFriendlyDisplayName(this AudioSessionControl session)
        {
            if (session.IsSystemSoundsSession)
                return "System Sound";

            if (!string.IsNullOrEmpty(session.DisplayName))
                return session.DisplayName;

            try
            {
                var process = Process.GetProcessById((int)session.GetProcessID);
                var displayName = process.GetProductName();
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
                displayName = process.MainWindowTitle;
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
                displayName = process.ProcessName;
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
                displayName = process.GetMainModuleFileName();
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
            }
            catch { /* don't fucking care */ }
            // ExtractAppPath from SessionIdentifier
            return "Unnamed";
        }
    }
}
