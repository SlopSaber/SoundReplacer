using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMarkupLanguage;
using IPA.Utilities;
using Zenject;

namespace SoundReplacer.UI
{
    [ViewDefinition("SoundReplacer.SettingsView.bsml")]
    [HotReload(RelativePathToLayout = "SettingsView.bsml")]
    internal class SettingsViewController : BSMLAutomaticViewController
    {
        private SongPreviewPlayer _songPreviewPlayer = null!;
        private PluginConfig _config = null!;
        private SoundLoader _soundLoader = null!;
        private BasicUIAudioManager _basicUIAudioManager = null!;
        private int _refreshVersion;

        [Inject]
        private void Construct(SongPreviewPlayer songPreviewPlayer, PluginConfig config, SoundLoader soundLoader)
        {
            _songPreviewPlayer = songPreviewPlayer;
            _config = config;
            _soundLoader = soundLoader;
        }

        private void Awake()
        {
            _basicUIAudioManager = BeatSaberUI.BasicUIAudioManager;
        }

        public async void RefreshSoundList()
        {
            int version = ++_refreshVersion;
            try
            {
                var directory = Path.Combine(UnityGame.UserDataPath, nameof(SoundReplacer));
                var sounds = await Task.Run(() =>
                {
                    var directoryInfo = new DirectoryInfo(directory);
                    directoryInfo.Create();
                    return SoundLoader.DefaultSounds
                        .Concat(directoryInfo
                            .EnumerateFiles("*", SearchOption.AllDirectories)
                            .Where(f => f.Extension is ".ogg" or ".mp3" or ".wav")
                            .Select(f => f.Name))
                        .ToArray();
                });
                if (this == null || version != _refreshVersion) return;
                SoundList = sounds;

                NotifyPropertyChanged(nameof(SoundList));
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Could not load sounds. {ex}");
            }
        }

        [UIValue("sound-list")]
        protected string[] SoundList { get; private set; } = SoundLoader.DefaultSounds;

        [UIValue("good-hitsound")]
        protected string SettingCurrentGoodHitSound
        {
            get => _config.CutSound;
            set
            {
                _config.CutSound = value;
                _soundLoader.Preload(SoundType.Cut);
            }
        }

        [UIValue("bad-hitsound")]
        protected string SettingCurrentBadHitSound
        {
            get => _config.BadCutSound;
            set
            {
                _config.BadCutSound = value;
                _soundLoader.Preload(SoundType.BadCut);
            }
        }

        [UIValue("menu-music")]
        protected string SettingCurrentMenuMusic
        {
            get => _config.MenuMusic;
            set
            {
                _config.MenuMusic = value;
                _soundLoader.Preload(SoundType.Menu);
                _songPreviewPlayer.CrossfadeToDefault();
            }
        }

        [UIValue("click-sound")]
        protected string SettingCurrentClickSound
        {
            get => _config.ClickSound;
            set
            {
                _config.ClickSound = value;
                _soundLoader.Preload(SoundType.Click);
                _basicUIAudioManager.Start();
            }
        }

        [UIValue("success-sound")]
        protected string SettingCurrentSuccessSound
        {
            get => _config.LevelClearedSound;
            set
            {
                _config.LevelClearedSound = value;
                _soundLoader.Preload(SoundType.LevelCleared);
            }
        }

        [UIValue("fail-sound")]
        protected string SettingCurrentFailSound
        {
            get => _config.LevelFailedSound;
            set
            {
                _config.LevelFailedSound = value;
                _soundLoader.Preload(SoundType.LevelFailed);
            }
        }
    }
}
