# Changelog

All notable changes to Parcheesi Access are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] — 2026-04-29

A significant UX overhaul focused on the first-time experience: a streamlined main menu, a fully interactive learn-by-doing tutorial, and a sweep of accessibility and screen-reader friendliness improvements throughout the interface.

### Added

- **Streamlined main menu** with a clear two-screen flow. The launch screen now shows just six top-level buttons (New game, Audio tutorial, Statistics, Achievements, Settings, Quit) plus a Resume section when a saved game exists. The "New game" button opens a dedicated configuration screen with a Back button. Replaces the previous single-page setup that mixed game configuration, secondary actions, and rules in one dense view. Inspired by the menu structure of accessible board game platforms (RS Games, Manamon).
- **Interactive tutorial** that teaches by doing rather than by reading. Twenty-six steps across four phases — basics, capturing, bringing home, and free play — with three pre-staged scenarios using forced dice rolls. The user actually performs each mechanic on a controlled situation and the tutorial validates each action before advancing. Ends with a free turn against the AI to bridge guided learning and real gameplay. Replaces the previous text-only tutorial that just narrated rules without practice.
  - **Auto-launch on first run**: the tutorial starts automatically the first time the application is launched.
  - **Contextual nudge on wrong key**: when the user presses an unexpected key, the tutorial briefly explains what the pressed key does in the game ("A applies the first die"), then repeats the expected action — instead of a generic "not quite" message.
  - **Advanced mechanics phase**: three text steps cover doubles, the triple-double penalty, safe squares, and dice preview keys (Shift+A/Z/S) before the free-play phase.
  - **Immediate feedback**: each completed action transitions to the next step instantly, following modern tutorial design principles ([Game Wisdom](https://game-wisdom.com/critical/tutorials-game-design-feedback)).

### Changed

- **Accessibility labels on every input field**: all text boxes and combo boxes in the main menu and settings now expose `AutomationProperties.LabeledBy` pointing to their visible label. Screen readers (NVDA/JAWS) now announce the label text when focus moves to the field, instead of just "edit" or "combobox". Previously, the user had to use the screen reader's review cursor to find the label. Affects player name fields, AI personality combos, walk-speed and AI-speed settings, and timer mode/behaviour combos.
- **Removed emoji glyphs from all UI labels**, end-game log entries, settings section titles, and tutorial prompts. NVDA reads emojis inconsistently (sometimes verbosely as "game die" or "trophy", sometimes silently), which broke the announcement flow. Plain text labels everywhere now.
- **First-time launch experience**: instead of focusing the "Tutorial" button and waiting for the user to find it, the tutorial now opens automatically.

### Fixed

- **Tutorial transitions no longer rebuild the board grid**: the previous implementation triggered a full rebuild of the board buttons when loading a snapshot for the next tutorial scenario, which destroyed keyboard focus and stranded the user on the "roll the dice" prompt with no way to continue. The internal `Board` property change notification was semantically incorrect — only the model contents changed, not the model identity. Removing the spurious notification preserves focus across snapshot transitions.
- **Capture-bonus flow**: after a capture, the tutorial now explicitly prompts the user to re-select their piece before applying the bonus with B, matching the actual game flow where the selection resets after each move.

[1.2.0]: https://github.com/PlatinumTsuki/Parcheesi-Access/releases/tag/v1.2.0

## [1.1.0] — 2026-04-27

This release brings audio polish, accessibility improvements, a game replay feature, and switches user data storage to the standard `%APPDATA%` location so progress is preserved across versions.

### Added

- **Accelerated game replay** (V key on the post-game screen): re-plays the entire game with the original audio (dice shake/throw, walk sounds with stereo pan, captures, turn changes, applause) at a faster pace. Press Space to skip to the next event, Escape to exit. Walk audio runs at 50 ms per square (vs 110 ms in-game).
- **Contextual keyboard help** (F1 key in-game and on the post-game screen): announces only the keys relevant to the current state (awaiting roll, choosing a piece, applying a die, opponent's turn, paused, end screen, tutorial). Complements the full H help without replacing it.
- **Average pieces home on defeats** statistic: visible in the stats summary. Helps new players gauge their progression even when losing.

### Changed

- **User data is now stored in `%APPDATA%\Parcheesi-Access\`** instead of next to the executable. Settings, statistics, achievements, current saved game, and logs are now shared across every installation of the game on the same Windows user account — installing a new version no longer resets your progress. On first launch, existing data next to the `.exe` is automatically copied to the new location (originals are kept intact for rollback).
- **Audio is now serialized between actions**: walk audio of a move finishes before the next action's audio begins (turn change, opponent thinking, end-of-game applause). Adopts the standard "serialisation" pattern of turn-based audio games (Manamon, Tactical Battle, RS Games), with a 300 ms silent gap as auditory punctuation between events.

### Fixed

- **Player input on opponent's turn**: pressing dice / piece-selection / move keys (Space, A/Z/S/B, 1-4, T) during the opponent's turn no longer interferes with their play. The game now politely announces "It's not your turn to play" instead of accidentally rolling dice for the opponent or moving their piece.

[1.1.0]: https://github.com/PlatinumTsuki/Parcheesi-Access/releases/tag/v1.1.0

## [1.0.1] — 2026-04-27

Bug-fix release addressing accessibility, localization, and stability issues found after 1.0.0. No gameplay or feature changes.

### Fixed

- **Localization** — board cell announcements (square coordinates, lane positions, occupants) were always read in French even when the game was set to English or Spanish. NVDA now reads them in the active language.
- **Localization** — the verbose game-start greeting embedded the raw difficulty name in French (`Facile` / `Moyen` / `Difficile`) regardless of the active language; now uses the localized label.
- **AI personality lost after a turn timeout** — when the turn timer expired in auto-play mode, the next AI player's personality could be overwritten with neutral, causing it to play without its assigned style for the rest of the game.
- **Resumed game ignored the turn timer** — reloading a saved game on a human turn left the player with unlimited time on that turn; the timer now starts immediately.
- **End-of-game screen was unreadable** — keyboard shortcuts to replay the result, read final board state, statistics, achievements, opponents, journal, and help (R / K / L / M / J / P / H) were blocked on the post-game summary. They now work in read-only mode after the game ends.
- **End-of-game announcement was spoken twice with overlap** — the post-game summary and the achievement notification were two separate announcements queued 2.5 seconds apart, causing NVDA to interrupt or skip one. They are now combined into a single announcement.

### Changed

- **Triumph win narration is more meaningful** — previously a single criterion (e.g. no captures suffered) was enough to trigger the highest-tier victory phrase and standing ovation. Now requires meeting at least 2 of 3 criteria (under 30 turns, against Hard AI, no captures suffered), so triumph narration is reserved for genuinely impressive games.
- **End-of-game announcement is shorter** — the full ranking and per-game statistics stay in the on-screen summary panel; only the essential result is spoken, leaving the detail available via the read-board / read-stats keys.
- **Save failure is now reported** — if saving the in-progress game fails (disk full, permissions, etc.), the game announces it once instead of failing silently.

### Added

- Diagnostic `audio_load.log` file written next to the executable if any embedded sound asset fails to load at startup. Empty / absent in normal operation.

[1.0.1]: https://github.com/PlatinumTsuki/Parcheesi-Access/releases/tag/v1.0.1

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
