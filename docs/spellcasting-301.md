# Spellcasting 301: Spring Break

Legend Entertainment, 1992. The third *Spellcasting*, by Steve Meretzky.

This is what the first look found: how the program is built and loaded, its vocabulary, its
grammar, its objects and flags, which routine is whose, its timers, its text, its pictures,
and where a command is parsed and performed. What is
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
earlier games do.

### Two more kinds of routine

An object can have two routines beside its own. Each kind is in a table of six bytes an
entry, an object's number and a far pointer, which a routine in segment `067E` goes down
looking for the object.

**Its own text**, 46 objects, at `2E2A:25CC`. `067E:00B0` is given the object and what is
wanted, and the object's routine answers with a far pointer to the text, or nothing:

| Asked for | What |
|---|---|
| 16 | its name |
| 17 | its second name |
| 18 | its line in a room's description |
| 19 | its line there before it has been touched |

The routines that print an object's name ask first, and use the name in its record when
they get nothing; so an object whose name changes has no name there at all, and six of the
46 have none. The routine that describes a room asks for 19 when the object doesn't have
the flag for touched, and then for 18. The beer keg answers 19 with `Here's the keg you
threw off the flying carpet!`, and 18 only in one room. They are Infocom's first description
and long description, as routines.

**Which object is meant**, 139 entries for 138 objects, in 42 routines, at `2E2A:228A`.
When the words of a phrase fit more than one object, the routine that finds them, in overlay
2, calls `067E:0065` with each object found, the phrase, and the list of what was found. An
object's routine can answer with the object the phrase is to mean, and the list is cut to
that one. The coin's looks for another coin in the list and picks between them by the
action being performed. It is Infocom's `GENERIC`.

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

### What each timer does

A timer's routine mostly prints, and since [a string is named where it's
used](#how-the-program-names-a-string), what each prints can be read beside it. These names
are from that.

| Timer | What it is | Timer | What it is |
|---|---|---|---|
| 1 | sleepiness | 34 | the magic mushrooms come out |
| 2 | two words forgotten | 35 | the mushrooms take hold, turn by turn |
| 3 | a byte forgotten | 36 | the tan fades |
| 4 | what was raised settles back | 37 | Moe's turn |
| 5 | you open your eyes | 38 | Vince's turn |
| 6 | a count down by sixes | 39 | Ralph's turn |
| 7 | the carpet is loaded, turn by turn | 40 | Ollie's turn |
| 8 | the carpet flies east, turn by turn | 41 | Izzie's turn |
| 9 | the carpet spirals down | 42 | Fred's turn |
| 10 | the world's most beautiful woman passes | 43 | the Pharts come to the Dance Club |
| 11 | Fred fetches you to the lobby | 44 | the building shakes |
| 12 | Sid takes you to the beach, turn by turn | 45 | the baby squid is found |
| 13 | Moe calls it a day | 46 | the hunger pains |
| 14 | Gary says the bash is near | 47 | the baby squid whimpers, every turn |
| 15 | the bimbos follow the studfinder | 48 | Joey shoots at your feet |
| 16 | The Judge passes through the lobby | 49 | the dog digs under the fence |
| 17 | The Judge comes to the party | 50 | Ollie says Fred has passed out |
| 18 | the cell's bars rust away | 51 | Sid visits the cell |
| 19 | the studfinder hums | 52 | a count down by fours |
| 20 | The Judge judges the sand castles | 53 | the mama bull returns |
| 21 | The Judge's event at the bandstand, turn by turn | 54 | the carpet sputters |
| 22 | the mouse race | 55 | your hostess in Room 1812 loses patience |
| 23 | the event at the pool, turn by turn | 56 | Snorty's nose clears |
| 24 | everyone comes into the bar, turn by turn | 57 | Snorty pops the inflatable cow |
| 25 | the Sheriff jails you | 58 | the crowds swarm to Blather Beach |
| 26 | BIGFINNO wears off | 59 | your order is called |
| 27 | the Soda-Barfing Contest, turn by turn | 60 | Moe says to get a move on |
| 28 | the drawing of straws, turn by turn | 61 | the human pyramid comes apart |
| 29 | the laxative works | 62 | the Copy Nymph flies in |
| 30 | the Sheriff jails you, from the arena | 63 | the moby berry shrinks |
| 31 | the volleyball face-off, turn by turn | 64 | the berry plant shrivels |
| 32 | the Yus swagger onto the beach | 65 | you feel filled with vigor |
| 33 | the showdown on the boulevard, turn by turn |  |  |

Timers 2 and 3 clear variables and print nothing, as the timers called `a word forgotten`
do in the games before; 6 and 52 count a byte down and start themselves again. Timers 37 to
42 are six of one kind, each handing one of your fraternity brothers to a routine they
share, and what that does hasn't been read. Timers 25 and 30 print nothing that was found
and are named from what they move: you, to the cell, with the Sheriff.

Much of the game is its contests, and each is one timer that runs every turn and steps
through the event: the sand castles, the soda-barfing, the volleyball, the drawing of
straws.

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

Nearly every string is named by a constant, though half don't look it. Where a routine
prints one of several strings, the compiler loaded each number where it was chosen and
jumped to the one place that does the rest:

```
37b7:1bd5  mov ax, 0x429  ; string 1.41: %s to be locked.\n
37b7:1bd8  jmp 0x1b90
  ...
37b7:1b90  mov dx, 0xf000
37b7:1b93  push dx
37b7:1b94  push ax
```

So the number and its `F000` can be far apart, and a search for the two together finds only
the strings that are a routine's one message. Following the jump finds the rest. The tools
take a string to be named where its number is loaded and `F000` is given at once or where a
jump from there lands; where the two are put by as a far pointer, in a routine's variables
or in the data; and where a far pointer of the kind is in the program's data as it starts.

| How | Places |
|---|---|
| Handed at once to the routine that prints, `1D3A:10C6` | 4,194 |
| Made in code for something else | 386 |
| Kept as data | 231 |

That is 4,811 places, and 4,737 of the file's 4,777 strings. The 40 left are a few the
tools miss, where the segment is given before the number, and some nothing names at all:
notes to the game's testers, `Welcome to the Gamma test version of S301!`, and messages for
faults, `Bug: set_spell_verb`.

## The pictures

649 of them, in 17 files, and kept a third way. The directory is still twelve bytes to a
picture, with the same things in the same places. The byte that says how a picture is kept
is read anew, and the pixels are packed by a method none of the earlier games used.

### The byte

| Bits | What |
|---|---|
| The low four | the display the picture is for: 1 the EGA, 2 the VGA in 16 colors, 3 the VGA in 256, 4 not known |
| `10` | it starts with a palette |
| `20` or `40` | its pixels follow, imploded |

Every picture has `20` or `40`, and both are packed the same way. Most of the `40`s are small
pieces drawn at a place; what the bit says isn't known.

The files go by display too. `S301_100.PIC` and `101` are the EGA's, `102` to `109` and
`200` and `201` the VGA's in 16 colors, `300` to `302` the VGA's in 256, and `400` and
`401` are the fourth display's, in 16 colors.

### The palette

For the EGA it is what the earlier games had, 16 bytes, each one of the EGA's 64 colors by
number. For the VGA it is three bytes a color, red, green and blue from 0 to 63, as the
VGA's own registers take them: 48 bytes for 16 colors, 768 for 256.

A picture without one is still a piece of the picture before it that has one, in its
colors, as in [*Timequest*](timequest.md#the-pictures).

### The pixels

They are packed by **PKWARE's implode**, the method of its Data Compression Library, which
the game has linked in: the routine that unpacks is at `154A:0CA6`, and takes two routines
of the game's, one to read more of the file and one to take what's unpacked. The file is
read 2,048 bytes at a time.

Imploded data starts with two bytes: 0, for bytes kept as they are and not by a code, and
6, the size of the window as a power of two, 4,096 bytes. Then bits, from each byte's low
bit up:

- a 0 bit, and a byte as it is;
- or a 1 bit, a length, and a distance back, for that many bytes copied from that far back
  in what has been unpacked. The length is a Huffman code and up to eight bits more. The
  distance is a Huffman code for its high bits, and six bits as they are for its low ones,
  or two when the length is 2.

A length of 519 ends it. The two Huffman codes are the method's own, the same for every
file, and aren't kept in it.

Unpacked, the pixels are rows, each starting on a byte:

| Colors | A byte is |
|---|---|
| 256 | one pixel |
| 16 | two, the left one in its low half |
| 2 | eight, the left one in its top bit |

The tools take which it is from how many bytes there are, since one picture for the EGA has
a 0 for its display. Every one of the 649 unpacks to exactly the bytes its width and height
need, and so do all 1,504 of *Gateway*'s, which are kept the same way. Those looked at are
right: a chapter's title in 256 colors, a room in 16 with its pieces in its colors, and
*Gateway*'s titles for the EGA and the VGA.

The `.FNT` files beside them are, by their names, the game's fonts, and aren't read.

## A saved game

`RESTART.DAT` is the game's state as it starts, 15,866 bytes, and a saved game is the same
thing. It is [*Spellcasting 201*'s](spellcasting-201.md#a-saved-game) with one part more:

| Bytes | What |
|---|---|
| 34 | what starts it, all zeros in `RESTART.DAT` |
| 15,200 | every object's record, in the table's order |
| 622 | 165 saved variables, each a stretch of the program's data |
| 8 | four words more |
| 2 | how many timers are going: none, in `RESTART.DAT` |
| 4 for each | the timers |

The parts add up to the file exactly.

The saved variables are listed in a table at `284D:022C`, six bytes each, where a variable
is and how long, with their count at `4934:392E`. The four words are at `3E77:4BFA` to
`4C00`, beside the parser's own variables; what they are isn't known. For two of the kinds
of saving the routine can be asked for, it fills them from four words of the data first.

The routine that saves is in overlay 4, at `350B:042C`, where the earlier games had it in
the root, which is all that kept the tools from finding it.

## Where a command is parsed and performed

The steps are the earlier games', and the places the player watches are these:

| What | Where |
|---|---|
| The routine that performs an action, given the action and its two objects | `0000:01A4` |
| How many are being performed, one inside another | `4934:3812` |
| The action, a byte, and the direction after it | `3E77:ABB6`, `ABB7` |
| Its object and its second object | `3E77:059C`, `3E77:059A` |
| The room, the actor, the turns taken, and the clock | `4934:388C`, `388E`, `3890`, `3892` |
| Where the main loop's `setjmp` returns | `0000:0847` |
| A token taken: the next, after a full stop, after `and` | `350B:236B`, `23FC`, `247A`, in overlay 2 |
| The routine that takes a word off the line | `350B:01F4`, in overlay 2 too |
| The word a token is | `3E77:4BF8` |
| The form found, which one, and the action it makes | `0000:0DE5`, `3E77:4C04`, `3E77:023E` |
| The objects found for a lot of phrases | `0166:0A63` |
| The two lots, twelve bytes each, and how many | `3E77:0226`, `3E77:0220` |

The ten places where a routine asked about an action has answered are at `0000:0370`,
`03A2`, `03DA`, `03F9`, `0416`, `044F`, `046E`, `04A4`, `04C0` and `04DA`. Going somewhere
is action 236. The clock is in steps of five minutes, as in the other *Spellcasting* games.

Two things are placed differently here. The objects a phrase could be are found from a
segment of their own, `0166`, and not from the main loop's. And the routine that gathers a
command's tokens is in an overlay with the routine that takes each word off the line, where
*Spellcasting 201* had the one in an overlay and the other in the root.

## The game as it runs

The game starts and plays on [the PC](the-pc.md), with its pictures and music, and the player's `--watch` and
`--panel` follow it as they do the other three.

```
world: 1:10 pm, turn 0, in Beach (1)
world:   here: spell book (368) [BLERK spell (374)], you (255) [coin (86), eyeglasses (89), bathing suit (87)]
world:   exits: none
world:   timer 10 (the world's most beautiful woman passes) started: 1 turn to go
world:   words: take [verb] off [preposition] glasses [noun] then
world:   form 377: take off OBJECT, which is action 57
world:   object "glasses", looked for held: eyeglasses (89)
world:   action 57: doff eyeglasses (89)
world:     answered by eyeglasses (89), asked as the object: 1
world: 1:15 pm, turn 1, in Beach (1)
```

Watching it found one thing about every game. In a phrase of a command, the count of its
adjectives is a byte, with the byte after it left as it was. The earlier games happen to
leave a zero there, and the count had been read as a word; this one doesn't.

`Legendary.Core` reads the newer linker's programs, the vocabulary whose size is of its
packed text, and this game's world.

## Still to do

- What display 4 is, and what the `40` in a picture's kind says.
- The fonts.
- What starts a saved game, and the four words before its timers.
- Twelve flags aren't named, and timers 37 to 42 are named only by whose they are.
- The four bytes a thing's record has more than *Spellcasting 201*'s.

The parser's own grammar is read, and is the first game's 46 ways. The music and the sounds
are read as they are.

The game hasn't been played through.
