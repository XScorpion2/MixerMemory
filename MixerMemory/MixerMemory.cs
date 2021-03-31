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

        private Dictionary<string, float> m_SavedVolumes = new Dictionary<string, float>();

        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        private bool m_Queued = false;

        public MixerMemory()
        {
            if (File.Exists(k_ConfigJson))
            {
                m_Logger.Info("Loading values from config.");
                var text = File.ReadAllText(k_ConfigJson);
                m_SavedVolumes = JsonConvert.DeserializeObject<Dictionary<string, float>>(text);
            }

            m_Enumerator = new MMDeviceEnumerator();
            m_Enumerator.RegisterEndpointNotificationCallback(this);
            Reload();
            Restore();
        }

        public void Reload()
        {
            m_Device = m_Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            m_Logger.Info($"Set active device to {m_Device.DeviceFriendlyName}.");
            m_Manager = m_Device.AudioSessionManager;
            m_Manager.OnSessionCreated += (s, a) => NewSession(new AudioSessionControl(a));
        }

        public void Save()
        {
            var sessions = m_Manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                SaveSession(sessions[i]);
        }

        public void Restore()
        {
            m_Manager.RefreshSessions();
            var sessions = m_Manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                RestoreSession(sessions[i]);
        }

        public void Flush()
        {
            m_Logger.Info($"Flushing Session Volumes to config.");
            var text = JsonConvert.SerializeObject(m_SavedVolumes, Formatting.Indented);
            File.WriteAllText(k_ConfigJson, text);
        }

        private async void NewSession(AudioSessionControl session)
        {
            var displayName = session.GetFriendlyDisplayName();
            m_Logger.Info($"New Session '{displayName}'.");
            RestoreSession(session);
            if (!m_Queued && displayName == "Zoom")
            {
                m_Queued = true;
                m_Logger.Info($"Delay Restore Queued.");
                await Task.Delay(TimeSpan.FromSeconds(5));
                Restore();
                m_Logger.Info($"Delay Restore Fired.");
                m_Queued = false;
            }
        }

        private void RestoreSession(AudioSessionControl session)
        {
            var displayName = session.GetFriendlyDisplayName();
            if (!m_SavedVolumes.TryGetValue(displayName, out float volume))
            {
                volume = 0.5f;
                m_SavedVolumes[displayName] = volume;
            }

            if (session.SimpleAudioVolume.Volume != volume)
            {
                session.SimpleAudioVolume.Volume = volume;
                m_Logger.Info($"Set '{displayName}' to {volume}.");
            }
        }

        private void SaveSession(AudioSessionControl session)
        {
            var displayName = session.GetFriendlyDisplayName();
            m_SavedVolumes[displayName] = session.SimpleAudioVolume.Volume;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            if (m_Device.ID == deviceId)
            {
                m_Logger.Info($"Device '{deviceId}' state changed to '{newState}'.");
                Reload();
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceRemoved(string deviceId)
        {
            if (m_Device.ID == deviceId)
            {
                m_Logger.Info($"Device '{deviceId}' removed.");
                Reload();
            }
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                m_Logger.Info($"Default device changed to '{defaultDeviceId}'.");
                Reload();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
