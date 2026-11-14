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

## The objects

The game's world is 427 objects, numbered from 1: its rooms, its things, its people, and the
ideas a player can talk about. The program has a table of them, in its own segment at `2929`,
with six bytes for each: its type, a zero, and a far pointer to its record. The count is at
`4517:34E4`.

There are eight types, and a table of three bytes for each, from `4517:34CC`: how many places
an object of the type can be in, how many lists of other objects it can hold, and the size of
its record. What each type is for is inferred from the objects that have it.

| Type | Objects | Bytes | Places | Lists | Such as |
|---|---|---|---|---|---|
| 1 | 59 | 28 | 0 | 1 | Rooms: Bedchamber, Potty Chamber, Rooftop, to Stunning Climax |
| 2 | 199 | 36 | 1 | 2 | Things that are somewhere and can hold things: writing desk, fireplace, cloak |
| 3 | 5 | 14 | 0 | 0 | Ideas: animal, number, love, sex, sleep |
| 4 | 10 | 22 | 0 | 2 | What's everywhere: room, you, floor, notes, lock, students |
| 5 | 34 | 22 | 0 | 2 | Scenery: crack, cobweb, your house, tree, mountain |
| 6 | 12 | 26 | 2 | 2 | What's between two rooms: shed door, trap door, chimney, moat |
| 7 | 32 | 32 | 1 | 3 | People: you, Miss Beancounter, the registrar |
| 8 | 76 | 22 | 1 | 0 | Things that hold nothing: rat, hole, dead branch, bridge |

Every record starts the same way. Offsets here and below are in hex:

| Offset | What |
|---|---|
| 0 | A far pointer to the name |
| 4 | A far pointer to a second name, or zero: `me` for `you`, `Miss B'counter` for `Miss Beancounter` |
| 8 | Flags, a bit each, numbered from 1 at the low bit of the first byte: 16 of them, or 48 for type 2 |
| 0A, or 0E for type 2 | Where the object's nouns start in the list of nouns, and then where its adjectives start in the list of adjectives: [the words it's called by](#what-an-object-is-called) |

and the rest depends on the type. The program never reads a record directly. It has a routine
for each field, taking an object's number, which looks up the type and goes to the offset that
type keeps the field at; the routines are together in the root from `01B9:30A1`, and they are
where the layouts here come from.

### The tree

Objects are in one another, as Infocom's are. An object that can be somewhere has three
fields: the number of the object it's in, which of that object's lists it's on, and the number
of the next object on that list. An object that can hold others has the number of the first
object on each of its lists.

| Type | In | Which list | Next | First on each list |
|---|---|---|---|---|
| 1 | | | | 0E |
| 2 | 12 | 14 | 16 | 18, 1A |
| 4, 5 | | | | 0E, 10 |
| 6 | 0E, 10 | | | 12, 14 |
| 7 | 0E | 10 | 12 | 14, 16, 18 |
| 8 | 0E | 10 | 12 | |

As the game starts, the Bedchamber's list is the rat, you, the pile of hay, the two windows,
the fireplace, the writing desk and the door. The fire is on the fireplace's first list, the
love note and the application on the desk's, and the cloak is on your second list, of three.
A person's lists are, by that, what they hold and what they wear, and a thing's are what's in
it and on it, but which is which isn't settled.

Infocom's objects had one list each. The lists here, and the two places of a door, are what
this system adds.

With the tree, the exit's routine at the top of these notes can be read in full. Object 153
is you, 62 the fireplace and 185 the chimney, and `01B9:0D5D` asks whether one object is in
another:

```c
if (why != 15)
    return 3;
if (is_in(YOU, FIREPLACE)) {
    some_flag = 1;
    perform(25, CHIMNEY, 0);
} else
    print(strs[25]);    /* If you want to climb the chimney, then say so. */
return 0;
```

The names are made up; the numbers and the shape are the program's. `why` is
[the reason the routine is asked](#from-an-action-to-what-happens), and `perform` here is the
routine that starts over with a new action. The flag it sets is a byte at `4517:3BD7`, one of the variables
the game saves.

Other routines found on the way: `01B9:1694` tests a flag of an object and `01B9:16E8` sets
one, `01B9:344C` gives the object an object is in, `01B9:3659` the first on one of its lists,
and `01B9:37F6` the next on the list it's on.

## The vocabulary

`VOCAB.DAT` is the words the game knows, 1,736 of them, in alphabetical order. It has the size
of the words' text in a word, the text, each word ending in a zero, a count, and then eight
bytes for each word:

| Bytes | What |
|---|---|
| 2 | Where the word's text starts |
| 2 | Its parts of speech, a bit each |
| 2 | The number of a word it's a synonym of, or zero |
| 2 | The parts of speech it's that word's synonym as |

The program numbers the words from 1, and everything else names a word by its number. In
memory each word has ten bytes, the same eight with a far pointer in place of the text's
start, in a table from `3B00:005C` that is filled from the file as the game starts.

| Bit | Part of speech | Words |
|---|---|---|
| 1 | Verb | 352 |
| 2 | Noun | 1,112 |
| 4 | Adjective | 464 |
| 8 | Preposition | 44 |
| 10 | Article | 2: `a` and `the` |
| 20 | Direction | 22 |

A word can be several: `board` and `crack` are verbs and nouns, and `north` is a direction, a
verb and a noun. Eight words are none, and are the parser's own: `all`, `and`, `both`, `but`,
`every`, `everything`, `except` and `then`. Four words start with an underline, which nothing typed can: `_integer`, `_string` and
`_time`, by their names for what the player types that isn't a word, and `_foo`, the only
noun of the object that is you.

207 words are synonyms. `destroy`, `demolish`, `damage` and `crack` stand for `break`,
`awaken` for `wake`, `atop` for `on`, and `n` for `north`. A synonym is a synonym only as some
part of speech, which is what the last two bytes are for: `crack` is `break` as a verb, and
itself as a noun. Wherever the parser wants a verb it takes the verb's synonym, and so on, so
the tables that follow have each word once.

### What an object is called

The program has a list of nouns and a list of adjectives, each a row of word numbers, in the
objects' segment at `2929:0A02` and `2929:180C`. An object's record says where its nouns start
in the one and where its adjectives start in the other, and each object's words end at a zero.

| Object | Nouns | Adjectives |
|---|---|---|
| Bedchamber | bedchamber, chamber | bed |
| writing desk | desk, drawer | writing, desk, table |
| Miss Beancounter | woman, beancounter, b'counter, minnie | miss, minnie |
| your house | house, home, cottage, building | my |
| rat | robbie, robby, rat, rodent | |

The routine at `0B6C:000A` decides whether what the player typed could be an object. It is
given a noun and some adjectives, and the object matches when the noun is among the object's
nouns and every adjective is among its adjectives. `WRITING DESK`, `TABLE DESK` and `DESK` are
all the desk, and `MISS MINNIE` is Miss Beancounter.

### The grammar

What a command may look like is in a table at `2B6A:00D1`: 376 forms of ten bytes, in order of
the verb's number and then the preposition's, which the routine at `12A6:01D0` searches by
halves.

| Offset | What |
|---|---|
| 0 | The verb |
| 2 | The preposition after the verb, or zero |
| 4 | Flags for the object, kept with the sentence. Two are known: 20 is that it should be in the actor's hands, and 10 that it must be about the actor |
| 5 | The action when the command has no object, or zero when it must have one |
| 6 | The action when it has one object |
| 7 | Where the form's entries start in a second table, and then how many it has |

The second table follows the first, at `2B6A:0F81`, with six bytes for each way a form can go
on to a second object: the preposition before it, flags, a byte not yet understood, and the
action. These are the forms of `drop`, as the tools print them:

```
drop OBJECT: action 50
drop OBJECT down OBJECT: action 51
drop OBJECT in OBJECT: action 51
drop OBJECT on OBJECT: action 2
drop OBJECT from OBJECT: action 52
drop OBJECT off OBJECT: action 52
drop OBJECT out OBJECT: action 52
drop OBJECT through OBJECT: action 52
```

In all, the table allows 507 shapes of command, and they come to 207 actions, numbered up to
209. Nineteen of the shapes with a second object have no action, and what they are for isn't
known. Many shapes make
one action: `drop OBJECT on OBJECT`, `put OBJECT on OBJECT` and `apply OBJECT to OBJECT` are
all action 2. This is ZIL's `SYNTAX` table, and an action is what ZIL called a verb: the
number the rest of the game is written against. `climb OBJECT` is action 25, which is what the
fireplace's exit performs on the chimney.

Before the forms, from the start of their segment at `2B6A:0000`, is a byte for each of the
209 actions. Its bit 1 is that the action takes no turn, which 29 don't: SAVE, RESTORE,
SCRIPT, VERBOSE and the like, and also LOOK and INVENTORY.

## A turn

The game's main routine is at `0C35:1AE8`, some 4,000 bytes of it. It sets up the screen, opens
the string file, checks there's memory enough, and then goes around once for each command:

1. It prints the prompt and reads a line, into `3B00:4FB6`.
2. It takes the next command off the line, with the routine at `11C5:0266`. A line can have
   several.
3. If the command starts with AGAIN, OOPS or UNDO, a routine for each deals with it.
4. It parses the command, with the routine at `0000:030B`, and searches the grammar for its
   verb and preposition, which gives the action. A direction typed alone becomes a number,
   kept in a byte at `3B00:0059`.
5. For each object, it checks the grammar's flags. With flag 20, an object that isn't in the
   actor's hands is taken first, and the game says `[Taking the cloak first.]`. With flag 10,
   an object that isn't somewhere about the actor ends the command with a message put
   together from pieces, `aren't holding` among them.
6. It [performs the action](#from-an-action-to-what-happens), once for each object when the
   command names several.
7. Unless the action takes no turn, the room's routine is asked once more, with 7 as the
   reason; the routine at `1272:018C` runs, by its place the game's timers; and the count of
   moves at `4517:0046` goes up by one.
8. If performing answered 2, the rest of the line is dropped. Otherwise it goes back to step 2
   for the next command, and to step 1 when there are no more.

Before it performs, it marks the place with `setjmp`, which is where a routine that starts
over with another action comes back to.

The words the main routine knows by sight are in the data as numbers, in the vocabulary's
order, from `4517:006E`: `again`, `all`, `and`, `but`, the directions, `of`, `oops`, `then`,
`to` and `undo`. They were written in when the program was built, which is presumably why it
checks the vocabulary's file as it loads and can say `Error: vocabulary data mismatch`.

The directions' numbers come from the main routine's tests of those words:

| 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| north | northeast | east | southeast | south | southwest | west | northwest | up | down | in | out |

The parser itself, the routine at `0000:030B` and the code around it that takes the words
apart and chooses among objects, hasn't been read. Its messages say what it can do: ask
`[Which ... do you mean, ... or ...?]`, and say `[There seems to be a verb missing in that
sentence.]`.

## From an action to what happens

The parser's work ends in three numbers: an action, and up to two objects. The program keeps
them where every routine can see them, the action in a byte at `3B00:0058` and the objects in
words at `3B00:0000` and `3B00:0056`, and the routine at `0C35:0FF4` performs the action. It
asks a row of routines in turn, each with a number that says why it's being asked, and stops
at the first that answers with something other than zero:

| Asked | Why |
|---|---|
| The room's routine | 1 |
| The routine of what the actor is in, when that isn't the room | 1 |
| The actor's routine | 2 |
| The routine that comes before the action's, for the 23 actions that have one | |
| The routine of what the second object is in | 3 |
| The second object's routine | 4 |
| The routine of what the first object is in | 5 |
| The first object's routine | 6 |
| The action's routine | |

The room is the object whose number is at `4517:0042`, and the actor the one at `4517:0044`:
the Bedchamber and you, as the game starts. A routine an exit names, like the fireplace's at
the top of these notes, is asked with 15, which is what that routine's first test is.

This is Infocom's order nearly to the letter: the actor, the room, the pre-action, the
indirect object, the direct object, and the verb. What it means for the game's writing is the
same too. The action's routine is the stock reply, what TAKE or CLIMB does to a thing nobody
gave a thought to, and everything that makes a thing itself is in its own routine, which gets
the first word.

The routine remembers what was being performed and puts it back when it's done, so a routine
can perform another action in the middle of its own: the count of them in progress is at
`4517:778C`, and the same action on the same objects more than three deep is stopped with
`[Warning: infinite perform recursion terminated.]`. A second routine, at `0C35:0E8D`, gives
up the action in progress and starts over with another, by a `longjmp` to where the game's
main loop performs the player's. It's what the fireplace's exit uses to turn going up into
climbing the chimney.

### Which routine is whose

Nothing in the data says which routine is an object's or an action's. The source had a C
`switch` for each, and the compiler made each a table of jumps in the code:

- The actions' is at `06E6:081E`: 209 cases, each a call to that action's routine. All but one
  are in overlays 1 and 2, the two that share the first place in memory and load string block
  65, which is the stock replies. So that place holds the verbs, and the other the story.
- The routines that come before are a smaller `switch` at `06E6:06DA`.
- The objects' is at `0596:146A`, which goes by the object's type to one of eight more, each a
  `switch` on the object's number. 395 of the 427 objects have a routine, each in the overlay
  of its part of the story; yours is in the root.

The [tools](../tools/README.md) read the cases out of the code, and with the grammar they
give the whole way from a command to its routines. `CLIMB CHIMNEY` is the form `climb OBJECT`,
action 25. The Bedchamber's routine is at `3670:0070` in overlay 3, the chimney's at
`3670:1E43` in the same overlay, and action 25's at `313A:0AB7` in overlay 2.

The chimney's routine is the first piece of the game itself to be read here. The numbers are
the program's, and the names are from the tables above:

```c
int chimney(int why)
{
    if (action != CLIMB)                    /* 25 */
        return 0;
    if (room == ROOFTOP)                    /* object 3 */
        print(strs[76]);                    /* You've already done that! */
    else if (is_in(FIRE, FIREPLACE))        /* objects 63 and 62 */
        start_over(27, FIREPLACE, 0);
    else if (flag(ROOFTOP, 10))
        some_routine(ROOFTOP);
    else if (is_in_on_list(CLOAK, YOU, 1))  /* object 82, on your second list */
        print(strs[77]);                    /* You claw your way up the chimney. It's a tight fit... */
    else if (is_somewhere_in(CLOAK, YOU))
        print(strs[78]);                    /* ...a protruding brick rips the... */
    else
        print(strs[79]);                    /* You scramble up the narrow chimney... */
    ...
}
```

While the fire is in the fireplace, climbing the chimney becomes action 27 on the fireplace. Without it, what
the game says depends on the cloak: on your second list, somewhere else about you, or not
with you. That settles a question from the tree: a person's second list is what they wear.

## A room's exits

A room's record ends with a count of its exits, a byte at offset 16, and a far pointer to
them at offset 18. The Bedchamber has six, after the fireplace message and its neighbors at
`2701:0123`:

```
2701:0123  01 01 c6 09 17 45
           03 03 00 00 01 27
           07 02 c8 09 17 45
           05 03 31 00 01 27
           0a 03 62 00 01 27
           09 04 92 01 1c 2f
```

Six bytes a row: a direction, a kind, and a far pointer. There are four kinds, and they are
the four ZIL had:

| Kind | The pointer is to | For the Bedchamber |
|---|---|---|
| 1 | A word with a room's number | North goes to the Potty Chamber |
| 2 | Two words, a room's number and a door's | West goes to the Alley by the door |
| 3 | A message, said in place of going | East, south and down: `If you want to enter the fireplace, then say so.` |
| 4 | A routine, by its thunk, which decides | Up runs the routine above, in overlay 3 |

The directions are numbered [as the main routine has them](#a-turn): 1 is north, 3 east, 7
west, 9 up.

## The files

| File | What it is |
|---|---|
| `S101.EXE` | The launcher: graphics, sound, the title, and then the game |
| `S101MAIN.EXE` | The root, PLINK86's loader, and the data |
| `S101MAIN.OVL` | The 13 overlays of code |
| `S101STR.DAT` | Text, packed, in 69 blocks: 1,204 strings |
| `VOCAB.DAT` | The vocabulary: 1,736 words, each with its parts of speech and its synonym |
| `RESTART.DAT` | The world as the game starts: every object's record, and 91 variables |
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

`VOCAB.DAT` is [the vocabulary](#the-vocabulary).

### The world as the game starts

`RESTART.DAT` is what the game saves, written once by its makers before a move was made. The
routine at `01B9:1C7D` writes it, and a saved game, and, inferred, what UNDO goes back to,
which it keeps in memory instead. It has four parts, and they come to the file's 13,463 bytes
exactly:

| Bytes | What |
|---|---|
| 4 | The four bytes at `4517:0488`, not yet understood |
| 12,862 | The record of every object, in the table's order |
| 595 | 91 saved variables: the program has a table at `2660:0370` of the pieces of its data that are part of the game's state, a far pointer and a size for each, most of them a byte or a word |
| 2 | A count, here 0, with six bytes to follow for each it counts; inferred, things due to happen later |

The records in the file are the ones in the program, byte for byte, except their far pointers:
all 590 have a segment `0E21` higher. `5338` is the data segment `4517`, and `3522` the
messages' `2701`, as they were when the program was loaded at `0E21` on the day the file was
made. So the file holds nothing the program doesn't, and what RESTART does is put the records
back. How the pointers are made right when the program loads somewhere else is still to be
found.

## Still to do

- What picks the block in the three calls that take it from a table, and where blocks 19, 39
  and 40 are loaded.
- What a routine's answer means beyond zero, and 2 from performing; the reasons from 8 to 14.
- The parser's code: how it takes a line apart and chooses among objects. The rest of the
  flags of the grammar's forms, and of the actions' bytes.
- The timers.
- The rest of an object's record: the flags' meanings, and the fields past the tree. Which of a thing's lists is which.
- How the far pointers in a saved state are made right, and what its last part counts.
- Where the system ends and the game begins among the root's segments and the overlays.
- The pictures, the regions, the music and the sounds.
