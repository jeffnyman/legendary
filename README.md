# Legendary

**Legendary** takes Legend Entertainment's adventure games apart, to show how they work: how
each is built, what's in its files, and how its parser, its world and its pictures are put
together. The notes are in [docs](docs/), a game at a time; the tools that did the digging are
in [tools](tools/); and the aim, in the end, is a player that plays the games from their own
files.

It is to these games what a Z-machine interpreter and its documents are to Infocom's, with one
difference found on the first day: Legend's games have no virtual machine. Each is a C program,
compiled for the PC, with its story in its code. So the work here is disassembly, and the
question of what a player is stays open until more of the games is understood.

## The games

| Game | Year | Short name | Where it stands |
|---|---|---|---|
| *Spellcasting 101: Sorcerers Get All the Girls* | 1990 | `sc101` | Plays, with AdLib music and the speaker's sounds. Its program and overlays are read, its text unpacked, its objects, vocabulary, grammar and routines listed, and its pictures, music and sounds unpacked; [notes](docs/spellcasting-101.md) |
| *Timequest* | 1991 | `tquest` | Plays, with its pictures, music and sounds; not yet played through. Built as *Spellcasting 101* is: its overlays are read, its text unpacked, and its objects, flags, exits, vocabulary, grammar, routines, timers and music listed, and `--watch` and `--panel` show its world; [notes](docs/timequest.md) |
| *Spellcasting 201: The Sorcerer's Appliance* | 1991 | `sc201` | Plays, with its pictures, music and sounds; not yet played through. Linked with .RTLink, not PLINK86: its program, run-time library and overlays are read, and its vocabulary, objects, flags, grammar, routines, timers, text and music, and
`--watch` and `--panel` show its world; [notes](docs/spellcasting-201.md) |
| *Gateway* | 1992 | `gateway` | Not yet tried |
| *Spellcasting 301: Spring Break* | 1992 | `sc301` | Starts on the PC and runs on into the game itself; not yet played. Linked with a newer .RTLink: its program, run-time library and overlays are read, and its vocabulary, objects, grammar, routines, timers, text, music and sounds; [notes](docs/spellcasting-301.md) |
| *Eric the Unready* | 1993 | `ericun` | Not yet tried |
| *Gateway II: Homeworld* | 1993 | `gateway2` | Not yet tried |
| *Mission Critical* | 1995 | `mcritic` | Not yet tried; a 32-bit program, and another kind of game |

None of the games are included: Legendary reads, and plays, copies you have.

## Playing

With the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```
dotnet run --project src/Legendary.Player -c Release -- "<the game's folder>"
```

The game's folder is the one with its programs in it, `S101.EXE` and `S101MAIN.EXE` for
*Spellcasting 101*. A folder given once is remembered, and the game is then played by its
short name:

```
dotnet run --project src/Legendary.Player -c Release -- sc101
```

With no game named, a menu lists them. `--help` lists the options, and `--args "cga"` hands
the game [a word it knows](docs/spellcasting-101.md#what-the-launcher-is-told).

The keyboard is the PC's, key for key. A click in the window gives the game the mouse, and
Ctrl+F10 takes it back. F11 is full screen, F12 full speed on and off, and Ctrl+F5 and Ctrl+F9
save and load the whole machine's state. The game's folder is never written to: what a game
saves goes to a folder of its own in your application data.

### Watching the game think

`--watch` has the player say, on the terminal it was started from, what the game has in its
memory as it's played: the time, the turn and the room, what is in the room and what those
things hold, the room's exits, each action as the game performs it, and what the action
changed, any object anywhere that moved or had a flag set or cleared, by the numbers
[the notes](docs/spellcasting-101.md#the-objects) use. An action is a number to the game; the
watch names it by the first command in the game's grammar that makes it, so `LOOK AT RAT` is
said as `examine`, which is the same action.

```
world: 3:35 pm, turn 0, in Bedchamber (1)
world:   here: rat (352), you (153) [cloak (82)], pile of hay (72), ..., fireplace (62) [fire (63)], writing desk (61) [love note (74), application (73)], door (60)
world:   exits: north to Potty Chamber (2); east: "If you want to enter the fireplace, then say so."; west to Alley (4) by door (60); ...; up: a routine decides
world:   words: examine [verb] rat [noun]
world:   action 60: examine rat (352)
world:     answered by rat (352), asked as the object: 1
world: 3:40 pm, turn 1, in Bedchamber (1)
world:   words: climb [verb] chimney [noun]
world:   action 25: climb chimney (185)
world:     given up by a routine, which starts over with another action
world:   action 27: climb in fireplace (62)
world:     answered by fireplace (62), asked as the object: 1
world: 3:45 pm, turn 2, in Bedchamber (1)
world:   words: open [verb or preposition] desk [adjective or noun]
world:   form 208: open OBJECT, which is action 128
world:   object "desk", looked for anywhere near: writing desk (61)
world:   action 128: open writing desk (61)
world:     answered by the action's own routine: 1
world:   writing desk (61): flag 10, touched, set
world:   writing desk (61): flag 12, open, set
world:   love note (74): flag 6, seen, set
world: 3:50 pm, turn 3, in Bedchamber (1)
world:   words: take [verb] note [adjective or noun]
world:   action 74: get love note (74)
world:     answered by the action's own routine: 1
world:   love note (74) moved: from writing desk (61) to you (153)
world:   words: n [verb or noun]
world:   action 197: go north
world:   you (153) moved: from Bedchamber (1) to Potty Chamber (2)
```

That is `EXAMINE RAT`, `CLIMB CHIMNEY`, `OPEN DESK`, `TAKE NOTE` and `N`, and it shows things the game hasn't told you:
what the desk has in it, and that climbing the chimney, with the fire lit,
[is turned into entering the fireplace](docs/spellcasting-101.md#from-an-action-to-what-happens).
Before each action are the command's words as the parser took them, each with the parts of
speech such a word can be: `desk` is an adjective or a noun, and the game's grammar settles
which. A word the game doesn't know is said as `[not known]`. After the words come the form
of the game's grammar that they fit, which is what makes them an action, and then each phrase
with the objects the parser found it could be and where the form let it look: `TAKE` doesn't
look in your hands, and `ASK RAT ABOUT SWORD` looks everywhere, and finds a sword that is in a
shop a long way off. The sample has those lines for `OPEN DESK` alone.
Every action is said, as the game gets to its routine that performs one: the PC tells the
watch each time the program is there. The game then asks a row of routines about the action,
the room's, the actor's, the objects' and at last the action's own, and stops at the first
that answers; the watch says which that was. The rat has its own answer to being examined.
Climbing the chimney is never answered at all: the chimney's routine started the turn over,
by a jump back to the game's main loop, and the watch says that too. Going somewhere ends
the same way, with a jump, once you've been moved. The game's timers are said as well, each as it
is started, as it runs at a turn's end, and as it is stopped: things the story has put by to
happen later, such as the two the Bedchamber starts before you have typed anything. The flags are the way into what the game
means by them: opening the desk sets its flag 12, so 12 is open. The room and its contents are looked at once a frame,
so a turn can be seen half done, the room changed and the turn not yet counted. The watch
reads the game's memory and changes nothing. For now it knows *Spellcasting 101*.

`--panel` shows the same beside the game, in a column down the right of the window: the time,
the turn and the room at the top, what is in the room with each thing under what holds it,
the exits, the timers that are going, and under those the last of what happened, the newest
at the bottom.

```
3:55 pm   turn 4
Potty Chamber (2)

Here
  you (153)
    love note (74)
    cloak (82), list 2
  tiny door (66)

Exits
  south  to Bedchamber (1)
  down   a routine decides

Timers
  the man with the whip: 37 turns to go

Lately
action 197: go north
you (153) moved: from Bedchamber (1) to Potty
  Chamber (2)
```

The cloak is on your second list, which is what you wear. `--watch` and `--panel` can be given
together, for the panel and the terminal's full record of the play.

*Spellcasting 201* and *Timequest* can be watched the same way, with all of it: the room,
each command's words, form and objects, each action and who answered it, the timers, and
what changed. For *Timequest* the room comes with the place and the year, as `Rome, 44 BC`.

The panel's text is small: 8 pixels, doubled once the window is 960 high. `--panel-text 1.5`
asks for the panel with its text one and a half times that size, and any size from 1 to 4 can
be asked for. While playing, Ctrl+plus makes the text half a size bigger and Ctrl+minus half a size
smaller, and the window widens or narrows with the panel. The size the text ends at is kept in
the settings, and is the size the panel has the next time, for any game. The panel stays 46
characters wide, so bigger text makes a wider panel. On a screen that isn't high-density, whole
sizes are the sharpest.

## What's here

- **`src/Legendary.Player`** is the player: a window, the keyboard, the mouse and sound around
  the PC, by SDL3. It began as a copy of Excavate's.
- **`src/Legendary.Pc`** is [a PC for the games to run on](docs/the-pc.md), in C#: a copy of
  Excavate's core. *Spellcasting 101* plays on it, from its title through commands typed
  at it.
- **`src/Legendary.Core`** reads the games' files, in C#. So far: a DOS program's MZ header, and
  a program linked by PLINK86 or by .RTLink/Plus, with its overlays loaded into the memory it
  would have, the packed text of a string file, the pictures of a picture file and their regions, the songs of a music file, and a
  RealSound recording, and the words of a vocabulary. It also reads a game's world from its
  memory as it runs: the room, the clock, the action, the tree of objects, and the grammar.
- **`tools`** has the same in Python, as commands to explore a game with: the overlays, the
  ways into them, memory in hex and as instructions, the text, the objects, the grammar, the
  pictures as PNG files, and the sounds as WAV files. See [tools/README.md](tools/README.md).
- **`docs`** has what was found, and how.

## Building

With the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```
dotnet build -c Release
dotnet test -c Release
```

The tests that need a game look for it in the folder `LEGENDARY_GAMES` names, or in a `legend`
folder above the tests, as `legend/sc101/game` and `legend/tquest/game`. Without a game, its
tests pass without checking.

## License

Legendary is free software under the GNU General Public License, version 3: see
[LICENSE](LICENSE), and [NOTICE.md](NOTICE.md) for the work it is derived from. The games are
their owners', and aren't included.
