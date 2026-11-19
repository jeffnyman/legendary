# Companions of Xanth

Legend Entertainment, 1993. Piers Anthony's *Demons Don't Dream*, made a game by Michael
Lindner. It is Legend's first game without a parser: everything is done with the mouse, on
pictures and on lists of verbs and things.

This is the start of the first look: what is in the game's folder, what of it the tools
already read, how its program is built, and its objects. What is known for certain is said plainly; what
is inferred says so. Addresses are `segment:offset` in hex, with segments relative to where
the program loads. Every one of them can be looked at with the [tools](../tools/README.md).

## What the folder has

| File | What it is |
|---|---|
| `XANTH.EXE` | The game: there is no program to run before it |
| `XANTH.OVL` | Its overlays, 326,091 bytes |
| `XANTHSTR.DAT` | The text |
| `OBJECT.DAT`, `RESTART.DAT` | The objects' names, and the state the game starts from |
| `XANTH_00.HLP` to `XANTH_02.HLP` | Three files of a kind the earlier games don't have, not looked at |
| `XANTH_nn.PIC`, `.RGN`, `.FNT`, `.MUS`, and `.RS` files | Pictures, their regions, fonts, music and sounds |
| `LEGEND.INI` | Where the game looks: `GAMEDATA=D:\XANTH\`, `SAVEDATA=C:\XANTH\` |

There is no `VOCAB.DAT`: a game with no parser has no words to know. But `OBJECT.DAT` and
`RESTART.DAT` are here as in every game before, so the world is still objects with records.

The game came on a CD, and an image of it is beside the game, in `cd`. It is a raw image,
2,352 bytes a sector with a `.cue` file, where *Gateway II*'s was plain ISO 9660: each
sector as the disc has it, with its 2,048 bytes of data sixteen bytes in. The tools' `disc`
reads either. The disc's folder `XANTH` has 96 files, and the game's folder has all but
four of them: three short lists of the installer's, and `XANTH_01.VOC`, 190,251,008
bytes, which by its name and size is the game's speech. It starts with a count, 1,327, and
that many places in the file; it hasn't been looked at further, or copied.

## What the tools read as it is

| What | How much |
|---|---|
| The text | 80 blocks, 5,542 strings, 387,571 bytes packed into 196,695 |
| The pictures | 27 files, 3,294 entries |
| The music | 4 files, 86 songs |
| The sounds | 29, RealSound |
| The fonts | 6 |
| The regions | 17 files |

The text, the pictures, the music and the regions are kept as the games before keep
theirs. The regions matter more here than they did: they say which part of a picture is
which object, which in this game is how the player says what they mean.

## How the program is built

It is Microsoft C linked by .RTLink/Plus, as every game since *Spellcasting 201*, but put
together a third way.

The games from *Spellcasting 201* to *Gateway II* are two programs, a launcher and the
game, that share a run-time library in a file of its own; and the game isn't a program DOS
can load, but records the linker's startup code puts in place. **This one is whole**: one
ordinary program, which DOS loads and relocates from its MZ header, with the linker's
overlay manager and the library linked into it. `XANTH.EXE` says so itself, in the text
`.RTLink(R)/Plus run-time code` where the manager's code begins.

| What | How much |
|---|---|
| The MZ header | 73,401 bytes, nearly all relocations: 5,304 of them |
| The image | 191,656 bytes |
| After the image | 43,193 bytes |
| Overlays | 70 |
| Thunks | 883 |

The program starts at `1DC1:0835`, in the overlay manager's segment, at the same offset
*Gateway II*'s manager starts at. The manager's table is found as before, and every one of
the 883 thunks goes into the overlay it names. The 70 overlays are small, from one to
seven kilobytes, and load at three places:

| Place | Overlays |
|---|---|
| `30CB` | 15 |
| `321E` | 53 |
| `322B` | 2 |

### What comes after the image

The file is longer than its MZ header says the program is, by 43,193 bytes. They are the
rest of the program's memory: its far data and its data segment, which come after the
place the overlays load. The file leaves out the stretch between, where the overlays go,
and the linker's own code reads the rest in.

Nothing in the header says where it goes, and no relocation of the header's is in it.
Where it goes was worked out from two things. The data segment of a Microsoft C program
starts eight bytes before the text `MS Run-Time Library`, which is 19,592 bytes into the
43,193; and the data segment is `38AF`, the segment past the overlays that the program's
relocations name most, 597 times. So the bytes go from `33E6:0008`, just past the last
overlay, which ends at `33E5`. Put there, 41 of the 75 places where the program pushes
the address of something in its data segment land on the start of a string; put anywhere
else tried, one to nine do.

The tools and `Legendary.Core` read the program that way.

## The objects

There are 498, and they are kept as [*Gateway II*'s](gateway-ii.md#the-objects) are: a
table of six bytes an object, its type and a far pointer to its record, after the types'
table and the flags' in the same segment, `23A1`, with the count a word of the data
segment, at `38AF:084C`. The names are in `OBJECT.DAT`. Object 1 is `You`, in the bedroom.

There are five types, where that game has seven:

| Type | What it is here | How many | Bytes | Bytes of flags |
|---|---|---|---|---|
| 1 | A room | 70 | 18 | 2 |
| 2 | A thing | 326 | 28 | 4 |
| 3 | A thing with two lists: a desk, an envelope, a computer | 69 | 32 | 4 |
| 4 | What is in many places: a door, a path, the sky | 14 | 14 | 2 |
| 5 | A person or a creature | 19 | 32 | 2 |

What each type is for is said from the objects that are of it, and not from the code.

| Field | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| The object it is in | | `08` | `08` | | `06` |
| Which of that one's lists it is on, a byte | | `0A` | `0A` | | `08` |
| The next on that list | | `0C` | `0C` | | `0A` |
| Its own lists | `06` | `0E` | `0E`, `10` | `06` | `0C`, `0E`, `10` |
| Its routine's number | `0A` | `1A` | `1E` | `0C` | `1E` |
| A room's exits: how many, and the first | `0E`, `10` | | | | |

All but the last row are from the routines that read and write each, which pick the place
by the object's type. They are the routines *Gateway II* has, in the same order, from the
same compiler told to make faster code: where that game multiplies an object's number by
six, this one adds it up. A room's exits are from the records.

**A record has no words.** The two words that say where an object's nouns and adjectives
start, which every game before has, aren't here: nothing is typed, so nothing has a name
to be typed by.

**Three fields are new**, each with a routine to read it and most with one to write it: a
word at `0C`, `14`, `18` and `18` of types 1, 2, 3 and 5; one at `16`, `1A`, `08` and `1A`
of types 2, 3, 4 and 5; and one at `18`, `1C`, `0A` and `1C` of those. The first counts up
through the rooms, 391, 392, 393, and is over 256 for things too. They are likely what the
game shows for an object, a picture or a region or a line of text; that hasn't been looked
at.

They are right by what they give: every object on a list says it is in the object whose
list it is, all 296 of them; every room's exits start where the room before's end, 152
exits in all; and the routines' numbers go from 1 to 448. The letter from Pia and a
bracelet are in the envelope, which is on the desk, which is in the bedroom.

There are 23 flags, in a table as before. None has a name yet.

## Still to do

- The speech, `XANTH_01.VOC`, which is on the CD alone.
- The three new fields of a record, the exits' table, the routines' table and the flags'
  names.
- How a click on a picture or a list becomes something done, which is the part that is new.
- The `.HLP` files.
- The game hasn't been run on the PC.
