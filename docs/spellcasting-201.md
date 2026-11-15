# Spellcasting 201: The Sorcerer's Appliance

Legend Entertainment, 1991. The second *Spellcasting*, by Steve Meretzky, on the system
[*Spellcasting 101*](spellcasting-101.md) was built on, a year on.

This is what the first look found, and so far it is how the program is built, its
vocabulary and its objects. What is
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
room's bit 6 has a number of its own, 16. For a thing, a flag's bit is, by this table, the
bit it had before, with the numbers after 15 moved up to make room. Whether each still means
what it meant hasn't been checked, and there is a reason to doubt it: a door lets you through
when it has flag 13, which is the bit that was "locked". The tools don't name them.

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
| 2 | 16 | A door's row in a table at `2918:2DF4`, of the room it goes to and the door's own object: the way is taken when the door has flag 13 |
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

## Still to do

Everything past the program, the vocabulary and the objects. The tools stop next at the code
that calls an object's routine and the code that searches the grammar, which they don't find.
The text and the music are each in a form *Spellcasting 101*'s readers don't read:

- `S201STR.DAT` starts as before, with its table of blocks, 87 of them. The tree after it is
  nearly twice the size, with more at its leaves than single characters.
- A song in a `.MUS` file has two more bytes in its entry.

The pictures, their regions and the sounds are read by the tools as they are. Their output
hasn't been checked by eye.
