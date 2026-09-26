using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
using Object = UnityEngine.Object;

namespace SoundReplacer
{
    internal class SoundLoader : IInitializable, IDisposable
    {
        public const string NoSoundID = "None";
        public const string DefaultSoundID = "Default";
        public static readonly string[] DefaultSounds = { NoSoundID, DefaultSoundID };
        // NoteCutSoundEffect needs a nonzero duration even when muted.
        public static readonly AudioClip Empty = AudioClip.Create("Empty", 44100, 1, 44100, false);
        private readonly PluginConfig _config;
        private readonly Dictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<SoundType, Handle> _preloads = new();
        private bool _disposed;

        private SoundLoader(PluginConfig config) => _config = config;
        public void Initialize()
        {
            foreach (SoundType type in Enum.GetValues(typeof(SoundType))) Preload(type);
        }
        public Handle CreateHandle(SoundType type) => new(this, type);
        public void Preload(SoundType type)
        {
            if (!_preloads.TryGetValue(type, out var handle))
            {
                handle = CreateHandle(type);
                _preloads.Add(type, handle);
            }
            handle.Load((_, _) => { });
        }

        private static AudioType GetAudioTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)) return AudioType.OGGVORBIS;
            if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)) return AudioType.MPEG;
            if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)) return AudioType.WAV;
            return AudioType.UNKNOWN;
        }

        private static async Task<AudioClip> LoadAudioClipAsync(string fileName)
        {
            try
            {
                var directory = Path.Combine(UnityGame.UserDataPath, nameof(SoundReplacer));
                var filePath = await Task.Run(() => Directory.Exists(directory)
                    ? Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories).FirstOrDefault()
                    : null);
                if (filePath is null)
                {
                    Plugin.Log.Error($"Could not find sound {fileName}.");
                    return Empty;
                }
                using var request = UnityWebRequestMultimedia.GetAudioClip(FileHelpers.GetEscapedURLForFilePath(filePath), GetAudioTypeFromPath(filePath));
                var operation = request.SendWebRequest();
                if (!operation.isDone)
                {
                    var completion = new TaskCompletionSource<bool>();
                    operation.completed += _ => completion.TrySetResult(true);
                    await completion.Task;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.Error($"Failed to load file {filePath} with error {request.error}.");
                    return Empty;
                }
                return DownloadHandlerAudioClip.GetContent(request) ?? Empty;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Could not load sound {fileName}: {ex}");
                return Empty;
            }
        }

        private string GetSoundFileName(SoundType type) => type switch
        {
            SoundType.Cut => _config.CutSound,
            SoundType.BadCut => _config.BadCutSound,
            SoundType.Menu => _config.MenuMusic,
            SoundType.Click => _config.ClickSound,
            SoundType.LevelCleared => _config.LevelClearedSound,
            SoundType.LevelFailed => _config.LevelFailedSound,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private Entry Acquire(string fileName)
        {
            if (!_cache.TryGetValue(fileName, out var entry))
            {
                entry = new Entry(fileName);
                _cache.Add(fileName, entry);
                entry.Loading = LoadAudioClipAsync(fileName);
            }
            entry.References++;
            return entry;
        }
        private void Release(Entry? entry)
        {
            if (entry is null || entry.References == 0 || --entry.References != 0) return;
            _cache.Remove(entry.FileName);
            DestroyWhenReady(entry);
        }
        private static async void DestroyWhenReady(Entry entry)
        {
            var clip = await entry.Loading;
            if (clip != null && clip != Empty) Object.Destroy(clip);
        }

        // SongPreviewPlayer reports when a channel stops using its clip.
        public Action? RetainForPlayback(AudioClip clip)
        {
            foreach (var entry in _cache.Values)
            {
                if (!entry.Loading.IsCompleted || entry.Loading.Result != clip || clip == Empty) continue;
                entry.References++;
                bool released = false;
                return () =>
                {
                    if (released) return;
                    released = true;
                    Release(entry);
                };
            }
            return null;
        }
        public void Dispose()
        {
            _disposed = true;
            foreach (var entry in _cache.Values)
            {
                entry.References = 0;
                DestroyWhenReady(entry);
            }
            _cache.Clear();
            foreach (var handle in _preloads.Values) handle.Dispose();
            _preloads.Clear();
        }

        private sealed class Entry
        {
            public readonly string FileName;
            public Task<AudioClip> Loading = null!;
            public int References;
            public Entry(string fileName) => FileName = fileName;
        }

        internal sealed class Handle : IDisposable
        {
            private readonly SoundLoader _loader;
            private readonly SoundType _type;
            private Entry? _current;
            private Entry? _pending;
            private int _version;
            private bool _disposed;
            internal Handle(SoundLoader loader, SoundType type)
            {
                _loader = loader;
                _type = type;
            }
            // Null means the game default. Ignore callbacks after replacement/disposal.
            public async void Load(Action<AudioClip?, bool> apply)
            {
                if (_disposed || _loader._disposed) return;
                int version = ++_version;
                var fileName = _loader.GetSoundFileName(_type);
                try
                {
                    Entry? entry = null;
                    if (fileName != NoSoundID && fileName != DefaultSoundID)
                        entry = StringComparer.OrdinalIgnoreCase.Equals(_current?.FileName, fileName) ? _current
                            : StringComparer.OrdinalIgnoreCase.Equals(_pending?.FileName, fileName) ? _pending
                            : _loader.Acquire(fileName);
                    if (_pending != entry) _loader.Release(_pending);
                    _pending = entry == _current ? null : entry;
                    bool delayed = entry != null && !entry.Loading.IsCompleted;
                    AudioClip? clip = entry != null ? await entry.Loading : fileName == NoSoundID ? Empty : null;
                    if (_disposed || _loader._disposed || version != _version) return;
                    var previous = _current;
                    _current = entry;
                    _pending = null;
                    try { apply(clip, delayed); }
                    finally { if (previous != entry) _loader.Release(previous); }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Could not apply {_type} sound: {ex}");
                }
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _version++;
                _loader.Release(_pending);
                _loader.Release(_current);
                _pending = _current = null;
            }
        }
    }
}
