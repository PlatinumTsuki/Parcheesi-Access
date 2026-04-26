using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Parcheesi.Audio;

/// <summary>
/// Moteur audio : précharge les .ogg, joue avec panoramique stéréo et volume,
/// supporte les variations (plusieurs fichiers pour un même effet, choisis aléatoirement).
/// Les sons sont chargés via une fonction "openSound" qui peut puiser dans les
/// ressources embarquées du .exe ou dans le système de fichiers.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private readonly Dictionary<SoundEffect, List<(byte[] pcm, WaveFormat fmt)>> _samples = new();
    private readonly List<IWavePlayer> _activePlayers = new();
    private readonly Lock _lock = new();
    private readonly Func<string, Stream?> _openSound;
    private readonly Random _rng = new();

    private IWavePlayer? _musicPlayer;
    private WaveStream? _musicReader;
    private Stream? _musicSource;
    private VolumeSampleProvider? _musicVolume;
    private float _musicBaseVolume;
    private bool _musicStopRequested;

    private readonly Dictionary<SoundEffect, IWavePlayer> _ambientLoops = new();

    public bool Enabled { get; set; } = true;
    public float MasterVolume { get; set; } = 0.85f;

    /// <summary>
    /// Liste des fichiers audio qui ont échoué au préchargement (introuvables ou corrompus).
    /// Reste vide en fonctionnement normal. Permet à l'UI d'écrire un log de diagnostic
    /// si un asset est manquant après build ou un .ogg corrompu.
    /// </summary>
    public List<string> PreloadFailures { get; } = new();

    /// <summary>
    /// Construit un moteur audio à partir d'une fonction qui ouvre un fichier son
    /// par son nom (par exemple "dice_shake_1.ogg"). Retourne null si introuvable.
    /// </summary>
    public AudioEngine(Func<string, Stream?> openSound)
    {
        _openSound = openSound;
    }

    /// <summary>Constructeur de commodité : charge depuis un dossier sur disque.</summary>
    public AudioEngine(string soundsRoot)
        : this(name =>
        {
            var path = Path.Combine(soundsRoot, name);
            return File.Exists(path) ? File.OpenRead(path) : null;
        })
    {
    }

    public void Preload()
    {
        var mapping = new Dictionary<SoundEffect, string[]>
        {
            // Dés : 3 variantes shake + 3 throw (tirées au hasard à chaque lancer)
            { SoundEffect.DiceShake,  new[] { "dice_shake_1.ogg", "dice_shake_2.ogg", "dice_shake_3.ogg" } },
            { SoundEffect.DiceThrow,  new[] { "dice_throw_1.ogg", "dice_throw_2.ogg", "dice_throw_3.ogg" } },

            // Sélection : 3 variantes (tirage aléatoire)
            { SoundEffect.PieceSelect, new[] { "piece_select_1.ogg", "piece_select_2.ogg", "piece_select_3.ogg" } },
            { SoundEffect.TurnChange,  new[] { "turn_change.ogg" } },
            { SoundEffect.Error,       new[] { "error.ogg" } },

            // Déplacement par couleur (un son fixe par couleur)
            { SoundEffect.PieceMoveRouge, new[] { "piece_move_rouge.ogg" } },
            { SoundEffect.PieceMoveJaune, new[] { "piece_move_jaune.ogg" } },
            { SoundEffect.PieceMoveBleu,  new[] { "piece_move_bleu.ogg" } },
            { SoundEffect.PieceMoveVert,  new[] { "piece_move_vert.ogg" } },

            // Événements (avec variations)
            { SoundEffect.SafeEntry,    new[] { "safe_entry_1.ogg", "safe_entry_2.ogg" } },
            { SoundEffect.LaneEntry,    new[] { "lane_entry.ogg" } },
            { SoundEffect.BaseExit,     new[] { "base_exit.ogg" } },
            { SoundEffect.Blocked,      new[] { "blocked.ogg" } },
            { SoundEffect.CanLeaveBase, new[] { "can_leave_base.ogg" } },
            { SoundEffect.AIThinking,   new[] { "ai_thinking.ogg" } },

            // Capture, maison, victoire : 3 variantes chacun (paysage sonore varié)
            { SoundEffect.Capture,      new[] { "capture_1.ogg", "capture_2.ogg", "capture_3.ogg" } },
            { SoundEffect.HomeArrive,   new[] { "home_arrive_1.ogg", "home_arrive_2.ogg", "home_arrive_3.ogg" } },
            { SoundEffect.Victory,      new[] { "victory_1.ogg", "victory_2.ogg", "victory_3.ogg" } },

            // Timer (sons dédiés, 3 variantes pour le tick)
            { SoundEffect.TimerTick,     new[] { "timer_tick_1.ogg", "timer_tick_2.ogg", "timer_tick_3.ogg" } },
            { SoundEffect.TimerWarn10s,  new[] { "timer_warn_10s.ogg" } },
            { SoundEffect.TimerWarn5s,   new[] { "timer_warn_5s.ogg" } },
            { SoundEffect.TimerExpired,  new[] { "timer_expired.ogg" } },

            // Pause / reprise (mirroirs)
            { SoundEffect.Pause,         new[] { "pause.ogg" } },
            { SoundEffect.Resume,        new[] { "resume.ogg" } },

            // Urgence (cloche dramatique)
            { SoundEffect.Urgency,       new[] { "urgency.ogg" } },

            // Ambiance salon victorien
            { SoundEffect.FireplaceLoop,     new[] { "fireplace.wav" } },
            { SoundEffect.MantelClockTick,   new[] { "mantel_clock.mp3" } },
            { SoundEffect.PoliteApplause,    new[] { "polite_applause.mp3" } },
            { SoundEffect.StandingApplause,  new[] { "applause_standing.mp3" } },
            { SoundEffect.ScatteredApplause, new[] { "applause_scattered.mp3" } },
        };

        foreach (var (effect, files) in mapping)
        {
            var samples = new List<(byte[], WaveFormat)>();
            foreach (var file in files)
            {
                using var srcStream = _openSound(file);
                if (srcStream == null)
                {
                    PreloadFailures.Add($"{file}: introuvable");
                    continue;
                }
                // Copie en mémoire pour avoir un Stream seekable utilisable par tous les readers.
                using var ms = new MemoryStream();
                srcStream.CopyTo(ms);
                ms.Position = 0;
                try
                {
                    using WaveStream reader = OpenReader(ms, file);
                    var sampleProvider = reader.ToSampleProvider();
                    var list = new List<float>();
                    var buf = new float[4096];
                    int read;
                    while ((read = sampleProvider.Read(buf, 0, buf.Length)) > 0)
                        for (int i = 0; i < read; i++) list.Add(buf[i]);
                    var pcm = new byte[list.Count * 4];
                    Buffer.BlockCopy(list.ToArray(), 0, pcm, 0, pcm.Length);
                    samples.Add((pcm, WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels)));
                }
                catch (Exception ex)
                {
                    PreloadFailures.Add($"{file}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            if (samples.Count > 0)
                _samples[effect] = samples;
        }
    }

    public void Play(SoundEffect effect, float pan = 0f, float volume = 1f)
    {
        if (!Enabled) return;
        if (!_samples.TryGetValue(effect, out var variants) || variants.Count == 0) return;

        var (pcm, fmt) = variants[_rng.Next(variants.Count)];
        try
        {
            var ms = new MemoryStream(pcm);
            var raw = new RawSourceWaveStream(ms, fmt);
            ISampleProvider source = raw.ToSampleProvider();
            if (source.WaveFormat.Channels == 1) source = new MonoToStereoSampleProvider(source);
            var panProvider = new PanningSampleProvider(ToMono(source))
            {
                Pan = Math.Clamp(pan, -1f, 1f)
            };
            ISampleProvider final = new VolumeSampleProvider(panProvider)
            {
                Volume = MasterVolume * Math.Clamp(volume, 0f, 1f)
            };

            var player = new WaveOutEvent();
            player.Init(final);
            player.PlaybackStopped += (_, _) =>
            {
                lock (_lock) _activePlayers.Remove(player);
                player.Dispose();
                ms.Dispose();
            };
            lock (_lock) _activePlayers.Add(player);
            player.Play();
        }
        catch { }
    }

    /// <summary>
    /// Joue N fois le son en succession rapide, en faisant glisser le panoramique
    /// du point de départ vers le point d'arrivée.
    /// </summary>
    public async Task PlayWalkAsync(SoundEffect effect, float startPan, float endPan, int steps,
                                    int delayMs = 130, float volume = 0.8f)
    {
        if (!Enabled || steps <= 0) return;
        for (int i = 0; i < steps; i++)
        {
            var t = steps == 1 ? 1f : i / (float)(steps - 1);
            var pan = startPan + (endPan - startPan) * t;
            Play(effect, pan, volume);
            if (i < steps - 1) await Task.Delay(delayMs);
        }
    }

    private static ISampleProvider ToMono(ISampleProvider source)
    {
        if (source.WaveFormat.Channels == 1) return source;
        return new StereoToMonoSampleProvider(source);
    }

    private static WaveStream OpenReader(Stream stream, string filename)
    {
        if (filename.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return new Mp3FileReader(stream);
        if (filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            return new WaveFileReader(stream);
        return new VorbisWaveReader(stream, closeOnDispose: false);
    }

    /// <summary>
    /// Joue une musique en streaming (pas pré-décodée en RAM, contrairement aux effets courts).
    /// Toute musique en cours est arrêtée avant le démarrage.
    /// <paramref name="onEnd"/> est invoqué quand la lecture se termine naturellement
    /// (mais PAS quand <see cref="StopMusic"/> est appelée), permettant de chaîner une playlist.
    /// </summary>
    public void PlayMusic(string filename, float volume = 0.3f, Action? onEnd = null)
    {
        if (!Enabled) return;
        StopMusic();
        lock (_lock) { _musicStopRequested = false; }
        var stream = _openSound(filename);
        if (stream == null)
        {
            // Asynchrone via ThreadPool : si onEnd chaine la playlist (PlayNextGameMusic),
            // un appel synchrone provoquerait une récursion de pile illimitée si tous les
            // fichiers manquaient. ThreadPool casse la chaîne de pile.
            if (onEnd != null) Task.Run(onEnd);
            return;
        }
        try
        {
            // Stream seekable nécessaire pour Mp3FileReader.
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            stream.Dispose();

            var reader = OpenReader(ms, filename);
            ISampleProvider source = reader.ToSampleProvider();
            if (source.WaveFormat.Channels == 1) source = new MonoToStereoSampleProvider(source);
            var clamped = Math.Clamp(volume, 0f, 1f);
            var volProvider = new VolumeSampleProvider(source)
            {
                Volume = MasterVolume * clamped,
            };

            var player = new WaveOutEvent();
            player.Init(volProvider);
            player.PlaybackStopped += (_, _) =>
            {
                bool wasNaturalEnd;
                lock (_lock)
                {
                    wasNaturalEnd = !_musicStopRequested && _musicPlayer == player;
                    if (_musicPlayer == player)
                    {
                        _musicPlayer = null;
                        _musicReader = null;
                        _musicSource = null;
                        _musicVolume = null;
                    }
                }
                player.Dispose();
                reader.Dispose();
                ms.Dispose();
                if (wasNaturalEnd) onEnd?.Invoke();
            };
            lock (_lock)
            {
                _musicPlayer = player;
                _musicReader = reader;
                _musicSource = ms;
                _musicVolume = volProvider;
                _musicBaseVolume = clamped;
            }
            player.Play();
        }
        catch { }
    }

    public void StopMusic()
    {
        IWavePlayer? p;
        lock (_lock)
        {
            p = _musicPlayer;
            _musicPlayer = null;
            _musicStopRequested = true;
        }
        try { p?.Stop(); } catch { }
    }

    /// <summary>
    /// Ajuste le volume de la musique en cours de lecture (slider live).
    /// Sans effet si aucune musique ne joue.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        var clamped = Math.Clamp(volume, 0f, 1f);
        lock (_lock)
        {
            _musicBaseVolume = clamped;
            if (_musicVolume != null)
                _musicVolume.Volume = MasterVolume * clamped;
        }
    }

    /// <summary>
    /// Joue un effet en boucle continue (cheminée, horloge…). Remplace toute boucle déjà active
    /// pour ce SoundEffect. Le sample doit être préchargé via Preload.
    /// </summary>
    public void PlayAmbientLoop(SoundEffect effect, float volume, float pan = 0f)
    {
        if (!Enabled) return;
        if (!_samples.TryGetValue(effect, out var variants) || variants.Count == 0) return;
        StopAmbientLoop(effect);
        try
        {
            var (pcm, fmt) = variants[0];
            var raw = new RawSourceWaveStream(new MemoryStream(pcm), fmt);
            var loop = new LoopStream(raw);
            ISampleProvider source = loop.ToSampleProvider();
            if (source.WaveFormat.Channels == 1) source = new MonoToStereoSampleProvider(source);
            var panProvider = new PanningSampleProvider(ToMono(source))
            {
                Pan = Math.Clamp(pan, -1f, 1f),
            };
            ISampleProvider final = new VolumeSampleProvider(panProvider)
            {
                Volume = MasterVolume * Math.Clamp(volume, 0f, 1f),
            };

            var player = new WaveOutEvent();
            player.Init(final);
            player.Play();
            lock (_lock) _ambientLoops[effect] = player;
        }
        catch { }
    }

    public void StopAmbientLoop(SoundEffect effect)
    {
        IWavePlayer? p;
        lock (_lock)
        {
            if (!_ambientLoops.TryGetValue(effect, out p)) return;
            _ambientLoops.Remove(effect);
        }
        try { p.Stop(); p.Dispose(); } catch { }
    }

    public void StopAllAmbientLoops()
    {
        IWavePlayer[] players;
        lock (_lock)
        {
            players = _ambientLoops.Values.ToArray();
            _ambientLoops.Clear();
        }
        foreach (var p in players)
        {
            try { p.Stop(); p.Dispose(); } catch { }
        }
    }

    public void Dispose()
    {
        StopMusic();
        StopAllAmbientLoops();
        lock (_lock)
        {
            foreach (var p in _activePlayers) p.Dispose();
            _activePlayers.Clear();
        }
    }
}

/// <summary>WaveStream qui boucle indéfiniment sur le source jusqu'à Stop explicite.</summary>
internal sealed class LoopStream : WaveStream
{
    private readonly WaveStream _source;

    public LoopStream(WaveStream source) { _source = source; }

    public override WaveFormat WaveFormat => _source.WaveFormat;
    public override long Length => long.MaxValue;
    public override long Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = _source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                if (_source.Length == 0) break;
                _source.Position = 0;
                continue;
            }
            total += read;
        }
        return total;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
}
