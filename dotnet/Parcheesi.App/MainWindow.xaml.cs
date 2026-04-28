using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Parcheesi.App.Game;
using Parcheesi.Core.Localization;
using Parcheesi.App.ViewModels;
using Parcheesi.Audio;
using Parcheesi.Core;

namespace Parcheesi.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly Dictionary<(int row, int col), Button> _cellButtons = new();
    private (int row, int col) _focusedCell = (0, 0);
    private CellKind? _previousZone = null;

    public MainWindow()
    {
        InitializeComponent();
        ApplyLocalization();
        DataContext = _vm;
        Loaded += (_, _) =>
        {
            RefreshMainMenu();
            _vm.StartMenuAmbience();
            // Premier lancement : déclencher automatiquement le tutoriel.
            // StartTutorial met HasSeenTutorialPrompt à true, donc il ne se relancera
            // plus tout seul, même si l'utilisateur quitte avec Échap dès la première étape.
            if (!_vm.Settings.HasSeenTutorialPrompt)
            {
                _vm.StartTutorial();
            }
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.Board) && _vm.Board != null)
                BuildBoardGrid();
            // Quand le tuto déclenche StartGame en interne, on amène le focus au plateau
            // (sinon il reste sur le bouton "Tutoriel" du menu, devenu invisible).
            if (e.PropertyName == nameof(_vm.IsInGame) && _vm.IsInGame && _vm.TutorialIsRunning)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _previousZone = null;
                    FocusBoardPosition(BoardLayoutData.RingCells[0]);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            // Quand on revient au menu principal (fin/abandon de tuto, abandon de partie, etc.),
            // on rétablit le focus sur le bon bouton du menu principal.
            if (e.PropertyName == nameof(_vm.IsInMainMenu) && _vm.IsInMainMenu)
            {
                Dispatcher.BeginInvoke(new Action(RefreshMainMenu), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
        _vm.AnnounceRequested += AnnounceToScreenReader;
        _vm.LogEntries.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ScrollLogToBottom),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Annonce un message à NVDA. Trick technique : sur l'événement LiveRegionChanged,
    /// NVDA récupère AutomationProperties.Name de l'élément, pas son Text. On met donc
    /// le message dans le Name (mis à jour dynamiquement), puis on lève l'événement.
    /// </summary>
    private void AnnounceToScreenReader(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => AnnounceToScreenReader(text)));
            return;
        }

        // Pour le visuel : conserver le texte aussi (utile si quelqu'un de voyant suit).
        LiveAnnouncerText.Text = text;

        // Pour NVDA : c'est le Name qui compte sur LiveRegionChanged.
        AutomationProperties.SetName(LiveAnnouncerText, text);

        var peer = UIElementAutomationPeer.FromElement(LiveAnnouncerText)
                   ?? UIElementAutomationPeer.CreatePeerForElement(LiveAnnouncerText);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>
    /// Applique les chaînes localisées aux contrôles XAML. Appelé une fois après InitializeComponent.
    /// Le texte par défaut en français reste dans le XAML comme fallback si Loc échoue.
    /// </summary>
    private void ApplyLocalization()
    {
        // Fenêtre + zone d'en-tête
        AutomationProperties.SetName(this, Loc.Get("xaml.window_automation_name"));
        AutomationProperties.SetName(TurnInfoText, Loc.Get("xaml.turn_info_label"));
        AutomationProperties.SetName(DiceInfoText, Loc.Get("xaml.dice_label"));

        // Menu principal (écran 1)
        MainMenuTitle.Text = Loc.Get("xaml.main_menu_title");
        AutomationProperties.SetName(MenuStatusText, Loc.Get("xaml.status_label"));
        ResumeSectionTitle.Text = Loc.Get("xaml.resume_section_title");
        ResumeButton.Content = Loc.Get("xaml.resume_button");
        MenuNewGameButton.Content = Loc.Get("xaml.menu_new_game_button");
        MenuTutorialButton.Content = Loc.Get("xaml.menu_tutorial_button");
        MenuStatsButton.Content = Loc.Get("xaml.menu_stats_button");
        MenuAchievementsButton.Content = Loc.Get("xaml.menu_achievements_button");
        MenuSettingsButton.Content = Loc.Get("xaml.menu_settings_button");
        MenuQuitButton.Content = Loc.Get("xaml.menu_quit_button");

        // Configuration nouvelle partie (écran 2)
        NewGameTitle.Text = Loc.Get("xaml.new_game_title");
        OpponentsLabel.Content = Loc.Get("xaml.opponents_label");
        AutomationProperties.SetName(GameModeCombo, Loc.Get("xaml.gamemode_automation"));
        GameModeSolo1.Content = Loc.Get("xaml.gamemode_solo_1");
        GameModeSolo2.Content = Loc.Get("xaml.gamemode_solo_2");
        GameModeSolo3.Content = Loc.Get("xaml.gamemode_solo_3");
        GameModeHotseat2.Content = Loc.Get("xaml.gamemode_hotseat_2");
        GameModeHotseat3.Content = Loc.Get("xaml.gamemode_hotseat_3");
        GameModeHotseat4.Content = Loc.Get("xaml.gamemode_hotseat_4");

        DifficultyLabel.Content = Loc.Get("xaml.difficulty_label");
        AutomationProperties.SetName(DifficultyCombo, Loc.Get("xaml.difficulty_automation"));
        DifficultyEasy.Content = Loc.Get("xaml.difficulty_easy");
        DifficultyMedium.Content = Loc.Get("xaml.difficulty_medium");
        DifficultyHard.Content = Loc.Get("xaml.difficulty_hard");

        CustomizeExpander.Header = Loc.Get("xaml.customize_header");
        CustomizeHint.Text = Loc.Get("xaml.customize_hint");
        NameRougeLabel.Content = Loc.Get("xaml.name_rouge_label");
        NameJauneLabel.Content = Loc.Get("xaml.name_jaune_label");
        NameBleuLabel.Content = Loc.Get("xaml.name_bleu_label");
        NameVertLabel.Content = Loc.Get("xaml.name_vert_label");
        PersonalityJauneLabel.Content = Loc.Get("xaml.personality_jaune_label");
        PersonalityBleuLabel.Content = Loc.Get("xaml.personality_bleu_label");
        PersonalityVertLabel.Content = Loc.Get("xaml.personality_vert_label");
        var pRandom = Loc.Get("xaml.personality_random");
        var pAggressive = Loc.Get("xaml.personality_aggressive");
        var pPrudent = Loc.Get("xaml.personality_prudent");
        var pCoureur = Loc.Get("xaml.personality_coureur");
        PersonalityJauneRandom.Content = pRandom;
        PersonalityJauneAggressive.Content = pAggressive;
        PersonalityJaunePrudent.Content = pPrudent;
        PersonalityJauneCoureur.Content = pCoureur;
        PersonalityBleuRandom.Content = pRandom;
        PersonalityBleuAggressive.Content = pAggressive;
        PersonalityBleuPrudent.Content = pPrudent;
        PersonalityBleuCoureur.Content = pCoureur;
        PersonalityVertRandom.Content = pRandom;
        PersonalityVertAggressive.Content = pAggressive;
        PersonalityVertPrudent.Content = pPrudent;
        PersonalityVertCoureur.Content = pCoureur;

        StartButton.Content = Loc.Get("xaml.start_button");
        BackToMenuButton.Content = Loc.Get("xaml.back_to_menu");
        RulesExpander.Header = Loc.Get("xaml.rules_header");
        RulesText.Inlines.Clear();
        RulesText.Inlines.Add(new System.Windows.Documents.Run(Loc.Get("xaml.rules_text")));

        // Plateau de jeu
        AutomationProperties.SetName(BoardGrid, Loc.Get("xaml.board_automation"));
        MyPiecesTitle.Text = Loc.Get("xaml.my_pieces_title");
        AutomationProperties.SetName(MyPiecesText, Loc.Get("xaml.my_pieces_automation"));
        ActionsTitle.Text = Loc.Get("xaml.actions_title");
        ActionRollButton.Content = Loc.Get("xaml.action_roll");
        ActionBoardButton.Content = Loc.Get("xaml.action_board");
        ActionOpponentsButton.Content = Loc.Get("xaml.action_opponents");
        ActionHelpButton.Content = Loc.Get("xaml.action_help");
        ActionEndTurnButton.Content = Loc.Get("xaml.action_end_turn");
        ActionAbandonButton.Content = Loc.Get("xaml.action_abandon");
        ActionQuitGameButton.Content = Loc.Get("xaml.action_close_game");
        ActionsHint.Text = Loc.Get("xaml.actions_hint");
        JournalTitle.Text = Loc.Get("xaml.journal_title");
        AutomationProperties.SetName(LogList, Loc.Get("xaml.journal_automation"));

        // Réglages
        SettingsTitle.Text = Loc.Get("xaml.settings_title");
        SettingsSubtitle.Text = Loc.Get("xaml.settings_subtitle");
        VolumesTitle.Text = Loc.Get("xaml.settings_volumes_title");
        MasterLabel.Content = Loc.Get("xaml.settings_master_label");
        AutomationProperties.SetName(MasterSlider, Loc.Get("xaml.settings_master_automation"));
        DiceLabel.Content = Loc.Get("xaml.settings_dice_label");
        AutomationProperties.SetName(DiceSlider, Loc.Get("xaml.settings_dice_automation"));
        MoveLabel.Content = Loc.Get("xaml.settings_move_label");
        AutomationProperties.SetName(MoveSlider, Loc.Get("xaml.settings_move_automation"));
        EventLabel.Content = Loc.Get("xaml.settings_event_label");
        AutomationProperties.SetName(EventSlider, Loc.Get("xaml.settings_event_automation"));
        AIVolLabel.Content = Loc.Get("xaml.settings_aivol_label");
        AutomationProperties.SetName(AIVolSlider, Loc.Get("xaml.settings_aivol_automation"));
        NavVolLabel.Content = Loc.Get("xaml.settings_navvol_label");
        AutomationProperties.SetName(NavVolSlider, Loc.Get("xaml.settings_navvol_automation"));
        AmbMusicLabel.Content = Loc.Get("xaml.settings_ambmusic_label");
        AutomationProperties.SetName(AmbienceMusicSlider, Loc.Get("xaml.settings_ambmusic_automation"));

        SpeedsTitle.Text = Loc.Get("xaml.settings_speeds_title");
        WalkSpeedLabel.Content = Loc.Get("xaml.settings_walkspeed_label");
        WalkSpeedSlow.Content = Loc.Get("xaml.settings_walkspeed_slow");
        WalkSpeedModerate.Content = Loc.Get("xaml.settings_walkspeed_moderate");
        WalkSpeedNormal.Content = Loc.Get("xaml.settings_walkspeed_normal");
        WalkSpeedFast.Content = Loc.Get("xaml.settings_walkspeed_fast");
        WalkSpeedVeryFast.Content = Loc.Get("xaml.settings_walkspeed_veryfast");
        AISpeedLabel.Content = Loc.Get("xaml.settings_aispeed_label");
        AISpeedPatient.Content = Loc.Get("xaml.settings_aispeed_patient");
        AISpeedNormal.Content = Loc.Get("xaml.settings_aispeed_normal");
        AISpeedQuick.Content = Loc.Get("xaml.settings_aispeed_quick");
        AISpeedExpress.Content = Loc.Get("xaml.settings_aispeed_express");

        TimerTitle.Text = Loc.Get("xaml.settings_timer_title");
        TimerModeLabel.Content = Loc.Get("xaml.settings_timer_mode_label");
        TimerDisabled.Content = Loc.Get("xaml.settings_timer_disabled");
        TimerRelaxed.Content = Loc.Get("xaml.settings_timer_relaxed");
        TimerStandard.Content = Loc.Get("xaml.settings_timer_standard");
        TimerFast.Content = Loc.Get("xaml.settings_timer_fast");
        TimerBehaviorLabel.Content = Loc.Get("xaml.settings_timer_behavior_label");
        TimerBehaviorAutoPlay.Content = Loc.Get("xaml.settings_timer_behavior_autoplay");
        TimerBehaviorSkip.Content = Loc.Get("xaml.settings_timer_behavior_skip");
        TimerHint.Text = Loc.Get("xaml.settings_timer_hint");

        BehaviorTitle.Text = Loc.Get("xaml.settings_behavior_title");
        LegalPreviewCheck.Content = Loc.Get("xaml.settings_legal_preview");
        OpportunityHintsCheck.Content = Loc.Get("xaml.settings_opportunity_hints");
        ZoneTransitionCheck.Content = Loc.Get("xaml.settings_zone_transition");
        EdgeBumpCheck.Content = Loc.Get("xaml.settings_edge_bump");
        VerboseCheck.Content = Loc.Get("xaml.settings_verbose");
        ImmersiveCheck.Content = Loc.Get("xaml.settings_immersive");

        LanguageTitle.Text = Loc.Get("xaml.settings_language_title");
        LanguageLabel.Content = Loc.Get("settings.language_label");
        AutomationProperties.SetName(LanguageCombo, Loc.Get("settings.language_label"));
        LanguageHint.Text = Loc.Get("settings.language_restart_hint");
        ResetSettingsButton.Content = Loc.Get("xaml.settings_reset");
        CloseSettingsButton.Content = Loc.Get("xaml.settings_back");

        // Écran de fin de partie
        EndPanelTitle.Text = Loc.Get("xaml.endpanel_title");
        AutomationProperties.SetName(EndGameSummaryText, Loc.Get("xaml.endpanel_summary_automation"));
        EndPanelWhatNow.Text = Loc.Get("xaml.endpanel_what_now");
        ReplayButton.Content = Loc.Get("xaml.endpanel_replay");
        NewGameButton.Content = Loc.Get("xaml.endpanel_new_game");
        EndPanelStatsButton.Content = Loc.Get("xaml.endpanel_view_stats");
        EndPanelQuitButton.Content = Loc.Get("xaml.endpanel_quit");
    }

    private void ScrollLogToBottom()
    {
        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        // Lecture des ComboBox (1 sélection chacune) au lieu de 9 RadioButtons
        bool[] aiPerSlot = GameModeCombo.SelectedIndex switch
        {
            0 => new[] { false, true },
            1 => new[] { false, true, true },
            2 => new[] { false, true, true, true },
            3 => new[] { false, false },
            4 => new[] { false, false, false },
            5 => new[] { false, false, false, false },
            _ => new[] { false, true },
        };

        var difficulty = DifficultyCombo.SelectedIndex switch
        {
            0 => AIDifficulty.Facile,
            2 => AIDifficulty.Difficile,
            _ => AIDifficulty.Moyen,
        };

        // Lit les noms personnalisés (vide = null = utilise le nom par défaut)
        var rawNames = new[] { NameRouge.Text, NameJaune.Text, NameBleu.Text, NameVert.Text };
        var customNames = new string?[aiPerSlot.Length];
        for (int i = 0; i < aiPerSlot.Length; i++)
        {
            var n = rawNames[i]?.Trim();
            customNames[i] = string.IsNullOrEmpty(n) ? null : n;
        }

        // Lit les personnalités choisies (Aléatoire = null = tirage au sort dans StartGame)
        var personalities = new AIPersonality?[aiPerSlot.Length];
        var personalityCombos = new[] { (ComboBox?)null, PersonalityJaune, PersonalityBleu, PersonalityVert };
        for (int i = 0; i < aiPerSlot.Length; i++)
        {
            personalities[i] = (aiPerSlot[i] && i > 0)
                ? ComboToPersonality(personalityCombos[i])
                : null;
        }

        _vm.StartGame(aiPerSlot, difficulty, customNames, personalities);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _previousZone = null;
            FocusBoardPosition(BoardLayoutData.RingCells[0]);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private AIPersonality? ComboToPersonality(ComboBox? combo)
    {
        if (combo == null) return null;
        return combo.SelectedIndex switch
        {
            1 => AIPersonality.Aggressive,
            2 => AIPersonality.Prudent,
            3 => AIPersonality.Coureur,
            _ => null, // Aléatoire
        };
    }

    /// <summary>Rafraîchit le menu principal : Reprise visible si save existe, focus prioritaire.</summary>
    private void RefreshMainMenu()
    {
        if (_vm.HasSavedGame())
        {
            ResumeSection.Visibility = Visibility.Visible;
            ResumeButton.Focus();
        }
        else if (!_vm.Settings.HasSeenTutorialPrompt)
        {
            ResumeSection.Visibility = Visibility.Collapsed;
            MenuTutorialButton.Focus();
        }
        else
        {
            ResumeSection.Visibility = Visibility.Collapsed;
            MenuNewGameButton.Focus();
        }
    }

    /// <summary>Rafraîchit l'écran de configuration : ajuste les slots IA selon le mode et focus la première combo.</summary>
    private void RefreshNewGameSetup()
    {
        UpdatePlayerSlotsVisibility();
        GameModeCombo.Focus();
    }

    private void GameModeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdatePlayerSlotsVisibility();
    }

    /// <summary>Affiche uniquement les slots de joueurs présents dans le mode choisi,
    /// et les personnalités d'IA uniquement pour les slots IA.</summary>
    private void UpdatePlayerSlotsVisibility()
    {
        if (GameModeCombo == null || PlayerSlotRouge == null) return; // pas encore chargé

        int idx = GameModeCombo.SelectedIndex;
        bool[] aiPerSlot = idx switch
        {
            0 => new[] { false, true },                      // Solo +1 IA
            1 => new[] { false, true, true },                 // Solo +2 IA
            2 => new[] { false, true, true, true },           // Solo +3 IA
            3 => new[] { false, false },                      // Hot-seat 2
            4 => new[] { false, false, false },               // Hot-seat 3
            5 => new[] { false, false, false, false },        // Hot-seat 4
            _ => new[] { false, true },
        };
        int count = aiPerSlot.Length;

        // Slots de joueurs (nom)
        PlayerSlotRouge.Visibility = count >= 1 ? Visibility.Visible : Visibility.Collapsed;
        PlayerSlotJaune.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
        PlayerSlotBleu.Visibility  = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
        PlayerSlotVert.Visibility  = count >= 4 ? Visibility.Visible : Visibility.Collapsed;

        // Personnalité visible uniquement pour les slots IA
        PersonalityJauneRow.Visibility = (count >= 2 && aiPerSlot[1]) ? Visibility.Visible : Visibility.Collapsed;
        PersonalityBleuRow.Visibility  = (count >= 3 && aiPerSlot[2]) ? Visibility.Visible : Visibility.Collapsed;
        PersonalityVertRow.Visibility  = (count >= 4 && aiPerSlot[3]) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AbandonButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.AbandonAndReturnToMenu();
        Dispatcher.BeginInvoke(new Action(RefreshMainMenu), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Depuis le menu principal : ouvrir l'écran de configuration de partie.</summary>
    private void MenuNewGameButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToNewGameSetup();
        Dispatcher.BeginInvoke(new Action(RefreshNewGameSetup), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Depuis l'écran de configuration : retour au menu principal.</summary>
    private void BackToMenuButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToMainMenu();
        Dispatcher.BeginInvoke(new Action(RefreshMainMenu), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Depuis l'écran de fin : retour direct à la configuration de partie (pour changer les paramètres).</summary>
    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.ReturnToMenu();
        _vm.GoToNewGameSetup();
        Dispatcher.BeginInvoke(new Action(RefreshNewGameSetup), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ReplayButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.ReplaySameSettings();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _previousZone = null;
            FocusBoardPosition(BoardLayoutData.RingCells[0]);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void MenuStatsButton_Click(object sender, RoutedEventArgs e) => _vm.ReadStats();
    private void MenuAchievementsButton_Click(object sender, RoutedEventArgs e) => _vm.ReadAchievements();

    private void TutorialButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.StartTutorial();
    }

    private void MenuSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenSettings();
        Dispatcher.BeginInvoke(new Action(SyncSettingsControlsFromSettings),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.CloseSettings();
        _vm.GoToMainMenu();
        Dispatcher.BeginInvoke(new Action(RefreshMainMenu), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.Settings.ResetToDefaults();
        SyncSettingsControlsFromSettings();
    }

    private void WalkSpeedCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (WalkSpeedCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var ms))
            _vm.Settings.WalkStepDelayMs = ms;
    }

    private void AISpeedCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AISpeedCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var ms))
            _vm.Settings.AIThinkPulseMs = ms;
    }

    private void TimerModeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TimerModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && Enum.TryParse<TurnTimerMode>(tag, out var mode))
            _vm.Settings.TurnTimerMode = mode;
    }

    private void TimeoutBehaviorCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TimeoutBehaviorCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && Enum.TryParse<TimeoutBehavior>(tag, out var behavior))
            _vm.Settings.TimeoutBehavior = behavior;
    }

    private void LanguageCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _vm.Settings.Language = tag;
    }

    /// <summary>Met les ComboBox à l'état correspondant aux réglages courants (utile au reset/affichage initial).</summary>
    private void SyncSettingsControlsFromSettings()
    {
        var walk = _vm.Settings.WalkStepDelayMs;
        WalkSpeedCombo.SelectedIndex = walk switch
        {
            >= 180 => 0, >= 130 => 1, >= 95 => 2, >= 65 => 3, _ => 4,
        };
        var ai = _vm.Settings.AIThinkPulseMs;
        AISpeedCombo.SelectedIndex = ai switch
        {
            >= 700 => 0, >= 425 => 1, >= 275 => 2, _ => 3,
        };
        TimerModeCombo.SelectedIndex = _vm.Settings.TurnTimerMode switch
        {
            TurnTimerMode.Relaxed  => 1,
            TurnTimerMode.Standard => 2,
            TurnTimerMode.Fast     => 3,
            _ => 0,
        };
        TimeoutBehaviorCombo.SelectedIndex = _vm.Settings.TimeoutBehavior == TimeoutBehavior.SkipTurn ? 1 : 0;
        LanguageCombo.SelectedIndex = _vm.Settings.Language switch
        {
            "en" => 1,
            "es" => 2,
            _ => 0,  // fr et fallback
        };
    }

    private void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.ResumeSavedGame())
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _previousZone = null;
                FocusBoardPosition(BoardLayoutData.RingCells[0]);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>Mappe une touche clavier vers une action attendue par le tutoriel interactif.</summary>
    private static MainViewModel.TutorialAction MapKeyToTutorialAction(Key key) => key switch
    {
        Key.Space or Key.Enter or Key.Right => MainViewModel.TutorialAction.DoRollDice,
        Key.D1 or Key.NumPad1 => MainViewModel.TutorialAction.DoSelectPiece1,
        Key.D2 or Key.NumPad2 => MainViewModel.TutorialAction.DoSelectPiece2,
        Key.D3 or Key.NumPad3 => MainViewModel.TutorialAction.DoSelectPiece3,
        Key.D4 or Key.NumPad4 => MainViewModel.TutorialAction.DoSelectPiece4,
        Key.A => MainViewModel.TutorialAction.DoApplyDieA,
        Key.Z => MainViewModel.TutorialAction.DoApplyDieZ,
        Key.S => MainViewModel.TutorialAction.DoApplyDieS,
        Key.B => MainViewModel.TutorialAction.DoApplyBonus,
        Key.T => MainViewModel.TutorialAction.DoEndTurn,
        Key.F => MainViewModel.TutorialAction.DoFinishFreePlay,
        _ => MainViewModel.TutorialAction.None,
    };

    private void RollButton_Click(object sender, RoutedEventArgs e) => _vm.RollDice();
    private void ReadBoardButton_Click(object sender, RoutedEventArgs e) => _vm.ReadBoard();
    private void ReadOpponentsButton_Click(object sender, RoutedEventArgs e) => _vm.ReadOpponents();
    private void HelpButton_Click(object sender, RoutedEventArgs e) => _vm.ReadHelp();
    private void EndTurnButton_Click(object sender, RoutedEventArgs e) => _vm.EndTurnManually();
    private void QuitButton_Click(object sender, RoutedEventArgs e) => Close();

    private void BuildBoardGrid()
    {
        BoardGrid.Children.Clear();
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();
        _cellButtons.Clear();

        for (int r = 0; r < BoardLayoutData.GridRows; r++)
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (int c = 0; c < BoardLayoutData.GridCols; c++)
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (_vm.Board == null) return;

        foreach (var cv in _vm.Board.Cells)
        {
            var btn = new Button
            {
                Style = (Style)Resources["BoardCellButton"]!,
                Tag = cv,
                Background = GetCellBackground(cv),
                BorderBrush = GetCellBorderBrush(cv),
                BorderThickness = GetCellBorderThickness(cv),
            };

            var nameBinding = new System.Windows.Data.Binding(nameof(BoardCellViewModel.AutomationName))
            {
                Source = cv,
                Mode = System.Windows.Data.BindingMode.OneWay,
            };
            btn.SetBinding(AutomationProperties.NameProperty, nameBinding);

            var contentBinding = new System.Windows.Data.Binding(nameof(BoardCellViewModel.OccupantGlyph))
            {
                Source = cv,
                Mode = System.Windows.Data.BindingMode.OneWay,
            };
            btn.SetBinding(Button.ContentProperty, contentBinding);

            btn.GotKeyboardFocus += (s, ev) =>
            {
                if (s is Button b && b.Tag is BoardCellViewModel vcv)
                    _focusedCell = (vcv.Cell.GridRow, vcv.Cell.GridCol);
            };
            btn.Click += BoardCell_Click;

            Grid.SetRow(btn, cv.Cell.GridRow);
            Grid.SetColumn(btn, cv.Cell.GridCol);
            BoardGrid.Children.Add(btn);
            _cellButtons[(cv.Cell.GridRow, cv.Cell.GridCol)] = btn;
        }
    }

    /// <summary>Quand on clique une case (souris OU Espace sur le focus), priorité au lancer si on attend les dés.</summary>
    private void BoardCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not BoardCellViewModel cv) return;
        if (_vm.Game == null) return;

        if (_vm.Game.AwaitingRoll)
        {
            _vm.RollDice();
            return;
        }

        var current = _vm.Game.Current;
        foreach (var piece in current.Pieces)
        {
            var cell = _vm.Board?.FindCellForPiece(piece);
            if (cell == cv) { _vm.SelectPiece(piece.Id); return; }
        }
    }

    private Brush GetCellBackground(BoardCellViewModel cv)
    {
        return cv.Cell.Kind switch
        {
            CellKind.Base => GetColorBrush(cv.Cell.Owner!.Value, alpha: 0.40),
            CellKind.Lane => GetColorBrush(cv.Cell.Owner!.Value, alpha: 0.25),
            CellKind.Home => Brushes.Goldenrod,
            CellKind.Ring => cv.Cell.RingPos.HasValue && Parcheesi.Core.BoardLayout.IsSafe(cv.Cell.RingPos.Value)
                ? (Brush)FindResource("SafeBrush")
                : (Brush)FindResource("PanelBg"),
            _ => Brushes.Transparent,
        };
    }

    /// <summary>Bordures plus marquées sur les cases-frontières d'une zone (utile pour les voyants).</summary>
    private Brush GetCellBorderBrush(BoardCellViewModel cv)
    {
        return cv.Cell.Kind switch
        {
            CellKind.Base => GetColorBrush(cv.Cell.Owner!.Value, alpha: 1.0),
            CellKind.Lane => GetColorBrush(cv.Cell.Owner!.Value, alpha: 0.7),
            CellKind.Home => Brushes.Gold,
            _ => (Brush)FindResource("BorderBrush"),
        };
    }

    private Thickness GetCellBorderThickness(BoardCellViewModel cv)
    {
        return cv.Cell.Kind switch
        {
            CellKind.Base => new Thickness(2),
            CellKind.Home => new Thickness(3),
            CellKind.Lane => new Thickness(1.5),
            _ => new Thickness(1),
        };
    }

    private Brush GetColorBrush(PlayerColor color, double alpha)
    {
        var key = color switch
        {
            PlayerColor.Rouge => "RougeBrush",
            PlayerColor.Jaune => "JauneBrush",
            PlayerColor.Bleu  => "BleuBrush",
            PlayerColor.Vert  => "VertBrush",
            _ => "PanelBg",
        };
        var solid = (SolidColorBrush)FindResource(key);
        var c = solid.Color;
        return new SolidColorBrush(Color.FromArgb((byte)(255 * alpha), c.R, c.G, c.B));
    }

    private void FocusBoardPosition((int row, int col) coord)
    {
        if (!_cellButtons.TryGetValue(coord, out var btn)) return;

        var newCv = btn.Tag as BoardCellViewModel;
        if (newCv != null && _vm.Settings.ZoneTransitionSounds)
        {
            var newKind = newCv.Cell.Kind;
            var pan = BoardLayoutData.StereoPan(coord.col);
            if (_previousZone.HasValue && _previousZone.Value != newKind)
                _vm.Audio.Play(SoundEffect.TurnChange, pan, _vm.Settings.NavigationVolume);
            else
                _vm.Audio.Play(SoundEffect.PieceSelect, pan, _vm.Settings.NavigationVolume * 0.8f);
            _previousZone = newKind;
        }

        btn.Focus();
        Keyboard.Focus(btn);
        _focusedCell = coord;
    }

    private (int row, int col)? FindNextFocusable((int row, int col) from, int dr, int dc)
    {
        for (int step = 1; step < Math.Max(BoardLayoutData.GridRows, BoardLayoutData.GridCols); step++)
        {
            int nr = from.row + dr * step, nc = from.col + dc * step;
            if (nr < 0 || nr >= BoardLayoutData.GridRows || nc < 0 || nc >= BoardLayoutData.GridCols) break;
            if (_cellButtons.ContainsKey((nr, nc))) return (nr, nc);
        }
        return null;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;

        // Tutoriel interactif : on intercepte les touches pour valider l'action attendue
        // à l'étape courante, et on bloque (avec un nudge) toute autre touche mutante.
        // Les touches d'inspection (lecture seule) sont toujours autorisées.
        if (_vm.TutorialIsRunning)
        {
            // Échap : sortir du tuto
            if (e.Key == Key.Escape)
            {
                _vm.InterruptTutorial();
                e.Handled = true;
                return;
            }
            // Modificateurs seuls : ignore
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt) return;

            // Touches d'inspection (lecture seule) : toujours autorisées même hors action attendue.
            switch (e.Key)
            {
                case Key.D when _vm.IsInGame: _vm.ReadDice(); e.Handled = true; return;
                case Key.L when _vm.IsInGame: _vm.ReadBoard(); e.Handled = true; return;
                case Key.J when _vm.IsInGame: _vm.ReadOpponents(); e.Handled = true; return;
                case Key.P when _vm.IsInGame: _vm.ReadRecentLog(); e.Handled = true; return;
                case Key.R: _vm.ReplayLastAnnouncement(); e.Handled = true; return;
                case Key.K when _vm.IsInGame: _vm.ReadStats(); e.Handled = true; return;
                case Key.M when _vm.IsInGame: _vm.ReadAchievements(); e.Handled = true; return;
                case Key.I when _vm.IsInGame: _vm.ReadSelectedPieceDetails(); e.Handled = true; return;
                case Key.H when _vm.IsInGame: _vm.ReadHelp(); e.Handled = true; return;
                case Key.F1: _vm.ReadContextualKeys(); e.Handled = true; return;
            }

            var attempted = MapKeyToTutorialAction(e.Key);
            var accepted = _vm.TutorialAcceptsKey(attempted);
            if (!accepted)
            {
                e.Handled = true;
                return;
            }
            // VM a accepté : on laisse passer aux handlers normaux ci-dessous.
        }

        // Sur l'écran de fin de partie, on autorise uniquement les touches en lecture seule
        // (relire le résumé, le plateau final, les stats…) sans aucune action de jeu.
        if (_vm.IsInEndScreen)
        {
            // Pendant un replay accéléré, Espace = suivant, Échap = sortir, autres touches bloquées
            // (pour ne pas que le replay soit pollué par d'autres lectures concurrentes).
            if (_vm.IsReplaying)
            {
                if (e.Key == Key.Space) { _vm.NextReplayEvent(); e.Handled = true; return; }
                if (e.Key == Key.Escape) { _vm.StopReplay(); e.Handled = true; return; }
                e.Handled = true; // bloque les autres touches
                return;
            }
            switch (e.Key)
            {
                case Key.R: _vm.ReplayLastAnnouncement(); e.Handled = true; return;
                case Key.K: _vm.ReadStats(); e.Handled = true; return;
                case Key.M: _vm.ReadAchievements(); e.Handled = true; return;
                case Key.L: _vm.ReadBoard(); e.Handled = true; return;
                case Key.J: _vm.ReadOpponents(); e.Handled = true; return;
                case Key.P: _vm.ReadRecentLog(); e.Handled = true; return;
                case Key.H: _vm.ReadHelp(); e.Handled = true; return;
                case Key.F1: _vm.ReadContextualKeys(); e.Handled = true; return;
                case Key.V: _vm.StartReplay(); e.Handled = true; return;
            }
            return;
        }

        if (!_vm.IsInGame) return;

        var focused = Keyboard.FocusedElement as Button;
        bool focusOnBoard = focused != null && focused.Tag is BoardCellViewModel;

        // Espace : sur une case du plateau, on lance les dés (priorité absolue).
        // Ailleurs (boutons d'action), on laisse l'activation normale du bouton se produire.
        if (e.Key == Key.Space && focusOnBoard && _vm.Game != null && _vm.Game.AwaitingRoll)
        {
            _vm.RollDice();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.D1: case Key.NumPad1: _vm.SelectPiece(1); e.Handled = true; return;
            case Key.D2: case Key.NumPad2: _vm.SelectPiece(2); e.Handled = true; return;
            case Key.D3: case Key.NumPad3: _vm.SelectPiece(3); e.Handled = true; return;
            case Key.D4: case Key.NumPad4: _vm.SelectPiece(4); e.Handled = true; return;
            // A/Z/S/B : appliquer le dé. Avec Shift : aperçu sans appliquer.
            case Key.A:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _vm.PreviewSingleDie(DiceUsage.Die1);
                else _vm.ApplyMove(DiceUsage.Die1);
                e.Handled = true; return;
            case Key.Z:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _vm.PreviewSingleDie(DiceUsage.Die2);
                else _vm.ApplyMove(DiceUsage.Die2);
                e.Handled = true; return;
            case Key.S:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _vm.PreviewSingleDie(DiceUsage.Sum);
                else _vm.ApplyMove(DiceUsage.Sum);
                e.Handled = true; return;
            case Key.B:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _vm.PreviewSingleDie(DiceUsage.Bonus);
                else if (_vm.Game != null && _vm.Game.Bonus > 0) _vm.ApplyMove(DiceUsage.Bonus);
                else _vm.ReadBoard();
                e.Handled = true; return;
            case Key.L: _vm.ReadBoard(); e.Handled = true; return;
            case Key.J: _vm.ReadOpponents(); e.Handled = true; return;
            case Key.H: _vm.ReadHelp(); e.Handled = true; return;
            case Key.P: _vm.ReadRecentLog(); e.Handled = true; return;
            case Key.R: _vm.ReplayLastAnnouncement(); e.Handled = true; return;
            case Key.K: _vm.ReadStats(); e.Handled = true; return;
            case Key.D: _vm.ReadDice(); e.Handled = true; return;
            case Key.I: _vm.ReadSelectedPieceDetails(); e.Handled = true; return;
            case Key.C: _vm.GiveTacticalAdvice(); e.Handled = true; return;
            case Key.M: _vm.ReadAchievements(); e.Handled = true; return;
            case Key.F1: _vm.ReadContextualKeys(); e.Handled = true; return;
            case Key.F2: _vm.TogglePause(); e.Handled = true; return;
            case Key.F3: _vm.QueryTimeRemaining(); e.Handled = true; return;
            case Key.T: _vm.EndTurnManually(); e.Handled = true; return;
        }

        // Flèches : valides uniquement quand le focus est sur le plateau.
        // On consomme TOUJOURS l'événement pour empêcher l'échappement vers le journal/etc.
        if (focusOnBoard)
        {
            int dr = 0, dc = 0;
            switch (e.Key)
            {
                case Key.Up:    dr = -1; break;
                case Key.Down:  dr =  1; break;
                case Key.Left:  dc = -1; break;
                case Key.Right: dc =  1; break;
                default: return;
            }
            e.Handled = true;
            var next = FindNextFocusable(_focusedCell, dr, dc);
            if (next.HasValue)
            {
                FocusBoardPosition(next.Value);
            }
            else if (_vm.Settings.EdgeBumpSound)
            {
                // Bord du plateau atteint : son d'erreur si activé dans les réglages.
                _vm.Audio.Play(SoundEffect.Error, 0f, 0.6f);
            }
        }
    }
}

/// <summary>Convertit un volume float [0..1] en pourcentage entier [0..100] et inversement.</summary>
public class VolumeToPercentConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is float f ? (double)(f * 100) : 0d;
    public object ConvertBack(object value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is double d ? (float)(d / 100.0) : 0f;
}

