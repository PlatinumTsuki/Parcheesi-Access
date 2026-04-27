using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Parcheesi.App;

/// <summary>
/// Préférences utilisateur persistantes (settings.json à côté du .exe).
/// Toutes les propriétés sont liables (INotifyPropertyChanged) pour que les changements
/// dans l'écran Réglages s'appliquent immédiatement au jeu en cours.
/// </summary>
public class Settings : INotifyPropertyChanged
{
    // --- Volumes (0.0 - 1.0) ---
    private float _masterVolume = 0.85f;
    public float MasterVolume { get => _masterVolume; set => Set(ref _masterVolume, Clamp(value)); }

    private float _diceVolume = 1.0f;
    public float DiceVolume { get => _diceVolume; set => Set(ref _diceVolume, Clamp(value)); }

    private float _moveVolume = 0.7f;
    public float MoveVolume { get => _moveVolume; set => Set(ref _moveVolume, Clamp(value)); }

    private float _eventVolume = 0.95f;
    public float EventVolume { get => _eventVolume; set => Set(ref _eventVolume, Clamp(value)); }

    private float _aiThinkVolume = 0.35f;
    public float AIThinkVolume { get => _aiThinkVolume; set => Set(ref _aiThinkVolume, Clamp(value)); }

    private float _navigationVolume = 0.5f;
    public float NavigationVolume { get => _navigationVolume; set => Set(ref _navigationVolume, Clamp(value)); }

    private float _ambienceMusicVolume = 0.20f;
    /// <summary>Volume des musiques d'ambiance (Chopin au menu, Satie en jeu). Multiplié par MasterVolume.</summary>
    public float AmbienceMusicVolume { get => _ambienceMusicVolume; set => Set(ref _ambienceMusicVolume, Clamp(value)); }

    // --- Vitesses (en millisecondes) ---
    private int _walkStepDelayMs = 110;
    /// <summary>Délai entre deux taps quand un pion bouge case par case (50-250).</summary>
    public int WalkStepDelayMs { get => _walkStepDelayMs; set => Set(ref _walkStepDelayMs, Math.Clamp(value, 50, 250)); }

    private int _aiThinkPulseMs = 500;
    /// <summary>Intervalle des pulsations "IA réfléchit" (200-1500). Plus court = IA plus rapide.</summary>
    public int AIThinkPulseMs { get => _aiThinkPulseMs; set => Set(ref _aiThinkPulseMs, Math.Clamp(value, 200, 1500)); }

    // --- Bascules de comportement ---
    private bool _legalMovePreviewSounds = true;
    /// <summary>Joue les 3 tons audio cloche/tic à la sélection d'un pion.</summary>
    public bool LegalMovePreviewSounds { get => _legalMovePreviewSounds; set => Set(ref _legalMovePreviewSounds, value); }

    private bool _opportunityHints = true;
    /// <summary>Annonce automatiquement "capture possible" ou "maison possible" après le lancer.</summary>
    public bool OpportunityHints { get => _opportunityHints; set => Set(ref _opportunityHints, value); }

    private bool _zoneTransitionSounds = true;
    /// <summary>Joue un son distinct quand on change de zone en navigant avec les flèches.</summary>
    public bool ZoneTransitionSounds { get => _zoneTransitionSounds; set => Set(ref _zoneTransitionSounds, value); }

    private bool _edgeBumpSound = true;
    /// <summary>Joue un son d'erreur quand on bute contre le bord du plateau avec les flèches.</summary>
    public bool EdgeBumpSound { get => _edgeBumpSound; set => Set(ref _edgeBumpSound, value); }

    private bool _verboseAnnouncements = false;
    /// <summary>Annonces longues style "première découverte" au lieu du mode concis.</summary>
    public bool VerboseAnnouncements { get => _verboseAnnouncements; set => Set(ref _verboseAnnouncements, value); }

    private bool _immersiveAmbience = true;
    /// <summary>
    /// Active l'ambiance "salon victorien" : phrases d'immersion au début/fin de partie et lors d'événements clés.
    /// Quand des fichiers audio seront ajoutés, contrôle aussi la musique de fond et les sons d'ambiance (cheminée, horloge).
    /// False = mode sobre (jeu sec, sans fioritures).
    /// </summary>
    public bool ImmersiveAmbience { get => _immersiveAmbience; set => Set(ref _immersiveAmbience, value); }

    private string _language = "";
    /// <summary>
    /// Code langue ISO 2-letter ("fr", "en", "es"). Vide au premier lancement → auto-détecté
    /// depuis la locale système. Le changement nécessite un redémarrage de l'app.
    /// </summary>
    public string Language { get => _language; set => Set(ref _language, value ?? ""); }

    // --- Premier lancement ---
    private bool _hasSeenTutorialPrompt = false;
    public bool HasSeenTutorialPrompt { get => _hasSeenTutorialPrompt; set => Set(ref _hasSeenTutorialPrompt, value); }

    // --- Timer de tour ---
    private TurnTimerMode _turnTimerMode = TurnTimerMode.Disabled;
    public TurnTimerMode TurnTimerMode { get => _turnTimerMode; set => Set(ref _turnTimerMode, value); }

    private TimeoutBehavior _timeoutBehavior = TimeoutBehavior.AutoPlay;
    public TimeoutBehavior TimeoutBehavior { get => _timeoutBehavior; set => Set(ref _timeoutBehavior, value); }

    /// <summary>Durée du timer de tour en secondes (0 = désactivé).</summary>
    public int TurnTimerSeconds => TurnTimerMode switch
    {
        TurnTimerMode.Relaxed  => 120,
        TurnTimerMode.Standard => 60,
        TurnTimerMode.Fast     => 30,
        _ => 0,
    };

    // --- Persistance ---

    private static string FilePath => UserDataPaths.Get("settings.json");

    public static Settings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Settings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void ResetToDefaults()
    {
        TurnTimerMode = TurnTimerMode.Disabled;
        TimeoutBehavior = TimeoutBehavior.AutoPlay;
        MasterVolume = 0.85f;
        DiceVolume = 1.0f;
        MoveVolume = 0.7f;
        EventVolume = 0.95f;
        AIThinkVolume = 0.35f;
        NavigationVolume = 0.5f;
        AmbienceMusicVolume = 0.20f;
        WalkStepDelayMs = 110;
        AIThinkPulseMs = 500;
        LegalMovePreviewSounds = true;
        OpportunityHints = true;
        ZoneTransitionSounds = true;
        EdgeBumpSound = true;
        VerboseAnnouncements = false;
        ImmersiveAmbience = true;
        Save();
    }

    private static float Clamp(float v) => Math.Clamp(v, 0f, 1f);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
        Save();
    }
}

public enum TurnTimerMode { Disabled, Relaxed, Standard, Fast }
public enum TimeoutBehavior { AutoPlay, SkipTurn }
