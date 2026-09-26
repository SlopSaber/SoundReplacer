using System;
using SiraUtil.Affinity;
using UnityEngine;

namespace SoundReplacer.Patches
{
    internal class LevelClearedSoundPatch : IAffinity, IDisposable
    {
        private readonly ResultsViewController _resultsViewController;
        private readonly SongPreviewPlayer _songPreviewPlayer;
        private readonly SoundLoader.Handle _clearedSound;
        private readonly SoundLoader.Handle _failedSound;
        private readonly PluginConfig _config;

        private readonly AudioClip _originalLevelClearedSound;
        private int _activation;

        private LevelClearedSoundPatch(ResultsViewController resultsViewController, SongPreviewPlayer songPreviewPlayer, SoundLoader soundLoader, PluginConfig config)
        {
            _resultsViewController = resultsViewController;
            _songPreviewPlayer = songPreviewPlayer;
            _clearedSound = soundLoader.CreateHandle(SoundType.LevelCleared);
            _failedSound = soundLoader.CreateHandle(SoundType.LevelFailed);
            _config = config;
            _originalLevelClearedSound = resultsViewController._levelClearedAudioClip;
        }

        public void Dispose()
        {
            _activation++;
            _clearedSound.Dispose();
            _failedSound.Dispose();
        }

        [AffinityPatch(typeof(ResultsViewController), nameof(ResultsViewController.DidDeactivate))]
        [AffinityPrefix]
        private void CancelPendingPlayback() => _activation++;

        [AffinityPatch(typeof(ResultsViewController), nameof(ResultsViewController.DidActivate))]
        [AffinityPrefix]
        // TODO: I'd rather avoid the bool Prefix but I don't think there's another way...
        private bool PlayLevelClearedSound(bool firstActivation, bool addedToHierarchy)
        {
            int activation = ++_activation;
            if (firstActivation)
            {
                _resultsViewController.buttonBinder.AddBinding(_resultsViewController._restartButton, _resultsViewController.RestartButtonPressed);
                _resultsViewController.buttonBinder.AddBinding(_resultsViewController._continueButton, _resultsViewController.ContinueButtonPressed);
            }

            if (addedToHierarchy)
            {
                _resultsViewController.SetDataToUI();

                if (_resultsViewController._levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared
                    && _resultsViewController._newHighScore)
                {
                    _resultsViewController._startFireworksAfterDelayCoroutine = _resultsViewController.StartCoroutine(_resultsViewController.StartFireworksAfterDelay(1.95f));
                    if (_config.LevelClearedSound != SoundLoader.NoSoundID)
                    {
                        // This changes the sound that gets played when there's a new personal best.
                        // It may be preferable to instead play the custom sound separately.
                        _clearedSound.Load((clip, delayed) =>
                        {
                            if (activation != _activation || _resultsViewController == null || (delayed && !_resultsViewController.isActiveAndEnabled)) return;
                            var sound = clip ?? _originalLevelClearedSound;
                            _resultsViewController._levelClearedAudioClip = sound;
                            _songPreviewPlayer.CrossfadeTo(sound, -4f, 0f, sound.length, null);
                        });
                    }
                }
                else if (_resultsViewController._levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed
                    && _config.LevelFailedSound != SoundLoader.DefaultSoundID && _config.LevelFailedSound != SoundLoader.NoSoundID)
                {
                    _failedSound.Load((clip, delayed) =>
                    {
                        if (activation != _activation || _resultsViewController == null || (delayed && !_resultsViewController.isActiveAndEnabled) || clip == null) return;
                        _songPreviewPlayer.CrossfadeTo(clip, -4f, 0f, clip.length, null);
                    });
                }

                if (_resultsViewController._menuDestinationRequest != null)
                {
                    _resultsViewController.ProcessMenuDestinationRequest(_resultsViewController._menuDestinationRequest);
                }
            }

            return false;
        }
    }
}
