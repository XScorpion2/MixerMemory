using System.Windows.Forms;
using System.ComponentModel;
using System.Drawing;
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace MixerMemory
{
    public class MixerContext : ApplicationContext
    {
        private MixerMemory m_MixerMemory;
        private IContainer m_Components;
        private NotifyIcon m_NotifyIcon;
        private Timer m_Timer;

        public MixerContext()
        {
            m_MixerMemory = new MixerMemory();

            m_Components = new Container();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Log", null, OpenLog);
            contextMenu.Items.Add("Open Config", null, OpenConfig);
            contextMenu.Items.Add("Restore Volumes", null, (s, e) => m_MixerMemory.RestoreVolumes());
            contextMenu.Items.Add("Reload Volumes", null, (s, e) => m_MixerMemory.LoadVolumes());
            contextMenu.Items.Add("Refresh Device", null, (s, e) => m_MixerMemory.RefreshDevice());
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
            var appPath = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(appPath);
            Process.Start(Path.Combine(dir, "info.log"));
        }

        private void OpenConfig(object sender, EventArgs e)
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(appPath);
            Process.Start(Path.Combine(dir, MixerMemory.k_ConfigJson));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && m_Components != null)
            {
                m_Components.Dispose();
                m_Components = null;
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            m_NotifyIcon.Visible = false;
            ExitThread();
        }
    }
}
