# Changelog

All notable changes to Parcheesi Access are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] — 2026-04-26

First public release. The solo experience is feature-complete, distributable as a single self-contained Windows executable, and localized in three languages.

### Added

- **Core gameplay**
  - Faithful Parcheesi rules engine: 4 pieces per player, 68-square ring with 12 safe squares, 7-square home stretch, 5 to leave the base, 20-square bonus on capture, 10-square bonus on home arrival, three-doubles penalty.
  - 1-vs-AI, 1-vs-2-AI, 1-vs-3-AI, 2/3/4-player hot-seat modes.
  - Move preview before committing (per-die or per-sum).
  - Auto-save and resume.

- **AI**
  - Three difficulty levels: Easy, Medium, Hard.
  - Three personalities: the Aggressor (capture-driven), the Cautious (defensive), the Runner (race-to-home).
  - Random or manual personality assignment per opponent.
  - On-demand AI-recommended move (Difficile-level lookahead).

- **Accessibility**
  - 100% keyboard-driven, designed for NVDA on Windows.
  - Verbose / concise announcement modes.
  - On-demand keys to read the board, opponents, current dice, last announcement, journal, statistics, achievements, full help.
  - Stereo panning of move sounds and ambient layers.

- **Audio**
  - 40 CC0 sound effects from Kenney.nl asset packs.
  - 8-track classical piano rotation in game (Satie Gymnopédies 1–3 and Gnossienne 1, Schumann Träumerei, Chopin Berceuse and Raindrop Prelude, Debussy Clair de Lune), shuffled without immediate repeats.
  - Chopin Nocturne Op. 9 No. 2 in the menu.
  - Per-track gain normalization (target RMS −28 dB) so all music plays at perceived equal loudness.
  - Live volume slider for music with real-time effect.
  - Continuous fireplace ambience during gameplay.
  - Three tiers of victory applause based on performance (standing ovation / polite / scattered).

- **Victorian parlor ambience**
  - Immersive narration phrases at game start, capture, and end of game.
  - End-of-game narration adapts to your performance: triumph / standard / close-call wins, and honorable / standard / crushing defeats — each with 4 randomized phrase variants and contextual flavor lines.
  - Single master toggle to disable the entire ambience for a sober experience.

- **Statistics and achievements**
  - 13 unlockable achievements (first win, lightning win, untouchable, hunter, marathoner, queen of hard, full house, chameleon, master of styles, etc.).
  - Cumulative statistics with breakdown by difficulty and AI personality.

- **Settings**
  - 7 independent volume controls (master, dice, moves, events, AI ticks, navigation, ambient music).
  - Walk speed (5 levels), AI thinking speed (4 levels).
  - Turn timer (off / 2 min / 60 s / 30 s) with auto-play or skip behavior on expiration.
  - Five audio behavior toggles (legal-move preview, opportunity hints, zone-transition sounds, edge-bump error, verbose announcements).

- **Localization**
  - Three full language packs: English, French (original), Spanish.
  - Automatic detection from system locale on first launch.
  - Manual switch in Settings (effective next launch).
  - Every announcement, label, achievement, tutorial step, and statistic line translated.

- **Distribution**
  - Self-contained single-file `.exe` build (~225 MB) — no .NET install required by end users.
  - All sounds, music, and language files embedded in the executable.

[1.0.0]: https://github.com/PlatinumTsuki/Parcheesi-Access/releases/tag/v1.0.0
