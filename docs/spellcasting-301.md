# Spellcasting 301: Spring Break

Legend Entertainment, 1992. The third *Spellcasting*, by Steve Meretzky.

This is the first look, and so far it is how the program is built and loaded, its
vocabulary, its grammar and its objects. What is
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

## The vocabulary

`VOCAB.DAT` is [*Spellcasting 201*'s](spellcasting-201.md#the-vocabulary) with one word
meaning something else: a tree, the words' text packed by Huffman's codes, a table of the
words in alphabetical order, and the synonyms.

The word before the text was the text's size there, unpacked. Here it is how many bytes the
text is packed into: 6,959, for 12,845 bytes of text. So the reader of the older file stopped
partway through the words and found nothing where it looked for the table. The tools take
the word either way, whichever leaves the rest of the file ending where the file does.

| What | How many |
|---|---|
| Words | 1,801 |
| The tree | 70 words, so 35 pairs |
| Synonyms | 303 |

*Gateway*'s vocabulary is kept the same way, and is read: 2,644 words.

## The grammar

458 forms that make 249 actions, in *Spellcasting 201*'s wider table. It is found and read
as that game's is, with nothing changed.

## The objects

517 of them, in the eight types: 84 rooms, 205 things, 46 people, 3 doors, and the rest
scenery and words. They are kept as
[*Spellcasting 201*'s](spellcasting-201.md#the-objects) are, with their names in
`OBJECT.DAT`, their flags by a table, and every room's exits in one.

| What | Where |
|---|---|
| The table of types, three bytes each | `2BDB:0000` |
| The table of flags, four bytes each: 51 flags | `2BDB:0018` |
| The table of objects, six bytes each | `2BDB:00E4` |
| How many objects | `4934:48B2` |
| The nouns, and the adjectives | `2BDB:0D02`, `2BDB:171E` |
| What's also in a room, and what's seen from it | `2BDB:1BEE`, `4934:48CC` |
| The exits, four bytes each: 332 rows | `2BDB:1F36` |
| The doors an exit goes by | `4934:48B4` |

A record is that game's with a word more at its end, and a thing's has four bytes more
besides:

| Type | Bytes | In *201* | The new word |
|---|---|---|---|
| 1, a room | 24 | 24 | none: it was already there, at `0E` |
| 2, a thing | 40 | 34 | `26` |
| 3 | 12 | 10 | `0A` |
| 4, 5 | 20 | 18 | `12` |
| 6, a door | 24 | 22 | `16` |
| 7, a person | 30 | 28 | `1C` |
| 8 | 20 | 18 | `12` |

Everything before it is where *Spellcasting 201* has it: a thing's place at `0E`, its
relation at `10`, the next at `12` and its three lists from `14`, and so on for the rest.

**The new word is the number of the object's routine.** The game before had a `switch`
for each type of object, with a case for each object that had a routine. Here there is a
table of routines, 458 far pointers at `3098:0000`, and each record says which is its own.
The routine at `067E:00F2` calls an object's: it has `0305:0E9F` read the number from the
record, whatever the type, and calls that entry of the table with the reason it's asking.

507 of the 517 objects have a routine, and the ten without have 0: of the six that are only
words, `sandwich`, `hotel room` and `sleep` have one and `number`, `love` and `sex` don't. A
room's number is at `0E`, in a word its record already had. In *Spellcasting 201* that word
is something else, and still isn't understood.

Two more things changed, both in how a list ends:

- **A list of words ends at a word with its top bit set**, not at a zero after it. The
  Beach's nouns are one word, `806A`: `beach`, and the end.
- **A room's rows of what's also there end the same way**, at a row whose object has its
  top bit set.

The exits are of three kinds: 191 to a room, 137 a numbered routine decides, and 4 by a door.

The code is compiled another way here, without the call that checks the stack at the start
of every routine, and that is all that kept the tools from finding these tables as they
find *Spellcasting 201*'s. The routines that read a record's fields are in segment `0305`,
and switch on the type as before. Four of them keep the flags: `0305:030F` finds a flag's
byte and bit from the table, `03A8` tests it, `03E2` sets it and `0415` clears it.

## Still to do

The tools that read the program load it, and its words and grammar are read. The rest stop
where something is kept a new way:

- The code that calls an action's routine isn't found, so the actions' routines aren't
  named, and the flags' uses, which are told by whose routine a call is in, wait on it.
- The flags have no names. The first of them look to be *Spellcasting 201*'s, with the same
  numbers.
- The routines that keep the timers aren't the ones that are known.
- The saved game's layout: the code that saves isn't found.
- `S301STR.DAT`'s table of blocks doesn't end where the file does.
- The pictures are [a third kind](timequest.md#the-pictures), many of them 320 by 200.

The parser's own grammar is read, and is the first game's 46 ways. The music and the sounds
are read as they are.

The game starts on the PC and runs on into the game itself. It hasn't been played.
