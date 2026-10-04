using ColonyGame.Settings;
using UnityEngine;

namespace ColonyGame.Presentation
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class LobbyAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip lobbyMusic = null;
        private AudioSource source;
        public void Initialize(AudioPreferences preferences)
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.clip = lobbyMusic;
            Apply(preferences);
            if (source.clip != null) source.Play();
        }
        public void Apply(AudioPreferences preferences)
        {
            if (source == null) return;
            source.volume = preferences.MusicVolume;
            source.mute = !preferences.LobbyMusicEnabled;
        }
    }
}

