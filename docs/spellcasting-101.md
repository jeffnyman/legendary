# Spellcasting 101: Sorcerers Get All the Girls

Legend Entertainment, 1990. Legend's first game, by Steve Meretzky, and the first use of the
system its later text adventures were built on.

This is what the first look found. What is known for certain is said plainly; what is inferred
says so. Addresses are `segment:offset` in hex, with segments relative to where the program
loads, as the files have them. Every one of them can be looked at with the
[tools](../tools/README.md).

## There is no virtual machine

Infocom's games were a story file and an interpreter: the story in the code of a machine that
never existed, and a small program for each computer that pretended to be it. Legend was
founded by people from Infocom, and the first question was whether its games work the same way.

They don't. *Spellcasting 101* is a C program, and its story is in its code:

- The files have nothing like a story file. There is text (`S101STR.DAT`), a vocabulary
  (`VOCAB.DAT`), the world's first state (`RESTART.DAT`), and pictures, music and sounds. The
  rest is the program: 328K of `S101MAIN.EXE` and 247K of `S101MAIN.OVL`.
- The program's messages are among its code, as a C compiler leaves them: `If you want to enter
  the fireplace, then say so.` is at `2701:0000`, and the other messages of that part of the
  game follow it.
- What the game does is 8086 code. This is the start of a routine in overlay 3, at
  `3670:054E`, which a room's exit runs:

  ```
  3670:054e  push bp
  3670:054f  mov bp, sp
  3670:0551  cmp word ptr [bp + 6], 0xf      ; is the argument 15?
  3670:0555  je 0x55c
  3670:0557  mov ax, 3                       ; no: return 3
  3670:055a  jmp 0x5a7
  3670:055c  mov ax, 0x3e
  3670:055f  push ax
  3670:0560  mov ax, 0x99
  3670:0563  push ax
  3670:0564  lcall 0x1b9, 0xd5d              ; a test of two numbers
  3670:0569  add sp, 4
  ```

  It is what Microsoft C makes of an `if` and a function call, with the arguments pushed from
  the last and the caller clearing the stack.

The design is still Infocom's. The program's own messages speak of the `main perform` and of
`perform recursion`, as ZIL's `PERFORM` runs a verb on its objects, and a room's exits are a
table of the kinds ZIL had (below). It is ZIL's way of making a game, written in C.

## How the program is built

- **Microsoft C 5**, in the large model: the runtime's notice, `MS Run-Time Library - Copyright
  (c) 1988, Microsoft Corp`, starts the program's data segment at `4517:0008`, and every call
  between its files is a far call. The root alone has about fifty code segments and 5,724
  relocations.
- **PLINK86**, Phoenix Software Associates' linker, cut it into overlays, to fit a game of more
  than 500K into a PC's 640K with DOS.

`S101.EXE` is what the player runs. By its messages, it sets up the graphics and the sound,
shows the title, and starts `S101MAIN.EXE`, which has a message of its own for being run by
itself: `Please use S101.EXE to start SPELLCASTING 101.`

### The overlays

The MZ header of `S101MAIN.EXE` describes only its first 224,672 bytes. That is the root: the
code that's always in memory, with PLINK86's overlay loader. DOS loads it and starts it at
`2E95:0322`, in the loader. The file's other 103,232 bytes, DOS never looks at.

The loader has a table at `2F1C:0000` of 14 overlays, and three places in memory to put them:

| Place | Overlays | What they are |
|---|---|---|
| `313A` | 1 and 2 | Code, about 20K each, from `S101MAIN.OVL` |
| `3670` | 3 to 13 | Code, 12K to 18K each, from `S101MAIN.OVL` |
| `3AFB` to `54D6` | 14 | The program's data, from the end of `S101MAIN.EXE`, loaded as it starts |

Overlays that share a place take each other's: one of 1 and 2 is in memory, and one of 3 to 13.
The data is the 14th, the rest of the .EXE, which the loader reads itself: 96,688 bytes of it,
with the memory above it to `54D6` for what starts as zero, and the stack.

Each entry in the table is 16 bytes, the first of them 6 into the table:

| Bytes | What |
|---|---|
| 2 | Status. `4000` is "load as the program starts"; `2000`, inferred, "in the .EXE"; the loader clears `8000` when another overlay takes its place, so, inferred, `8000` is "in memory". `FFFF` ends the table |
| 2 | The count of relocations |
| 2 | The segment it loads at |
| 2 | The first segment after it |
| 2 | Attributes, not yet understood (`00F5` for code, `00E8` for the data) |
| 4 | Where it starts in its file, in paragraphs |
| 2 | Its size in the file, in paragraphs |

In its file an overlay is its relocations, four bytes each as an offset and a segment, then, on
the next paragraph, its contents. The overlays fill both files exactly, each starting where the
one before ends, which is how the table's reading was checked.

After the table come the two files' names, and after them 700 thunks, of 12 bytes each. The
root never calls into an overlay; it calls a thunk:

```
2f1c:0192  lcall 0x2e95, 0x2ec       ; PLINK86's loader
2f1c:0197  dw 3                      ; the overlay's number
2f1c:0199  ljmp 0x3670:0x54e         ; the routine
```

The loader takes the number from after the call, loads overlay 3 if it isn't the one at `3670`,
and returns past the number, to the jump. A pointer to a routine in an overlay is a pointer to
its thunk, so tables of routines work wherever the routines are.

*Timequest*, a year later, is built the same way, with 768 thunks. From *Spellcasting 201* on,
the games are linked with .RTLink instead.

## A room's exits

After the fireplace message and its neighbors, at `2701:0123`, is a table the data points to:

```
2701:0123  01 01 c6 09 17 45
           03 03 00 00 01 27
           07 02 c8 09 17 45
           05 03 31 00 01 27
           0a 03 62 00 01 27
           09 04 92 01 1c 2f
```

Six bytes a row: a number, a kind, and a far pointer. With kind 3 the pointer is to a message:
`2701:0000` is the fireplace one, and `2701:0031`, `If you want to jump out the window, then
say so.` With kind 4 it is to a thunk, `2F1C:0192`, the routine above. With kinds 1 and 2 it is
into the data, at `4517`. So, inferred: a row is an exit, the number its direction, and the
kinds are a room to go to, a message that says why not, and a routine that decides. Those are
ZIL's exits.

## The files

| File | What it is |
|---|---|
| `S101.EXE` | The launcher: graphics, sound, the title, and then the game |
| `S101MAIN.EXE` | The root, PLINK86's loader, and the data |
| `S101MAIN.OVL` | The 13 overlays of code |
| `S101STR.DAT` | Text, packed, in 69 blocks: 1,204 strings |
| `VOCAB.DAT` | The vocabulary: 1,736 words |
| `RESTART.DAT` | The world as the game starts |
| `S101_C*.PIC`, `S101_E*.PIC` | Pictures; by their names and sizes, for the CGA and the EGA |
| `S101_R*.RGN` | Not yet looked at; by the name, regions of the pictures |
| `S101_M*.MUS`, `S101.BNK` | Music, and the AdLib instruments it plays on |
| `*.RS`, `MUSIC.RS` | Sounds; by the name, RealSound |

### The text

Not all the text is in the program. The longer pieces are in `S101STR.DAT`, in what the
program's messages call string blocks, loaded into a table as they're needed: 1,204 strings
in 69 blocks, 196,654 bytes of them packed into 103,937. Block 0 is what the game says when
there's too little memory, block 1 is jokes, block 2 starts with the application to Sorcerer
University, and the story goes on from there.

The file has four parts:

| Part | What |
|---|---|
| The table | A count of blocks, 69, then three words for each: its size unpacked, its size packed, and its count of strings |
| The tree | A count of nodes, 218, then two words for each |
| The words | A count, 128; a word for where each starts, and one more for where the last ends; then the words, each ending in a zero |
| The blocks | One after another, to the end of the file |

The text is packed twice over. First, the 128 commonest words are each made one character,
from 128 up: `A `, `Balmoral `, `He `, `Hillary ` and on through `your `, most with the space
that follows them. `Professor `, `Sorcerer ` and `Tickingclock ` are there with `the ` and
`of `. Then the characters, the words among them, are given Huffman codes: the commoner the
character, the fewer bits.

The tree is how a code is read. Start at node 0, and take a bit: 0 for the node's first word, 1
for its second. A word above zero is the node to go to for the next bit. A word below zero is a
character, negated, and the code is done. Zero itself is the end of the string. The tree
starts:

```
node 0:    1    81
node 1:    2     6
node 2:    3     5
node 3:  't'     4
node 4:  'c'   'u'
node 5:  'a'   'r'
```

so `0000` is a `t`, `0010` an `a`, and `00010` a `c`. Bits come from each byte's low bit up.
When a string ends, what's left of its last byte is skipped, and the next string starts on the
next byte, so a string can be found without unpacking the ones before it bit by bit, though
nothing in the file says where each starts.

This was checked by unpacking all of it: every block comes to exactly the size and the count
of strings the table gives it. *Timequest*'s `TIMESTR.DAT` is packed the same way. The later
games' string files don't unpack this way, and aren't read yet.

### How the program names a string

Nothing in the file names a string, and nothing in the program does either, by number. The
program has a routine at `01B9:2B7F` that loads a block, which in C would be

```c
int load_strings(char far * far *array, int block);
```

It is given an array of far pointers and a block's number. It finds the block in the file by
adding up the packed sizes of the ones before it, allocates the block's unpacked size, unpacks
all of it there, and then walks the text, putting a pointer to each string in the array, in
order. It returns the count of strings. From then on, a string is an element of the array: in
the game's source, something like `strs[25]`.

So the exit's routine from the top of these notes, when the test fails, does this:

```
3670:058f  mov es, word ptr [0xa0d6]
3670:0593  push word ptr es:[0x7eda]
3670:0598  push word ptr es:[0x7ed8]     ; string 2.25: If you want to climb the chimney, then say so.
3670:059d  lcall 0x142c, 0x1175          ; print it
```

The array is at `3B00:7E74`, and overlay 3 loads block 2 into it. The pointer pushed is at
`7ED8`, a hundred bytes on, and pointers are four bytes: string 25 of block 2. The
[tools](../tools/README.md) work this out for any instruction, and write the string beside
it.

The routine is called from 118 places, and they show how the game is laid out. A few arrays in
the data are shared by all of it:

| Array | What's loaded into it |
|---|---|
| `3B00:7E74`, with room for 128 pointers before the next | The text of the part of the game the player is in: block 2 by overlay 3, block 3 by overlay 4, block 5 by overlays 5, 6 and 7, and so on to block 57 by overlay 11 |
| `3B00:8074`, with room for 32 | A smaller block beside it: in overlay 12, block 45 is in the first array throughout while this one has block 46, 47 or on to 55 |
| `3B00:80F4` | Another, used the same way |
| `3B00:8274` | Block 65, by overlays 1 and 2, the two that share the first place in memory |
| `3B00:8ABC` | Block 0, what the game says of memory, by the root |

An overlay is a part of the story, then, and brings its text with it: its code is swapped in
by PLINK86, and its routines load its block into the shared array, whatever part of the game
had it before. The loader keeps a list of up to 16 arrays with
blocks in them, and a second routine, at `01B9:2E87`, frees an array's block and takes it off
the list. Loading into an array that still has a block frees the old one, and says
`[WARNING: string block loading over existing block]`, which a finished game never should.

Fifteen of the calls load into an array of the caller's own, on the stack: a block of a few
strings, loaded, printed from and freed in one routine. The jokes of block 1 are read that way.

Three calls take the block's number from a table, so sixteen blocks are named by no call:
blocks 6 to 15 are in two tables of five, `7 9 11 13 15` at `4517:593C` and `6 8 10 12 14`
after `4517:59FC`, and blocks 66 to 68 are in one after `4517:5DEE`. Blocks 19, 39 and 40
aren't accounted for yet.

The limits the routine was written with: a block has at most 255 strings, since the count is
read as a byte; the words can take 786 bytes, which is what *Spellcasting 101*'s take exactly;
and the file is read 2,048 bytes at a time.

`VOCAB.DAT` is a byte, then the words in order, each ending in a zero, from `-'n'` to `zoe`,
and then 13,890 bytes about them that aren't read yet.

`RESTART.DAT` is memory as it was, written to a file: the game's objects, with far pointers in
them. The pointers have segments like `5338` and `3522`, which are the data segment `4517` and
the messages' `2701` with `0E21` added to each: the program was loaded there on the day the
file was made. How the game uses them when it loads somewhere else is still to be found.

## Still to do

- What picks the block in the three calls that take it from a table, and where blocks 19, 39
  and 40 are loaded.
- The rest of `VOCAB.DAT`, and the parser that reads it.
- The objects: what `RESTART.DAT` and the data hold for each room and thing.
- Where the system ends and the game begins among the root's segments and the overlays.
- The pictures, the regions, the music and the sounds.
