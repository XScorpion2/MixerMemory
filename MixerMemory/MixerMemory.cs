using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MixerMemory
{
    public class MixerMemory : IMMNotificationClient
    {
        public const string k_ConfigJson = "MixerMemory.json";

        private MMDeviceEnumerator m_Enumerator;

        private string m_DeviceId;
        private bool m_IgnoreDevice = false;
        private AudioSessionManager m_Manager;

        private MixerMatching m_MixerMatching = new MixerMatching();
        private Dictionary<string, float> m_CategoryVolumes = new Dictionary<string, float>();

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
        }

        public void LoadVolumes()
        {
            m_CategoryVolumes.Clear();
            if (File.Exists(k_ConfigJson))
            {
                m_Logger.Info("Loading values from {configFile}.", k_ConfigJson);
                string text = File.ReadAllText(k_ConfigJson);
                m_MixerMatching = JsonConvert.DeserializeObject<MixerMatching>(text);
            }
            else
            {
                m_Logger.Info("{configFile} not found. Loading default values.", k_ConfigJson);
                m_MixerMatching.Categories = new[] { new CategoryData { Name = "System", Volume = 0.5f } };
                m_MixerMatching.Rules = new[] { new ApplicationData { Type = MatchType.Always, Match = "", Category = "System" } };
            }

            foreach (CategoryData data in m_MixerMatching.Categories)
            {
                if (m_CategoryVolumes.TryGetValue(data.Name, out float volume))
                    m_Logger.Error("Category {category} already exists with Volume {volume}.", data.Name, volume);
                else
                    m_CategoryVolumes.Add(data.Name, data.Volume);
            }

            foreach (ApplicationData data in m_MixerMatching.Rules)
            {
                if (data.Category == "Ignore")
                    continue;

                if (!m_CategoryVolumes.ContainsKey(data.Category))
                    m_Logger.Error("Rules using Category {category} that is undefined in the \"Categories\" section.", data.Category);
            }
        }

        public async void RefreshDevice()
        {
            try
            {
                using (MMDevice device = m_Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    if (m_DeviceId != device.ID)
                        m_Logger.Info("Set active device to {deviceName}.", device.DeviceFriendlyName);

                    string category = "";
                    foreach (ApplicationData rule in m_MixerMatching.Rules)
                    {
                        if (MixerMatching.Matchers[(int)rule.Type](device.DeviceFriendlyName, device.InstanceId, rule.Match))
                        {
                            category = rule.Category;
                            break;
                        }
                    }
                    m_IgnoreDevice = category == "Ignore";
                    if (m_IgnoreDevice && m_DeviceId != device.ID)
                        m_Logger.Info("Active device {deviceName} is in the ignore category. Will not change session volumes.", device.DeviceFriendlyName);

                    m_DeviceId = device.ID;
                    m_Manager = device.AudioSessionManager;
                    m_Manager.OnSessionCreated += (s, a) =>
                    {
                        using (AudioSessionControl session = new AudioSessionControl(a))
                            NewSession(session);
                    };
                }
            }
            catch (Exception e)
            {
                m_Logger.Debug("{functionName} Handled Exception: {message}. Retrying in 5 seconds.", nameof(RefreshDevice), e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            RestoreVolumes();
        }

        public void SetCatagoryVolume(string category, float newVolume)
        {
            if (newVolume < 0f)
            {
                newVolume = 0f;
                m_Logger.Error("Volume {volume} is out of the expected [0.00, 1.00] range.", newVolume);
            }
            else if (newVolume > 1f)
            {
                newVolume = 1f;
                m_Logger.Error("Volume {volume} is out of the expected [0.00, 1.00] range.", newVolume);
            }

            if (!m_CategoryVolumes.TryGetValue(category, out float currentVolume))
            {
                m_Logger.Error("Cannot update Volume for undefined Category {category}.", category);
                return;
            }

            if (currentVolume != newVolume)
            {
                m_Logger.Info("Updating Category {category} to new Volume {volume}.", category, newVolume);
                m_CategoryVolumes[category] = newVolume;
                RestoreVolumes(true);
            }
        }

        public void RestoreVolumes(bool fastUpdate = false)
        {
            if (m_IgnoreDevice) return;
            if (!fastUpdate)
            {
                m_Manager.RefreshSessions();
                Extensions.PruneNameAndPathCache();
            }
            SessionCollection sessions = m_Manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                RestoreSession(sessions[i], fastUpdate);
        }

        private void NewSession(AudioSessionControl session)
        {
            if (!session.GetFriendlyDisplayNameAndApplicationPath(out string displayName, out _))
                m_Logger.Error("{functionName} Failed to get 'displayName'.", nameof(NewSession));
            m_Logger.Info("New Session {displayName}.", displayName);
            RestoreSession(session);
        }

        private void RestoreSession(AudioSessionControl session, bool fastUpdate = false)
        {
            string category = "";
            if (!session.GetFriendlyDisplayNameAndApplicationPath(out string displayName, out string applicationPath))
                m_Logger.Error("{functionName} Failed to get 'displayName' and 'applicationPath'.", nameof(RestoreSession));

            foreach (ApplicationData rule in m_MixerMatching.Rules)
            {
                if (MixerMatching.Matchers[(int)rule.Type](displayName, applicationPath, rule.Match))
                {
                    category = rule.Category;
                    break;
                }
            }

            if (category == "Ignore")
                return;

            float volume = string.IsNullOrEmpty(category) ? 0.5f : m_CategoryVolumes[category];
            if (session.SimpleAudioVolume.Volume == volume)
                return;

            if (!fastUpdate)
                m_Logger.Info("Matched Session {displayName} from {applicationPath} to {category} volume {volume}.", displayName, applicationPath, category, volume);
            session.SimpleAudioVolume.Volume = volume;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            if (m_DeviceId != deviceId)
                return;
            m_Logger.Info("Device {deviceId} state changed to {newState}.", deviceId, newState);
            RefreshDevice();
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceRemoved(string deviceId)
        {
            if (m_DeviceId != deviceId)
                return;
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
