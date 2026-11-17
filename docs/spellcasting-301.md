# Spellcasting 301: Spring Break

Legend Entertainment, 1992. The third *Spellcasting*, by Steve Meretzky.

This is the first look, and so far it is only how the program is built and loaded. What is
known for certain is said plainly; what is inferred says so. Addresses are `segment:offset`
in hex, with segments relative to where the program loads. Every one of them can be looked
at with the [tools](../tools/README.md).

## How the program is built

As [*Spellcasting 201*](spellcasting-201.md#how-the-program-is-built) is: Microsoft C, linked
by **.RTLink/Plus**, a year newer. Its header has `01F6` for the version, which is 502 and
so, by the same reading, 5.02.

| File | What it is |
|---|---|
| `S301.EXE` | What the player runs: the title, and then the game in its place |
| `S301MAIN.EXE` | The game |
| `S301MAIN.OVL` | Its overlays |
| `S301.RTL` | The run-time library the two programs share |
| `RTLINKST.COM` | The linker's startup code, which loads them |

### The file is the older version's

Nothing in how the program is kept changed. The header is the same, field for field; the
records are the same; and every fixup's flags are bits the older version's reader already
knew. Read as a 4.10 program, with nothing but the version let through:

| What | How many |
|---|---|
| Segments | 228 |
| Groups | 2: the data's at `4934:0000`, and the overlay manager's at `232A:0000` |
| Symbols | 33 |
| Records in the program | 51 |
| Records in the library | 67 |
| Plain relocations | 6,725 |

The program starts at `232A:0956`, with its stack at `555C:2000`.

This was checked against the game itself. Run on [the PC](the-pc.md), the game was stopped
at its first instruction, after the startup code had done all its loading and before the
program had changed anything. Every byte of every record, 136,552 of the program's and
104,238 of the library's, is in memory as the tools put it.

### The overlay manager is not

What changed is the manager, which is the program's own code and not the linker's format.

- **Its table is found the same way**, by the three words that say where the count is,
  where the entries start and an entry's size. The count is at `232A:0E69` and the entries
  start at `232A:0E71`, eight bytes on, where the older one's were four. An entry is the
  same 18 bytes.
- **The thunks are no longer in its segment.** They come after it, from `232A:138D` to
  `232A:389F`, in segments that belong to the manager's group, and are still called through
  its segment. A thunk is the same ten bytes.
- **It has its own messages now**, numbered `eov0001` and up: `Cannot find overlay file`.

There are 36 overlays, in three areas, and 949 thunks:

| Area | Overlays | The biggest |
|---|---|---|
| `350B` | 1 to 5 | 10,944 bytes |
| `37B7` | 6 to 13 | 12,832 bytes |
| `3AD9` | 14 to 36 | 14,816 bytes |

Overlay 10 is empty. `S301MAIN.OVL` is laid out as before: each overlay's relocations, and
then its contents on a paragraph, and the file is eight bytes short of where the last one's
size would end it.

## Still to do

Everything else. The tools that read the program now load it; the ones that need its words
or its text stop at those:

- `VOCAB.DAT` is packed another way again: the reader of *Spellcasting 201*'s finds its
  words don't end where the file does.
- `S301STR.DAT`'s table of blocks doesn't end where the file does either.
- The pictures are [a third kind](timequest.md#the-pictures), many of them 320 by 200.
- The objects, flags, grammar, routines and timers wait on the words.

The parser's own grammar is read, and is the first game's 46 ways. The music and the sounds
are read as they are.

The game starts on the PC and runs on into the game itself. It hasn't been played.
