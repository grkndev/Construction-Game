using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Plays the game's music and sound effects. It only listens to <see cref="GameController"/> (nothing else calls it except
    /// the UI buttons for clicks): the controller says what happened, this decides how it sounds. Music and effects each have a
    /// level (0..1, set in the settings window); the choice is remembered. Automated runs (-shot / -demo) are silent unless -sound is passed.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int VoiceCount = 10;
        const string MusicKey = "audio.music.level";
        const string SfxKey = "audio.sfx.level";
        const string OldMusicKey = "audio.music";   // before levels: an on/off switch
        const string OldSfxKey = "audio.sfx";
        const float MusicVolume = 0.42f;
        const float SfxVolume = 0.9f;
        const float WorkerHitVolume = 0.28f;
        const float WorkerHitMinGap = 0.11f;

        [SerializeField] GameController game;
        [SerializeField] AudioClip[] sfx;               // indexed by SfxKind
        [SerializeField] AudioClip musicDashboard;
        [SerializeField] AudioClip musicDistrict;

        AudioSource dashboardSource, districtSource;
        AudioSource[] voices;
        int nextVoice;
        float lastWorkerHit = -10f;
        float musicMix;       // 0 = dashboard track, 1 = district track
        float musicLevel = 1f, sfxLevel = 1f;
        float lastClick = -10f;

        public float MusicLevel => musicLevel;
        public float SfxLevel => sfxLevel;

        void Awake()
        {
            musicLevel = LoadLevel(MusicKey, OldMusicKey);
            sfxLevel = LoadLevel(SfxKey, OldSfxKey);

            dashboardSource = NewSource(musicDashboard, loop: true);
            districtSource = NewSource(musicDistrict, loop: true);
            voices = new AudioSource[VoiceCount];
            for (var i = 0; i < voices.Length; i++) voices[i] = NewSource(null, loop: false);

            if ((LaunchArgs.Has("-demo") || LaunchArgs.Value("-shot") != null) && !LaunchArgs.Has("-sound")) AudioListener.volume = 0f;
        }

        void Start()
        {
            game.Sfx += OnSfx;
            musicMix = game.InDistrict ? 1f : 0f;
            dashboardSource.Play();
            districtSource.Play();
            ApplyMusicVolumes(immediate: true);
        }

        void OnDestroy()
        {
            if (game != null) game.Sfx -= OnSfx;
        }

        // The level key wins; a save from before levels only has the old on/off switch (off = 0, on = full).
        static float LoadLevel(string key, string oldKey)
        {
            if (PlayerPrefs.HasKey(key)) return Mathf.Clamp01(PlayerPrefs.GetFloat(key, 1f));
            return PlayerPrefs.GetInt(oldKey, 1) != 0 ? 1f : 0f;
        }

        AudioSource NewSource(AudioClip clip, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // plain 2D: the sound is the same wherever the camera is
            return source;
        }

        void Update()
        {
            // Fade between the map track and the district track.
            var target = game.InDistrict ? 1f : 0f;
            musicMix = Mathf.MoveTowards(musicMix, target, Time.unscaledDeltaTime / 1.2f);
            ApplyMusicVolumes(immediate: false);
        }

        void ApplyMusicVolumes(bool immediate)
        {
            var master = MusicVolume * musicLevel;
            var dashboard = master * Mathf.Cos(musicMix * Mathf.PI * 0.5f);
            var district = master * Mathf.Sin(musicMix * Mathf.PI * 0.5f);
            dashboardSource.volume = immediate ? dashboard : Mathf.MoveTowards(dashboardSource.volume, dashboard, Time.unscaledDeltaTime * 2f);
            districtSource.volume = immediate ? district : Mathf.MoveTowards(districtSource.volume, district, Time.unscaledDeltaTime * 2f);
        }

        // Phones: silence when the app goes to the background; browsers: when the tab loses focus.
        void OnApplicationFocus(bool focused) => AudioListener.pause = !focused;

        public void SetMusicLevel(float level)
        {
            musicLevel = Mathf.Clamp01(level);
            PlayerPrefs.SetFloat(MusicKey, musicLevel);
            PlayerPrefs.Save();
        }

        public void SetSfxLevel(float level)
        {
            sfxLevel = Mathf.Clamp01(level);
            PlayerPrefs.SetFloat(SfxKey, sfxLevel);
            PlayerPrefs.Save();
            // A tick lets the player hear the new level while dragging the slider (not on every pixel of the drag).
            if (Time.unscaledTime - lastClick > 0.12f) { lastClick = Time.unscaledTime; Play(SfxKind.Click); }
        }

        public void Click() => Play(SfxKind.Click);

        void OnSfx(SfxKind kind) => Play(kind);

        void Play(SfxKind kind)
        {
            if (sfxLevel <= 0f || (int)kind >= sfx.Length || sfx[(int)kind] == null) return;

            var volume = SfxVolume;
            var pitch = 1f;
            switch (kind)
            {
                case SfxKind.HitWorker:
                    // Up to eight helpers hammer at once: thin them out and keep them under the player's own hits.
                    if (Time.unscaledTime - lastWorkerHit < WorkerHitMinGap) return;
                    lastWorkerHit = Time.unscaledTime;
                    volume = WorkerHitVolume;
                    pitch = Random.Range(0.85f, 1.15f);
                    break;
                case SfxKind.HitPlayer:
                case SfxKind.RepairHit:
                    pitch = Random.Range(0.94f, 1.08f);
                    break;
                case SfxKind.Click:
                    volume = 0.6f;
                    break;
            }

            // Round-robin over a small pool; the oldest sound is cut when all voices are busy.
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.clip = sfx[(int)kind];
            voice.volume = volume * sfxLevel;
            voice.pitch = pitch;
            // The level-up jingle waits for the building fanfare; the "Coşku" whoosh for the purchase ping.
            var delay = kind == SfxKind.LevelUp ? 0.75f : kind == SfxKind.Boost ? 0.12f : 0f;
            if (delay > 0f) voice.PlayDelayed(delay);
            else voice.Play();
        }
    }
}
