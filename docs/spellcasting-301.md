# Spellcasting 301: Spring Break

Legend Entertainment, 1992. The third *Spellcasting*, by Steve Meretzky.

This is the first look, and so far it is how the program is built and loaded, its
vocabulary, its grammar, its objects, which routine is whose, its timers and its text. What is
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

### What the flags are

51 flags, each with a bit and the types that have it, in the table at `2BDB:0018`. The names
are from the code that asks about each, which the tools list, or from what has it.

| Flag | What it is | From |
|---|---|---|
| 1 | its name takes no article | what has it |
| 2 | its name takes `an` | what has it |
| 3 | plural | what has it |
| 4 | takes `some` | what has it |
| 5 | seen by the player | set 346 times, by nearly everything that describes |
| 6 | touched; a place, visited | `open` sets it |
| 7 | taking it is special | the routine before `get` asks it |
| 8 | not listed among a room's contents | `get` clears it |
| 9 | gives light | 79 of the 84 places; `light` sets it and `blow out` clears it |
| 10 | an animal | what has it: a bull, a dog, a seagull; `greet` asks |
| 11 | can't be opened or closed | `open` and `close` |
| 12 | a door | the three doors |
| 14 | can be read | `examine`; what has it |
| 15 | locked | `open` says it appears to be locked; `unlock` asks it |
| 16 | open | `open` sets it, and says `already open` |
| 17 | things can be on it | what has it; `empty` and `climb off` ask |
| 18 | a container | `open`, `fill` |
| 19 | can be taken | the routine before `get` asks it |
| 20 | a place under water | what has it: Harbor Bottom, Sunken Ship; `swim` asks |
| 23 | lit | `light` sets it, `blow out` clears it |
| 24 | can be seen through | what has it: the trophy case, the lobster trap |
| 26 | can be got in or on | `climb in` |
| 27 | burns | `burn` |
| 30 | can be worn | `wear` |
| 31 | a key | `open` and `unlock` ask it of what's used |
| 32, 33 | cuts | `cut` asks both |
| 35 | can be eaten | `eat` |
| 37 | magic: a spell, its box, or the spell book | what has it |
| 38 | can be lit | `light` and `blow out` |
| 39 | a bed | `make` |
| 41 | a tube of paint | what has it |
| 45 | a seahorse | what has it |
| 46 | outdoors | 35 places; `tan` and `look up` ask |
| 47 | someone other than you | everyone has it but you |
| 48 | female | what has it |

They are the same things as *Spellcasting 201*'s flags, in another order: `open` was 13
there and `locked` 14. The trophy case, as the game starts, is locked, a container, and can
be seen through.

Flags 20, 24 and 17 are named from what has them more than from a test that settles it. Not
named: 13, which the Crawl Space alone has; 22, 25, 34, 42 and 43, which nothing starts
with and the game sets and clears; 28 and 29, of places; 36, 40 and 44, of one or a few
things each; and 50, which the banker has. Flags 21, 49 and 51 are in the table and nothing
has them or asks about them.

## Which routine is whose

The actions' routines are in a table too. The earlier games had a `switch` on the action,
found by its table of jumps. Here segment `2E2A`, the one the grammar's forms are in, starts
with the routines:

| At | What |
|---|---|
| `2E2A:0000` | 20 far pointers: the routines that come before an action's own |
| `2E2A:0050` | 249 entries of six bytes, one for each action |
| `2E2A:0626` | the grammar's forms |

An action's entry is a byte of flags, a byte that is the number of the routine that comes
before its own, or 0, and a far pointer to its own. Two routines in segment `067E` use it:
`067E:0002` calls the one before an action's, and `067E:0040` the action's.

The byte of flags is the one the earlier games kept apart, before the forms, a byte an
action: its bit 0 is still that the action takes no turn, and the 30 that have it are the
ones that would, `inventory`, `look`, `quit`, `restart`. The other bits aren't the earlier
games': the four ways of asking have 20 here, and had 08.

So every routine is named: 249 actions, 61 of them with one of the 20 that come first, and
[507 objects](#the-objects). All but one of the actions' routines are in overlays 6 to 9.

The routine that performs an action is at `0000:01A4`, and asks them in the order the
earlier games do. Segment `067E` has two more tables it reads the same way, each a word and
a far pointer: 139 entries at `2E2A:228A` and 46 at `2E2A:25CC`. What they are isn't known
yet.

With the routines named, the tool that finds every test of a flag works: 1,243 calls about
a flag are read, and 146 more aren't.

## The timers

65 of them, numbered, and kept as
[*Spellcasting 201*'s](spellcasting-201.md#the-timers) are: a table of sixteen, four bytes
each, a timer's number in a byte and its count in a word two bytes on, and a second table
with a far pointer to each number's routine.

| What | Where |
|---|---|
| The table of timers going | `3E77:4D68` |
| How many are in it | `4934:3852` |
| Which one is being run | `4934:3854` |
| The byte that makes the next run skip the counting | `4934:3850` |
| The routines, 65 far pointers | `3113:0000` |
| The routine that calls a timer's routine by its number | `09C9:0004` |

The routines that keep the table are in segment `02D3`, the same six in the same order:

| Routine | What it does |
|---|---|
| `02D3:000E` | empties the table |
| `02D3:002C` | starts a timer, given its number and a count |
| `02D3:009E` | stops one |
| `02D3:010B` | says whether one is going |
| `02D3:0143` | gives one's count |
| `02D3:0180` | runs them, after a turn |

Starting a seventeenth says `[ERROR: Queue overflow]`.

62 of the 65 are started, stopped or asked about by number, in 305 calls; 15 more calls give
a number that is worked out. 21 calls start one to run every turn. All but six of the
routines are in overlays. Timers 20, 32 and 51 aren't named by any call.

## The text

`S301STR.DAT` has 37 blocks and 4,777 strings: 512,066 bytes of text packed into 269,161. *Spellcasting 201*'s file had 1,815 strings, 292,504 bytes of text, and left most of the game's
text in its overlays.

The packing is that game's: a Huffman tree in the form its vocabulary's has, 217 nodes, with
128 common words that a code can stand for. What changed is how a string is found.

| The file | What |
|---|---|
| A word | how many blocks: 37 |
| Four bytes for each block | how many strings it has, and how many bytes it is packed into |
| A word for every string in the file | how many bytes the string is packed into |
| The tree | a count of words, and the words in pairs |
| The words | a count, where each starts, and each with a zero |
| The blocks | each string's codes, one string after another |

The older file had six bytes for a block, with its size unpacked, and nothing for a string:
strings ran on from one to the next, a bit after the last, and the tenth was reached by
unpacking the nine before it. Here every string starts on a byte and the file says how long
each is, so one can be gone to directly. The lengths of a block's strings add up to the
block's size, for every block, which is how the form is known to be read right.

*Gateway*'s text is kept the same way, and is read: 6,013 strings.

### How the program names a string

*Spellcasting 201* had two routines that took a string's number, one to give its text and
one to print it. Here there are none. A string is named as a far pointer, wherever a far
pointer to text could be, with `F000` for its segment and the string's number for its
offset:

```
3ad9:01ef  mov ax, 0x240b  ; string 9.11: \tYou suddenly feel filled with vigor.\n
3ad9:01f2  mov dx, 0xf000
3ad9:01f5  push dx
3ad9:01f6  push ax
3ad9:01f7  lcall 0x1d3a, 0x10c6
```

No text is ever at segment `F000`, which is the PC's ROM, so the pointer can't be mistaken
for a real one. The number is the block times 1,024, and the string's place in the block: a
block can have more than 256 strings now, and one has 284, so the eight bits the older
number gave the place aren't enough.

The routine at `03F8:05B7` makes a pointer of either kind into text. One whose segment isn't
`F000` is given back as it is. For one that is, it looks in a list of the strings it has
unpacked lately, six bytes each at `280A:032E`, and unpacks the string from the file if it
isn't there. So every routine that takes text takes a string's number as well, with no code
of its own for it, and the game's writers could move a string out of the program and into
the file by changing only how it was named.

2,473 places in the program name a string this way, 2,424 different strings of the 4,777.
The routine at `1D3A:10C6`, which prints, is handed 2,348 of them at once. The other strings
are reached some other way, by a number that is worked out or from tables in the data,
which the tools don't follow.

## Still to do

The tools that read the program load it, and its words and grammar are read. The rest stop
where something is kept a new way:

- Twelve flags aren't named.
- The timers have no names.
- The saved game's layout: the code that saves isn't found.
- The strings that aren't named by a constant: half of them.
- The pictures are [a third kind](timequest.md#the-pictures), many of them 320 by 200.

The parser's own grammar is read, and is the first game's 46 ways. The music and the sounds
are read as they are.

The game starts on the PC and runs on into the game itself. It hasn't been played.
