using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MixerMemory
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var mutexName = $"Local\\{Assembly.GetExecutingAssembly().GetType().GUID}";
            using (var mutex = new Mutex(false, mutexName, out var newSingleInstance))
            {
                if (!newSingleInstance)
                    return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MixerContext());
            }
        }
    }
}
