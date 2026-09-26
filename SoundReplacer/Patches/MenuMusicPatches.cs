using System;
using System.Collections.Generic;
using SiraUtil.Affinity;
using UnityEngine;

namespace SoundReplacer.Patches
{
    internal class MenuMusicPatches : IAffinity, IDisposable
    {
        private readonly SongPreviewPlayer _songPreviewPlayer;
        private readonly SoundLoader _soundLoader;
        private readonly SoundLoader.Handle _sound;
        private readonly HashSet<Action> _playbackReleases = new();
        private bool _wantsDefaultMusic = true;

        private readonly AudioClip _originalMenuMusic;
        private readonly AudioClip _originalLobbyMusic;
        private AudioClip? _menuMusic;

        private MenuMusicPatches(SongPreviewPlayer songPreviewPlayer, GameServerLobbyFlowCoordinator lobbyFlowCoordinator, SoundLoader soundLoader)
        {
            _songPreviewPlayer = songPreviewPlayer;
            _soundLoader = soundLoader;
            _sound = soundLoader.CreateHandle(SoundType.Menu);
            _originalMenuMusic = songPreviewPlayer.defaultAudioClip;
            _originalLobbyMusic = lobbyFlowCoordinator._ambienceAudioClip;
        }

        public void Dispose()
        {
            _sound.Dispose();
            foreach (var release in new List<Action>(_playbackReleases)) release();
        }

        [AffinityPatch(typeof(SongPreviewPlayer), "Awake")]
        [AffinityPrefix]
        private void ReplaceMenuMusic()
        {
            ApplyMenuMusic();
        }

        [AffinityPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeToDefault))]
        [AffinityPrefix]
        private void ReplaceMenuMusicOnDefault()
        {
            ApplyMenuMusic();
        }

        private void ApplyMenuMusic()
        {
            _sound.Load((clip, delayed) =>
            {
                if (_songPreviewPlayer == null) return;
                _menuMusic = clip;
                _songPreviewPlayer._defaultAudioClip = clip ?? _originalMenuMusic;
                if (delayed && _wantsDefaultMusic && _songPreviewPlayer.isActiveAndEnabled)
                    _songPreviewPlayer.CrossfadeToDefault();
            });
        }

        [AffinityPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeTo), AffinityMethodType.Normal, new[] { typeof(AudioClip), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(Action) })]
        [AffinityPrefix]
        private void RetainPlayingSound(AudioClip audioClip, bool isDefault, ref Action? onFadeOutCallback)
        {
            _wantsDefaultMusic = isDefault;
            var retain = _soundLoader.RetainForPlayback(audioClip);
            if (retain == null) return;
            Action release = null!;
            release = () =>
            {
                _playbackReleases.Remove(release);
                retain();
            };
            _playbackReleases.Add(release);
            onFadeOutCallback += release;
        }

        [AffinityPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeToNewDefault))]
        [AffinityPrefix]
        private bool PreventExternalDefaultMenuMusic(AudioClip audioClip)
        {
            // If there is no custom sound in use, use the new default.
            if (_songPreviewPlayer.defaultAudioClip != _menuMusic && _songPreviewPlayer.defaultAudioClip != SoundLoader.Empty)
            {
                return true;
            }

            // If the new default is the default menu music, cancel the method.
            return audioClip != _originalMenuMusic && audioClip != _originalLobbyMusic;
        }
    }
}
