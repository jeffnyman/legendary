# Eric the Unready

Legend Entertainment, 1993. Bob Bates's comedy of a knight who is not up to it, on the
system of [*Gateway*](gateway.md) and [*Spellcasting 301*](spellcasting-301.md).

This is what the first look found: how the program is built, what of it the tools already
read, and its objects. What is known for certain is said plainly; what is inferred says so.
Addresses are `segment:offset` in hex, with segments relative to where the program loads.
Every one of them can be looked at with the [tools](../tools/README.md).

## The copy looked at

The game came on floppy disks and, later, on a CD. The copy here has both beside the
installed game: its folder has `floppy`, the disks' files; `CD`, an image of the disc; and
`ERIC`, the game as installed, which is what is read. So the game's files are a folder
deeper than the other games', and the tools look there for them.

It is run as `ERIC XGA XMOUSE BLASTER 7 220`, with `MT32 2 330` after it for a Roland
MT-32. `XGA` is new: the earlier games were told `VGA` at the most.

## How the program is built

As *Gateway* is: Microsoft C, linked by .RTLink/Plus 5.02, with a run-time library and
overlays in files of their own. The linker's files are the same, and the tools read them
with nothing changed.

| File | What it is |
|---|---|
| `ERIC.EXE` | What the player runs |
| `ERICMAIN.EXE` | The game |
| `ERICMAIN.OVL` | Its overlays, 372,336 bytes |
| `ERIC.RTL` | The run-time library the two programs share |
| `RTLINKST.COM` | The linker's startup code |

| What | How many |
|---|---|
| Segments | 248 |
| Records in the program | 58 |
| Records in the library | 68 |
| Relocations in the program | 6,269 |
| Overlays | 29 |
| Thunks | 990 |

The program starts at `2307:076C`, in the overlay manager's segment, and its memory ends
at `572B`. The 29 overlays load at three places, each past the biggest overlay of the one
before, as in the games before *Gateway*:

| Place | Overlays |
|---|---|
| `31DF` | 6 |
| `345B` | 3 |
| `37C8` | 20 |

## What the tools read as it is

With nothing changed for this game:

| What | How much |
|---|---|
| The vocabulary | 1,683 words |
| The text | 30 blocks, 4,923 strings, 472,425 bytes packed into 242,183 |
| The grammar | 450 forms, making 200 actions |
| The actions' routines | found, each with its overlay |
| The music | 80 songs in one file, 40 of them for the AdLib |
| The sounds | 53, RealSound |
| The fonts | 20 |
| The regions | 6 files |
| The pictures | 938 of 1,285, and all of them [with one change](#the-pictures) |

The pictures took one change, [below](#the-pictures).

## The objects

There are 537, in the eight types the system has had since *Spellcasting 101*:

| Type | What it is here | How many | Bytes | Bytes of flags |
|---|---|---|---|---|
| 1 | A room | 74 | 24 | 2 |
| 2 | A thing | 225 | 36 | 6 |
| 3 | A name with nothing to it: `yes`, `music`, a pirate spoken of | 17 | 12 | 2 |
| 4 | What is everywhere: the sky, the sun, a wall, a number | 11 | 20 | 2 |
| 5 | Scenery that many rooms share: water, trees, hills | 38 | 22 | 4 |
| 6 | A door, in two rooms | 4 | 26 | 4 |
| 7 | A person or a creature | 58 | 32 | 4 |
| 8 | Something fixed in a room, or a part of something | 110 | 22 | 4 |

What each type is for is said from the objects that are of it, and not from the code.

The table of them, six bytes an object, and the tables beside it are found by the same
code as in *Gateway*, in segment `2B79`: the nouns at `2B79:0D4E`, the adjectives at
`2B79:183C`, the exits at `2B79:1CF2` and the doors at `2B79:2006`. The lists end at a
word with its top bit set, as from *Spellcasting 301*.

A record is *Gateway*'s, in three types changed:

- **A room** is *Spellcasting 301*'s, 24 bytes with two bytes of flags, where *Gateway*'s
  had four.
- **A door** is two bytes longer, 26, with four bytes of flags.
- **Something fixed** is two bytes shorter, 22.

Each was read off the records themselves: a record ends with the number of the object's
routine, which counts up through the objects, and the words before it fall into place from
there. They are right by what they give. Every object on a list says it is in the object
whose list it is, for all 537; a room's exits start where the room before's end; and
`RESTART.DAT` is as long, to the byte, as the records make it.

| What | How many |
|---|---|
| Names, from `OBJECT.DAT` | 537 |
| Routines of objects, by number | 517 |
| Flags | 40, of which 33 are set on something as the game starts |
| Exits | 197 |

The exits are of three of the five kinds *Gateway* has: 88 to a room, 6 through a door, and
103 that are a routine's to decide, more than half, where *Gateway* had under half.

The flags have no names yet.

## The pictures

There are 1,309 entries in 39 files, 24 of them a palette alone and 1,285 with a picture,
kept as [*Spellcasting 301*'s](spellcasting-301.md) are:
packed by PKWARE's implode, each saying the display it is for. The file's number says it
too, `ERIC_1nn.PIC` for display 1 and so on.

| Display | What | Entries | The biggest |
|---|---|---|---|
| 1 | The EGA | 28 | |
| 2 | The VGA, 16 colors | 481 | |
| 3 | The VGA, 256 colors | 429 | |
| 4 | A screen of 16 colors wider than the VGA's | 24 | 713 by 108 |
| 5 | The Super VGA, 256 colors | 347 | 640 by 480 |

**Display 5 is new**, and is what the `XGA` the game is run with asks for: 640 by 480 in
256 colors. The title's picture for it says so in its own letters, `256 Color Super VGA`.
It is kept as display 3 is, a palette of 256 colors, three bytes each, and a byte a pixel.
The reader took its palette for one of 16, and so started unpacking in the middle of the
colors; told it is 256, all 347 unpack to exactly the bytes their sizes need. Those looked
at are right: the title, filling the screen, and a man in a green tunic.

**Display 4** is in *Spellcasting 301* and *Gateway* too, the same 24 pictures in each: the
compass and its pieces, and `LEGEND ENTERTAINMENT COMPANY presents`, which is 713 across.
So it is for a screen wider than 640, in 16 colors. That would be the Super VGA's 800 by
600, which hasn't been confirmed from the program.

## How a string is named

As [*Spellcasting 301*](spellcasting-301.md) and *Gateway* name theirs, with nothing
changed: by a far pointer that is no pointer, whose segment is `F000` and whose offset is
the string's block times 1,024 and its place in the block. The routine most are handed
to, which prints, is at `1C8A:10F7`. The tools' `loads` finds them as it is.

| What | How many |
|---|---|
| Places that name a string | 4,984: 164 in the root and 4,820 in the overlays |
| Handed to the routine that prints | 3,445 |
| Handed to another routine | 159 |
| Kept in data, as an object's text is | 1,380 |
| Strings named, of the file's 4,923 | 4,870 |

Of the 53 no place names, 25 are all of block 22, which is the help: the keys and what
the mouse does, shown a screen at a time, so by its block and not string by string. The
other 28 are 17 in block 17 and a few in six others. *Spellcasting 301* has strings named
by a number worked out as the game runs, and these may be the same; they haven't been
looked at.

## The timers

There are 71, numbered, and found as in *Gateway*: the tools list each with the routine it
runs and the places that start it. They have no names yet.

## A saved game

`RESTART.DAT` is 16,395 bytes, and is laid out as *Spellcasting 301*'s is:

| Bytes | What |
|---|---|
| 34 | That start it |
| 15,516 | The records of the 537 objects |
| 835 | 84 saved variables |
| 8 | Four words more |
| 2 | A count of timers, 0 as the game starts, and four bytes for each |

## Still to do

- Which screen display 4 is.
- The 28 strings no place names, outside the help.
- The flags' names and the timers'.
- Where a command is parsed and performed, for the player's `--watch`.
- The game hasn't been run on the PC.
