# Gateway

Legend Entertainment, 1992. Frederik Pohl's *Gateway*, made a game by Glen Dahlgren and Mike
Verdu, on the system of [*Spellcasting 301*](spellcasting-301.md), which came out the same
year.

This is the first look, and so far it is how the program is built and what of it the tools
already read. What is known for certain is said plainly; what is inferred says so. Addresses
are `segment:offset` in hex, with segments relative to where the program loads. Every one of
them can be looked at with the [tools](../tools/README.md).

## How the program is built

As *Spellcasting 301* is: Microsoft C, linked by .RTLink/Plus 5.02, with a run-time library
and overlays.

| File | What it is |
|---|---|
| `GATE.EXE` | What the player runs |
| `GATEMAIN.EXE` | The game |
| `GATEMAIN.OVL` | Its overlays, 498,115 bytes |
| `GATE.RTL` | The run-time library the two programs share |
| `RTLINKST.COM` | The linker's startup code |

| What | How many |
|---|---|
| Segments | 283 |
| Groups | 2: the data's at `4C0B:0000`, and the overlay manager's at `1CF4:0000` |
| Records in the program | 56 |
| Records in the library | 64 |
| Overlays | 58 |
| Thunks | 1,284 |

The program starts at `1CF4:076C`, in the overlay manager's segment.

### The overlay manager

It is *Spellcasting 301*'s, with its table in another place again. The three words that say
where the table is are found the same way: the count is at `1CF4:0C19` and the entries
start at `1CF4:0C1F`, six bytes on. That game's are eight bytes on and *Spellcasting 201*'s
four, so what the manager keeps between the count and the first entry differs from one
build of it to the next, and the tools take any of the three.

An entry is the same 18 bytes, and a thunk the same ten. The thunks come after the
manager's segment, from `1CF4:12C7`, and every one goes into the overlay it names.

The 58 overlays load at six places:

| Place | Overlays |
|---|---|
| `33DF` | 5 |
| `3727` | 4 |
| `3A56` | 35 |
| `3A65` | 5 |
| `3A77` | 7 |
| `3A9F` | 2 |

The last four are close together, 15, 18 and 40 paragraphs apart, where the earlier games'
places were each past the biggest overlay of the one before. So an overlay at `3A56`
would lie over the three places after it, and how the manager keeps them apart, or which
can be in memory together, hasn't been worked out.

`GATEMAIN.OVL` is laid out as before, and is 13 bytes short of where the last overlay's size
would end it.

## What is already read

These are kept as *Spellcasting 301* keeps them, and are read by what reads that game:

- **The vocabulary**: 2,644 words.
- **The text**: 56 blocks and 6,013 strings, 663,324 bytes packed into 335,613.
- **The grammar**: 402 forms that make 195 actions, with the actions' routines in a table
  and 18 of them with a routine that comes first.
- **The parser's own grammar**, the same 46 ways.
- **The pictures**: [imploded](spellcasting-301.md#the-pictures), 1,519 of them, 15 of no
  size.
- **The saved game**: 34 bytes that start it, 20,650 in the records of 734 objects, 1,079 in
  261 saved variables, and a count of timers, which come to `RESTART.DAT`'s 21,765 bytes
  exactly. It has no four words before the count, as *Spellcasting 301*'s has.
- **The music, the sounds, and the pictures' regions.**

## Still to do

- **The objects.** The table is found, 734 objects in eight types, and the records' sizes are
  no known game's: a room's is 26 bytes and a person's 32.
- **The objects' routines**, and the flags and timers, which are told by whose routine a call
  is in: the code that calls an object's routine isn't found.
- **How the program names a string.** Only 140 of the 6,013 strings are named as
  *Spellcasting 301* names them, and 30 of the 56 blocks aren't named at all.
- The game hasn't been run on the PC.
