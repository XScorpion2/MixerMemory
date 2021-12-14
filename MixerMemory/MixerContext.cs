using NLog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MixerMemory
{
    public class MixerContext : ApplicationContext
    {
        private MixerMemory m_MixerMemory;
        private MixerDevice m_MixerDevice;
        private IContainer m_Components;
        private NotifyIcon m_NotifyIcon;
        private Timer m_Timer;

        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        public MixerContext()
        {
            m_MixerMemory = new MixerMemory();
            m_MixerDevice = new MixerDevice(m_MixerMemory.SetCatagoryVolume);

            m_Components = new Container();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Log", null, OpenLog);
            contextMenu.Items.Add("Open Config", null, OpenConfig);
            contextMenu.Items.Add("Reload Config", null, ReloadConfig);
            contextMenu.Items.Add("Restore Volumes", null, RestoreVolumes);
            contextMenu.Items.Add("Refresh Device", null, RefreshDevice);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, Exit);

            m_NotifyIcon = new NotifyIcon(m_Components)
            {
                Icon = new Icon("volume_control.ico"),
                Text = "Volume Mixer Memory System",
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            m_Timer = new Timer(m_Components);
            m_Timer.Tick += (s, e) => m_MixerMemory.RestoreVolumes();
            m_Timer.Interval = 300000;
            m_Timer.Start();
        }

        private void OpenLog(object sender, EventArgs e)
        {
            m_Logger.Info("{functionName} requested.", nameof(OpenLog));
            var appPath = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(appPath);
            Process.Start(Path.Combine(dir, "info.log"));
        }

        private void OpenConfig(object sender, EventArgs e)
        {
            m_Logger.Info("{functionName} requested.", nameof(OpenConfig));
            var appPath = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(appPath);
            Process.Start(Path.Combine(dir, MixerMemory.k_ConfigJson));
        }

        private void ReloadConfig(object sender, EventArgs e)
        {
            m_Logger.Info("{functionName} requested.", nameof(ReloadConfig));
            m_MixerMemory.LoadVolumes();
        }

        private void RestoreVolumes(object sender, EventArgs e)
        {
            m_Logger.Info("{functionName} requested.", nameof(RestoreVolumes));
            m_MixerMemory.RestoreVolumes();
        }

        private void RefreshDevice(object sender, EventArgs e)
        {
            m_Logger.Info("{functionName} requested.", nameof(RefreshDevice));
            m_MixerMemory.RefreshDevice();
        }

        private void Exit(object sender, EventArgs e)
        {
            m_Logger.Info("{functionName} requested.", nameof(Exit));
            m_MixerDevice.Dispose();
            m_NotifyIcon.Visible = false;
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && m_Components != null)
            {
                m_Components.Dispose();
                m_Components = null;
            }
            base.Dispose(disposing);
        }
    }
}
