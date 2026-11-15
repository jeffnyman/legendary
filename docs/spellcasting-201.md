# Spellcasting 201: The Sorcerer's Appliance

Legend Entertainment, 1991. The second *Spellcasting*, by Steve Meretzky, on the system
[*Spellcasting 101*](spellcasting-101.md) was built on, a year on.

This is what the first look found, and so far it is only how the program is built. What is
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

## Still to do

Everything past the program. The vocabulary, the text and the music are each in a form
*Spellcasting 101*'s readers don't read:

- `VOCAB.DAT` is packed. It starts with what looks like a tree of Huffman's codes, where
  *Spellcasting 101*'s was plain words.
- `S201STR.DAT` starts as before, with its table of blocks, 87 of them. The tree after it is
  nearly twice the size, with more at its leaves than single characters.
- A song in a `.MUS` file has two more bytes in its entry.
- `OBJECT.DAT` is new, and starts with the rooms' names, which were in the program before.

The pictures, their regions and the sounds are read by the tools as they are. Their output
hasn't been checked by eye.
