using System;
using SiraUtil.Affinity;
using UnityEngine;

namespace SoundReplacer.Patches
{
    internal class BadCutSoundPatch : IAffinity, IDisposable
    {
        private readonly SoundLoader.Handle _sound;
        private readonly PluginConfig _config;

        private readonly AudioClip[] _badCutSounds = { SoundLoader.Empty };

        private BadCutSoundPatch(SoundLoader soundLoader, PluginConfig config)
        {
            _sound = soundLoader.CreateHandle(SoundType.BadCut);
            _config = config;
        }

        public void Dispose()
        {
            _sound.Dispose();
        }

        [AffinityPatch(typeof(EffectPoolsManualInstaller), nameof(EffectPoolsManualInstaller.ManualInstallBindings))]
        [AffinityPrefix]
        private void ReplaceBadCutSounds()
        {
            _sound.Load((clip, _) => _badCutSounds[0] = clip ?? SoundLoader.Empty);
        }

        [AffinityPatch(typeof(NoteCutSoundEffect), nameof(NoteCutSoundEffect.Init))]
        [AffinityPrefix]
        private void BindBadCutSounds(NoteCutSoundEffect __instance)
        {
            if (_config.BadCutSound != SoundLoader.DefaultSoundID)
                __instance._badCutSoundEffectAudioClips = _badCutSounds;
        }
    }
}
