using System;
using UnityEngine;
using Zenject;

namespace SoundReplacer.Patches
{
    internal class CutSoundPatch : IInitializable, IDisposable
    {
        private readonly NoteCutSoundEffectManager _noteCutSoundEffectManager;
        private readonly SoundLoader.Handle _sound;

        private readonly AudioClip[] _cutSounds = new AudioClip[1];
        private readonly AudioClip[] _originalLongCutSounds;
        private readonly AudioClip[] _originalShortCutSounds;

        private CutSoundPatch(NoteCutSoundEffectManager noteCutSoundEffectManager, SoundLoader soundLoader)
        {
            _noteCutSoundEffectManager = noteCutSoundEffectManager;
            _sound = soundLoader.CreateHandle(SoundType.Cut);
            _originalShortCutSounds = noteCutSoundEffectManager._shortCutEffectsAudioClips;
            _originalLongCutSounds = noteCutSoundEffectManager._longCutEffectsAudioClips;
        }

        public void Initialize()
        {
            _sound.Load((clip, _) =>
            {
                if (_noteCutSoundEffectManager == null) return;
                _cutSounds[0] = clip!;
                _noteCutSoundEffectManager._shortCutEffectsAudioClips = clip == null ? _originalShortCutSounds : _cutSounds;
                _noteCutSoundEffectManager._longCutEffectsAudioClips = clip == null ? _originalLongCutSounds : _cutSounds;
            });
        }

        public void Dispose()
        {
            _sound.Dispose();
        }
    }
}
