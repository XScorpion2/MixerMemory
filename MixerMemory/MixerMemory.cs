using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MixerMemory
{
    public class MixerMemory : IMMNotificationClient
    {
        public const string k_ConfigJson = "MixerMemory.json";

        private MMDeviceEnumerator m_Enumerator;
        private MMDevice m_Device;
        private AudioSessionManager m_Manager;

        private Dictionary<string, float> m_CategoryVolumes = new Dictionary<string, float>();
        private MixerMatching m_MixerMatching = new MixerMatching();

        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        private bool m_Queued = false;

        public MixerMemory()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>() { new MatchTypeConverter() },
                Formatting = Formatting.Indented
            };

            LoadVolumes();

            m_Enumerator = new MMDeviceEnumerator();
            m_Enumerator.RegisterEndpointNotificationCallback(this);
            RefreshDevice();
            RestoreVolumes();
        }

        public void LoadVolumes()
        {
            m_CategoryVolumes.Clear();
            if (File.Exists(k_ConfigJson))
            {
                m_Logger.Info("Loading values from config.");
                string text = File.ReadAllText(k_ConfigJson);
                m_MixerMatching = JsonConvert.DeserializeObject<MixerMatching>(text);

                foreach (CategoryData data in m_MixerMatching.Categories)
                {
                    if (m_CategoryVolumes.TryGetValue(data.Name, out float volume))
                        m_Logger.Error($"Category '{data.Name}' already exists with Volume '{volume}'.");
                    else
                        m_CategoryVolumes.Add(data.Name, data.Volume);
                }

                foreach (ApplicationData data in m_MixerMatching.Rules)
                {
                    if (!m_CategoryVolumes.TryGetValue(data.Category, out float volume))
                        m_Logger.Error($"Rules using Category '{data.Category}' that is undefined in the \"Categories\" section.");
                }
            }
        }

        public void RefreshDevice()
        {
            m_Device = m_Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            m_Logger.Info($"Set active device to {m_Device.DeviceFriendlyName}.");
            m_Manager = m_Device.AudioSessionManager;
            m_Manager.OnSessionCreated += (s, a) => NewSession(new AudioSessionControl(a));
        }

        public void RestoreVolumes()
        {
            m_Manager.RefreshSessions();
            SessionCollection sessions = m_Manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                RestoreSession(sessions[i]);
        }

        private async void NewSession(AudioSessionControl session)
        {
            string displayName = session.GetFriendlyDisplayName();
            m_Logger.Info($"New Session '{displayName}'.");
            RestoreSession(session);

            // Zoom is odd in that we need to delay Restoring
            // TODO: might want to try delaying all restoring
            if (!m_Queued && displayName == "Zoom")
            {
                m_Queued = true;
                m_Logger.Info($"Delay Restore Queued.");
                await Task.Delay(TimeSpan.FromSeconds(5));
                RestoreVolumes();
                m_Logger.Info($"Delay Restore Fired.");
                m_Queued = false;
            }
        }

        private void RestoreSession(AudioSessionControl session)
        {
            string category = "";
            string displayName = session.GetFriendlyDisplayName();
            string applicationPath = session.GetApplicationPath();
            foreach (ApplicationData rule in m_MixerMatching.Rules)
            {
                if (MixerMatching.Matchers[(int)rule.Type](displayName, applicationPath, rule.Match))
                {
                    category = rule.Category;
                    break;
                }
            }

            float volume = string.IsNullOrEmpty(category) ? 0.5f : m_CategoryVolumes[category];
            m_Logger.Info($"Matched '{displayName}' at '{applicationPath}' to '{category}' volume '{volume}'.");
            session.SimpleAudioVolume.Volume = volume;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            if (m_Device.ID == deviceId)
            {
                m_Logger.Info($"Device '{deviceId}' state changed to '{newState}'.");
                RefreshDevice();
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceRemoved(string deviceId)
        {
            if (m_Device.ID == deviceId)
            {
                m_Logger.Info($"Device '{deviceId}' removed.");
                RefreshDevice();
            }
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                m_Logger.Info($"Default device changed to '{defaultDeviceId}'.");
                RefreshDevice();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
