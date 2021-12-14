using NLog;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace MixerMemory
{
    public class MixerDevice : IDisposable
    {
        private Thread m_Thread;
        private SerialPort m_SerialPort;
        private bool m_Stopping = false;

        private readonly Action<string, float> m_SetVolume;
        private readonly string[] m_Mappings = new[] { "System", "Browser", "Game", "Music", "Voice Chat" };
        private readonly float[] m_Updates = new[] { -1f, -1f, -1f, -1f, -1f };
        private readonly object[] m_Locks = new[] { new object(), new object(), new object(), new object(), new object() };
        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        public MixerDevice(Action<string, float> setVolume)
        {
            m_SetVolume = setVolume;
            try
            {
                m_Thread = new Thread(Update);
                m_Thread.Name = "MixerDevice";
                m_Thread.Start();
                m_SerialPort = new SerialPort("COM4", 9600);
                m_SerialPort.Open();
                m_SerialPort.DiscardInBuffer();
                m_SerialPort.DiscardOutBuffer();
                m_SerialPort.DataReceived += OnDataReceived;
            }
            catch (Exception e)
            {
                m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(MixerDevice), e.Message);
            }
        }

        ~MixerDevice()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            m_Stopping = true;
            if (m_Thread != null)
            {
                m_Thread.Join();
                m_Thread = null;
            }

            if (m_SerialPort != null)
            {
                try
                {
                    m_SerialPort.Close();
                }
                catch (Exception e)
                {
                    m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(Dispose), e.Message);
                }
                m_SerialPort = null;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
        {
            while (m_SerialPort.BytesToRead > 0)
            {
                try
                {
                    string data = m_SerialPort.ReadLine();
                    int command = data[0] - 65;
                    if (command >= 0 && command <= 4)
                    {
                        float value = float.Parse(data.Substring(2)) / 100f;
                        Console.WriteLine($"Data Received: {command}:{value}");

                        Interlocked.Exchange(ref m_Updates[command], value);
                    }
                    else
                        Console.WriteLine($"Data Received: {data}");
                }
                catch (Exception e)
                {
                    m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(OnDataReceived), e.Message);
                }
            }
        }

        // NOTE: Using polling with a basic most recent value approach to rate limit volume updates as some applications are VERY slow to update
        // (Discord, NVidia Container, Steam, probably more)
        private void Update()
        {
            while (true)
            {
                for (int i = 0; i < m_Updates.Length; i++)
                {
                    float volume = Interlocked.Exchange(ref m_Updates[i], -1f);
                    if (volume < 0f)
                        continue;
                    m_SetVolume?.Invoke(m_Mappings[i], volume);
                }

                Thread.Sleep(10);
                if (m_Stopping)
                    break;
            }
        }
    }
}
