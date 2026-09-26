using System;
using SiraUtil.Affinity;
using UnityEngine;

namespace SoundReplacer.Patches
{
    internal class ClickSoundPatch : IAffinity, IDisposable
    {
        private readonly SoundLoader.Handle _sound;

        private readonly AudioClip[] _clickSounds = new AudioClip[1];
        private AudioClip[]? _originalClickSounds;

        private ClickSoundPatch(SoundLoader soundLoader)
        {
            _sound = soundLoader.CreateHandle(SoundType.Click);
        }

        public void Dispose()
        {
            _sound.Dispose();
        }

        [AffinityPatch(typeof(BasicUIAudioManager), nameof(BasicUIAudioManager.Start))]
        [AffinityPrefix]
        private void ReplaceClickSounds(BasicUIAudioManager __instance)
        {
            _originalClickSounds ??= __instance._clickSounds;

            _sound.Load((clip, _) =>
            {
                if (__instance == null) return;
                _clickSounds[0] = clip!;
                __instance._clickSounds = clip == null ? _originalClickSounds : _clickSounds;
            });
        }
    }
}
