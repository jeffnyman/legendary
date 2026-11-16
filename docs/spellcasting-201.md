# Spellcasting 201: The Sorcerer's Appliance

Legend Entertainment, 1991. The second *Spellcasting*, by Steve Meretzky, on the system
[*Spellcasting 101*](spellcasting-101.md) was built on, a year on.

This is what the first look found, and so far it is how the program is built, its
vocabulary, its objects, its grammar, which routine is whose, its timers, its text and its music. What is
known for certain is said plainly; what is inferred says so. Addresses are `segment:offset` in
hex, with segments relative to where the program loads. Every one of them can be looked at
with the [tools](../tools/README.md).

## How the program is built

- **Microsoft C**, in the large model, as before: the same notice, `MS Run-Time Library -
  Copyright (c) 1988, Microsoft Corp`, starts the program's data at `4974:0008`.
- **.RTLink/Plus**, Pocket Soft's linker, in place of PLINK86. Its header has `019A` for the
  version, which is 410 and so, by the look of it, 4.10.

`S201.EXE` is still what the player runs, and it still replaces itself with `S201MAIN.EXE` by
having DOS load it as an overlay where its own code is. `S201MAIN.EXE` still says `Please use
S201.EXE to start Spellcasting 201.` when it's run by itself.

Three files are new, and all three are the linker's:

| File | What it is |
|---|---|
| `RTLINKST.COM` | .RTLink's startup code: what loads the program, in place of DOS |
| `S201.RTL` | A run-time library: code the launcher and the game share |
| `S201MAIN.OVL` | The overlays, as before |

### A program DOS can't load

The MZ header of `S201MAIN.EXE` has no relocations, and says the program starts at
`0037:0071`. What's there is a small stub. It finds `RTLINKST.COM`, reads all
of it into memory, and jumps into it. The startup code then does the loading itself: the
program's memory, by the time its first real instruction runs, is nothing like the file.

The image starts with a header of the linker's own:

| At | What |
|---|---|
| `00` | The program's first instruction, offset then segment: `2EF0:0491` |
| `04` | Its stack, offset then segment: `5613:2000` |
| `08` | Where the table of segments is, and at `0A` how many: 175 |
| `0C` | Where the table of groups is, and how many: 1, the data's, at `4974:0000` |
| `10` | Where the table of symbols is, and how many: 10 |
| `14` | Where the records start, and at `16` how many: 40 |
| `1C` | A run-time library: a 1, its name `S201`, and at `26` a stamp, `2826`, that the library's file has too |
| `34` | The linker's version, `019A` |
| `4E` | The library's extension, `RTL` |

A segment in the table is four bytes, where it starts: an offset from 0 to 15, then a segment.
A group is the same. A symbol is eight bytes: the letter `R`, a group's number or 0, a
segment's number, and an offset.

### Records, not an image

The program isn't kept as it sits in memory. It is kept as records, each a stretch of one
segment, and the startup code puts each where the table of segments says. So the file can
leave out everything that starts as zero, and the gaps between segments.

A record is made of numbers, and a number is one to four bytes. A first byte under `80` is the
number. Otherwise its top bits say how many bytes follow, most significant first: `10` one,
`110` two, `1110` three. So `A3 C2` is `23C2`.

| A record's part | What |
|---|---|
| A number | The segment, by its place in the table, from 1 |
| A number | How many bytes |
| A number | How far into the segment they go |
| A number | 1 when the bytes follow; 0 when the stretch is only cleared |
| The bytes | |
| A number | How many lists of fixups follow |

A list of fixups starts with a word of flags, low byte first. In the program itself there is
one kind, flags `1000`: a count, and that many places in the record, each a number. Every
place is a word that holds a segment, to which the startup code adds the segment the program
loaded at. That is exactly what a relocation in an MZ header is, kept by the segment it's in.
The program's 40 records have 5,608 of them.

The first record is segment 76, which the table puts at `0000:0000`: 9,154 bytes of code, then
one list of 300 places. The 40 records end at the file's last byte, which is how the reading
was checked.

### The library

`S201.RTL` is not a program. It starts with 18 bytes, among them `37BA`, which the startup
code looks for; the stamp `2826`; the count of its records, 58; and the linker's version
again. Its records start at byte 32, are of the same kind, and end at its last byte.

What's notable is where they go. A record in the library names a segment by the *program's*
table. The linker gave the library's code its places when it linked the program, among the
program's own segments, and left the bytes out. So the library fills 42 code segments between
`1408` and `25BD`, from the C run-time at `1408` on, and puts the first 10,692 bytes of the
data segment in place before the program's own data.

Because the library was made apart from the program, its fixups have to do what a linker
does, and they are the other kind: flags with bit 0 set. After the flags come two numbers, a
frame and a target, and then the places. The flags say what each number is of, what goes at
the place, and how:

| Bits | What |
|---|---|
| `0001` | Names a frame and a target; without it, a plain relocation |
| `0002` | The value is counted from the frame; without it, from the place itself, as a jump's is |
| `001C` | What goes there: 0 a byte, 1 an offset, 2 a segment, 3 an offset and a segment, 4 a high byte |
| `0020` | A number to add comes with the places |
| `01C0` | What the target's number is of: 0 a segment, 1 a group, 2 a symbol, 3 a segment's value itself |
| `0E00` | The same for the frame |
| `1000` | A count of places follows, and not one place |
| `4000` | The word there is shifted up 12 bits before the segment is added |

The library has 3,804 fixups. The symbols are, by where they are, how it reaches the program:
eight of the ten are places among the program's own records.

The launcher, `S201.EXE`, is linked the same way and uses the same library, which is why the
library is a file of its own: the code is on the disk once.

### Checked against the running game

The whole of this was read out of `RTLINKST.COM`, and then checked. *Spellcasting 201* runs on
[Legendary's PC](the-pc.md), and its memory there, once loaded, was compared with the memory
the tools build from the three files: every byte of every record of code is the same, in the
root and in the three overlays that were loaded at the time, and every one of 5,301 places
that hold a segment in code differs by exactly the segment the program loaded at.

### The overlays

The program starts at `2EF0:0491`, and segment `2EF0` is .RTLink's overlay manager, with its
table and its thunks. The table is at `2EF0:08E0`: 22 overlays, in three places.

| Place | Overlays | Size |
|---|---|---|
| `31E1` | 1 to 4 | 3K to 6K each |
| `3369` | 5 and 6 | 24K and 22K |
| `3958` | 7 to 22 | 11K to 25K each |

As before, overlays that share a place take each other's. The data is no longer an overlay:
it is in the program's records and the library's.

An entry is 18 bytes:

| Bytes | What |
|---|---|
| 2 | The segment it loads at |
| 2 | `0A80` in every one; not yet understood |
| 2 | Where it starts in `S201MAIN.OVL`, in paragraphs |
| 2 | Status, `0400` in the file. In memory, `0200` is set in the three that were loaded |
| 2 | Its size, in paragraphs |
| 2 | The count of relocations |
| 2 | `FFFF` |
| 2 | The number of the next, counting from 1 |
| 2 | Its size again |

The manager keeps, just before the table, where the table's count is, where its entries
start, and the size of an entry, one after another. That is how the tools find it. The count
is 23, one more than there are.

In `S201MAIN.OVL` an overlay is just what it was under PLINK86: its relocations, four bytes
each as an offset and a segment, then its contents on the next paragraph. The 22 fill the
file, each starting where the one before ends; the file stops 8 bytes short of the last one's
size, which is taken here to be zeros left off.

The thunks are at the end of the manager's segment, from `2EF0:0B80` to its last byte: 790 of
them, of 10 bytes each.

```
2ef0:0b80  call 0x424                ; the manager
2ef0:0b83  ljmp 0x3252:0x401         ; the routine
2ef0:0b88  dw 2                      ; the overlay, counting from 0
```

The number is one less than the overlay's here, where overlays are counted from 1 as
*Spellcasting 101*'s are. The call is a near one, since the thunks are in the manager's own
segment, and the number comes after the jump, not before it.

*Spellcasting 301* and *Gateway* have `01F6` for the linker's version, and *Gateway II* has
`0262`: by the same reading, 5.02 and 6.10. Their records haven't been read, and the tools say
so and stop.

## The vocabulary

`VOCAB.DAT` has 1,609 words, and is packed, where *Spellcasting 101*'s was plain text. It is
14,118 bytes; the same words in the old form would be 23,984. The file is four things, one
after another.

**A tree** of Huffman's codes: a count, 64, and that many words, taken in pairs, one for a 0
bit and one for a 1. A word from zero up is where in the tree the next pair is. A word below
zero is a character, kept as its complement, so that `FFFF` is the zero that ends a word and
`FF9E` is `a`. The last pair is the root. It is the tree of [the text's
packing](spellcasting-101.md#the-text) again, with two differences: there a pair was named by
its number and a character was negated.

**The words' text**: its size unpacked, 11,108, and then the codes, read from each byte's low
bit up. Each word ends in a zero. They aren't in alphabetical order any more. They are, by
the look of them, in the order the game's source first used them: the parser's own words,
`_integer`, `_string`, `a`, `again`, `all`; then `entrance`, `main`, `pi`, `frat`, `gep`,
`building`, `gramma`, `eta`, which are the first rooms' names; and so on.

**A table of the words**, which is in alphabetical order: a count, and four bytes for each.

| Bytes | What |
|---|---|
| 2 | Where the word's text starts |
| 2 | Its parts of speech, a bit each, as before |

A word's number is its place in this table, from 1, as it was.

**The synonyms**, which had four bytes in every word's entry and now are apart, since most
words have none: a count of groups, 125, and for each group a word's number, a count, and
that many pairs.

| Bytes | What |
|---|---|
| 2 | A word that stands for the group's |
| 2 | The parts of speech it stands for it as |

So `on` has four, `atop`, `onto`, `top` and `upon`, each as a preposition, and `old` has
`ancient` as an adjective. 264 words stand for another, and none for two.

This was read from the file alone, not from the code that unpacks it. It is taken as right
because the four parts end at the file's last byte, the table's words come out in
alphabetical order, and the synonyms are synonyms.

*Spellcasting 301* and the two *Gateway* games start their `VOCAB.DAT` with a tree too, and
the same first words come out, but the text stops short of the size given: something in the
form is different again.

## The objects

The world is 482 objects, in the same eight types, found the same way: a table of six bytes
for each object, its type and a far pointer to its record, and a table of three bytes for each
type. The table of objects is at `2918:00B8`, the types' at `2918:0000`, and the count at
`4974:3E70`.

| Type | Objects | Bytes | Places | Lists | *Spellcasting 101*'s bytes and lists |
|---|---|---|---|---|---|
| 1 | 78 | 24 | 0 | 1 | 28, 1 |
| 2 | 161 | 34 | 1 | 3 | 36, 2 |
| 3 | 5 | 10 | 0 | 0 | 14, 0 |
| 4 | 12 | 18 | 0 | 2 | 22, 2 |
| 5 | 44 | 18 | 0 | 2 | 22, 2 |
| 6 | 6 | 22 | 2 | 2 | 26, 2 |
| 7 | 31 | 28 | 1 | 3 | 32, 3 |
| 8 | 145 | 18 | 1 | 0 | 22, 0 |

Every record is four bytes shorter, and a thing, type 2, has a third list. Three things
changed, each read from the routines that take a field from a record, which are together in
the root from `023C:1583` as *Spellcasting 101*'s were.

### The names are in a file

A record began with two far pointers to its names, eight bytes. It begins now with two words,
four bytes, and every field after is four bytes sooner. `OBJECT.DAT` is the names: the size of
its text in a word, and then 591 names, each ending in a zero. A record's word is where its
name starts in that text, counted from 1, so that zero can be no name: `0001` for object 1,
the `Main Entrance`. So the names are out of the program's memory, and read from the disk
when one is wanted.

### A flag is looked up

In *Spellcasting 101* flag 12 was bit 11 of the record's flags, for every type, and so the
same bit meant one thing for a thing and another for a place. Now there is a table, from
`2918:0018`, of four bytes for each flag: the bit it is, a zero, and a word with a bit for
each type that has it. The routine that finds a flag, `023C:1583`, looks the flag up, answers
that an object of a type without it doesn't have it, and otherwise gives the byte and the
bit. The table has room for 40 flags before the table of objects starts, and all 40 are used.

| Flags | Bits | Types that have them |
|---|---|---|
| 1 to 4 | 0 to 3 | All |
| 5 to 14 | 4 to 13 | Some, each its own |
| 15, 18 to 32, 34 to 36 | 14 to 32 | Things, type 2, alone |
| 16, 17, 33 | 6, 7, 9 | Rooms |
| 37, 38, 39 | 6, 8, 9 | People |
| 40 | 5 | Type 8 |

So bits 6 to 9 are each two or three flags. That is what *Spellcasting 101* did without
saying so, where flag 7 was "not listed" for a thing and "outdoors" for a place: here the
room's bit 6 has a number of its own, 16.

Three routines do everything with a flag, as before, each given an object and a flag's
number: `023C:1660` tests one, `023C:169F` sets it and `023C:16D7` clears it, and each asks
`023C:1583` where the flag is first. The program calls them 812 times, from the root and
every overlay, and 803 of the calls name their flag as a constant. So each flag has the list
of who tests it and who changes it that named *Spellcasting 101*'s.

The numbers aren't the first game's. Flag 13 is open here: the action for `open` sets it and
the one for `close` clears it, and it is what a door has to have to let you through. It was
12. So the meanings can't be carried over by number, and each was found again from who has
it and who uses it.

### What the flags are

| Flag | What | In *101* | How it's known |
|---|---|---|---|
| 1 | Its name takes no article | 5 | `Your Room`, `Gramma Eta Pi` and the people with names have it; the routine that writes an article, `023C:0002`, tests it |
| 2 | Its name takes `an` | 2 | `envelope`, `ancient key`, `emerald`; the same routine |
| 3 | Plural | 3 | `eyeglasses`, `nerds`, `cookies`; the same routine |
| 4 | Takes `some` | 4 | `water`, `pizza`, `ale`, `graffiti`; the same routine |
| 5 | Noted: something to do with it has happened | | Nothing but what you start with has it; set 201 times, nearly all by one object's routine of another, and tested by the routines that ask whether a thing has been done yet. *101*'s 6, seen, may be the same |
| 6 | Touched; for a place, visited | 10 | Nothing has it at the start. Taking a thing sets it, and so does opening one; the routine that describes the room sets it of the room |
| 7 | Taking it is special | 8 | `torch`, `crocodile`, the spell boxes, the donuts. Taking one clears it and sets 15 |
| 8 | Not listed among a room's contents | 7 | 202 objects, most of them type 8, the scenery; taking one clears it |
| 9 | Gives light | 9 | 73 of the 78 rooms, and `fire`, `torch` and `firefly` |
| 10 | A door | 11 | The six doors, and nothing else |
| 11 | Can't be opened or closed | 14 | `pool`, `bathtub`, `sink`, `mixing bowl`: each is open too. `open` and `close` test it |
| 12 | Can be read | 20 | `notebook`, `letter`, `billboard`, `poster` |
| 13 | Open | 12 | `open` sets it and `close` clears it |
| 14 | Locked | 13 | A desk and four doors; `unlock` tests it |
| 15 | Can be taken | 23 | 101 things; the actions that drop, put and throw test it first |
| 16 | A place in the sewer | | Five rooms: `Access Chamber`, `Active Sewer Node`, `Sewer Valve`. Tested 25 times, by `drop`, `sit`, `lie down`, `smell`, `hear`; what each does about it isn't read |
| 17 | A place at the university | | 40 rooms, from `Main Entrance` to `President's Quarters`, and none of the places beyond it |
| 18 | Left out of `all` | 16 | What is in the trophy case; `get` clears it, and so does the cushions' routine, of each of them. Tested by the routine that tests 7, 15 and 25 together |
| 19 | Lit | 17 | `light` sets it and `blow out` clears it |
| 20 | Can be seen through | 19 | `trophy case`, `glass case`, `spell book` |
| 21 | Things can be on it | 33 | Beds, desks, `shelf`, `tray`, `lab bench` |
| 22 | Can be got in or on | 21 | Beds, `bathtub`, `pool`, `simulation chair`; `climb in` tests it |
| 23 | Burns | 22 | 37 things, paper most of them; `burn` tests it |
| 24 | A container | 15 | `envelope`, `bag`, `dresser`; `open`, `close`, `fill` and `empty` test it |
| 25 | Can be worn | 24 | The two cloaks, the eyeglasses and the goggles |
| 26 | A key | 25 | The three keys; `unlock` tests it |
| 27, 28 | Cuts | 26, 27 | `fruit slicer` alone has them, and `cut` tests each. As in *101*, what tells them apart isn't known |
| 29 | Can be eaten | 28 | `serving of casserole`, `banana`; `eat` tests it |
| 30 | What's dropped in it lands outside it | 29 | `lecture seat`, `simulation chair`; `drop` tests it. Named from *101*'s, whose uses are the same |
| 31 | Magic | 30 | The spell book, the spells and their boxes |
| 32 | Can be lit | 31 | Nothing has it, as nothing had; `light` and `blow out` test it |
| 33 | Outdoors | 7, for a place | 27 rooms, the quads, the roads and the forest; `look up` tests it of the room |
| 34 | A part of the Sorcerer's Appliance | | The eleven attachments, `The Antenna of Putzburg` and the rest; the Appliance's own routine tests it |
| 35 | Not known | | `banana`, `fruit slicer`, `package`, `pile of gold pieces`, three spell boxes; `drop`, `throw` and `frimp` test it |
| 36 | A bed | | The four beds |
| 37 | Someone other than you | | 30 of the 31 people: all but `you` |
| 38 | Female | 9, for a person | Hillary, Lola, the barmaids, the sisters, the Queen; the routine that writes an article tests it, for `she` and `her` |
| 39 | Not known | | `you` and eight more, the students among the people |
| 40 | Not known | | Eight dials and knobs, of type 8 |

Thirty-one of the forty are a flag *Spellcasting 101* had, most under a new number. The ones without
are this game's own: its sewer, its campus, its Appliance and its beds. *101*'s flag 32, on
80 objects and never explained, has no heir here, unless 34 is one: both mark the things a
game is a search for.

Flags 5, 16, 17, 30, 36 and 37 are named from who has them more than from code that was read,
and say so in the table.

### The exits are rows of one table

A room had a count of its exits and a far pointer to them, six bytes each. Now it has the
count and the number of its first row in one table for every room, at `2918:29CC`, of four
bytes each: 266 rows.

| Bytes | What |
|---|---|
| 1 | The direction, numbered as before |
| 1 | The kind |
| 2 | A word, which the kind says how to read |

| Kind | Rows | The word |
|---|---|---|
| 1 | 172 | The room it goes to |
| 2 | 16 | A door's row in a table at `2918:2DF4`, of the room it goes to and the door's own object: the way is taken when the door has flag 13, which is open |
| 4 | 78 | A number for a routine at `0777:0000`, which is asked with it and the action |

The kind that only said something, 3, is gone, and so is every pointer: a room's record now
holds none.

A room's record in full, with offsets in hex:

| Offset | What |
|---|---|
| 0 | Its name, and at 2 a second |
| 4 | Its flags, two bytes |
| 6 | Where its nouns start, and at 8 its adjectives |
| 0A | The first object on its list |
| 0C | A word a routine reads and sets, zero in most rooms; not yet understood |
| 0E | A word not yet understood |
| 10 | Where its row starts in the list of what is also there, at `2918:22E8` |
| 12 | And in the list of the places seen into from it, at `4974:3E72` |
| 14 | The count of its exits |
| 16 | Its first row in the table of exits |

The other types keep the order they had: where it is, which list it's on, the next on that
list, and the first on each of its own lists.

The lists of nouns and adjectives are as before, at `2918:0C04` and `2918:195A`, but the code
that reads them, the parser's, is in overlay 3 and no longer in the root.

Read this way the world comes out whole: `Your Room` holds a refrigerator, a duct, a trophy,
a bedsheet, your desk and your bed, with an envelope on the bed and a letter in the envelope,
and goes down and out to `Hu Delta Phart`.

## The grammar

The grammar is the two tables [it was](spellcasting-101.md#the-grammar), in a segment of their
own at `2C03`: 405 forms of ten bytes from `2C03:00E2`, and 149 ways on to a second object,
of six bytes, from `2C03:10B4`. Before the forms is a byte for each action, 226 of them.

Two things are different. The code that searches the tables is the parser's, and the parser
is in overlay 3 now, not the root. And the flags that say where an object may be looked for
have a byte after them in both tables, which moves what follows:

| Offset | A form | Offset | A second object |
|---|---|---|---|
| 0 | The verb | 0 | The preposition before it, or 0 for none |
| 2 | The preposition after it, or 0 | 2 | Flags, and at 3 the new byte |
| 4 | Flags, and at 5 the new byte | 4 | A byte not yet understood |
| 6 | The action with no object | 5 | The action |
| 7 | The action with one | | |
| 8 | Where its entries start in the second table | | |
| 9 | How many it has | | |

The new byte is zero in all but one form and nine second objects, where it is from `1A` to
`1D`. Whether it is more flags or a number of its own isn't known; the tools read it as the
flags' high byte and say nothing of it. The tools tell the two layouts apart by reading the forms each way and keeping the one in which
every entry a form points at starts with a preposition.

A verb and a preposition can now have more than one form. `apply` alone has two: one for
`apply OBJECT`, whose object may be anywhere about and is taken first, and one for `apply
OBJECT to OBJECT`, whose first object must be in hand and may be several. In *Spellcasting
101* a form's one set of flags had to do for both.

After the second table, at `2C03:1432`, is a third the same code searches, of ten bytes each,
with three rows: `ask`, `order` and `tell`. It isn't read yet; by the verbs, it is for
commands given to someone else.

## Which routine is whose

As before, no table says which routine is an object's or an action's. The source had a C
switch for each, and [the cases are read from the code](spellcasting-101.md#which-routine-is-whose).

- **An object's**: the routine at `077C:16EC` takes the object's type from the table and goes
  by a table of jumps to one of eight switches, one for each type, on the object's number.
  445 of the 482 objects have a routine of their own, as 395 of *Spellcasting 101*'s 427
  did. All but 15 of them are in overlays 2 and 7 to 22.
- **An action's**: the switch at `08F8:08CB` has a case for each of the 226 actions, and the
  one at `08F8:06C2`, of compares, has the 38 that have a routine before their own.

The switches themselves are the same code. What had to change to find them is how each
starts. This program was compiled to check its stack, so every routine begins with a far call
to the run-time, at `1408:0276`, before its first real instruction; and the two switches on
the action first hand the action to a routine at `0D68:0BFC`, which every switch on an object
calls too, before they take it to switch on.

*Timequest*'s action switch starts the same way, and reading it this way found its actions'
routines too.

## The timers

The timers work as [they did](spellcasting-101.md#the-timers): a table of sixteen, each with a
count that goes down a turn at a time, a routine run when the count is zero or less, and -1
for a routine that runs every turn. What changed is how a timer is named. It was a far pointer
to its routine. It is a number now.

| What | Where |
|---|---|
| The table, 16 entries of four bytes: a timer's number in a byte, a byte unused, and its count | `3FA7:5D2A` |
| How many are in it | `4974:7EF0` |
| Which one is running, or `FFFF` | `4974:7EF2` |
| The routines by number, a far pointer each, from 1 | `2E00:0000` |

There are 49 numbers. For 43 of them the pointer is a thunk, to a routine in an overlay, and
for six it is a routine in the root. The routines of a room's exits are numbered the same
way, through a table of their own. A number is two bytes less than a far pointer in every
call and in the table, and it leaves the far pointers in one place.

The routines are in a segment of their own, at `13A2`:

| Routine | What it does |
|---|---|
| `13A2:0002` | Empties the table |
| `13A2:0027` | Starts a timer: a number and a count. One already there gets the new count |
| `13A2:009E` | Stops one, and closes the table up |
| `13A2:0110` | Says whether one is in the table |
| `13A2:014D` | Gives one's count, or `7FFF` when it isn't there. New, and nothing calls it with a number that is a constant |
| `13A2:018F` | Runs them, for a number of turns |

The one that runs them goes down the table as before, and calls a timer's routine through
`0D63:0006`, which takes the number, looks it up in the table of routines, and calls what it
finds. After each turn's timers it adds one to the clock, at `4974:2A0C`, and asks the room's
own routine, with a reason of 27, before the next.

The program starts a timer 155 times, stops one 52 times and asks about one 50 times, and 250
of those 257 calls name their timer as a constant. Of the starts, 81 are for the next turn
and 16 are for every turn. Timers 8 and 17 are never named by a constant: something works
their numbers out.

### What each timer does

Each was named by reading its routine, and for most that is reading what it prints, which
the tools now write beside the code. Who starts it and with what count is from the calls.

| Timer | What | What its routine does |
|---|---|---|
| 1 | The day's initiation | Pledgemaster Cowpatty comes down the stairs with the rubber chicken and gives each Pledge the day's assignment |
| 2 | The upperclassmen find you | They haul you back to the cellar, where Cowpatty goes down the line asking who did what |
| 3 | The serpent heads speak | At Frogkisser House, every turn: prints a line it takes from the string file |
| 4 | The simulation ends | `The world dissolves around you...`, and the monitoring professor grades you |
| 5 | Sleepiness | `You begin to feel kind of sleepy`, then worse, then asleep where you are, or drowned in the pool |
| 6 | The class bell | A bell for the start and the finish of each class; the lecture hall fills and empties; in the wrong place you get `A Truancy` |
| 7 | The tower clock chimes | Restarts itself |
| 8 | Nothing | A routine that only returns. Nothing starts it by a number that is a constant |
| 9 | A word forgotten | Clears a word at `4974:4418` a turn after `burp`, `kiss`, `naughty` or `score` set it |
| 10 | A byte forgotten | Clears a byte at `4974:441A`, a turn after any of seven objects' routines |
| 11 | What was raised sinks back | `%s sinks back down.`, a turn after `frimp` |
| 12 | The simpleberries wear off | `The feeling of furtiveness passes`, eight turns after the bush; Lyle, Hiddenmolar or Hillary notices you |
| 13 | The speed potion wears off | `You suddenly feel sluggish`, eight turns on |
| 14 | The mascot swells back | Twelve turns on: `A wall of flesh smashes you to one side` |
| 15 | The party starts | At I Phelta Thi or Tappa Kegga Bru: `Let the party begin!` |
| 16 | You open your eyes | A turn after closing them |
| 17 | The Brus make ready | Seniors in TKB sweaters guard the stadium and decorate the pool. Never started by a constant |
| 18 | The Poolside Party | Every turn in the Pool Room: the toast, the tenth keg, the Sacred Tuxedo Removal |
| 19 | Hiddenmolar and Cowpatty whisper | Every turn, from the jukebox, in the Pub |
| 20 | Word of Tickingclock's death | Simon Sudsywater tells you how the President died |
| 21 | Tickingclock's errand | Otto comes in, tells the legend of the Appliance, and gives you the key to the laboratory |
| 22 | Tickingclock goes | Twelve turns after the letter: takes the President away, unless you are in the President's House |
| 23 | The sewer gurgles | 32 turns after `ugugoowah` |
| 24 | The last newspaper goes | `Someone takes the last newspaper in the pile.` |
| 25 | A second byte forgotten | Clears a byte at `4974:6158` |
| 26 | The Appliance runs | A turn after the silver lever: arcs of magical energy, and what comes out |
| 27 | The Sorcerer returns | 24 turns into the simulation: he looks over the bathtub and the daiquiri |
| 28 | The banana fetches water | Every turn: up the hill with the bucket, and back to the bathtub |
| 29 | The army of bananas | Every turn, the same by the dozen |
| 30 | A knock from the yard | Three turns on: the package |
| 31 | Twelve bytes forgotten | Clears twelve bytes at `3FA7:00BA`, a turn after any of the plunger, keys, lever, valve and pedals |
| 32 | Everyone out of the pool | From the right galumph pedal: and at your feet, the cookie cutter |
| 33 | The Welcome Back Party fills | The dining workers, then Barmaid U's finest |
| 34 | The Royal Parade | Every turn: the heralds, the Queen's float, the largesse, the marching band |
| 35 | The hellhounds come | A turn after the whistle |
| 36 | The bier is brought in | Pall bearers carry in President Tickingclock |
| 37 | You are yourself again | Sixteen turns after the Appliance changed you |
| 38 | Hiddenmolar tells all | Every turn at the end: his story, the magic monitor, the Trustees |
| 39 | The messenger | `I had an important letter for you, but I didn't want to interrupt` |
| 40 | The cloak is finished | Ten turns on, at Heftysum Hall |
| 41 | The cloak is brought | Twenty turns on, she climbs up to you with it |
| 42 | The iron turns to copper | From the pouches and the bottle of the Alchemy Lab |
| 43 | The copper dissolves | From the same |
| 44 | The hanging | 24 turns after the Queen: the drums, the noose, the charges, the platform |
| 45 | The cook and the hors d'oevres | Every turn: the tray, the pastry cart, the dumbwaiter |
| 46 | The tree is a sapling again | Fifteen turns after it grew |
| 47 | The Social Hall, turn by turn | Every turn at the party; it prints nothing of its own, and what it does isn't read |
| 48 | A count down by sixes | Takes one from a byte at `4974:52EB` and starts itself again for six turns, until the byte is gone. From the pellet and the two daiquiris; what the byte is isn't read |
| 49 | Seen in the Ancient Lab | Sets a byte when you are in the Ancient Lab with object 235 there |

Three of them, 9, 10 and 25, are *Spellcasting 101*'s one-turn memories again: a routine
sets a variable and starts the timer for the next turn, and all the timer does is clear it.
Sleepiness, the class bell and the party are back from the first game too, as timers 5, 6
and 15.

Timers 47, 48 and 49 are named from what they touch and not from what they mean.

## The text

Most of the game's text isn't packed at all any more. *Spellcasting 101* kept nearly every
string in `S101STR.DAT`, because its overlays were code and its data was always in memory.
Here an overlay has its own data with its code, and a routine in overlay 10 prints `The
statue is two or three times larger than lifesize.` from `3BA7:083D`, in that same overlay:
the text comes and goes with the code that says it.

`S201STR.DAT` is what is left: 1,815 strings in 87 blocks, 292,504 bytes packed into
154,898. By the look of them they are the longer passages: scenes, newspaper stories, and
in block 0 seven sets of jokes. It is built as [the first
game's was](spellcasting-101.md#the-text), a table of the blocks, a tree of Huffman's codes,
128 words, and the blocks, with two differences:

- The tree is in the form [the vocabulary's](#the-vocabulary) is in: a count of words, 422,
  and the words in pairs, a link being a place in the tree, a character kept as its
  complement, and the last pair the root.
- A string starts at the bit after the one before it ends, and no longer on a new byte.

The reading was checked by its sizes: the table gives each block's size packed and unpacked,
and all 87 come out to both.

The 128 words are this game's commonest: `Hiddenmolar`, `Moldybreadcrust `, `Professor `,
`Sorcerer `, and at the end `you`, `you `, `you're `, `you've ` and `your `.

### How the program names a string

*Spellcasting 101* loaded a block into an array of far pointers and named a string by its
place in the array. Here a string is one word, the block in its high byte and the string's
number in the block in its low: `0C2E` is string 46 of block 12, `"I don't have to take orders
from Phart trash like you!"`. Two routines take one:

| Routine | What it does |
|---|---|
| `0520:07DA` | Gives the string's far pointer, loading its block first if it isn't in memory |
| `0520:0853` | Prints it, by calling that one |

A third, `0520:0882`, takes a block's number and lets the block go. The blocks in memory are
kept in a table of eight bytes each, a block's number, its count of strings and a far pointer
to its array of pointers, which is the first game's array made the program's own business.

The program calls the two 766 times, and in 741 of the calls the word is a constant. No
call names a string in blocks 0, 44 to 71, 73, 74 or 82 to 86 that way: those are reached by
numbers worked out, the jokes of block 0 among them.

## The music

The music is two files, `INTRO_M1.MUS` for the title and `S201_M1.MUS` for the game, where
*Spellcasting 101* had six. A `.MUS` file is still [songs kept
twice](spellcasting-101.md#the-music), once for a Roland MT-32 and once for an AdLib card,
after a table of them. Two things are different.

**The table is bigger.** It has 120 entries, where it had 30, and an entry is twelve bytes,
where it was ten:

| Bytes | What |
|---|---|
| 4 | Where the song starts in the file; 0 for no song |
| 2 | Its length |
| 2 | 1 for the MT-32's, 0 for the AdLib's |
| 2 | A word, not yet understood; 0 for the AdLib's |
| 2 | A second word, new, the same |

A byte follows the table, and a byte follows each song. The game's file has 46 songs, each
both ways, and the title's has one.

**The AdLib's song is a MIDI file.** In *Spellcasting 101* it was a `.ROL` file, AdLib
Visual Composer's own, played with the instruments in `S101.BNK`. Here it is a MIDI file of
one track, of the kind Ad Lib called `.MDI`: the notes are MIDI's, and the instruments come
in the song itself, as events kept for a sequencer's own use that start with Ad Lib's
number, `00 00 3F`, and then a word that says what they are. Three kinds are in the songs:
1, with a voice's number and 28 values, 2 and 3, each with one byte. As the format is
generally described, the first defines a voice's instrument, the second sets the rhythm
mode and the third the range of the pitch wheel; the game's own player hasn't been read to
confirm it. So there is no `.BNK` file, and each song carries the instruments it uses.

Twenty of the game's 46 AdLib songs are 119 bytes, a header and no notes: those songs play
on the MT-32 alone.

*Timequest* already kept its AdLib songs this way, in a table of 120 entries of ten bytes,
and the three games after *Spellcasting 201* keep theirs as it does. The tools list and
write out all six games' songs. None of the `.MDI` files written has been played to check
it.

## Still to do

Everything past the program, the vocabulary, the objects, the grammar, the routines, the
timers, the text and the music. Flags 35, 39 and 40 have no meaning yet, and six more are
named from who has them. Three timers are named only from what they touch. The rules of the
parser's own grammar aren't found either, nor what a saved game holds.

The pictures, their regions and the sounds are read by the tools as they are. Their output
hasn't been checked by eye.
