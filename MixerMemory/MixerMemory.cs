using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace MixerMemory
{
    public class MixerMemory
    {
        const string k_ConfigJson = "MixerMemory.json";

        private MMDevice m_Device;
        private AudioSessionManager m_Manager;

        private Dictionary<string, float> m_SavedVolumes = new Dictionary<string, float>();

        public MixerMemory()
        {
            if (File.Exists(k_ConfigJson))
            {
                var text = File.ReadAllText(k_ConfigJson);
                m_SavedVolumes = JsonConvert.DeserializeObject<Dictionary<string, float>>(text);
            }

            Reload();
        }

        public void Reload()
        {
            var enumerator = new MMDeviceEnumerator();
            m_Device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            m_Manager = m_Device.AudioSessionManager;
            m_Manager.OnSessionCreated += (s, a) => RestoreSession(new AudioSessionControl(a));
        }

        public void Save()
        {
            var sessions = m_Manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                SaveSession(sessions[i]);
        }

        public void Restore()
        {
            Reload();
            var sessions = m_Manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                RestoreSession(sessions[i]);
        }

        public void Flush()
        {
            var text = JsonConvert.SerializeObject(m_SavedVolumes, Formatting.Indented);
            File.WriteAllText(k_ConfigJson, text);
        }

        private void RestoreSession(AudioSessionControl session)
        {
            var displayName = session.GetFriendlyDisplayName();
            if (!m_SavedVolumes.TryGetValue(displayName, out float volume))
            {
                volume = 0.5f;
                m_SavedVolumes[displayName] = volume;
            }
            session.SimpleAudioVolume.Volume = volume;
        }

        private void SaveSession(AudioSessionControl session)
        {
            var displayName = session.GetFriendlyDisplayName();
            m_SavedVolumes[displayName] = session.SimpleAudioVolume.Volume;
        }
    }
}
