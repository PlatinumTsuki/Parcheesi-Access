==========================================================
  PARCHEESI ACCESS — Accessible Parcheesi for blind players
==========================================================


WHAT'S IN THIS FOLDER
---------------------

  Parcheesi-Access.exe   The game (single self-contained file).
  README.txt             This file.
  LICENSE                GPL v3 — full license text.
  CREDITS.md             Third-party assets and required attributions.


HOW TO INSTALL
--------------

1. Extract this folder anywhere you like (Desktop, Downloads, etc.).
2. That's it. No .NET runtime to install, no dependencies.
   Everything is bundled inside the .exe (~225 MB).


HOW TO LAUNCH
-------------

Just double-click "Parcheesi-Access.exe". The game opens in a
native Windows window. Your screen reader (NVDA, JAWS, Narrator)
takes over immediately for all announcements.

On first launch, the game auto-detects your Windows display
language. English, French, and Spanish are supported. You can
switch later in Settings -> Language.


SCREEN READER NOTES
-------------------

Tested primarily with NVDA on Windows 10/11. The game uses
WPF native controls and UIA Live Regions, so it should also
work with JAWS and Narrator.

Make sure focus mode is active (NVDA+Space toggles it).
Live region announcements are on by default in NVDA.


MAIN KEYS
---------

  Space            Roll the dice
  1 to 4           Select your piece (also reads its position)
  A / Z / S / B    Apply die 1 / die 2 / sum / bonus
  Shift + A/Z/S/B  Preview where that move would land
  Arrows           Navigate the board cell by cell
  Tab              Cycle through zones (board, journal, etc.)
  T                End your turn manually


ON-DEMAND KEYS
--------------

  D     Read current dice (values, which are used)
  I     List every legal move available right now
  L     Read the entire board (all pieces)
  J     Read opponent pieces only
  C     Tactical advice (AI-recommended move)
  R     Repeat last announcement
  P     Last 5 lines of the journal
  K     Cumulative statistics
  M     Achievement list
  H     Full keyboard help
  F2    Pause / resume
  F3    Toggle verbose mode


HOW TO PLAY (BRIEF)
-------------------

Goal: bring all 4 of your pieces home before the other players.

  1. Roll two dice (Space).
  2. Select one of your pieces (1 to 4).
  3. Apply a die (A, Z, S or B) to move it forward.

To LEAVE the base, you need a 5 (on a single die OR as the sum
of both dice).

Roll DOUBLES, you play again. Three doubles in a row sends
your most-advanced piece back to the base.

Land on an opponent's piece (outside a safe square), you
CAPTURE it and earn a 20-square bonus (apply with B).

Bring a piece home, you earn a 10-square bonus.

12 squares are safe (4 starting squares + 8 intermediates).
No capture can happen on those.


GAME MODES
----------

At setup, you choose:

  Opponents:
    - Solo vs 1 computer  (you are Red)
    - Solo vs 2 computers
    - Solo vs 3 computers
    - 2 / 3 / 4 humans hot-seat (no AI)

  AI difficulty:
    - Easy    (mostly random, relaxed)
    - Medium  (tactical)
    - Hard    (anticipates captures, real challenge)

  AI personality (per opponent, manual or random):
    - The Aggressor (capture-driven)
    - The Cautious  (defensive)
    - The Runner    (race to home)


SAVE AND RESUME
---------------

Close the game mid-match and the next launch shows
"Resume saved game" pre-focused at the top of the menu.
You pick up exactly where you left off.


VICTORIAN PARLOR AMBIENCE
-------------------------

The game ships with a discreet Victorian parlor mood:
fireplace crackling, classical piano (Satie, Chopin, Schumann,
Debussy), polite applause when you win. If you prefer pure
gameplay audio, flip the master toggle in Settings -> Sober mode.


KNOWN ISSUES
------------

  - On first launch, Windows SmartScreen may show
    "Windows protected your PC". Click "More info" then
    "Run anyway". This is normal for an unsigned .exe.
    The game is harmless and open source — the full source
    is at https://github.com/PlatinumTsuki/Parcheesi-Access.

  - If your screen reader stays silent, check that focus mode
    is active (NVDA+Space).


LICENSE AND CREDITS
-------------------

The game source code is licensed under the GNU General Public
License v3. See LICENSE.

Game sound effects come from Kenney.nl asset packs (CC0).
Music recordings are public domain or CC-BY (Kevin MacLeod,
Christine Hartley-Troskie, Laurens Goedhart). See CREDITS.md
for the full attribution list.


ENJOY!
