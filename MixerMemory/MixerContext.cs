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
            contextMenu.Items.Add("Open", null, Open);
            contextMenu.Items.Add("Save", null, (s, e) => m_MixerMemory.Save());
            contextMenu.Items.Add("Restore", null, (s, e) => m_MixerMemory.Restore());
            contextMenu.Items.Add("Flush", null, (s, e) => m_MixerMemory.Flush());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, contextMenu_OnExit);

            m_NotifyIcon = new NotifyIcon(m_Components)
            {
                Icon = new Icon("volume_control.ico"),
                Text = "Volume Mixer Memory System",
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            m_Timer = new Timer(m_Components);
            m_Timer.Tick += (s, e) => m_MixerMemory.Restore();
            m_Timer.Interval = 300000;
            m_Timer.Start();
        }

        private void Open(object sender, EventArgs e)
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(appPath);
            Process.Start(dir);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && m_Components != null)
            {
                m_Components.Dispose();
                m_Components = null;
            }
        }

        private void contextMenu_OnExit(object sender, EventArgs e)
        {
            m_MixerMemory.Flush();
            m_NotifyIcon.Visible = false;
            ExitThread();
        }
    }
}
