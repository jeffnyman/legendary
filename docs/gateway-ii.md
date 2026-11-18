# Gateway II: Homeworld

Legend Entertainment, 1993. The second game from Frederik Pohl's books, by Glen Dahlgren
and Mike Verdu.

This is the start of the first look: where the game's files are, how its program is
built, its text, and how its engine differs from the games' before. What is known for certain is said plainly; what is inferred says so. Addresses are
`segment:offset` in hex, with segments relative to where the program loads. Every one of
them can be looked at with the [tools](../tools/README.md).

## The copy looked at

The copy here is from Legend's collection *The Lost Adventures of Legend*, whose files are
dated 1996: one disc with eight games' folders and four demos. The copy has two
parts.

- **The installed game**, a folder of 46 files: the program, its text, vocabulary and
  objects, the music, fonts and regions.
- **The disc**, as an image of 397 megabytes, with a folder `GW2` of 76 files.

The 37 files that are in both are the same, byte for byte. What the disc has more is the
pictures, 38 files and 24 megabytes of them, and what the installed folder has more is what
the installer wrote: `RESTART.DAT`, and `LEGEND.INI`, which says where the rest is:

```
MODE=VGA
MOUSE=MOUSE
GAMEDATA=D:\GW2\
SAVEDATA=C:\GATEWAY2\
```

So everything but the pictures can be read from the game's folder alone, and the pictures
are on the disc and nowhere else. The folder has no recorded sounds, and neither has the
disc's.

The disc has the six games before this one too, in `S101`, `S201`, `S301`, `TQ`, `GW` and
`ERIC`; *Companions of Xanth* in `XANTH`; and `DGATE`, `MISSION`, `SHANNARA` and `HOBOKEN`,
which by their sizes and what is in them are demos of later games.

## How the program is built

As *Gateway* and *Eric the Unready* are: a program for the player to run, the game, its
overlays, a run-time library the two programs share, and the linker's startup code.

| File | What it is |
|---|---|
| `HOME.EXE` | What the player runs |
| `HOMEMAIN.EXE` | The game |
| `HOMEMAIN.OVL` | Its overlays, 341,318 bytes |
| `HOME.RTL` | The run-time library |
| `RTLINKST.COM` | The linker's startup code, 11,031 bytes where it was 7,615 |

| What | How many |
|---|---|
| Segments | 274 |
| Records in the program | 54 |
| Records in the library | 75 |
| Relocations in the program | 5,773 |
| Overlays | 34, one of them empty |
| Thunks | 1,003 |

The program starts at `1F61:0835`, in the overlay manager's segment, and its memory ends
at `56A0`. The overlays load at three places, each past the biggest overlay of the one
before:

| Place | Overlays |
|---|---|
| `3246` | 9 |
| `34AD` | 5 |
| `372D` | 20 |

Every one of the 1,003 thunks goes into the overlay it names.

### The linker is a newer one

The linker is .RTLink/Plus 6.10, where the three games before were linked by 5.02: the
header's version word, at `34`, is `0262`. Its file is 5.02's in every way but one.

**It has a second table of symbols.** The header has a table of symbols at `10`, eight
bytes each, by which the library's fixups name things in the program. 6.10 has another
after it, at the address in the header's `4A`, with how many at `4C`: one symbol here, in
the same form. A fixup says what its numbers are of, a segment, a group, a symbol or a
segment's value, by a number from 0 to 3; 6.10 has a fifth, 4, for a symbol of the second
table.

One fixup in the whole library uses it. It is in the run-time's startup code, a far call
with 3 as its argument, made, by the code around it, as the program ends in an error; and
what it calls is in the same segment at `0634`. Why that one call is named another way isn't known: what was
needed was to know where it goes, and the table says.

The records, the other fixups, the manager's table and the thunks are read by what reads
5.02's, with nothing changed.

## What the tools read as it is

| What | How much |
|---|---|
| The program and its overlays | all of it |
| The vocabulary | 2,028 words |
| The text | 4,875 strings, [with one change](#the-text) |
| The music | 82 songs in one file, 41 of them for the AdLib |
| The fonts | 17 |
| The regions | 9 files |

And what they don't yet:

- **The grammar, the objects and the timers.** The code the tools look for, to find each
  table, isn't as it is in the games before: [the engine was built
  again](#the-engine-was-built-again).
- **The pictures**, which are on the disc, and whose files are named another way:
  `EP_LAND.PIC` and `HT_DOCK4.PIC`, where the earlier games had `GATE_301.PIC`.

## The text

`HOMESTR.DAT` is 32 blocks of 4,875 strings, 628,562 bytes of text packed into 313,989,
with a tree of 216 nodes and 128 words: the commonest are `After`, `Fogram`, `Gateway`,
`He`, `Heechee`.

It is kept as [*Spellcasting 301*'s](spellcasting-301.md) is, a word for every string
saying how many bytes it is packed into, with one thing changed. **A block's size is four
bytes**, where it was two, so its entry in the table that starts the file is six bytes and
not four: how many strings, a word, and the size. No block needs it: the biggest is 56,371
bytes, under the 65,535 a word holds. The game before, *Eric the Unready*, still has two.

Read that way the file comes out whole: each block's strings add up to its size, the
blocks end where the file does, and all 4,875 strings unpack to plain text.

### How a string is named

As from *Spellcasting 301*: by a far pointer whose segment is `F000`, and whose offset is
the block times 1,024 and the string's place in it. The routine most are handed to, which
prints, is at `18A2:11FC`. There are 5,054 places that name a string, and 4,785 of the
4,875 strings are named by one. Block 1 is named by none: it is the help, the keys and what the mouse
does, shown by its block as *Eric the Unready*'s is.

## The engine was built again

The games before were found by the same few instructions in each: the ones that read the
table of objects, compare a number with their count, and look a type up. None of them is
in this game as it was. The routines are the same routines, in the same order, but they
were compiled by a later compiler and with the data kept another way:

- **A register is cleared with `xor`**, where the compiler before wrote `sub`.
- **The data segment's variables are read as they are**, `mov ax,[3336]`, where every game
  before loaded a segment into `ES` first even for those. So the count of objects is a
  plain word of the data segment, at `4B4C:3336`.
- **A table's entry is found with `BX`**, where it was `SI`.

Set beside *Eric the Unready*'s, 1,177 of the root's first 2,600 instructions are the same
but for their addresses, where that game and *Spellcasting 301* had 2,519. The routine that
performs an action is at `0000:0184`, where it was at `01A4` in the three games before.

## The objects, so far

The table is found, and it is kept as before: six bytes an object, its type and a far
pointer to its record, in segment `27D7` from `00BE`. There are 521 objects.

**The records are a new design.** There are seven types, where there were eight, and
their sizes are none of the earlier games':

| Type | How many | Bytes | Places | Lists |
|---|---|---|---|---|
| 1 | 123 | 22 | 0 | 1 |
| 2 | 210 | 28 | 1 | 1 |
| 3 | 79 | 32 | 1 | 2 |
| 4 | 5 | 12 | 0 | 0 |
| 5 | 7 | 18 | 0 | 2 |
| 6 | 73 | 18 | 0 | 1 |
| 7 | 24 | 34 | 1 | 3 |

A record still starts with its two names, as places in `OBJECT.DAT`, and its flags. After
that the fields are in another order: by the records themselves, the number of an object's
routine and where its nouns and adjectives start come last in most types, where the words
came straight after the flags. Type 1 is the rooms, 123 of them, and type 7 has the
player, who is object 1.

What each field is, for each type, hasn't been worked out, so the tools find the table and
can't yet list the objects. The grammar and the timers are found by code of the same old
kind, and aren't found yet either.

## Still to do

- Each of those.
- The game hasn't been run on the PC, which will need the disc as a drive for its pictures.
