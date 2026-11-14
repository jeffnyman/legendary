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
| *Spellcasting 101: Sorcerers Get All the Girls* | 1990 | `sc101` | Starts on Legendary's PC. Its program and overlays are read, its text unpacked, its objects, vocabulary, grammar and routines listed, and its pictures, music and sounds unpacked; [notes](docs/spellcasting-101.md) |
| *Timequest* | 1991 | `tquest` | Built as *Spellcasting 101* is: its overlays are read, and its text unpacked |
| *Spellcasting 201: The Sorcerer's Appliance* | 1991 | `sc201` | Not yet tried; linked with .RTLink, not PLINK86 |
| *Gateway* | 1992 | `gateway` | Not yet tried |
| *Spellcasting 301: Spring Break* | 1992 | `sc301` | Not yet tried |
| *Eric the Unready* | 1993 | `ericun` | Not yet tried |
| *Gateway II: Homeworld* | 1993 | `gateway2` | Not yet tried |
| *Mission Critical* | 1995 | `mcritic` | Not yet tried; a 32-bit program, and another kind of game |

None of the games are included: Legendary reads copies you have.

## What's here

- **`src/Legendary.Pc`** is [a PC for the games to run on](docs/the-pc.md), in C#: a copy of
  Excavate's core. *Spellcasting 101* starts on it, to its title and into the game. It has no
  window yet.
- **`src/Legendary.Core`** reads the games' files, in C#. So far: a DOS program's MZ header, and
  a program linked by PLINK86, with its overlays loaded into the memory it would have, the
  packed text of a string file, the pictures of a picture file and their regions, the songs of a music file, and a
  RealSound recording.
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
