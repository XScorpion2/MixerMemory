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
                        m_Logger.Error("Category {category} already exists with Volume {volume}.", data.Name);
                    else
                        m_CategoryVolumes.Add(data.Name, data.Volume);
                }

                foreach (ApplicationData data in m_MixerMatching.Rules)
                {
                    if (!m_CategoryVolumes.TryGetValue(data.Category, out float volume))
                        m_Logger.Error("Rules using Category {category} that is undefined in the \"Categories\" section.", data.Category);
                }
            }
        }

        public void RefreshDevice()
        {
            m_Device = m_Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            m_Logger.Info("Set active device to {deviceName}.", m_Device.DeviceFriendlyName);
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
            m_Logger.Info("New Session {displayName}.", displayName);

            // Some apps change their own volume shortly after loading, delay slightly to handle this case
            await Task.Delay(TimeSpan.FromSeconds(1));
            RestoreSession(session);
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
            m_Logger.Info("Matched Session {displayName} from {applicationPath} to {category} volume {volume}.", displayName, applicationPath, category, volume);
            session.SimpleAudioVolume.Volume = volume;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            try
            {
                if (m_Device.ID != deviceId)
                    return;
            }
            catch (Exception e)
            {
                m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(OnDeviceStateChanged), e.Message);
            }

            m_Logger.Info("Device {deviceId} state changed to {newState}.", deviceId, newState);
            RefreshDevice();
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceRemoved(string deviceId)
        {
            try
            {
                if (m_Device.ID != deviceId)
                    return;
            }
            catch (Exception e) 
            {
                m_Logger.Debug("{functionName} Handled Exception: {message}.", nameof(OnDeviceRemoved), e.Message);
            }

            m_Logger.Info("Device {deviceId} removed.", deviceId);
            RefreshDevice();
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                m_Logger.Info("Default device changed to {deviceId}.", defaultDeviceId);
                RefreshDevice();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
