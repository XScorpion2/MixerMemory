using NAudio.CoreAudioApi;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace MixerMemory
{
    static class Extensions
    {
        private static readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

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
            catch (Exception e)
            {
                m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(GetProductName), e.Message);
            }
            return "";
        }

        // Basic LRU cache as dealing with process is slow
        static Dictionary<string, (string, string, DateTime)> m_NameAndPathCache = new Dictionary<string, (string, string, DateTime)>();

        public static void PruneNameAndPathCache()
        {
            TimeSpan ejectionTime = new TimeSpan(0, 5, 0);
            var keys = m_NameAndPathCache.Where(x => x.Value.Item3 - DateTime.Now > ejectionTime).Select(x => x.Key);
            foreach (var key in keys)
                m_NameAndPathCache.Remove(key);
        }

        public static bool GetFriendlyDisplayNameAndApplicationPath(this AudioSessionControl session, out string displayName, out string applicationPath)
        {
            if (session.IsSystemSoundsSession)
            {
                displayName = "System Sound";
                applicationPath = "System Sound";
                return true;
            }

            if (m_NameAndPathCache.TryGetValue(session.GetSessionIdentifier, out var results))
            {
                displayName = results.Item1;
                applicationPath = results.Item2;
                results.Item3 = DateTime.Now;
                m_NameAndPathCache[session.GetSessionIdentifier] = results;
                return true;
            }

            displayName = session.DisplayName;
            applicationPath = session.IconPath;
            bool result = true;
            try
            {
                var process = Process.GetProcessById((int)session.GetProcessID);

                applicationPath = process.GetMainModuleFileName();
                // process.MainModule.FileName is identical to the GetMainModuleFileName extension method
                // but the extension method was witten to be more flexible and not throw an exception.
                // so if the extension method returns null, then we don't have access, simple as that.
                // There are no fallbacks worth exploring here.

                displayName = process.GetProductName();
                if (string.IsNullOrEmpty(displayName))
                    displayName = process.MainWindowTitle;
                if (string.IsNullOrEmpty(displayName))
                    displayName = process.ProcessName;
                if (string.IsNullOrEmpty(displayName))
                    displayName = Path.GetFileNameWithoutExtension(applicationPath);
            }
            catch (Exception e)
            {
                m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(GetFriendlyDisplayNameAndApplicationPath), e.Message);
                result = false;
            }

            if (string.IsNullOrEmpty(displayName))
                displayName = "Unnamed";

            if (string.IsNullOrEmpty(applicationPath))
                applicationPath = "Unavailable";

            m_NameAndPathCache[session.GetSessionIdentifier] = (displayName, applicationPath, DateTime.Now);
            return result;
        }
    }
}
