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
| *Timequest* | 1991 | `tquest` | Built as *Spellcasting 101* is: its overlays are read, and its text unpacked |
| *Spellcasting 201: The Sorcerer's Appliance* | 1991 | `sc201` | Not yet tried; linked with .RTLink, not PLINK86 |
| *Gateway* | 1992 | `gateway` | Not yet tried |
| *Spellcasting 301: Spring Break* | 1992 | `sc301` | Not yet tried |
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
things hold, and each action as the game performs it, by the numbers
[the notes](docs/spellcasting-101.md#the-objects) use. An action is a number to the game; the
watch names it by the first command in the game's grammar that makes it, so `LOOK AT RAT` is
said as `examine`, which is the same action.

```
world: 3:35 pm, turn 0, in Bedchamber (1)
world:   here: rat (352), you (153) [cloak (82)], pile of hay (72), ..., fireplace (62) [fire (63)], writing desk (61) [love note (74), application (73)], door (60)
world:   action 60: examine rat (352)
world: 3:40 pm, turn 1, in Bedchamber (1)
world:   action 25: climb chimney (185)
world:   action 27: climb in fireplace (62)
world: 3:45 pm, turn 2, in Bedchamber (1)
```

That is `EXAMINE RAT` and then `CLIMB CHIMNEY`, and it shows things the game hasn't told you:
what the desk has in it, and that climbing the chimney, with the fire lit,
[is turned into entering the fireplace](docs/spellcasting-101.md#from-an-action-to-what-happens).
Every action is said, as the game gets to its routine that performs one: the PC tells the
watch each time the program is there. The room and its contents are looked at once a frame,
so a turn can be seen half done, the room changed and the turn not yet counted. The watch
reads the game's memory and changes nothing. For now it knows *Spellcasting 101*.

## What's here

- **`src/Legendary.Player`** is the player: a window, the keyboard, the mouse and sound around
  the PC, by SDL3. It began as a copy of Excavate's.
- **`src/Legendary.Pc`** is [a PC for the games to run on](docs/the-pc.md), in C#: a copy of
  Excavate's core. *Spellcasting 101* plays on it, from its title through commands typed
  at it.
- **`src/Legendary.Core`** reads the games' files, in C#. So far: a DOS program's MZ header, and
  a program linked by PLINK86, with its overlays loaded into the memory it would have, the
  packed text of a string file, the pictures of a picture file and their regions, the songs of a music file, and a
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
