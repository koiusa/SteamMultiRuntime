using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class BgmManager : MonoBehaviour
    {
        [Header("BGM")]
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool loop = true;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private AudioMixerGroup outputMixerGroup;

        [Header("Mixer Volume")]
        [SerializeField] private bool useMixerVolume = true;
        [SerializeField] private bool mixerVolumeParameterReady;
        [SerializeField] private AudioMixer outputMixer;
        [SerializeField] private string volumeExposedParameter = "BgmVolume";
        [SerializeField] private float minVolumeDb = -80f;

        [Header("Loop Crossfade")]
        [SerializeField] private bool useLoopCrossfade = true;
        [SerializeField, Min(0.01f)] private float loopCrossfadeDuration = 0.15f;

        private AudioSource audioSource;
        private AudioSource crossfadeSource;
        private Coroutine loopCrossfadeCoroutine;
        private float audioSourceBaseVolume = 1f;
        private float crossfadeSourceBaseVolume = 1f;

        private void Awake()
        {
            if (persistAcrossScenes)
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null, true);
                }

                DontDestroyOnLoad(gameObject);
            }

            audioSource = GetComponent<AudioSource>();
            ConfigureSource(audioSource);
            ResolveMixerReference();
            ApplyVolume();

            if (playOnAwake)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                ConfigureSource(audioSource);
            }

            if (audioSource.clip == null)
            {
                return;
            }

            StopLoopCrossfadeRoutine();

            if (ShouldUseLoopCrossfade())
            {
                EnsureCrossfadeSource();

                audioSource.outputAudioMixerGroup = outputMixerGroup;
                audioSource.loop = false;
                audioSource.volume = GetEffectiveSourceVolume(audioSource);
                audioSource.time = 0f;

                crossfadeSource.outputAudioMixerGroup = outputMixerGroup;
                crossfadeSource.loop = false;
                crossfadeSource.volume = 0f;
                crossfadeSource.clip = audioSource.clip;

                audioSource.Play();
                loopCrossfadeCoroutine = StartCoroutine(LoopCrossfadeRoutine());
                return;
            }

            audioSource.outputAudioMixerGroup = outputMixerGroup;
            audioSource.loop = loop;
            audioSource.volume = GetEffectiveSourceVolume(audioSource);
            audioSource.Play();
        }

        public void Stop()
        {
            StopLoopCrossfadeRoutine();

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            if (crossfadeSource != null)
            {
                crossfadeSource.Stop();
            }
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            ApplyVolume();
        }

        public void SetClip(AudioClip clip, bool restart = true)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                ConfigureSource(audioSource);
            }

            audioSource.clip = clip;
            if (crossfadeSource != null)
            {
                crossfadeSource.clip = clip;
            }

            if (restart)
            {
                Stop();
                Play();
            }
        }

        private IEnumerator LoopCrossfadeRoutine()
        {
            var current = audioSource;
            var next = crossfadeSource;

            while (loop && useLoopCrossfade && isActiveAndEnabled)
            {
                if (current.clip == null)
                {
                    yield break;
                }

                if (!current.isPlaying)
                {
                    current.time = 0f;
                    current.volume = GetEffectiveSourceVolume(current);
                    current.Play();
                }

                var clipLength = current.clip.length;
                var fadeDuration = Mathf.Clamp(loopCrossfadeDuration, 0.01f, clipLength * 0.5f);
                var crossfadeStartTime = Mathf.Max(0f, clipLength - fadeDuration);

                while (current.isPlaying && current.time < crossfadeStartTime)
                {
                    yield return null;
                }

                if (!loop || !useLoopCrossfade || !isActiveAndEnabled)
                {
                    yield break;
                }

                next.clip = current.clip;
                next.time = 0f;
                next.loop = false;
                next.volume = 0f;
                next.Play();

                var elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / fadeDuration);
                    current.volume = GetEffectiveSourceVolume(current) * (1f - t);
                    next.volume = GetEffectiveSourceVolume(next) * t;
                    yield return null;
                }

                current.Stop();
                current.volume = 0f;

                var temp = current;
                current = next;
                next = temp;
            }

            if (loop && isActiveAndEnabled && audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.loop = true;
                audioSource.volume = GetEffectiveSourceVolume(audioSource);
                audioSource.Play();
            }
        }

        private bool ShouldUseLoopCrossfade()
        {
            if (!loop || !useLoopCrossfade || audioSource == null || audioSource.clip == null)
            {
                return false;
            }

            return audioSource.clip.length > loopCrossfadeDuration + 0.01f;
        }

        private void EnsureCrossfadeSource()
        {
            if (crossfadeSource != null)
            {
                return;
            }

            var sourceObject = new GameObject("BgmCrossfadeSource");
            sourceObject.transform.SetParent(transform, false);
            crossfadeSource = sourceObject.AddComponent<AudioSource>();
            crossfadeSourceBaseVolume = audioSourceBaseVolume;
            ConfigureSource(crossfadeSource);
        }

        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            CaptureBaseVolume(source);
            source.volume = GetEffectiveSourceVolume(source);
            source.outputAudioMixerGroup = outputMixerGroup;
        }

        public void SetOutputMixerGroup(AudioMixerGroup mixerGroup)
        {
            outputMixerGroup = mixerGroup;
            ResolveMixerReference();

            if (audioSource != null)
            {
                audioSource.outputAudioMixerGroup = outputMixerGroup;
            }

            if (crossfadeSource != null)
            {
                crossfadeSource.outputAudioMixerGroup = outputMixerGroup;
            }

            ApplyVolume();
        }

        private void StopLoopCrossfadeRoutine()
        {
            if (loopCrossfadeCoroutine == null)
            {
                return;
            }

            StopCoroutine(loopCrossfadeCoroutine);
            loopCrossfadeCoroutine = null;
        }

        private void ResolveMixerReference()
        {
            if (outputMixer == null && outputMixerGroup != null)
            {
                outputMixer = outputMixerGroup.audioMixer;
            }
        }

        private bool IsUsingMixerVolumeControl()
        {
            return useMixerVolume
                && mixerVolumeParameterReady
                && outputMixer != null
                && !string.IsNullOrEmpty(volumeExposedParameter);
        }

        private float GetSourceBaseVolume(AudioSource source)
        {
            if (source == audioSource)
            {
                return audioSourceBaseVolume;
            }

            if (source == crossfadeSource)
            {
                return crossfadeSourceBaseVolume;
            }

            return 1f;
        }

        private float GetEffectiveSourceVolume(AudioSource source)
        {
            var multiplier = IsUsingMixerVolumeControl() ? 1f : volume;
            return Mathf.Clamp01(GetSourceBaseVolume(source) * multiplier);
        }

        private void CaptureBaseVolume(AudioSource source)
        {
            var baseVolume = Mathf.Clamp01(source.volume);
            if (source == audioSource)
            {
                audioSourceBaseVolume = baseVolume;
            }
            else if (source == crossfadeSource)
            {
                crossfadeSourceBaseVolume = baseVolume;
            }
        }

        private void ApplyVolume()
        {
            var usingMixer = IsUsingMixerVolumeControl();
            if (usingMixer)
            {
                var dB = volume <= 0.0001f ? minVolumeDb : Mathf.Lerp(minVolumeDb, 0f, volume);
                outputMixer.SetFloat(volumeExposedParameter, dB);
            }

            if (audioSource != null)
            {
                audioSource.volume = GetEffectiveSourceVolume(audioSource);
            }

            if (crossfadeSource != null)
            {
                crossfadeSource.volume = GetEffectiveSourceVolume(crossfadeSource);
            }
        }
    }
}
