# Parcheesi Access

**An accessible Parcheesi game for blind and visually impaired players, with a Victorian parlor atmosphere.**

Parcheesi Access is a Windows desktop game that brings the classic Parcheesi (also known as Pachisi or Parchís) to keyboard-only, screen-reader-driven play. Every dice roll, every move, every capture is announced clearly. The game is wrapped in a discreet Victorian parlor ambience — fireplace crackling, classical piano in the background, polite applause when you win — that you can turn off entirely if you prefer a sober experience.

## Status

- **Solo experience: complete and stable.** Playable end-to-end against 1, 2, or 3 computer opponents, or in 2-to-4-player hot-seat mode.
- **Online multiplayer:** planned for version 2.0. Not yet implemented.

## Features

- **100% keyboard play**, designed and tested with NVDA on Windows.
- **3 AI difficulty levels × 3 personalities** (the Aggressor, the Cautious, the Runner) = 9 distinct opponents.
- **Audio tutorial** in 10 user-paced steps.
- **13 achievements** with persistent unlock tracking.
- **Cumulative statistics** (wins, captures, fastest victory, breakdown by difficulty and personality).
- **Optional turn timer** (relaxed / standard / fast) with auto-play or skip-turn behavior.
- **Auto-save and resume**: abandon a game and pick it up later.
- **Pause / resume** at any time (F2).
- **Per-piece move preview** before committing.
- **Verbose / concise announcement modes**, plus on-demand keys for board summary, opponent state, journal replay, and AI-recommended move.
- **Rich audio**: 40 short sound effects, 8-track classical music rotation in game (Satie, Chopin, Schumann, Debussy), Chopin nocturne in the menu, ambient fireplace, polite-applause variants tiered by your performance.
- **Three languages**: English, French, Spanish — auto-detected from your Windows locale on first launch, switchable in Settings.
- **Sober mode**: a single toggle disables the Victorian ambience, narration phrases, and music if you want only the strict gameplay audio.

## Download

Pre-built single-file Windows executables (~225 MB self-contained, no .NET install required) are attached to each release on the [Releases page](https://github.com/PlatinumTsuki/Parcheesi-Access/releases).

## Build from source

### Prerequisites

- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download))
- **Windows 10/11** (the app uses WPF)

### Build

```sh
cd dotnet
dotnet build -c Release
```

The compiled executable will be at:

```
dotnet/Parcheesi.App/bin/Release/net10.0-windows/Parcheesi-Access.exe
```

### Build a self-contained release (single .exe)

```sh
cd dotnet
dotnet publish Parcheesi.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

This produces a single `publish/Parcheesi-Access.exe` (~225 MB) that includes the .NET runtime and all embedded assets (sounds, music, languages). It can be zipped and distributed to anyone with Windows 10/11 — no installation required.

## Project structure

```
Parcheesi-Access/
├── README.md
├── LICENSE              # GPL v3
├── CREDITS.md           # third-party asset attributions
├── CHANGELOG.md
├── .gitignore
├── release/
│   └── README.txt       # English readme bundled with the release ZIP
└── dotnet/
    ├── Parcheesi.slnx
    ├── Parcheesi.Core/      # pure game logic — rules, board, AI
    ├── Parcheesi.Audio/     # NAudio-based audio engine (multi-format, looping, streaming)
    ├── Parcheesi.App/       # WPF window, MVVM, settings, achievements
    │   ├── Assets/Sounds/   # game SFX + music/ambience tracks
    │   └── Assets/Lang/     # fr.json / en.json / es.json
    ├── _levelmeter/         # internal CLI tool to measure peak/RMS of music files
    └── KEYBOARD_REFERENCE.md
```

## Keyboard reference

A complete shortcut list lives in [`dotnet/KEYBOARD_REFERENCE.md`](dotnet/KEYBOARD_REFERENCE.md). The most-used keys:

- **Space** — roll the dice / advance tutorial
- **1–4** — select your piece
- **A / Z / S / B** — apply die 1, die 2, sum, or bonus
- **Shift + A / Z / S / B** — preview where that die would land you
- **T** — end turn manually
- **F2** — pause / resume
- **L** — read the board
- **D** — read current dice
- **R** — repeat last announcement
- **H** — full keyboard help
- **C** — AI-recommended move
- **K** — statistics
- **M** — achievements

## Localization

The game ships with three full language packs:

- 🇬🇧 **English** (default for non-French/Spanish locales)
- 🇫🇷 **Français** (the original)
- 🇪🇸 **Español**

The game auto-detects your system language on first launch. To change later: Settings → 🌐 Language. The change takes effect the next time the application starts.

Translations cover every announcement, error message, achievement name, tutorial step, statistics line, and on-screen label.

## Credits & licenses

See [CREDITS.md](CREDITS.md) for the full list of third-party audio assets, their sources, and any attribution requirements (CC0 / CC BY / public domain).

The game source code is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE).

## Acknowledgments

- **Kenney.nl** — for the generous CC0 sound asset packs that power the gameplay audio.
- **Wikimedia Commons** and **Musopen** — for hosting public-domain classical recordings.
- **BigSoundBank** and **OpenGameArt** — for high-quality CC0 ambient sounds.
- **NAudio** — for making cross-format audio playback in .NET painless.
- **The audiogames.net community** — for setting the bar on what an accessible game should sound like.

## Reporting issues

Bugs, feature requests, and feedback are welcome on the [Issues page](https://github.com/PlatinumTsuki/Parcheesi-Access/issues).
