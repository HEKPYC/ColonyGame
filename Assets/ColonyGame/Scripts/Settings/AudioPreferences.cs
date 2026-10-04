using UnityEngine;

namespace ColonyGame.Settings
{
    public sealed class AudioPreferences
    {
        private readonly string enabledKey;
        private readonly string volumeKey;
        public bool LobbyMusicEnabled { get; private set; }
        public float MusicVolume { get; private set; }
        public AudioPreferences(string keyPrefix = "ColonyGame.Audio.")
        {
            enabledKey = keyPrefix + "LobbyMusicEnabled";
            volumeKey = keyPrefix + "MusicVolume";
            LobbyMusicEnabled = PlayerPrefs.GetInt(enabledKey, 1) != 0;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(volumeKey, 0.6f));
        }
        public void SetLobbyMusic(bool enabled)
        {
            LobbyMusicEnabled = enabled;
            PlayerPrefs.SetInt(enabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(volumeKey, MusicVolume);
            PlayerPrefs.Save();
        }
    }
}

