using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Parcheesi.App.Game;
using Parcheesi.Core.Localization;
using Parcheesi.Audio;
using Parcheesi.Core;
using CoreGame = Parcheesi.Core.Game;

namespace Parcheesi.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    /// <summary>Seuil "speed run" : moins de tours = victoire considérée rapide (achievement, palier triomphe).</summary>
    private const int SpeedRunThreshold = 30;
    /// <summary>Seuil "tour rapide" pour la phrase contextuelle "course menée tambour battant".</summary>
    private const int FastWinThreshold = 50;

    private CoreGame? _game;
    public CoreGame? Game => _game;

    public BoardModel? Board { get; private set; }
    public AudioEngine Audio { get; }
    public AIDifficulty AIDifficulty { get; set; } = AIDifficulty.Moyen;
    public GameStats Stats { get; }
    public Achievements Achievements { get; }
    public Settings Settings { get; }

    /// <summary>Compteurs locaux de la partie en cours, pour fusionner dans les stats à la fin.</summary>
    private int _turnsThisGame = 0;
    private int _capturesMadeThisGame = 0;
    private int _capturesReceivedThisGame = 0;
    private int _piecesHomeThisGame = 0;
    private bool _statsRecordedForCurrentGame = false;

    /// <summary>
    /// Suivi du nombre de pions à la maison de chaque adversaire pour détecter
    /// les passages de palier (3 pions = urgence, 4 pions = victoire).
    /// </summary>
    private readonly Dictionary<PlayerColor, int> _opponentHomeCounts = new();

    // --- Pause ---
    private bool _isPaused;
    public bool IsPaused { get => _isPaused; private set { _isPaused = value; OnPropertyChanged(); } }

    private readonly List<System.Windows.Threading.DispatcherTimer> _activeAITimers = new();

    // --- Timer de tour ---
    private DateTime _turnStartTime;
    private System.Windows.Threading.DispatcherTimer? _turnTimer;
    private bool _warnedHalf, _warned10s, _warned5s;
    private double _pausedRemainingSeconds; // utilisé pour reprendre après pause

    public ObservableCollection<string> LogEntries { get; } = new();

    private string _turnInfo = "";
    public string TurnInfo
    {
        get => _turnInfo;
        set { if (_turnInfo != value) { _turnInfo = value; OnPropertyChanged(); } }
    }

    private string _diceInfo = "";
    public string DiceInfo
    {
        get => _diceInfo;
        set { if (_diceInfo != value) { _diceInfo = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Événement levé chaque fois que le jeu veut faire une annonce.
    /// </summary>
    public event Action<string>? AnnounceRequested;

    /// <summary>Dernière annonce, pour la touche R (réécoute).</summary>
    private string _lastAnnouncement = "";

    private bool _isInGame;
    public bool IsInGame
    {
        get => _isInGame;
        set
        {
            if (_isInGame != value)
            {
                _isInGame = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInSetup));
                OnPropertyChanged(nameof(IsInEndScreen));
            }
        }
    }

    private bool _isInEndScreen;
    public bool IsInEndScreen
    {
        get => _isInEndScreen;
        set
        {
            if (_isInEndScreen != value)
            {
                _isInEndScreen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInSetup));
            }
        }
    }

    private bool _isInSettings;
    public bool IsInSettings
    {
        get => _isInSettings;
        set
        {
            if (_isInSettings != value)
            {
                _isInSettings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInSetup));
            }
        }
    }

    public bool IsInSetup => !_isInGame && !_isInEndScreen && !_isInSettings;

    public void OpenSettings() => IsInSettings = true;
    public void CloseSettings() => IsInSettings = false;

    /// <summary>Statut affiché en haut du menu principal.</summary>
    public string MenuStatusLine
    {
        get
        {
            if (Stats.GamesPlayed == 0) return Loc.Get("menu.no_games");
            return Loc.Format("menu.status_line", Stats.GamesPlayed, Stats.GamesWon, $"{Stats.WinRate * 100:F0}");
        }
    }

    private string _endGameSummary = "";
    public string EndGameSummary
    {
        get => _endGameSummary;
        set { _endGameSummary = value; OnPropertyChanged(); }
    }

    /// <summary>Conserve la dernière configuration pour permettre "Rejouer même config".</summary>
    private bool[]? _lastAiPerSlot;
    private AIDifficulty _lastDifficulty = AIDifficulty.Moyen;

    private int _selectedPieceId = 0;
    public int SelectedPieceId
    {
        get => _selectedPieceId;
        set { _selectedPieceId = value; OnPropertyChanged(); OnPropertyChanged(nameof(MyPiecesDescription)); }
    }

    public string MyPiecesDescription
    {
        get
        {
            if (_game == null) return "";
            var p = _game.Current;
            var lines = p.Pieces.Select(piece =>
            {
                var prefix = piece.Id == _selectedPieceId ? "→ " : "   ";
                return $"{prefix}{piece.Describe()}";
            });
            return string.Join("\n", lines);
        }
    }

    public MainViewModel()
    {
        Settings = Settings.Load();

        // Localisation : si pas encore définie (premier lancement), auto-détection depuis la locale système.
        var asm = typeof(MainViewModel).Assembly;
        if (string.IsNullOrEmpty(Settings.Language))
            Settings.Language = Loc.DetectSystemLanguage();
        Loc.Init(Settings.Language, lang => asm.GetManifestResourceStream($"Lang.{lang}.json"));

        // Maintenant que la localisation est chargée, initialise les valeurs par défaut localisées.
        _turnInfo = Loc.Get("turn_info.default");
        _diceInfo = Loc.Get("dice_info.none");
        _tutorialChunks = BuildTutorialChunks();

        // Sons embarqués comme ressources dans le .exe (logical name "Sounds.<filename>").
        Audio = new AudioEngine(name => asm.GetManifestResourceStream($"Sounds.{name}"));
        Audio.Preload();
        Audio.MasterVolume = Settings.MasterVolume;

        // Diagnostic : si des sons embarqués ont échoué au préchargement (.exe cassé ou
        // asset corrompu), trace ça dans audio_load.log à côté de l'.exe. Le fichier
        // n'est créé que s'il y a au moins un échec.
        if (Audio.PreloadFailures.Count > 0)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "audio_load.log");
                var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {Audio.PreloadFailures.Count} effet(s) audio en échec :\n"
                            + string.Join("\n", Audio.PreloadFailures.Select(f => "  - " + f));
                File.WriteAllText(path, content);
            }
            catch { /* ne pas crasher si le log lui-même échoue */ }
        }
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.MasterVolume))
                Audio.MasterVolume = Settings.MasterVolume;
            else if (e.PropertyName == nameof(Settings.AmbienceMusicVolume))
                Audio.SetMusicVolume(Settings.AmbienceMusicVolume * _currentMusicGain);
            else if (e.PropertyName == nameof(Settings.ImmersiveAmbience) && !Settings.ImmersiveAmbience)
            {
                Audio.StopMusic();
                Audio.StopAllAmbientLoops();
            }
        };
        Stats = GameStats.Load();
        Achievements = Achievements.Load();
    }

    public void ReadAchievements() => Announce(Achievements.Summary());

    /// <summary>
    /// Démarre la musique de salon (Chopin Nocturne) si l'ambiance est activée.
    /// Appelé au lancement de l'app et au retour au menu après une partie.
    /// </summary>
    public void StartMenuAmbience()
    {
        if (Settings.ImmersiveAmbience)
        {
            _currentMusicGain = 1.0f; // Chopin Nocturne menu : pas de compensation pour l'instant
            Audio.PlayMusic("parlor_music_menu.ogg", Settings.AmbienceMusicVolume);
        }
    }

    /// <summary>
    /// Playlist de musiques d'ambiance pour le jeu (toutes Ogg Vorbis, salon victorien).
    /// Le second élément de chaque tuple est le gain de normalisation : multiplicateur
    /// appliqué pour ramener tous les morceaux à un niveau perçu uniforme (cible RMS -28 dB,
    /// peak max -1 dB). Mesuré empiriquement avec _levelmeter.
    /// </summary>
    private static readonly (string File, float Gain)[] GameMusicPlaylist = new[]
    {
        ("satie_gymnopedie_1.ogg",     1.12f),
        ("satie_gymnopedie_2.ogg",     0.56f),
        ("satie_gymnopedie_3.ogg",     0.53f),
        ("satie_gnossienne_1.ogg",     2.29f),
        ("schumann_traumerei.ogg",     1.71f),
        ("chopin_berceuse.ogg",        1.92f),
        ("debussy_clair_de_lune.ogg",  0.79f),
        ("chopin_raindrop.ogg",        1.34f),
    };

    private readonly List<int> _gameMusicQueue = new();
    private readonly Random _musicRng = new();
    /// <summary>Gain de normalisation du morceau actuellement en lecture (1.0 = pas de musique ou pas de compensation).</summary>
    private float _currentMusicGain = 1.0f;

    /// <summary>
    /// Joue le morceau suivant de la playlist. Quand il finit, se rappelle automatiquement
    /// (chaînage via PlayMusic.onEnd). La file est ré-mélangée quand vidée — pas de répétition
    /// avant que toute la playlist ait été parcourue.
    /// </summary>
    private void PlayNextGameMusic()
    {
        if (!Settings.ImmersiveAmbience || !IsInGame) return;
        if (_gameMusicQueue.Count == 0)
        {
            var indices = Enumerable.Range(0, GameMusicPlaylist.Length).ToList();
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = _musicRng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            _gameMusicQueue.AddRange(indices);
        }
        var idx = _gameMusicQueue[0];
        _gameMusicQueue.RemoveAt(0);
        var (file, gain) = GameMusicPlaylist[idx];
        _currentMusicGain = gain;
        Audio.PlayMusic(file, Settings.AmbienceMusicVolume * gain, onEnd: PlayNextGameMusic);
    }

    private int _tutorialIndex = -1;

    /// <summary>
    /// Étapes du tutoriel résolues à la volée depuis Loc (suit la langue active).
    /// </summary>
    private static string[] BuildTutorialChunks() => new[]
    {
        Loc.Get("tutorial.step_1"),
        Loc.Get("tutorial.step_2"),
        Loc.Get("tutorial.step_3"),
        Loc.Get("tutorial.step_4"),
        Loc.Get("tutorial.step_5"),
        Loc.Get("tutorial.step_6"),
        Loc.Get("tutorial.step_7"),
        Loc.Get("tutorial.step_8"),
        Loc.Get("tutorial.step_9"),
        Loc.Get("tutorial.step_10"),
    };
    private string[] _tutorialChunks = Array.Empty<string>();

    public bool TutorialIsRunning => _tutorialIndex >= 0;

    /// <summary>Démarre le tutoriel à pas-à-pas (l'utilisateur avance avec Espace).</summary>
    public void StartTutorial()
    {
        Settings.HasSeenTutorialPrompt = true;
        _tutorialIndex = 0;
        AnnounceRequested?.Invoke(_tutorialChunks[0]);
    }

    /// <summary>Avance d'une étape dans le tutoriel. Appelé sur Espace.</summary>
    public bool AdvanceTutorial()
    {
        if (_tutorialIndex < 0) return false;
        _tutorialIndex++;
        if (_tutorialIndex >= _tutorialChunks.Length)
        {
            _tutorialIndex = -1;
            return true; // fini
        }
        AnnounceRequested?.Invoke(_tutorialChunks[_tutorialIndex]);
        return true;
    }

    /// <summary>Sort du tutoriel sans le finir. Appelé sur Échap.</summary>
    public void InterruptTutorial()
    {
        if (_tutorialIndex >= 0)
        {
            _tutorialIndex = -1;
            AnnounceRequested?.Invoke(Loc.Get("tutorial.exited"));
        }
    }

    /// <summary>
    /// Donne le coup recommandé par l'IA Difficile pour la situation courante.
    /// Touche C en jeu.
    /// </summary>
    public void GiveTacticalAdvice()
    {
        if (_game == null) { Announce(Loc.Get("error.no_game")); return; }
        if (_game.AwaitingRoll) { Announce(Loc.Get("error.roll_dice_first")); return; }
        if (_game.Current.IsComputer) { Announce(Loc.Get("error.computer_turn")); return; }

        var action = ComputerPlayer.DecideNextAction(_game, AIDifficulty.Difficile);
        if (action == null)
        {
            Announce(Loc.Get("error.no_legal_move"));
            return;
        }
        var preview = _game.Preview(action.Piece, action.Steps);
        var dieLabel = action.Usage switch
        {
            DiceUsage.Die1  => Loc.Format("advice.die1", action.Steps),
            DiceUsage.Die2  => Loc.Format("advice.die2", action.Steps),
            DiceUsage.Sum   => Loc.Format("advice.sum", action.Steps),
            DiceUsage.Bonus => Loc.Format("advice.bonus", action.Steps),
            _ => "",
        };
        Announce(Loc.Format("advice.recommended", action.Piece.Id, dieLabel, DescribePreviewOutcome(preview)));
    }

    /// <summary>
    /// Vérifie si un adversaire vient de franchir le seuil de 3 pions à la maison
    /// (= proche de gagner). Joue un son grave d'alerte et annonce l'urgence.
    /// </summary>
    private void CheckUrgencyTransitions()
    {
        if (_game == null) return;
        foreach (var player in _game.Players)
        {
            var prev = _opponentHomeCounts.TryGetValue(player.Color, out var v) ? v : 0;
            var now = player.Pieces.Count(p => p.Status == PieceStatus.Home);
            _opponentHomeCounts[player.Color] = now;

            // Seuil 3 : alerte uniquement pour les adversaires
            if (now == 3 && prev < 3 && !player.HasFinished())
            {
                Audio.Play(SoundEffect.Urgency, 0f, 0.9f);
                Announce(player.IsComputer
                    ? Loc.Format("urgency.three_pieces_home_ai", player.Label)
                    : Loc.Format("urgency.three_pieces_home_human", player.Label));
            }
        }
    }

    /// <summary>Vérifie si une capture ou un retour à la maison est possible avec les dés courants.</summary>
    private string? DetectOpportunity()
    {
        if (_game == null || _game.Current.IsComputer) return null;
        bool capture = false, home = false;
        var steps = _game.AvailableSteps();
        foreach (var piece in _game.Current.Pieces)
        {
            foreach (var s in steps)
            {
                var preview = _game.Preview(piece, s);
                if (!preview.Ok) continue;
                if (preview.Captures != null) capture = true;
                if (preview.ReachedHome) home = true;
                if (capture && home) break;
            }
            if (capture && home) break;
        }
        if (capture && home) return Loc.Get("opportunity.capture_and_home");
        if (capture) return Loc.Get("opportunity.capture");
        if (home) return Loc.Get("opportunity.home");
        return null;
    }

    private static string SaveFilePath => Path.Combine(AppContext.BaseDirectory, "current_game.json");

    public bool HasSavedGame() => File.Exists(SaveFilePath);

    /// <summary>
    /// Annonce une seule fois par session qu'une sauvegarde échoue, pour ne pas spammer
    /// si le disque est plein ou les permissions cassées (chaque coup déclenche une save).
    /// </summary>
    private bool _saveFailureAnnounced = false;

    private void SaveCurrentGame()
    {
        if (_game == null || _game.Finished) return;
        try
        {
            var snap = _game.ToSnapshot(AIDifficulty);
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(snap, opts);
            File.WriteAllText(SaveFilePath, json);
            _saveFailureAnnounced = false; // reset si la sauvegarde refonctionne
        }
        catch
        {
            if (!_saveFailureAnnounced)
            {
                _saveFailureAnnounced = true;
                Announce(Loc.Get("error.save_failed"));
            }
        }
    }

    private void DeleteSavedGame()
    {
        try { if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath); } catch { }
    }

    public bool ResumeSavedGame()
    {
        try
        {
            if (!File.Exists(SaveFilePath)) return false;
            var json = File.ReadAllText(SaveFilePath);
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var snap = System.Text.Json.JsonSerializer.Deserialize<GameSnapshot>(json, opts);
            if (snap == null) return false;

            AIDifficulty = snap.Difficulty;
            _game = CoreGame.FromSnapshot(snap);
            Board = new BoardModel(snap.Players.Count);
            Board.RefreshOccupancy(_game);
            IsInGame = true;
            SelectedPieceId = 0;
            LogEntries.Clear();

            _turnsThisGame = 0;
            _capturesMadeThisGame = 0;
            _capturesReceivedThisGame = 0;
            _piecesHomeThisGame = 0;
            _statsRecordedForCurrentGame = false;

            OnPropertyChanged(nameof(Board));
            Log(Loc.Format("game.resumed_log", snap.SavedAt.ToString("dd/MM HH:mm")));
            UpdateTurnInfo();
            if (Settings.ImmersiveAmbience)
            {
                Audio.StopMusic();
                Audio.PlayAmbientLoop(SoundEffect.FireplaceLoop, 0.95f, -0.15f);
                _gameMusicQueue.Clear();
                PlayNextGameMusic();
            }
            Announce(_game.AwaitingRoll
                ? Loc.Format("game.resumed_announce_roll", _game.Current.Label)
                : Loc.Format("game.resumed_announce_choose", _game.Current.Label));
            Audio.Play(SoundEffect.TurnChange);
            if (_game.Current.IsComputer) ScheduleAITurn();
            else StartTurnTimer();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ReadStats()
    {
        AnnounceRequested?.Invoke(Stats.Summary());
    }

    /// <summary>Annonce l'état des dés courants : valeurs, usage, bonus, etc.</summary>
    public void ReadDice()
    {
        if (_game == null) { Announce(Loc.Get("error.no_game")); return; }
        if (_game.AwaitingRoll)
        {
            Announce(Loc.Format("dice.no_roll_yet", _game.Current.Label));
            return;
        }
        var parts = new List<string>();
        if (_game.Die1.HasValue)
            parts.Add(_game.Die1Used
                ? Loc.Format("dice.die1_used", _game.Die1)
                : Loc.Format("dice.die1_available", _game.Die1));
        if (_game.Die2.HasValue)
            parts.Add(_game.Die2Used
                ? Loc.Format("dice.die2_used", _game.Die2)
                : Loc.Format("dice.die2_available", _game.Die2));
        if (_game.Die1.HasValue && _game.Die2.HasValue && !_game.Die1Used && !_game.Die2Used)
            parts.Add(Loc.Format("dice.sum_available", _game.Die1 + _game.Die2));
        if (_game.Die1 == _game.Die2 && _game.Die1.HasValue) parts.Add(Loc.Get("dice.is_double"));
        if (_game.Bonus > 0) parts.Add(Loc.Format("dice.bonus_available", _game.Bonus));
        Announce(string.Join(". ", parts) + ".");
    }

    /// <summary>
    /// Programme un tour d'IA. Pendant le délai de réflexion, joue des pulsations
    /// discrètes pour indiquer à l'utilisateur que l'ordi "pense" et n'est pas bloqué.
    /// </summary>
    private void ScheduleAITurn()
    {
        if (_game == null || _game.Finished || !_game.Current.IsComputer) return;
        var current = _game.Current;
        ScheduleAIPulses(3, Settings.AIThinkPulseMs, () =>
        {
            if (_game == null || _game.Finished || _game.Current != current) return;
            if (_game.AwaitingRoll) RollDice();
            if (_game == null || _game.Finished || _game.Current != current) return;
            ScheduleNextAIMove(current);
        });
    }

    private void ScheduleNextAIMove(Player aiPlayer)
    {
        ScheduleAIPulses(2, Settings.AIThinkPulseMs, () =>
        {
            if (_game == null || _game.Finished || _game.Current != aiPlayer) return;
            var action = ComputerPlayer.DecideNextAction(_game, AIDifficulty);
            if (action == null) { EndTurnManually(); return; }
            SelectedPieceId = action.Piece.Id;
            ApplyMove(action.Usage);
            if (_game != null && !_game.Finished && _game.Current == aiPlayer && !_game.AwaitingRoll)
                ScheduleNextAIMove(aiPlayer);
        });
    }

    /// <summary>
    /// Joue N pulsations sonores discrètes ("tick tick tick") espacées de intervalMs,
    /// puis exécute l'action quand toutes les pulsations sont jouées.
    /// Les timers actifs sont trackés pour pouvoir les arrêter sur pause.
    /// </summary>
    private void ScheduleAIPulses(int pulseCount, int intervalMs, Action onComplete)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        int ticks = 0;
        timer.Tick += (_, _) =>
        {
            if (_isPaused) return; // on attend la reprise
            ticks++;
            if (ticks <= pulseCount) Audio.Play(SoundEffect.AIThinking, 0f, Settings.AIThinkVolume);
            if (ticks >= pulseCount)
            {
                timer.Stop();
                _activeAITimers.Remove(timer);
                onComplete();
            }
        };
        _activeAITimers.Add(timer);
        timer.Start();
    }

    private void StopAllAITimers()
    {
        foreach (var t in _activeAITimers.ToList()) t.Stop();
        _activeAITimers.Clear();
    }

    // ---------- PAUSE ----------

    /// <summary>Bascule pause/reprise. F2 en jeu.</summary>
    public void TogglePause()
    {
        if (!IsInGame || _game == null || _game.Finished) return;
        if (IsPaused)
        {
            // Reprise
            IsPaused = false;
            Audio.Play(SoundEffect.Resume, 0f, 0.7f);
            Announce(Loc.Get("game.resumed_simple"));
            // Reprend les actions appropriées
            if (_game.Current.IsComputer)
                ScheduleAITurn();
            else if (Settings.TurnTimerSeconds > 0)
                ResumeTurnTimer();
        }
        else
        {
            // Mise en pause
            IsPaused = true;
            StopAllAITimers();
            PauseTurnTimer();
            Audio.Play(SoundEffect.Pause, 0f, 0.7f);
            Announce(Loc.Get("game.paused"));
        }
    }

    // ---------- TIMER DE TOUR ----------

    /// <summary>Démarre un nouveau timer pour le tour humain courant.</summary>
    private void StartTurnTimer()
    {
        StopTurnTimer();
        if (_game == null || _game.Current.IsComputer) return;
        if (Settings.TurnTimerSeconds <= 0) return;

        _turnStartTime = DateTime.Now;
        _warnedHalf = _warned10s = _warned5s = false;
        _turnTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _turnTimer.Tick += TurnTimer_Tick;
        _turnTimer.Start();
    }

    /// <summary>Arrête le timer (par exemple à la fin du tour ou en fin de partie).</summary>
    private void StopTurnTimer()
    {
        _turnTimer?.Stop();
        _turnTimer = null;
    }

    private void PauseTurnTimer()
    {
        if (_turnTimer == null) return;
        var elapsed = (DateTime.Now - _turnStartTime).TotalSeconds;
        _pausedRemainingSeconds = Math.Max(0, Settings.TurnTimerSeconds - elapsed);
        StopTurnTimer();
    }

    private void ResumeTurnTimer()
    {
        if (_pausedRemainingSeconds <= 0 || _game == null) return;
        // Décale le start time pour que le restant soit correct
        _turnStartTime = DateTime.Now - TimeSpan.FromSeconds(Settings.TurnTimerSeconds - _pausedRemainingSeconds);
        _turnTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _turnTimer.Tick += TurnTimer_Tick;
        _turnTimer.Start();
    }

    private void TurnTimer_Tick(object? sender, EventArgs e)
    {
        if (_isPaused || _game == null || _game.Finished) return;
        var elapsed = (DateTime.Now - _turnStartTime).TotalSeconds;
        var total = Settings.TurnTimerSeconds;
        var remaining = total - elapsed;

        if (!_warnedHalf && elapsed >= total / 2.0)
        {
            _warnedHalf = true;
            Audio.Play(SoundEffect.TimerTick, 0f, 0.55f);
        }
        if (!_warned10s && remaining <= 10)
        {
            _warned10s = true;
            Announce(Loc.Get("timer.warning_10s"));
            Audio.Play(SoundEffect.TimerWarn10s, 0f, 0.7f);
        }
        if (!_warned5s && remaining <= 5)
        {
            _warned5s = true;
            Audio.Play(SoundEffect.TimerWarn5s, 0f, 0.85f);
        }
        if (remaining <= 0)
        {
            StopTurnTimer();
            Audio.Play(SoundEffect.TimerExpired, 0f, 0.9f);
            HandleTimeOut();
        }
    }

    /// <summary>Gère le dépassement du timer : auto-play par IA ou saut de tour selon réglages.</summary>
    private void HandleTimeOut()
    {
        if (_game == null || _game.Finished || _game.Current.IsComputer) return;

        if (Settings.TimeoutBehavior == TimeoutBehavior.SkipTurn)
        {
            Announce(Loc.Get("timer.expired_skip_announce"));
            Log(Loc.Get("timer.expired_skip_log"));
            var t = _game.NextTurn();
            FinishTurnTransition(t);
        }
        else
        {
            // Auto-play par l'IA en niveau Moyen avec personnalité Standard
            Announce(Loc.Get("timer.expired_autoplay_announce"));
            Log(Loc.Get("timer.expired_autoplay_log"));
            // Sauve la personnalité originale du joueur humain courant, met Standard temporairement,
            // joue, puis restaure. On capture la *référence* du joueur (pas _game.Current) car
            // l'auto-play peut faire avancer le tour, auquel cas _game.Current pointerait sur
            // un autre joueur (typiquement l'IA suivante) et on écraserait sa personnalité.
            var humanPlayer = _game.Current;
            var savedPersonality = humanPlayer.Personality;
            humanPlayer.Personality = AIPersonality.Standard;
            try
            {
                if (_game.AwaitingRoll) RollDice();
                if (_game == null || _game.Finished) { return; }
                // Joue tous les coups possibles automatiquement
                while (!_game.Finished && _game.Current == humanPlayer && !_game.AwaitingRoll)
                {
                    var action = ComputerPlayer.DecideNextAction(_game, AIDifficulty.Moyen);
                    if (action == null) { EndTurnManually(); break; }
                    SelectedPieceId = action.Piece.Id;
                    ApplyMove(action.Usage);
                }
            }
            finally
            {
                humanPlayer.Personality = savedPersonality;
            }
        }
    }

    /// <summary>Annonce le temps restant pour le tour. Touche F3.</summary>
    public void QueryTimeRemaining()
    {
        if (Settings.TurnTimerSeconds <= 0)
        {
            Announce(Loc.Get("timer.disabled"));
            return;
        }
        if (_game == null || _game.Current.IsComputer)
        {
            Announce(Loc.Get("timer.human_only"));
            return;
        }
        if (_turnTimer == null)
        {
            Announce(Loc.Get("timer.not_running"));
            return;
        }
        var elapsed = (DateTime.Now - _turnStartTime).TotalSeconds;
        var remaining = Math.Max(0, Settings.TurnTimerSeconds - elapsed);
        Announce(Loc.Format("timer.remaining", (int)remaining));
    }

    private string?[]? _lastCustomNames;
    private AIPersonality?[]? _lastPersonalities;

    public void StartGame(bool[] isComputerPerSlot, AIDifficulty difficulty = AIDifficulty.Moyen,
                          string?[]? customNames = null, AIPersonality?[]? personalities = null)
    {
        _lastAiPerSlot = (bool[])isComputerPerSlot.Clone();
        _lastDifficulty = difficulty;
        _lastCustomNames = customNames?.ToArray();
        _lastPersonalities = personalities?.ToArray();
        AIDifficulty = difficulty;
        _game = new CoreGame(isComputerPerSlot);

        // Assigne personnalités et noms aux joueurs
        var rng = new Random();
        for (int i = 0; i < _game.Players.Count; i++)
        {
            var player = _game.Players[i];
            if (player.IsComputer)
            {
                // null = aléatoire, sinon valeur explicitement choisie par l'utilisateur
                if (personalities != null && i < personalities.Length && personalities[i].HasValue)
                    player.Personality = personalities[i]!.Value;
                else
                    player.Personality = (AIPersonality)(1 + rng.Next(3));
            }
            if (customNames != null && i < customNames.Length)
                player.CustomName = customNames[i];
        }
        Board = new BoardModel(isComputerPerSlot.Length);
        Board.RefreshOccupancy(_game);
        IsInEndScreen = false;
        IsInGame = true;
        SelectedPieceId = 0;
        LogEntries.Clear();
        OnPropertyChanged(nameof(Board));

        // Réinitialise les compteurs de la partie en cours.
        _turnsThisGame = 0;
        _capturesMadeThisGame = 0;
        _capturesReceivedThisGame = 0;
        _piecesHomeThisGame = 0;
        _statsRecordedForCurrentGame = false;
        _opponentHomeCounts.Clear();
        foreach (var player in _game.Players) _opponentHomeCounts[player.Color] = 0;

        // Réinitialise le fichier de log pour la nouvelle partie.
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "last_game.log");
            File.WriteAllText(path, Loc.Format("log.new_game_file_header", DateTime.Now) + "\n");
        }
        catch { }

        // Une nouvelle partie écrase la sauvegarde précédente.
        DeleteSavedGame();

        var aiCount = isComputerPerSlot.Count(x => x);
        var humanCount = isComputerPerSlot.Length - aiCount;
        Log(Loc.Format("log.new_game", humanCount, aiCount));
        UpdateTurnInfo();

        // Annonce du début de partie. En mode verbeux : explication étoffée.
        var aiOpponents = _game.Players.Where(p => p.IsComputer).ToList();
        string greeting;
        if (Settings.VerboseAnnouncements)
        {
            if (aiOpponents.Count == 0)
                greeting = Loc.Format("announce.greeting_verbose_hotseat", isComputerPerSlot.Length, _game.Current.Label);
            else
            {
                var oppList = string.Join(", ", aiOpponents.Select(p => p.Label));
                var difficultyLabel = Loc.Get(difficulty switch
                {
                    AIDifficulty.Facile => "difficulty.facile",
                    AIDifficulty.Difficile => "difficulty.difficile",
                    _ => "difficulty.moyen",
                });
                greeting = Loc.Format("announce.greeting_verbose_vs_ai", oppList, difficultyLabel, _game.Current.Label);
            }
        }
        else if (aiOpponents.Count == 0)
        {
            greeting = Loc.Format("announce.greeting_concise_hotseat", _game.Current.Label);
        }
        else
        {
            var oppList = string.Join(", ", aiOpponents.Select(p => p.Label));
            greeting = Loc.Format("announce.greeting_concise_vs_ai", oppList, _game.Current.Label);
        }
        if (Settings.ImmersiveAmbience)
        {
            greeting = Loc.Get("announce.immersive_prefix") + greeting;
            Audio.StopMusic();
            // Ambiance continue pendant toute la partie, volumes calibrés pour rester en arrière-plan
            // sans masquer les indices audio du jeu.
            Audio.PlayAmbientLoop(SoundEffect.FireplaceLoop, 0.95f, -0.15f);
            _gameMusicQueue.Clear();
            PlayNextGameMusic();
        }
        Announce(greeting);
        Audio.Play(SoundEffect.TurnChange);

        if (_game.Current.IsComputer)
            ScheduleAITurn();
        else
            StartTurnTimer();
    }

    public void RollDice()
    {
        if (_game == null || !_game.AwaitingRoll)
        {
            Announce(Loc.Get("error.already_rolled"));
            Audio.Play(SoundEffect.Error);
            return;
        }

        Audio.Play(SoundEffect.DiceShake, 0f, Settings.DiceVolume);
        var (d1, d2, isDouble) = _game.RollDice();
        Audio.Play(SoundEffect.DiceThrow, 0f, Settings.DiceVolume);
        Log(Loc.Format("log.dice_roll", _game.Current.Label, d1, d2, isDouble ? Loc.Get("log.dice_roll_double_suffix") : ""));
        UpdateTurnInfo();

        // Petit son d'encouragement si un 5 est sorti et qu'au moins un pion est en base
        if ((d1 == 5 || d2 == 5 || d1 + d2 == 5) &&
            _game.Current.Pieces.Any(p => p.Status == PieceStatus.Base))
        {
            Audio.Play(SoundEffect.CanLeaveBase, 0f, 0.7f);
        }

        if (_game.DoubleStreak >= 3)
        {
            var t = _game.NextTurn();
            if (t.PenaltyMessage != null) Log(t.PenaltyMessage);
            FinishTurnTransition(t);
            return;
        }

        if (!_game.HasAnyLegalMove())
        {
            var p = _game.Current;
            var detail = ExplainNoLegalMove(p, d1, d2);
            Log(Loc.Format("log.no_move_possible_passed", d1, d2, detail, NextPlayerName()));
            Announce(Loc.Format("announce.no_move_possible", d1, d2, detail));
            var t = _game.NextTurn();
            FinishTurnTransition(t);
            return;
        }

        SelectedPieceId = 0;
        Board?.RefreshOccupancy(_game);
        var rolledBy = _game.Current.IsComputer ? $"{_game.Current.Label} : " : "";
        var opportunity = Settings.OpportunityHints ? DetectOpportunity() : null;
        var opportunitySuffix = opportunity != null ? " " + opportunity : "";

        if (Settings.VerboseAnnouncements && !_game.Current.IsComputer)
        {
            var hint = isDouble ? Loc.Get("announce.dice_double_hint") : "";
            Announce(Loc.Format("announce.dice_rolled_verbose", d1, d2, hint, opportunitySuffix));
        }
        else
        {
            Announce(Loc.Format("announce.dice_rolled_concise", rolledBy, d1, d2, isDouble ? Loc.Get("announce.dice_double_short") : "", opportunitySuffix));
        }
    }

    public void SelectPiece(int id)
    {
        if (_game == null || _game.AwaitingRoll)
        {
            Announce(Loc.Get("error.roll_dice_first_short"));
            Audio.Play(SoundEffect.Error);
            return;
        }
        var p = _game.Current;
        var piece = p.Pieces.FirstOrDefault(x => x.Id == id);
        if (piece == null) return;
        if (piece.Status == PieceStatus.Home)
        {
            Announce(Loc.Format("error.piece_already_home", id));
            Audio.Play(SoundEffect.Error);
            return;
        }
        SelectedPieceId = id;
        Audio.Play(SoundEffect.PieceSelect, 0f, Settings.NavigationVolume);
        if (Settings.LegalMovePreviewSounds)
            _ = Task.Run(async () => await PlayLegalMovePreview(piece));

        if (Settings.VerboseAnnouncements)
        {
            // En mode verbeux, on inclut directement les destinations possibles à la sélection
            Announce(Loc.Format("announce.piece_selected_verbose", id, PieceShortPosition(piece), DescribeAvailableMoves(piece)));
        }
        else
        {
            // En mode concis : juste la position. L'utilisateur appuie sur I ou Maj+touche pour les détails.
            Announce(Loc.Format("announce.piece_selected_concise", id, PieceShortPosition(piece)));
        }
    }

    /// <summary>Description courte de la position d'un pion, sans préfixe "pion N".</summary>
    private static string PieceShortPosition(Piece piece)
    {
        return piece.Status switch
        {
            PieceStatus.Base => Loc.Get("position.base"),
            PieceStatus.Home => Loc.Get("position.home"),
            PieceStatus.Lane => Loc.Format("position.lane", piece.Position + 1, Parcheesi.Core.BoardLayout.HomeLaneSize - 1 - piece.Position),
            PieceStatus.Ring => Parcheesi.Core.BoardLayout.IsSafe(piece.Position!.Value)
                ? Loc.Format("position.ring_safe", piece.Position, Parcheesi.Core.BoardLayout.RingProgress(piece.Color, piece.Position!.Value))
                : Loc.Format("position.ring_unsafe", piece.Position, Parcheesi.Core.BoardLayout.RingProgress(piece.Color, piece.Position!.Value)),
            _ => "",
        };
    }

    /// <summary>Aperçu d'un seul dé/bonus appliqué au pion sélectionné, sans rien appliquer.</summary>
    public void PreviewSingleDie(DiceUsage usage)
    {
        if (_game == null) { Announce(Loc.Get("error.no_game")); return; }
        if (_selectedPieceId == 0)
        {
            Announce(Loc.Get("error.no_piece_selected"));
            Audio.Play(SoundEffect.Error);
            return;
        }
        var piece = _game.Current.Pieces.First(p => p.Id == _selectedPieceId);

        int steps; string label;
        switch (usage)
        {
            case DiceUsage.Die1:
                if (!_game.Die1.HasValue) { Announce(Loc.Get("error.roll_dice_first")); return; }
                if (_game.Die1Used) { Announce(Loc.Format("error.die1_already_used", _game.Die1)); return; }
                steps = _game.Die1.Value;
                label = Loc.Format("advice.die1", steps);
                break;
            case DiceUsage.Die2:
                if (!_game.Die2.HasValue) { Announce(Loc.Get("error.roll_dice_first")); return; }
                if (_game.Die2Used) { Announce(Loc.Format("error.die2_already_used", _game.Die2)); return; }
                steps = _game.Die2.Value;
                label = Loc.Format("advice.die2", steps);
                break;
            case DiceUsage.Sum:
                if (!_game.Die1.HasValue || !_game.Die2.HasValue) { Announce(Loc.Get("error.roll_dice_first")); return; }
                if (_game.Die1Used || _game.Die2Used) { Announce(Loc.Get("error.sum_unavailable")); return; }
                steps = _game.Die1.Value + _game.Die2.Value;
                label = Loc.Format("advice.sum", steps);
                break;
            case DiceUsage.Bonus:
                if (_game.Bonus <= 0) { Announce(Loc.Get("error.no_bonus")); return; }
                steps = _game.Bonus;
                label = Loc.Format("advice.bonus", steps);
                break;
            default: return;
        }

        var preview = _game.Preview(piece, steps);
        Announce(Loc.Format("announce.preview_outcome", label, DescribePreviewOutcome(preview)));
    }

    /// <summary>Annonce détaillée du pion sélectionné + tous les coups possibles. À la demande (touche I).</summary>
    public void ReadSelectedPieceDetails()
    {
        if (_game == null) { Announce(Loc.Get("error.no_game")); return; }
        if (_selectedPieceId == 0) { Announce(Loc.Get("error.no_piece_selected")); return; }
        var p = _game.Current;
        var piece = p.Pieces.FirstOrDefault(x => x.Id == _selectedPieceId);
        if (piece == null) return;
        Announce(Loc.Format("announce.piece_details", piece.Id, piece.Describe(), DescribeAvailableMoves(piece)));
    }

    private async Task PlayLegalMovePreview(Piece piece)
    {
        if (_game == null) return;
        await Task.Delay(180); // laisse le clic de sélection finir
        var checks = new List<(string label, bool ok)>();
        if (_game.Bonus > 0)
        {
            checks.Add(("B", _game.Preview(piece, _game.Bonus).Ok));
        }
        else
        {
            if (!_game.Die1Used && _game.Die1.HasValue)
                checks.Add(("A", _game.Preview(piece, _game.Die1.Value).Ok));
            if (!_game.Die2Used && _game.Die2.HasValue)
                checks.Add(("Z", _game.Preview(piece, _game.Die2.Value).Ok));
            if (!_game.Die1Used && !_game.Die2Used && _game.Die1.HasValue && _game.Die2.HasValue)
                checks.Add(("S", _game.Preview(piece, _game.Die1.Value + _game.Die2.Value).Ok));
        }

        // Pan progressif gauche → droite pour distinguer les 3 tons.
        for (int i = 0; i < checks.Count; i++)
        {
            var pan = checks.Count == 1 ? 0f : -0.7f + (1.4f * i / (checks.Count - 1));
            var (_, ok) = checks[i];
            Audio.Play(ok ? SoundEffect.SafeEntry : SoundEffect.AIThinking, pan, ok ? 0.55f : 0.4f);
            await Task.Delay(180);
        }
    }

    private string DescribeAvailableMoves(Piece piece)
    {
        if (_game == null) return "";
        var opts = new List<string>();
        if (_game.Bonus > 0)
        {
            var r = _game.Preview(piece, _game.Bonus);
            opts.Add(Loc.Format("moves.b_bonus", _game.Bonus, DescribePreviewOutcome(r)));
        }
        else
        {
            if (!_game.Die1Used && _game.Die1.HasValue)
            {
                var r = _game.Preview(piece, _game.Die1.Value);
                opts.Add(Loc.Format("moves.a_die1", _game.Die1, DescribePreviewOutcome(r)));
            }
            if (!_game.Die2Used && _game.Die2.HasValue)
            {
                var r = _game.Preview(piece, _game.Die2.Value);
                opts.Add(Loc.Format("moves.z_die2", _game.Die2, DescribePreviewOutcome(r)));
            }
            if (!_game.Die1Used && !_game.Die2Used && _game.Die1.HasValue && _game.Die2.HasValue)
            {
                var sum = _game.Die1.Value + _game.Die2.Value;
                var r = _game.Preview(piece, sum);
                opts.Add(Loc.Format("moves.s_sum", sum, DescribePreviewOutcome(r)));
            }
        }
        return string.Join(". ", opts);
    }

    /// <summary>
    /// Décrit en mots ce qui se passerait si on appliquait ce coup : case d'arrivée,
    /// si elle est sûre, si on capture, si on entre dans le couloir, si on rentre à la maison.
    /// </summary>
    private string DescribePreviewOutcome(MovePreview preview)
    {
        if (!preview.Ok) return Loc.Format("preview.impossible", preview.Reason);

        // Cas spécial : sortie de la base (depuis Base vers la case de départ sur l'anneau)
        if (preview.NewStatus == PieceStatus.Ring && preview.NewPosition.HasValue)
        {
            var pos = preview.NewPosition.Value;
            var captureMention = preview.Captures != null
                ? Loc.Format("preview.capture_suffix", preview.Captures.Player.Label, preview.Captures.Piece.Id)
                : "";
            var landKey = Parcheesi.Core.BoardLayout.IsSafe(pos) ? "preview.land_safe" : "preview.land_unsafe";
            return Loc.Format(landKey, pos) + captureMention;
        }

        if (preview.NewStatus == PieceStatus.Lane && preview.NewPosition.HasValue)
        {
            var laneCell = preview.NewPosition.Value + 1; // 1-indexé pour l'utilisateur
            var remaining = Parcheesi.Core.BoardLayout.HomeLaneSize - 1 - preview.NewPosition.Value;
            return Loc.Format("preview.enter_lane", laneCell, remaining);
        }

        if (preview.ReachedHome)
        {
            return Loc.Get("preview.reach_home");
        }

        return Loc.Get("preview.unknown");
    }

    public void ApplyMove(DiceUsage usage)
    {
        if (_game == null || _game.AwaitingRoll)
        {
            Announce(Loc.Get("error.roll_dice_first_short"));
            Audio.Play(SoundEffect.Error);
            return;
        }
        if (_selectedPieceId == 0)
        {
            Announce(Loc.Get("error.no_piece_selected"));
            Audio.Play(SoundEffect.Error);
            return;
        }
        var p = _game.Current;
        var piece = p.Pieces.First(x => x.Id == _selectedPieceId);

        int steps;
        switch (usage)
        {
            case DiceUsage.Die1:
                if (!_game.Die1.HasValue) { Announce(Loc.Get("error.roll_dice_first")); Audio.Play(SoundEffect.Error); return; }
                if (_game.Die1Used) { Announce(Loc.Format("error.die1_already_used", _game.Die1)); Audio.Play(SoundEffect.Error); return; }
                steps = _game.Die1.Value; break;
            case DiceUsage.Die2:
                if (!_game.Die2.HasValue) { Announce(Loc.Get("error.roll_dice_first")); Audio.Play(SoundEffect.Error); return; }
                if (_game.Die2Used) { Announce(Loc.Format("error.die2_already_used", _game.Die2)); Audio.Play(SoundEffect.Error); return; }
                steps = _game.Die2.Value; break;
            case DiceUsage.Sum:
                if (_game.Die1Used || _game.Die2Used) { Announce(Loc.Get("error.sum_unavailable")); Audio.Play(SoundEffect.Error); return; }
                steps = _game.Die1!.Value + _game.Die2!.Value; break;
            case DiceUsage.Bonus:
                if (_game.Bonus <= 0) { Announce(Loc.Get("error.no_bonus")); Audio.Play(SoundEffect.Error); return; }
                steps = _game.Bonus; break;
            default: return;
        }

        // Capture la position d'avant pour pouvoir jouer le "tap par case" ensuite.
        var startPan = ComputePanForPiece(piece);
        var startCellExists = piece.Status != PieceStatus.Base && piece.Status != PieceStatus.Home;
        var wasOnSafeBefore = piece.Status == PieceStatus.Ring && piece.Position.HasValue
                               && Parcheesi.Core.BoardLayout.IsSafe(piece.Position.Value);
        var wasInBase = piece.Status == PieceStatus.Base;
        var wasOnRing = piece.Status == PieceStatus.Ring;

        var result = _game.ApplyMove(piece, steps);
        if (!result.Ok)
        {
            Announce(Loc.Format("error.move_impossible", result.Reason));
            Audio.Play(SoundEffect.Blocked);
            return;
        }

        _game.ConsumeDie(usage);

        // Met à jour les compteurs de stats de la partie en cours.
        if (result.CaptureMessage != null)
        {
            if (p.IsComputer) _capturesReceivedThisGame++;
            else _capturesMadeThisGame++;
        }
        if (result.ReachedHome && !p.IsComputer) _piecesHomeThisGame++;

        // Calcul de la position après pour le walk audio.
        var endPan = ComputePanForPiece(piece);
        var moveSound = ColorToMoveSound(piece.Color);

        // Joue le walk-along de N pas en arrière-plan, à la vitesse + volume des réglages.
        var settingsRef = Settings;
        _ = Task.Run(async () =>
        {
            if (wasInBase)
            {
                Audio.Play(SoundEffect.BaseExit, endPan, settingsRef.EventVolume);
                await Task.Delay(200);
                Audio.Play(moveSound, endPan, settingsRef.MoveVolume);
            }
            else
            {
                await Audio.PlayWalkAsync(moveSound, startPan, endPan, steps,
                    delayMs: settingsRef.WalkStepDelayMs, volume: settingsRef.MoveVolume);
            }

            // Sons d'événement post-walk
            if (result.CaptureMessage != null)
                Audio.Play(SoundEffect.Capture, endPan, settingsRef.EventVolume);
            else if (result.ReachedHome)
                Audio.Play(SoundEffect.HomeArrive, endPan, settingsRef.EventVolume);
            else if (piece.Status == PieceStatus.Lane && wasOnRing)
                Audio.Play(SoundEffect.LaneEntry, endPan, settingsRef.EventVolume * 0.85f);
            else if (piece.Status == PieceStatus.Ring && piece.Position.HasValue
                     && Parcheesi.Core.BoardLayout.IsSafe(piece.Position.Value)
                     && !wasOnSafeBefore)
                Audio.Play(SoundEffect.SafeEntry, endPan, settingsRef.EventVolume * 0.75f);
        });

        // Journal complet (gardé verbeux pour P et fichier log)
        var fullMsg = Loc.Format("log.move_full", p.Label, piece.Id, steps, piece.Describe());
        if (result.CaptureMessage != null) fullMsg += " " + result.CaptureMessage + Loc.Get("log.bonus_20_suffix");
        if (result.BonusMessage != null) fullMsg += " " + result.BonusMessage + Loc.Get("log.bonus_10_suffix");
        Log(fullMsg);

        // Détection d'urgence : un adversaire vient-il de passer un palier de pions à la maison ?
        CheckUrgencyTransitions();

        // Mode verbeux = la version longue du journal. Mode concis = brief.
        var announceMsg = Settings.VerboseAnnouncements ? fullMsg : BuildBriefMoveMessage(p, piece, steps, result);
        if (Settings.VerboseAnnouncements)
        {
            if (result.CaptureMessage != null) announceMsg += Loc.Get("announce.bonus_apply_hint");
            if (result.BonusMessage != null) announceMsg += Loc.Get("announce.bonus_apply_hint");
        }
        Announce(announceMsg);

        Board?.RefreshOccupancy(_game);
        UpdateTurnInfo();
        SaveCurrentGame();

        if (_game.Finished)
        {
            EndGame();
            DeleteSavedGame();
            return;
        }

        if (_game.TurnIsOver())
        {
            var t = _game.NextTurn();
            FinishTurnTransition(t);
        }
        else if (!_game.HasAnyLegalMove())
        {
            Log(Loc.Get("log.no_more_moves"));
            Announce(Loc.Get("announce.no_more_moves"));
            var t = _game.NextTurn();
            FinishTurnTransition(t);
        }
        else
        {
            SelectedPieceId = 0;
        }
    }

    /// <summary>Construit une annonce concise de mouvement : juste position + événements importants.</summary>
    private string BuildBriefMoveMessage(Player p, Piece piece, int steps, MoveResult result)
    {
        // Préfixe différent selon humain/IA pour clarté
        var prefix = p.IsComputer
            ? Loc.Format("move.brief_ai_prefix", p.Label, piece.Id)
            : Loc.Format("move.brief_human_prefix", piece.Id);
        var pos = piece.Status switch
        {
            PieceStatus.Ring => Parcheesi.Core.BoardLayout.IsSafe(piece.Position!.Value)
                ? Loc.Format("move.brief_pos_ring_safe", piece.Position)
                : Loc.Format("move.brief_pos_ring", piece.Position),
            PieceStatus.Lane => Loc.Format("move.brief_pos_lane", piece.Position! + 1),
            PieceStatus.Home => Loc.Get("move.brief_pos_home"),
            _ => "",
        };
        var msg = Loc.Format("move.brief_msg", prefix, pos);
        if (result.CaptureMessage != null) msg += Loc.Format("move.brief_capture_suffix", result.CaptureMessage);
        if (result.ReachedHome) msg += Loc.Get("move.brief_home_suffix");
        return msg;
    }

    private float ComputePanForPiece(Piece piece)
    {
        if (Board == null) return 0;
        var cell = Board.FindCellForPiece(piece);
        if (cell == null) return 0;
        return BoardLayoutData.StereoPan(cell.Cell.GridCol);
    }

    private static SoundEffect ColorToMoveSound(PlayerColor c) => c switch
    {
        PlayerColor.Rouge => SoundEffect.PieceMoveRouge,
        PlayerColor.Jaune => SoundEffect.PieceMoveJaune,
        PlayerColor.Bleu  => SoundEffect.PieceMoveBleu,
        PlayerColor.Vert  => SoundEffect.PieceMoveVert,
        _ => SoundEffect.PieceMoveRouge,
    };

    public void EndTurnManually()
    {
        if (_game == null) return;
        Log(Loc.Format("log.passed_turn", _game.Current.Label));
        Announce(Loc.Get("announce.passed_turn"));
        var t = _game.NextTurn();
        FinishTurnTransition(t);
    }

    private void FinishTurnTransition(CoreGame.TurnTransition t)
    {
        _turnsThisGame++;
        if (t.PenaltyMessage != null)
        {
            Announce(t.PenaltyMessage);
            Audio.Play(SoundEffect.Error);
        }
        SelectedPieceId = 0;
        Board?.RefreshOccupancy(_game!);
        UpdateTurnInfo();
        if (t.Rerolled)
        {
            var who = _game!.Current.IsComputer
                ? _game.Current.Label + Loc.Get("announce.double_rerolled_ai_suffix")
                : _game.Current.Label;
            Announce(Loc.Format("announce.double_rerolled", who));
        }
        else
        {
            Audio.Play(SoundEffect.TurnChange);
            if (_game!.Current.IsComputer)
                Announce(Loc.Format("announce.next_turn_ai", _game.Current.Label));
            else
                Announce(Loc.Format("announce.next_turn_human", _game.Current.Label));
        }

        if (_game!.Current.IsComputer)
        {
            StopTurnTimer();
            ScheduleAITurn();
        }
        else
        {
            StartTurnTimer();
        }
    }

    private void EndGame()
    {
        StopTurnTimer();
        Audio.Play(SoundEffect.Victory);
        var winner = _game!.Winner!.Value.Label();
        var ranks = string.Join(", ", _game.Rankings.Select((c, i) => Loc.Format("endgame.ranking_entry", i + 1, c.Label())));
        var humanWon = !_game.Players.First(p => p.Color == _game.Winner!.Value).IsComputer;

        var summary = new System.Text.StringBuilder();
        summary.AppendLine(humanWon
            ? Loc.Format("endgame.victory_log", winner)
            : Loc.Format("endgame.defeat_log", winner));
        summary.AppendLine();
        summary.AppendLine(Loc.Format("endgame.summary_ranking", ranks));
        summary.AppendLine();
        summary.AppendLine(Loc.Get("endgame.summary_this_game"));
        summary.AppendLine(Loc.Format("endgame.summary_turns", _turnsThisGame));
        summary.AppendLine(Loc.Format("endgame.summary_captures_made", _capturesMadeThisGame));
        summary.AppendLine(Loc.Format("endgame.summary_captures_received", _capturesReceivedThisGame));
        summary.AppendLine(Loc.Format("endgame.summary_pieces_home", _piecesHomeThisGame));
        EndGameSummary = summary.ToString();

        var msg = humanWon
            ? Loc.Format("endgame.victory_announce", winner, _turnsThisGame, _capturesMadeThisGame)
            : Loc.Format("endgame.defeat_announce", winner, _turnsThisGame);

        if (Settings.ImmersiveAmbience)
        {
            Audio.StopAllAmbientLoops();
            var (ambiencePhrase, applause) = ChooseEndGameAmbience(humanWon);
            if (!string.IsNullOrEmpty(ambiencePhrase)) msg += " " + ambiencePhrase;
            var contextual = ChooseContextualPhrase(humanWon);
            if (!string.IsNullOrEmpty(contextual)) msg += " " + contextual;
            if (applause.HasValue)
                Audio.Play(applause.Value.effect, 0f, applause.Value.volume);
        }

        // Calcule les succès débloqués pour pouvoir les concaténer à l'annonce principale,
        // au lieu de faire une seconde annonce 2.5 s plus tard qui chevauche la première.
        var achievementSuffix = RecordGameStats();
        if (!string.IsNullOrEmpty(achievementSuffix)) msg += " " + achievementSuffix;

        Log(msg);
        Announce(msg);
        TurnInfo = msg;

        // Bascule sur l'écran de fin (le menu principal n'est pas encore réaffiché).
        IsInGame = false;
        IsInEndScreen = true;
        OnPropertyChanged(nameof(MenuStatusLine));
    }

    // ---------- Fin de partie : narration et applaudissements adaptés ----------

    // Les phrases d'ambiance de fin de partie sont stockées comme préfixes de clés
    // dans fr.json (par ex. "victory.triumph.1" .. "victory.triumph.4"). Pick résout
    // la traduction au moment où on la sélectionne, ce qui suit la langue active.
    private static readonly string[] TriumphPhraseKeys = { "victory.triumph.1", "victory.triumph.2", "victory.triumph.3", "victory.triumph.4" };
    private static readonly string[] StandardWinPhraseKeys = { "victory.standard.1", "victory.standard.2", "victory.standard.3", "victory.standard.4" };
    private static readonly string[] CloseCallPhraseKeys = { "victory.close.1", "victory.close.2", "victory.close.3", "victory.close.4" };
    private static readonly string[] HonorableDefeatPhraseKeys = { "defeat.honorable.1", "defeat.honorable.2", "defeat.honorable.3", "defeat.honorable.4" };
    private static readonly string[] StandardDefeatPhraseKeys = { "defeat.standard.1", "defeat.standard.2", "defeat.standard.3", "defeat.standard.4" };
    private static readonly string[] CrushingDefeatPhraseKeys = { "defeat.crushing.1", "defeat.crushing.2", "defeat.crushing.3", "defeat.crushing.4" };

    private string Pick(string[] phraseKeys) => Loc.Get(phraseKeys[_musicRng.Next(phraseKeys.Length)]);

    /// <summary>
    /// Détermine la phrase d'ambiance et l'applaudissement à jouer selon la performance
    /// du joueur humain. Renvoie (phrase, sound) — sound est null pour les défaites.
    /// </summary>
    private (string phrase, (SoundEffect effect, float volume)? applause) ChooseEndGameAmbience(bool humanWon)
    {
        if (humanWon)
        {
            // Triomphe éclatant : il faut combiner au moins 2 critères sur 3 (speed run,
            // adversaire Difficile, défense parfaite). Auparavant un seul critère suffisait,
            // ce qui rendait le triomphe banal — par exemple ne pas être capturé en 1v1
            // contre Facile suffisait, alors que c'est un scénario fréquent.
            int triumphPoints = 0;
            if (_turnsThisGame < SpeedRunThreshold) triumphPoints++;
            if (AIDifficulty == AIDifficulty.Difficile) triumphPoints++;
            if (_capturesReceivedThisGame == 0) triumphPoints++;
            bool triumph = triumphPoints >= 2;
            // Coude-à-coude : adversaire à 3+ pions à la maison, ou ≥4 captures subies
            bool closeCall = !triumph
                          && (_opponentHomeCounts.Values.Any(c => c >= 3) || _capturesReceivedThisGame >= 4);

            if (triumph)
                return (Pick(TriumphPhraseKeys), (SoundEffect.StandingApplause, 0.65f));
            if (closeCall)
                return (Pick(CloseCallPhraseKeys), (SoundEffect.ScatteredApplause, 0.45f));
            return (Pick(StandardWinPhraseKeys), (SoundEffect.PoliteApplause, 0.55f));
        }

        // Défaite : palier selon le rang final
        var humanColor = _game!.Players.First(p => !p.IsComputer).Color;
        int rank = _game.Rankings.IndexOf(humanColor);
        int total = _game.Players.Count;

        if (rank == 1)
            return (Pick(HonorableDefeatPhraseKeys), null);
        if (total >= 4 && rank == total - 1 && _piecesHomeThisGame <= 1)
            return (Pick(CrushingDefeatPhraseKeys), null);
        return (Pick(StandardDefeatPhraseKeys), null);
    }

    /// <summary>
    /// Phrase contextuelle bonus : un détail saillant de la partie (capture offensive,
    /// défense parfaite, course rapide, etc.). Peut être vide.
    /// </summary>
    private string ChooseContextualPhrase(bool humanWon)
    {
        if (humanWon)
        {
            if (_capturesMadeThisGame >= 5)
                return Loc.Get("context.win_offensive");
            if (_capturesReceivedThisGame == 0 && _piecesHomeThisGame == 4)
                return Loc.Get("context.win_perfect_defense");
            if (_piecesHomeThisGame == 4 && _turnsThisGame < FastWinThreshold)
                return Loc.Get("context.win_fast_home");
        }
        else
        {
            if (_capturesReceivedThisGame >= 5)
                return Loc.Get("context.lose_many_captures");
            if (_piecesHomeThisGame == 0)
                return Loc.Get("context.lose_no_home");
        }
        return "";
    }

    /// <summary>Relance une partie avec exactement la même configuration que la précédente.</summary>
    public void ReplaySameSettings()
    {
        if (_lastAiPerSlot == null) { ReturnToMenu(); return; }
        StartGame(_lastAiPerSlot, _lastDifficulty, _lastCustomNames, _lastPersonalities);
    }

    /// <summary>Quitte la partie ou l'écran de fin pour revenir au menu principal.</summary>
    public void ReturnToMenu()
    {
        StopTurnTimer();
        StopAllAITimers();
        Audio.StopAllAmbientLoops();
        IsPaused = false;
        IsInGame = false;
        IsInEndScreen = false;
        OnPropertyChanged(nameof(MenuStatusLine));
        StartMenuAmbience();
    }

    /// <summary>Abandonne la partie en cours en la sauvegardant pour reprise ultérieure, retour au menu.</summary>
    public void AbandonAndReturnToMenu()
    {
        if (_game != null && !_game.Finished)
        {
            SaveCurrentGame();
            Log(Loc.Get("log.abandoned_saved"));
        }
        ReturnToMenu();
    }

    /// <summary>
    /// Enregistre les statistiques de la partie et calcule les succès nouvellement débloqués.
    /// Retourne le texte d'annonce pour les succès (pour concaténation à l'annonce principale),
    /// ou null s'il n'y en a pas.
    /// </summary>
    private string? RecordGameStats()
    {
        if (_game == null || _statsRecordedForCurrentGame) return null;
        _statsRecordedForCurrentGame = true;

        Stats.GamesPlayed++;
        var humanWon = _game.Winner.HasValue && !_game.Players.First(p => p.Color == _game.Winner.Value).IsComputer;
        if (humanWon) Stats.GamesWon++;
        else Stats.GamesLost++;
        Stats.CapturesMade += _capturesMadeThisGame;
        Stats.CapturesReceived += _capturesReceivedThisGame;
        Stats.PiecesBroughtHome += _piecesHomeThisGame;
        Stats.TotalTurnsPlayed += _turnsThisGame;
        if (humanWon && (Stats.FewestTurnsToWin == null || _turnsThisGame < Stats.FewestTurnsToWin))
            Stats.FewestTurnsToWin = _turnsThisGame;
        Stats.LastPlayed = DateTime.Now;

        // Stats par niveau de difficulté (uniquement quand il y a au moins un IA)
        if (_game.Players.Any(p => p.IsComputer))
        {
            switch (AIDifficulty)
            {
                case AIDifficulty.Facile:    Stats.GamesEasy++; if (humanWon) Stats.WinsEasy++; break;
                case AIDifficulty.Moyen:     Stats.GamesMedium++; if (humanWon) Stats.WinsMedium++; break;
                case AIDifficulty.Difficile: Stats.GamesHard++; if (humanWon) Stats.WinsHard++; break;
            }
        }

        // Stats par personnalité affrontée (chaque IA présente compte)
        var personalitiesFaced = _game.Players
            .Where(p => p.IsComputer)
            .Select(p => p.Personality)
            .Distinct()
            .ToList();
        foreach (var pers in personalitiesFaced)
        {
            switch (pers)
            {
                case AIPersonality.Aggressive: Stats.GamesVsAggressive++; if (humanWon) Stats.WinsVsAggressive++; break;
                case AIPersonality.Prudent:    Stats.GamesVsPrudent++;    if (humanWon) Stats.WinsVsPrudent++; break;
                case AIPersonality.Coureur:    Stats.GamesVsCoureur++;    if (humanWon) Stats.WinsVsCoureur++; break;
            }
        }

        Stats.Save();
        return ComputeAchievementAnnouncement(humanWon);
    }

    /// <summary>
    /// Vérifie et débloque les succès basés sur la partie courante. Retourne le texte
    /// d'annonce à concaténer à l'annonce de fin de partie, ou null si rien de nouveau.
    /// </summary>
    private string? ComputeAchievementAnnouncement(bool humanWon)
    {
        var newlyUnlocked = new List<Achievement>();

        // Cumulés (basés sur stats globales)
        if (humanWon && Stats.GamesWon == 1) Try("first_win");
        if (Stats.GamesPlayed >= 10) Try("marathon");
        if (Stats.GamesPlayed >= 50) Try("dedicated");
        if (Stats.CapturesMade >= 25) Try("hunter_master");
        if (Stats.CapturesMade >= 100) Try("centurion");

        // Per-game (basés sur cette partie)
        if (humanWon && _turnsThisGame < SpeedRunThreshold) Try("speed_run");
        if (humanWon && _capturesReceivedThisGame == 0) Try("untouchable");
        if (_capturesMadeThisGame >= 3) Try("hunter");
        if (humanWon && AIDifficulty == AIDifficulty.Difficile) Try("hard_winner");
        if (humanWon && _piecesHomeThisGame == 4 && _capturesReceivedThisGame == 0) Try("perfect_home");

        // Cumulés sur les nouvelles stats détaillées
        if (Stats.WinsHard >= 3) Try("triple_hard");
        if (Stats.WinsVsAggressive >= 1 && Stats.WinsVsPrudent >= 1 && Stats.WinsVsCoureur >= 1)
            Try("all_personalities");
        if (Stats.WinsVsAggressive >= 3 && Stats.WinsVsPrudent >= 3 && Stats.WinsVsCoureur >= 3)
            Try("personality_master");

        if (newlyUnlocked.Count > 0)
        {
            Audio.Play(SoundEffect.Victory, 0f, 0.7f);
            var entries = string.Join(", ", newlyUnlocked.Select(a => Loc.Format("achievement.entry_format", a.Name, a.Description)));
            var msg = Loc.Format("achievement.unlocked_announce", entries);
            Log(msg);
            return msg;
        }
        return null;

        void Try(string id)
        {
            var ach = Achievements.Unlock(id);
            if (ach != null) newlyUnlocked.Add(ach);
        }
    }

    public void ReadBoard()
    {
        if (_game == null) return;
        var lines = _game.Players.Select(p =>
            $"{p.Label} : " + string.Join(", ", p.Pieces.Select(piece => piece.Describe())) + ".");
        var msg = string.Join(" ", lines);
        Announce(msg);
        Log("— " + msg);
    }

    public void ReadOpponents()
    {
        if (_game == null) return;
        var me = _game.Current;
        var lines = _game.Players.Where(p => p != me).Select(p =>
            $"{p.Label} : " + string.Join(", ", p.Pieces.Select(piece => piece.Describe())) + ".");
        Announce(string.Join(" ", lines));
    }

    public void ReadHelp()
    {
        Announce(Loc.Get("help.full"));
    }

    public void ReadRecentLog()
    {
        if (LogEntries.Count == 0) { Announce(Loc.Get("log.empty")); return; }
        var last5 = LogEntries.Skip(Math.Max(0, LogEntries.Count - 5));
        Announce(Loc.Format("log.last_five", string.Join(" — ", last5)));
    }

    /// <summary>Donne une raison lisible pour expliquer pourquoi aucun coup n'est possible.</summary>
    private string ExplainNoLegalMove(Player p, int d1, int d2)
    {
        var inBase = p.Pieces.Count(x => x.Status == PieceStatus.Base);
        var onRing = p.Pieces.Count(x => x.Status == PieceStatus.Ring);
        var inLane = p.Pieces.Count(x => x.Status == PieceStatus.Lane);
        var atHome = p.Pieces.Count(x => x.Status == PieceStatus.Home);
        var parts = new List<string>();
        if (inBase > 0) parts.Add(Loc.Format("no_legal.in_base", inBase));
        if (onRing > 0) parts.Add(Loc.Format("no_legal.on_ring", onRing));
        if (inLane > 0) parts.Add(Loc.Format("no_legal.in_lane", inLane));
        if (atHome > 0) parts.Add(Loc.Format("no_legal.at_home", atHome));
        return string.Join(", ", parts);
    }

    private string NextPlayerName()
    {
        if (_game == null) return Loc.Get("no_legal.next_player_default");
        var idx = _game.CurrentIndex;
        for (int i = 1; i <= _game.Players.Count; i++)
        {
            var nextIdx = (idx + i) % _game.Players.Count;
            if (!_game.Rankings.Contains(_game.Players[nextIdx].Color))
                return _game.Players[nextIdx].Label + (_game.Players[nextIdx].IsComputer ? Loc.Get("no_legal.next_player_ai_suffix") : "");
        }
        return Loc.Get("no_legal.next_player_default");
    }

    public void UpdateTurnInfo()
    {
        if (_game == null) return;
        var p = _game.Current;
        var msg = Loc.Format("turn_info.player_plays", p.Label);
        if (_game.Die1.HasValue && _game.Die2.HasValue)
        {
            var usedSuffix = Loc.Get("turn_info.dice_used_suffix");
            var diceParts = new List<string>
            {
                Loc.Format("turn_info.dice_d1", _game.Die1, _game.Die1Used ? usedSuffix : ""),
                Loc.Format("turn_info.dice_d2", _game.Die2, _game.Die2Used ? usedSuffix : ""),
            };
            if (_game.Die1 == _game.Die2) diceParts.Add(Loc.Get("turn_info.double_marker"));
            DiceInfo = string.Join(" — ", diceParts);
        }
        else
        {
            DiceInfo = Loc.Get("dice_info.none");
        }
        if (_game.Bonus > 0) msg += Loc.Format("turn_info.bonus_avail", _game.Bonus);
        TurnInfo = msg;
        OnPropertyChanged(nameof(MyPiecesDescription));
    }

    private void Announce(string text)
    {
        _lastAnnouncement = text;
        AnnounceRequested?.Invoke(text);
    }

    public void ReplayLastAnnouncement()
    {
        if (string.IsNullOrEmpty(_lastAnnouncement))
        {
            AnnounceRequested?.Invoke(Loc.Get("announce.last_replay_empty"));
            return;
        }
        AnnounceRequested?.Invoke(_lastAnnouncement);
    }

    private void Log(string entry)
    {
        LogEntries.Add(entry);
        while (LogEntries.Count > 200) LogEntries.RemoveAt(0);
        AppendLogFile(entry);
    }

    /// <summary>Ajoute aussi l'entrée à un fichier 'last_game.log' pour debug et partage.</summary>
    private void AppendLogFile(string entry)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "last_game.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {entry}\n");
        }
        catch { /* Ne pas planter le jeu si le log fichier échoue. */ }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
}
