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

`S101.EXE` is what the player runs. It sets up the graphics and the sound, shows the title,
and at a key starts `S101MAIN.EXE`, which has a message of its own for being run by itself:
`Please use S101.EXE to start SPELLCASTING 101.`

It doesn't run the game as one program runs another, and wait. It replaces itself. Run on
[Legendary's PC](the-pc.md), it frees every block of memory it has by writing to DOS's memory
control blocks itself, which it finds by DOS's undocumented list of lists; makes its own
block as large as DOS will; and has DOS load `S101MAIN.EXE` as an overlay at the very segment
its own code is at. That is how Microsoft C starts a program in place of the one running, and
it leaves the game the whole machine, which it needs: 347K, on a PC with 640K and DOS in it.

### What the launcher is told

The launcher hands the game its own command line, in small letters, after a `>`. The `>` is
how the game knows the launcher started it: its main routine looks for one first, and without
it says to use `S101.EXE` and ends. Then it reads the words, and knows seven:

| Word | What it does |
|---|---|
| `cga` | Draws for the CGA, 640 by 200 in two colors |
| `nocycle` | Draws for the EGA without, by the name, changing its colors as it goes |
| `real` | Sound by RealSound, on the PC's speaker |
| `noreal` | No RealSound |
| `adlib` | Music by an AdLib card |
| `mt32` | Music by a Roland MT-32; a digit after it is, by its use, the interrupt the MIDI card is on, 2 if none is given |
| `xmouse` | Has the game draw [the mouse's pointer](#the-mouse) itself, and not leave it to the driver |

With none of them, the game draws for the EGA, and its setting for sound starts with
RealSound's bit in it. What each word sets is in the game's code; what the settings then do
is inferred from the words.

For the EGA, the game asks Microsoft's graphics library to set 640 by 350 in 16 colors, and
if the library won't, it sets the CGA's mode and goes on as if `cga` had been said. So a PC
with only a CGA needs no word at all.

The copy these notes are made from was started by a batch file with `SVGA BLASTER 7 220`
after the name, which is none of the seven. The launcher hands those on and the game passes
over them.

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
the games are linked with .RTLink instead, which is in
[*Spellcasting 201*'s notes](spellcasting-201.md#how-the-program-is-built).

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

Other routines found on the way: `01B9:1694` tests [a flag](#what-the-flags-are) of an object and `01B9:16E8` sets
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
| 4 | Flags for the object, kept with the sentence: [where it may be looked for](#a-sentence-into-objects), and what's asked of it |
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
2. It looks at the line's next word. If it is AGAIN, OOPS or UNDO, a routine for each deals
   with it.
3. It [parses the next command](#the-parser) on the line, by the routine at `0C35:0793`. A
   line can have several.
4. It searches the grammar for the command's verb and preposition, which gives the action. A direction typed alone becomes a number,
   kept in a byte at `3B00:0059`.
5. For each object, it checks the grammar's flags. With flag 20, an object that isn't in the
   actor's hands is taken first, and the game says `[Taking the cloak first.]`. With flag 10,
   an object that isn't somewhere about the actor ends the command with a message put
   together from pieces, `aren't holding` among them.
6. It [performs the action](#from-an-action-to-what-happens), once for each object when the
   command names several.
7. Unless the action takes no turn, the room's routine is asked once more, with 7 as the
   reason; the routine at `1272:018C` runs [the game's timers](#the-timers); and the count of
   moves at `4517:0046` goes up by one. The word after it, at `4517:0048`, is the game's clock,
   in steps of five minutes from midnight: [run](the-pc.md#the-notes-checked), it starts at
   187, which is 3:35 in the afternoon, and goes up by one a move.
8. If performing answered 2, the rest of the line is dropped. Otherwise it goes back to step 2
   for the line's next command, and to step 1 when there are no more.

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

## The parser

The parser is in three layers, and the middle one is a surprise: the game has a grammar of
English commands in it as data, written out rule by rule, with the names its writers gave the
rules.

### Words into tokens

The routine at `11C5:0266` takes the next word off the line and says what kind of thing it
is. It skips blanks; a word is letters, up to a blank or one of `. , ; ! ? "`, and is looked
up in the vocabulary. What it gives back is a token: a code for the kind, and with it the
word's number.

| What was typed | Token |
|---|---|
| `,` `.` `;` | One each |
| `and`, `then`, `all`, `but`, `to` | One each: the words the grammar names |
| The end of the line | One |
| A number | The word `_integer`, with its value kept beside it |
| A time, as `10:30` | The word `_time`, with its minutes |
| Something in quotation marks | The word `_string` |
| Any other word | By its part of speech |

A word of one part of speech has a code for it. A word of several has a code for the mix, and
which it is in this command is left to the grammar:

| Code | A word that is |
|---|---|
| -2 | A verb |
| -3 | A preposition |
| -4 | An adjective |
| -5 | A noun |
| -6 | An article |
| -8 | A verb or a noun |
| -9 | A verb or an adjective |
| -10 | A verb, an adjective or a noun |
| -11 | An adjective or a noun |
| -12 | A verb or a preposition |
| 0 | Not in the vocabulary |

`open` is -12 and `desk` -11; `note`, which is in the love note's name and is a thing itself, is
-11 as well. The code is what the routine returns, and the word's number is left in a word at
`3B00:4AA0`. Some of the choosing goes by what came before: the routine keeps the last token
at `4517:7850`. How isn't read through yet.

The player's `--watch` says each command's words this way before the action they make, as
`words: open [verb or preposition] desk [adjective or noun]`.

### Tokens into a sentence

The routine at `130E:005E` gathers a command's tokens, to a full stop, a semicolon, `then`, an
`and` with a verb after it, or the end of the line. It looks a word ahead twice: after a full
stop, for the end of the line, and after `and`, where a word that is a verb and neither a noun
nor an adjective ends the command and starts the next. So `i and take note` is two commands,
and `i and i` is one that fails, `i` being an adjective too. A word that isn't in the
vocabulary ends the gathering, and the rest of the line is dropped. Then it hands them to a matcher, at
`135C:0072`, with a rule to match them against.

The matcher knows nothing of English. It is given a grammar: rules, each with the ways it can
go, each way a row of other rules, parts of speech and tokens. It tries a rule's ways in
order, keeps what it's in the middle of on two stacks, and backs up to the next way when one
fails. The grammar is in the program's data from `4517:7CD8`, and this is all of it, as the
[tools](../tools/README.md) print it:

```
<sentence_gotten>       ::= <intro> <verb> <pe_prep> <pe_object_pair> <sentence_terminator>

<intro>                 ::= <direct_address_object>
                          | <inf_intro>
                          | (nothing)
<direct_address_object> ::= <object> <comma>
<inf_intro>             ::= <verb> <pe_prep> <object> <to>

<pe_object_pair>        ::= <obj_spec> <pe_prep> <pe_obj_spec>
                          | (nothing)
<pe_obj_spec>           ::= <obj_spec>
                          | (nothing)
<pe_prep>               ::= <prep>
                          | (nothing)

<obj_spec>              ::= <str>
                          | <all_but_list>
                          | <obj_list> <obj_conjunction> <all_but_list>
                          | <obj_list>
<all_but_list>          ::= <all_item> <but> <obj_list>
<all_item>              ::= <all_object>
<all_object>            ::= <all> <pe_object>
<pe_object>             ::= <object>
                          | (nothing)
<obj_list>              ::= <obj_list_item> <obj_conjunction> <obj_list>
                          | <obj_list_item>
<obj_list_item>         ::= <all_object>
                          | <object>
<obj_conjunction>       ::= <comma> <and>
                          | <comma>
                          | <and>

<object>                ::= <noun_phrase>
<noun_phrase>           ::= <pe_article> <pe_adjective_phrase> <noun>
<pe_article>            ::= <art>
                          | (nothing)
<pe_adjective_phrase>   ::= <adjective_phrase>
                          | (nothing)
<adjective_phrase>      ::= <adj_list>
<adj_list>              ::= <adj>
                          | <adj> <adj_list>

<sentence_terminator>   ::= <standard_terminator>
                          | .
                          | <then>
                          | <semi>
                          | <and>
<standard_terminator>   ::= . <eol>
                          | <eol>

<obj_gotten>            ::= <obj_spec> <standard_terminator>
<adj_gotten>            ::= <adjective_phrase> <standard_terminator>
```

Twenty-five rules and 46 ways. `pe_` is, by every rule that has it, "possibly empty". A
command is an introduction, a verb, perhaps a preposition, perhaps objects, and an end. The
introduction is how the player speaks to someone, `JOEY, GIVE ME THE BOX` or `ASK JOEY TO
GIVE ME THE BOX`. The objects are one, a list with commas and `and`, `ALL`, `ALL BUT THE
CLOAK`, or `ALL THE SPELLS`; and then, after a preposition, perhaps the same again. A noun
phrase is an article or none, adjectives or none, and a noun.

There are three rules to start from, and which is used goes by what the game is waiting for.
`<sentence_gotten>` is a command. The other two, by their names, are for what the player
types when the game has asked a question: `<obj_gotten>` for an object, when it has asked
what, and `<adj_gotten>` for an adjective alone, when it has asked which.

The data is plain. A rule, a part of speech and a token are each ten bytes: a kind, a far
pointer to the name, and two words, which for a part of speech have its bit and for a token
its code. A way is a far pointer to its name, which is the rule's with a number after it
(`<obj_spec3>`), one to its rule, one to a row of far pointers to what it's made of, and a
count. A list of every way, in the rules' order, is how a rule's ways are found. Nothing
read so far uses the names. Presumably they were left in by whatever made the tables out of
the grammar the writers typed, and they are why the grammar can be read here just as it was.

### A sentence into objects

What matched is a verb, perhaps a preposition, and one or two lots of noun phrases, each
phrase a noun and its adjectives. The verb and preposition find a form in
[the grammar of verbs](#the-grammar), and the form's flags say where each object may be
looked for. They are ZIL's, bit for bit:

| Bit | The object may be | ZIL called it |
|---|---|---|
| 1 | In the actor's hands | HELD |
| 2 | In something the actor has | CARRIED |
| 4 | In the place the actor is | ON-GROUND |
| 8 | In something there | IN-ROOM |
| 40 | Several objects, not one | MANY |
| 20 | Taken first, if the actor hasn't got it | TAKE |
| 10 | Refused, if the actor hasn't got it | HAVE |

`take OBJECT` has 2, 4 and 8 and not 1: what's already in your hands can't be taken. `drop
OBJECT` has 1 alone. `ask OBJECT about OBJECT` has none of the four for its second object,
and none means anywhere: you can ask about a thing that's nowhere near.

The routine at `0B6C:0684` finds the objects each phrase could be, by
[their nouns and adjectives](#what-an-object-is-called), looking further afield only while a
phrase has found nothing:

1. It starts from the actor and goes out, to what the actor is in and what that is in, for as
   long as each can be seen out of, and usually ends at the room. From there it walks the
   tree down, keeping count of where it is by the four bits. It goes into a list of an object
   only when the list's contents can be seen: a room's and a person's always, a thing's first
   list when the thing has flag 15 and either flag 12 or flag 19, by their use a container
   that's open or can be seen through. The fire is found in the fireplace, which has 12 and
   15; the love note isn't found in the desk, which has 15 alone.
2. In a room, it tries again with the bits the form didn't have, so what's there but in the
   wrong place is still found. Presumably that is for the sake of a better message than not
   being seen at all.
3. Then the room's own row of what is also there. A room's record has, at offset 12, where
   its row starts in a list of objects that are there without being in it: the scenery, the
   doors, other rooms. Each has a byte for whether it counts for now. The Bedchamber's row is
   the cobweb, the crack, the chimney and your house, and four more that don't count as the
   game starts: the Alley, the old horse, the cart and the Tigerbelly house, which by that are
   what's seen from the window.
4. Then the room's row of a second list, at offset 14: places whose contents can be seen from
   it. The Tool Shed's is the Alley.
5. Then every object of type 4, which is how `floor` and `room` are everywhere.
6. And when the form's bits are none, every object in the game.

A phrase with no adjectives that still has nothing may be a pronoun. `it`, `him`, `her` and
`them` are tried against four remembered objects, at `3B00:5168` and the three words after.

If a phrase has found several objects, the game gets a say before the player is asked. The
routine at `06E6:000A` is another of the source's `switch` statements, on the first of the
objects found, and its cases call routines in the overlays that pick one, for the objects
the story has look-alikes of. Only when nothing picks does the parser print `[Which ... do
you mean, ... or ...?]` and wait, presumably with `<obj_gotten>` or `<adj_gotten>` as the rule for
the answer. With nothing found at all it says so, and the turn is over.

One object for one phrase goes straight into the words the action is performed on. Several,
from `ALL` or a list, are performed one at a time.

### Where the parse is kept

What the parser makes of a command is in the action's segment, from `3B00:0004`, and the
player's `--watch` reads it there as the game goes:

| At | What |
|---|---|
| `3B00:0038` | A byte: how many lots of objects the command has, none, one or two |
| `3B00:0039` | A byte for each lot: the form's flags for it, where its objects may be looked for |
| `3B00:003C` | The lots, 12 bytes each: a count of phrases, and at 2 a far pointer to them |
| `3B00:0054` | A byte: the action the form makes of the command, copied from here to `3B00:0058` when it is performed |
| `3B00:8AD6` | Which form the search found, counting from 0, or FFFF |
| `3B00:8B78` | What was found, 60 hex bytes for each lot: six for each phrase, a count and at 2 a far pointer to that many objects' numbers |

A phrase is 40 bytes of words' numbers: the word `all` when the phrase is that, the article, a
count of adjectives and the adjectives, and at 26 hex the noun. The lists of objects found
are in memory the program asks for as it goes, and grow one object at a time.

The main loop calls the search of the grammar from `0C35:21E8`, and the routine that finds
the objects from `0C35:0CC6`, once for each lot and the second object's lot first, with the
lot, its flags and where to put what's found. So a command is four things, one after another,
and the watch says each:

```
words: put [verb] note [adjective or noun] in [verb or preposition] writing [adjective] desk [adjective or noun]
form 236: put OBJECT in OBJECT, which is action 51
second object "writing desk", looked for anywhere near: writing desk (61)
object "note", looked for held: love note (74)
action 51: drop love note (74) down writing desk (61)
```

`note` is looked for in your hands alone, since only what you hold can be put somewhere.
`examine window` finds two, `left window (65) or right window (64)`, and the game asks which.
`take sword` in the Bedchamber finds nothing; `ask rat about sword` finds the lead-plated
sword, which is in the Shop, because that form's second object may be anywhere at all. For
`take all` nothing is found by name: what `all` takes in is gathered another way, by
[the routine that says what fits the action](#what-the-flags-are), and where that is called from isn't read yet.

## The timers

Not everything in the game waits for a command. Rottenwood comes to the window on his own, a
professor loses patience, and a spell wears off. The game does these with timers: routines it
has put by to run on a later turn, or on every turn. ZIL's games had the same, as interrupts
and daemons, and here they are one table.

The table is at `3B00:4F30` and holds sixteen timers, six bytes each: a far pointer to a
routine, and a count. A word at `4517:7858` is how many it has. Five routines in one small
segment are all that touch it:

| Routine | What it does |
|---|---|
| `1272:000E` | Empties the table |
| `1272:0033` | Starts a timer: given a routine and a count, puts them at the table's end, or gives the routine its new count if it is there already. A seventeenth gets `[ERROR: Queue overflow]` |
| `1272:00BF` | Stops a timer: takes a routine out, and closes the gap |
| `1272:013E` | Says whether a routine is in the table |
| `1272:018C` | Runs the timers, at the end of a turn |

The one that runs them goes down the table once. A timer whose count is above zero has it
taken down by one, and that is all for this turn. A timer whose count is zero or less has its
routine called. Afterward a timer whose count is still zero is taken out of the table: it has
happened. One whose count is -1 stays, and so is called every turn. And a routine can start
itself again, with a new count, to happen again later. A timer started with a count of 2 in
the course of a turn is taken down at that turn's end and the next one's, and runs at the end
of the turn after that.

When the table has been gone down, the same routine moves the clock: the word at `4517:0048`
goes up by one, which is five minutes. So it is the timers' routine that makes a turn take
time, and an action that takes no turn skips it. A byte at `4517:3532` turns all of it around:
while it is set the clock goes down by one and the timers' counts go up. Presumably that is a
spell, and what sets it isn't read yet. The routine is given two numbers, whether to stop when
something has happened and how many turns to run, which the main loop gives as no and one;
other callers, presumably waiting, aren't followed.

The game's source started and stopped each timer by its routine's name, so
[`timers`](../tools/README.md#the-program) can list every call: 46 timers, in 120 starts, 66
stops and 5 askings. Whose routine a call is in says what the timer is about:

```
timer 07ec:06df: the man with the whip, which calls 3670:0e53 in overlay 3
    3670:01be in overlay 3: object 1 Bedchamber starts it with a count of 41
    3981:0223 in overlay 3: the routine at 3981:019e in overlay 3 stops it
timer 07ec:06f1: Miss Beancounter's spectacles, which calls 3670:2dac in overlay 3
    388e:0882 in overlay 3: object 79 rusty key starts it with a count of 1
    388e:0a71 in overlay 3: object 154 Miss Beancounter stops it
    388e:0bd8 in overlay 3: object 154 Miss Beancounter starts it to run every turn
```

A timer's routine has to be where a far pointer can find it whatever overlays are loaded, so
it is in the root. Most are six bytes there, one after another from `07EC:06D9`: a call into
an overlay, where the work is done, and a return. One isn't in the root: the registration
line's is in overlay 4 itself, and is started and stopped only by the long line's routine,
in the same overlay.

The Bedchamber's routine starts two as the game begins, before any command. The one with a
count of 2 runs at the end of the third turn, and then starts itself again with a count of 1,
turn after turn: footsteps on the stair, footsteps closer, and your stepfather through the
door. The other has a count of 41, and is the man with the whip, who comes if you are still
there. The player's `--watch` says each timer as it is started, as it runs and as it is
stopped, and `--panel` lists the ones that are going with the turns each has to go.

### What each timer does

A timer's routine says what it is: the text it prints, the objects it moves, the room it
asks whether you are in. These are all 46, named here by what they do; the names are made
up, and the tools and the player use them. Most were read from their text and what they
move, not instruction by instruction, and where less than that was read the table says so.

| Timer | What it is | What its routine does |
|---|---|---|
| `07EC:04B4` | The simulation ends | The castle's routine starts it with a count of 8. It says `The world dissolves around you...` and puts you back in the simulation chair |
| `07EC:06D9` | The stepfather on the stairs | The Bedchamber starts it with 2, and it starts itself again with 1: footsteps on the stair, footsteps closer, and then your stepfather bursts in, throws down the application and locks the door behind him |
| `07EC:06DF` | The man with the whip | The Bedchamber starts it with 41: a vicious-looking man bursts in, cracking a whip. Getting out in time stops it |
| `07EC:06E5` | The potty door closes | Started with 4 by the tiny door: `The potty door creaks slowly closed.` |
| `07EC:06EB` | The voice at the gate | Started with 2 by the flat rock: a familiar voice from the far side of the gate |
| `07EC:06F1` | Miss Beancounter's spectacles | Every turn while she rummages for them, finds them, cleans them and puts them on |
| `07EC:06F7` | The amazon leader | In the VIP Suite: she comes in behind you and gets down to business |
| `07EC:06FD` | The amazons' visits | Every turn in the VIP Suite: one amazon warrior after another comes for the Man, and leaves if you are dressed as a woman |
| `07EC:0703` | The chambermaid | Started with 2 by the SIP Suite: a chambermaid comes in with linens |
| `07EC:0709` | The waiter comes | Every turn in the Restaurant until the waiter has come to your table |
| `07EC:070F` | The waiter brings it | Started with 1 by the waiter: what you ordered is brought to your table |
| `07EC:0715` | The islanders gather | Every turn in the Meadow: a count kept at `4517:7202` goes up, by chance, toward 80 |
| `07EC:071B` | The islanders' thanks | Started with 1: `80 cheering islanders carry you off on their shoulders`, and you are given the big key |
| `07EC:0721` | Ocarina at home | The longest of them, started by the two condos and the curtains: people approach, Ocarina and Glockenspiel argue, he leaves, and she turns to you, or to her crossword |
| `07EC:0727` | Glockenspiel comes back | Started with 1 by Ocarina and by the crossword puzzle: he bursts in and sets you to clearing the garbage dump |
| `07EC:072D` | The gods quarrel | Started with 1 by the garbage: the other gods pour out of the condo and fall to fighting, and a thunderstorm begins |
| `07EC:0733` | The gold gate shuts | Started with 1: clears the gate's flag 12, and says so if you are there to see |
| `07EC:0739` | The silver gate shuts | The same, for the silver gate |
| `07EC:073F` | The bronze gate shuts | The same, for the bronze gate |
| `07EC:0745` | The ending | Every turn at the Stunning Climax: Joey explains himself, presses the two buttons, the Appliance does its work, and `THE END` |
| `07EC:074B` | To Batguano Court | Six instructions: it moves you to Batguano Court. The long line's routine starts it, with a count it works out; why isn't followed |
| `07EC:075A` | The role-players' game | Every turn in Frogkisser House: the Malls 'n' Muggers players' game, a line at a time |
| `07EC:0760` | Meals at the Cafeteria | Every turn: the cook strikes the cowbell, and the staff start or stop serving breakfast, lunch or dinner |
| `07EC:0766` | The Stadium's day | Every turn: Physical Skills class comes and goes, and later the fans file in |
| `07EC:076C` | The professor tires | Started with 60 by the Professor's Office: `it's getting rather late` |
| `07EC:0772` | The party starts | At the two fraternities: a band starts, and the house fills with partygoers |
| `07EC:0778` | The party's chatter | Every turn at a fraternity after eight in the evening: by chance, one of a set of lines about the party |
| `07EC:077E` | Gretchen with you | Every turn: Gretchen follows you, asks your name, and in the end passes out in your arms, or leaves |
| `07EC:0784` | Gretchen arrives | At I Phelta Thi: it moves Gretchen in and starts the timer above. Inferred from what it moves; its text isn't read |
| `07EC:078A` | The fratman | At a fraternity: `A fratman with a build like a Pokkaball star claps you on the shoulder` |
| `07EC:07D2` | Dinner with Hillary | In the Housemaster's Suite: the pheasant, the potatoes and the wine come and go, and Hillary has her eye on you |
| `07EC:07D8` | The class bell | A bell rings for the start or the finish of a class, the lecture hall empties, and the last newspaper is taken from the pile |
| `07EC:07DE` | The dinner invitation | Professor Tickingclock waves to you and reminds you that you are expected at the suite |
| `07EC:07E4` | Tickingclock comes in | It moves the professor into his office: `Professor Tickingclock creaks into the room` |
| `07EC:07EA` | The lecture | Every turn in Meltingwolf Hall while a class is on: the professor lectures, and praises a spell well cast |
| `07EC:07F0` | The day's first look | Started with 45 by the routine that starts a day: it describes the room again, and at the Boat Dock, once, brings Professor Peelerofsmallfigs. Only partly read |
| `07EC:089F` | A word forgotten | Four instructions: it clears a word at `3B00:3BD4`. A dozen actions start it with 1, so the word lasts one turn. What the word is isn't read |
| `07EC:08AD` | A byte forgotten | The same for a byte at `3B00:3BD6`, started with 1 by several rooms and objects |
| `07EC:08BA` | FRIMP wears off | Started with 1 by `frimp` and by the beds: what was floating `sinks back down` |
| `07EC:09F3` | Sleepiness | Started with a long count: `You begin to feel kind of sleepy`, then `really really tired`, and then you fall asleep where you are and the next day starts |
| `07EC:128A` | ZEM wears off | Started with 5: `You suddenly feel less robust as the ZEM spell wears off.` |
| `07EC:12A3` | The dragon's breath | At the Base of Keep: the atomic dragon breathes fire, which you dodge once |
| `07EC:12A9` | The pendulum | Started with 7 at the Far Side of Moat: the pendulum reaches Lola, with a different line for how near you are. The lever stops it |
| `09BB:0D1E` | The red button resets | Three instructions: it clears a byte at `4517:37AE`, a turn after the red button is pressed |
| `09BB:0D26` | The surfboard's trip | Every turn while the surfboard is under way: it crosses the water, arrives, or sputters to a stop |
| `3670:0899` | The registration line | Every turn in Donkeydung Hall: the line moves, you reach the registrar, and you are registered and given the spell book, the notebook and the rest |

They are the story's clockwork, and nearly all of it. A place that is alive while you are
there has a timer that runs every turn: the Cafeteria's meals, the Stadium's day, the
lecture, the party. A thing that is going to happen has one with a count: the man with the
whip in 41 turns, the pendulum in 7, the professor's patience in 60. A scene told a line at
a time is a timer that starts itself again with a count of 1, as the stepfather's is. And
four are only housekeeping, a few instructions that clear a variable a turn after something
set it. Sleepiness is one too, and its routine is the one that goes on to start the next day.

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

The main loop's `setjmp` is at `0C35:2911`, on a buffer at `3B00:8262`, and what it gets back
says what to do:

| From `setjmp` | Who jumped | The loop then |
|---|---|---|
| 0 | Nobody: the loop has just set it | Performs the action the parser left |
| 1 | The routine that starts over, `0C35:0E8D` | Performs the action that routine left in its place, with the count of actions in progress back at zero |
| 2 | The routine that goes somewhere, `07EC:1893` | Performs nothing more: the action is done, as if answered with 1 |

Going is the second kind of jump, and the reason a watch on the routines that are asked never
sees going answered. Action 197's routine, at `313A:3FE0` in overlay 1, hands the direction,
the byte at `3B00:0059`, to `07EC:1893`. That takes the room's exit that way (`07EC:16B1`,
which reads the exit's kind and for a plain one calls `07EC:13A8` to move you and show the new
room) and then calls `longjmp` with 2. It never returns to the action's routine, which never
returns to the routine that performs, and whatever was being performed around it is dropped
with them. The reason, presumably: a routine that moved the player in the middle of something
else couldn't go on as if it were still in the old room. The count of actions in progress is left where it was,
and is put right at the next command.

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

### What the flags are

An object has 16 flags, or 48 if it is a thing, and the record says nothing of what any of
them is for. The code does. Three routines, one after another, are the only code that reads
or writes a flag: `01B9:1694` tests one, `01B9:16E8` sets one and `01B9:1735` clears one,
each given an object and the flag's number. The game's source called them with the flag as a
constant, so every call says which flag it is about, and whose routine the call is in says
what the flag is for. The routine for `open` sets flag 12 of its object and the one for
`close` clears it: 12 is open.

There are 684 such calls in the root and the overlays, and
[`flags`](../tools/README.md#the-program) lists them by flag, 679 with the flag read from the
code. With what has each flag as the game starts, they give this:

| Flag | What it is | How it's known |
|---|---|---|
| 2 | Its name takes `an` | The routine that writes an article, `01B9:000C`, adds `n` to `a`. The Alley, the application and the old horse have it |
| 3 | It is plural | The same routine writes `some` for `a`, and `they` and `them` for it. The overalls, your hands, the potatoes |
| 4 | It takes `some`, and is still `it` | With flag 4 the routine writes `some` but not `they`. The wine, the cow manure, the graffiti |
| 5 | Its name takes no article | The routine writes neither `the` nor `a`. Batguano Court, Your Room, Lola Tigerbelly |
| 6 | The player has seen it | Nothing has it at the start. A room's routine sets it on each thing its description mentions, 198 calls in all. The routine for `cast` with nothing named tests it of thirteen people, one by one |
| 7 | It isn't listed among a room's contents | The routine that lists what is in a room, `01B9:0993`, passes over what has it: the door, the desk, the fireplace, you. Taking a thing clears it. 165 objects start with it |
| 7, of a place | It is outdoors | The word for what is underfoot is `ground` with it and `floor` without. The Rooftop, the Alley and the Meadow have it, and the Bedchamber and the Library don't |
| 8 | Taking it is special | A thing that can be taken isn't picked up along the way, with `[Taking it first]`, when it has flag 8, and `take all` tries a thing that has it though it can't be taken. A click on it offers `Take`. The grate, the rusty key, the flat rock, whose own routines clear it |
| 8, of a person | They can be asked things | `ask for` with nobody named looks in the room for a person with it, and to `ask about` a person with it answers `"I don't know much about that."` You, the dragon and the whale don't have it |
| 9 | It gives light | By what has it, more than by the code: 57 of the 59 rooms, the two without it being the Maze and the Meeting Room, and one thing, the fire. `01B9:125D` asks it of places and things only |
| 9, of a person | She is female | The pronoun is `she` and `her` with it, `he` and `him` without. Miss Beancounter, Hillary, Gretchen, Lola |
| 10 | It has been touched | Nothing has it at the start. Taking a thing sets it, and opening one. The routine that lists a room's contents asks a thing's routine one question until it is set and another after, presumably for how it is described before it's disturbed |
| 10, of a place | It has been visited | The routine that moves the player, `07EC:13A8`, sets it of the room arrived in |
| 11 | It is a door | Every door and window has it. A click on one offers `Open` or `Close` |
| 12 | It is open | `open` sets it and `close` clears it |
| 13 | It is locked | `open`, `close` and `unlock` test it, and the doors' own routines set and clear it. The Bedchamber's door, the shed door and the trap door start locked |
| 14 | It can't be opened or closed | `open` and `close` test it, and a click on a container that has it offers neither. The fireplace, the potty, the cart: open, and with no lid |
| 15 | It is a container | `open`, `close`, `look in`, `put in` and `empty` test it |
| 16 | It is left out of `all` | The routine that says what `all` takes in, `0000:0937`, leaves it out. Nothing has it, and nothing sets it |
| 17 | It is lit | `light` sets it and `blow out` clears it, of a thing with flag 31 |
| 19 | It can be seen through | [What's in a container can be seen](#a-sentence-into-objects) when it is open or has this. The windows, the display case, the spell book |
| 20 | It can be read | A click on it offers `Read`. The love note, the notebook, the newspaper |
| 21 | It can be got in or on | `climb in` tests it, and a click offers `Get in`, or `Get on` with flag 33. The fireplace, the old horse, your bed |
| 22 | It burns | `burn` tests it, and the fire's routine and the flamethrower's. The pile of hay, the love note |
| 23 | It can be taken | `get` tests it, and a click offers `Take`. 77 things |
| 24 | It can be worn | `wear` tests it. The cloak, the overalls, the bonnet, the lipstick |
| 25 | It is a key | `unlock` and `open` test it of the second object. The three keys |
| 26, 27 | It cuts | `cut` goes ahead when what it's done with has either, and says `If you think that will cut...` otherwise. Only the sword has them, and both. What tells them apart isn't known |
| 28 | It can be eaten | `eat` tests it, and a click offers `Eat` |
| 29 | What is dropped in it lands outside it | `drop` starts from what you are in, and goes out from it while it has this flag. The old horse, the beds, the lecture seat |
| 30 | It is magic | The spells, their boxes and the spell book. Opening a box with it moves the spell inside to the book |
| 31 | It can be lit | `light` and `blow out` test it. Nothing has it, and nothing sets it: this game has no lamp to light |
| 32 | It is to be looked for | `find` says `Keep looking! You might get lucky.` of a thing with it. 80 things have it, objects 246 to 325, with names like `pronoun atop the wooden structure`. What they are hasn't been followed |
| 33 | Things can be on it | `drop` puts a thing on its second list rather than its first, and what is on that list can always be seen. A click offers `Get on` rather than `Get in`. The writing desk, the old horse, the surfboard |

Four of the flags are two things: 7 to 10 mean one thing of a place or a person and another
of a thing, and the code asks for the type before it asks for the flag. The other flags of a
thing, 1, 18 and 34 to 48, nothing has and nothing asks about. 16 and 31 are asked about, but
nothing ever has them. These are presumably the system's, written for more games than this
one.

A few routines have turned up on the way. `0000:123C` picks the verb a click on an object
offers, from its type and flags: `Take`, `Open` or `Close`, `Read`, `Get on` or `Get in`,
`Eat`, and for a room `Go to` or `Look at`, for a person `Greet`. `0000:0C8B` is the taking
of a thing along the way. `0000:0C0A` says whether what is in an object, or on it, can be
seen.

The player's `--watch` says each flag by name as the game sets or clears it, and `--panel`
says what is open, locked or lit beside its name.

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
| 3 | A message, said in place of going | East: `If you want to enter the fireplace, then say so.` South and down: `If you want to jump out the window, then say so.` |
| 4 | A routine, by its thunk, which decides | Up runs the routine above, in overlay 3 |

The directions are numbered [as the main routine has them](#a-turn): 1 is north, 3 east, 7
west, 9 up.

The routine that takes an exit, `07EC:16B1`, tests for a fifth kind, 5, as well. No exit of
the Bedchamber's is one, and it hasn't been followed.

## The files

| File | What it is |
|---|---|
| `S101.EXE` | The launcher: the title, and then the game, which it loads over itself |
| `S101MAIN.EXE` | The root, PLINK86's loader, and the data |
| `S101MAIN.OVL` | The 13 overlays of code |
| `S101STR.DAT` | Text, packed, in 69 blocks: 1,204 strings |
| `VOCAB.DAT` | The vocabulary: 1,736 words, each with its parts of speech and its synonym |
| `RESTART.DAT` | The world as the game starts: every object's record, and 91 variables |
| `S101_C*.PIC`, `S101_E*.PIC` | [The pictures](#the-pictures), for the CGA and the EGA: 362 in each set |
| `S101_R*.RGN` | [What is where in each picture](#what-is-where-in-a-picture), for the mouse |
| `S101_M*.MUS`, `S101.BNK` | [The music](#the-music), as MIDI and as AdLib's `.ROL`, and the AdLib instruments |
| `*.RS`, `MUSIC.RS` | [The sounds](#the-sounds): RealSound, for the PC's speaker |

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

`RESTART.DAT` is what the game saves, before a move is made. The game writes it itself each
time it starts, as running it shows, for RESTART to read back; the copy that came with the
game is from the last time it was run. The routine at `01B9:1C7D` writes it, and a saved game,
and, inferred, what UNDO goes back to, which it keeps in memory instead. It has four parts, and they come to the file's 13,463 bytes
exactly:

| Bytes | What |
|---|---|
| 4 | The four bytes at `4517:0488`, not yet understood |
| 12,862 | The record of every object, in the table's order |
| 595 | 91 saved variables: the program has a table at `2660:0370` of the pieces of its data that are part of the game's state, a far pointer and a size for each, most of them a byte or a word |
| 2 | A count, here 0, with six bytes to follow for each it counts: by its shape [the table of timers](#the-timers), none of them going yet |

The records in the file are the ones in the program, byte for byte, except their far pointers:
all 590 have a segment `0E21` higher. `5338` is the data segment `4517`, and `3522` the
messages' `2701`, as they were when the program was loaded at `0E21` on the day the file was
made. So the file holds nothing the program doesn't, and what RESTART does is put the records
back. The pointers are right for as long as the program stays where it is, which a file
written at each start is good for. A saved game is kept longer than that, and how its
pointers are made right is still to be found.

## The pictures

The pictures are in twelve files, six for the EGA and six for the CGA: `S101_E1.PIC` to
`S101_E6.PIC`, and `S101_C1.PIC` to `S101_C6.PIC`. The two sets have the same 362 pictures,
number for number, drawn twice: in 16 colors at 640 by 350, and in black and white at 640 by
200. The title is picture 38 of the first file in each.

The code that reads them is in both programs: in `S101.EXE`, for the title, where it was read
for these notes, and again in `S101MAIN.EXE`, for the rest.

A file starts with a directory, twelve bytes for each picture, to where the first picture
starts. Two of the EGA's files have room for 256 pictures, most of it empty; the rest have
room for what they hold.

| Offset | What |
|---|---|
| 0 | Where the picture starts in the file, four bytes; zero for none |
| 4 | How many places it's drawn at |
| 5 | How it's kept: bit 0 set for no palette, and the rest the way it's packed |
| 6 | Its width, and then its height, in pixels |
| 0A | A word not yet understood: zero for most, and for a few a round number from 100 to 1,000, which looks like a time |

A picture starts with its places, when it has any: two words each, across and down. Most
that have one have one, the pieces of the screen that belong at a spot. A few have five, one
small picture drawn in a row. Then, in the EGA's files only, a picture with a palette has it:
16 bytes, each one of the EGA's 64 colors. Then the pixels, one of four ways:

| Way | What it is | Used for |
|---|---|---|
| 4 | As they are, a bit a pixel, row after row | Small pieces in two colors |
| 5 | As they are in four planes: each row as four rows of bits, blue first | Small pieces in color |
| 1 | Huffman's codes, a byte of eight pixels at a time | The CGA's large pictures |
| 0 | LZW, a pixel at a time | The EGA's large pictures |

### The CGA's Huffman

A count less one, then for each code its length in bits, the byte it stands for, and its
bits, in one byte or in two when it's longer than eight. Then the picture's bytes, a code
each, read from each byte's high bit down. Each picture has its own codes. Two pictures that
are one byte over and over have a single code of no bits at all.

### The EGA's LZW

It is the scheme GIF made common, changed in every detail:

- The codes are of pixels, not bytes: 0 to 15 are the 16 colors.
- They start five bits wide, and are read from each byte's high bit down.
- Code 16 says that the codes are a bit wider from now on. A GIF's reader works out for
  itself when that happens; here the file says.
- Code 17 starts the table over, at five bits. The first code after it is written and adds
  nothing to the table.
- New strings are numbered from 18.
- The code with every bit set, whatever the width is by then, ends the picture.

The title's 224,000 pixels are 42,194 bytes this way, under a fifth of what four planes of
them take.

All 724 pictures unpack, each to exactly its width times its height. The
[tools](../tools/README.md) write them as PNG files.

### What is where in a picture

`S101_R1.RGN` to `S101_R6.RGN` go with the picture files of the same numbers, and say what
the mouse is pointing at. A file starts with a directory of six bytes for each of the
picture file's pictures, to where the first picture's regions start:

| Offset | What |
|---|---|
| 0 | Where the picture's regions start in the file, or zero when it has none |
| 2 | An object's number, or zero: by its use, what the rest of the picture is |
| 4 | How many regions it has |

A region is six bytes: an object's number, and a rectangle as four bytes, its left, top, right
and bottom. Across is counted in pairs of pixels, so that a picture 379 pixels wide fits in a
byte; down is in the EGA's rows.

79 pictures have regions, 906 in all. A room's picture has its things: the Bedchamber's, which
is picture 45 of the first file, has sixteen, with the floor across the bottom, the door in the
middle, and the rat where its shadow is on the wall. A map has its rooms: picture 44, the map
the game starts with, has the Bedchamber, the Potty Chamber, the Rooftop, the Alley and the
Tool Shed, each where it is drawn.

The object in the directory ties some rooms to their pictures: the Bedchamber is object 1,
and picture 45's entry has 1. Most entries have zero, though, and one has a thing, the tiny
door. How the game picks the picture for a room is still to be found.

## The music and the sounds

Nothing here needed working out from nothing. The music is in two formats other programs
wrote and others can read, and the sounds are in a third that was licensed.

### The music

`S101_M1.MUS` to `S101_M6.MUS` have 23 songs, each kept twice. A file starts with 30 entries
of ten bytes, and a byte:

| Offset | What |
|---|---|
| 0 | Where the song starts in the file, four bytes; zero for no song |
| 4 | Its length |
| 6 | 1 or 0, for which of the two formats it is |
| 8 | A word the two of a pair share, not yet understood |

With 1, the song is a Standard MIDI File, whole, from its `MThd` on. It is for a Roland MT-32,
and its tracks are named for the MT-32's sounds: `SYN BASS 3`, `BOTTLEBLOW`, `HONKYTONK
PIANO`. With 0, it is a `.ROL` file, as AdLib's Visual Composer saved it, for an AdLib card.
The instruments a `.ROL` names are in `S101.BNK`, which is AdLib's instrument bank as AdLib's
own tools made it, and the playing is done by `SOUND.COM`, AdLib's driver, which the player
runs before the game and which stays in memory, 17,648 bytes of it.

The game needs no telling. Run [on Legendary's PC](the-pc.md) with the driver loaded, the
launcher opens `S101_M1.MUS` and `S101.BNK` and plays the title's music by the FM chip;
without the driver it opens `MUSIC.RS` and plays the title's music on the speaker.

So the songs were written once and arranged twice, and the game ships the arrangers' files
as they were.

### The sounds

The `.RS` files are RealSound: recorded sound for the PC's speaker, by a method Access
Software made and Legend licensed. There are 23, from a bip to 26 seconds of music for the
title, `MUSIC.RS`, for a PC with no sound card at all.

A file is 32 bytes and then the sound:

| Offset | What |
|---|---|
| 0 | `STEVE` |
| 5 | How many times each byte is played: 1, 2 or 4 |
| 6 | 72 |
| 7 | The count of the sound's bytes, four bytes |

The rest of the 32 is whatever was in memory when the file was written: in some, a few bytes
of a program's code. Most of the files end with 288 bytes more than their count.

The recorded sounds are the speaker's whatever plays the music: an AdLib card of 1990 had no
way to play a recording, and the game has no other. Three turns into the game Joey Rottenwood
comes up the stairs, and his laugh is `ROTTEN3.RS`, on the speaker.

Two rules of the game's decide whether a sound is heard, and both were found by a sound that
wasn't:

- **No recording while a song plays.** The routines that load a sound and play it, at
  `233C:0455` and `233C:0476`, do nothing while bit 40 of the byte at `4517:9126` is set, and
  it is set for as long as a song is playing: the music's driver and RealSound both want the
  PC's timer. The song that starts the game lasts most of a minute. Get to Rottenwood sooner
  and there is music and no laugh.
- **Any key stops a sound.** While a sound plays, the keyboard's interrupt goes to RealSound's
  own handler, which ends the sound and leaves a space for the game to read as a key. It
  doesn't look at what the keyboard sent: a key let go ends a sound as surely as a key
  pressed.

The second has a consequence [that took a disk to see](the-pc.md#a-laugh-and-a-disk). The
laugh is started by a key, the one pressed at the prompt for more that the scene stops at, and
that key must be let go before the laugh starts or it ends it.

The speaker has two states, in and out, and no levels between. RealSound gets levels from it
by time: it sends a pulse 16,572 times a second, faster than can be heard, and how long each
pulse lasts is how far the speaker's cone gets before it's let go. The game's player, at
`24AF:0028`, sets the PC's timer to interrupt every 72 ticks, and at each interrupt gives the
speaker's own timer the next byte of the sound as the pulse's length. So a byte is from 0 to
72, and a zero ends the sound. Playing each byte for 2 pulses or for 4 halves or quarters the
speed, which is the header's count: the sounds are at 16,572, 8,286 and 4,143 bytes a second.

The [tools](../tools/README.md) write the songs out as `.mid` and `.rol` files, and the
sounds as WAV files, a pulse's length as a level. The WAV files are made as the player's code
says they should be; they haven't been listened to.

## The mouse

The game's code for the mouse is its own segment, `167E`, and everything else asks it. It
keeps one word, at `4517:8610`, that says what kind of pointing there is and whether the
pointer is up:

| Bits | Meaning |
|---|---|
| 01h | The pointer is on the screen |
| 02h | No mouse's driver: the pointer is the game's, moved without one |
| 04h | A mouse, with the game drawing the pointer: the word `xmouse` |
| 08h | A driver that says it has no mouse. The game asks it for the pointer and the buttons all the same |
| 10h | A mouse, with the driver drawing the pointer |

`167E:0002` sets it at the start. It first reads where INT 33h points: nowhere, or at an
`IRET`, is a PC with no driver, and the word is 2. Otherwise it calls the driver's functions
20h and 0, to turn it on and reset it, and an answer other than FFFFh, with no `xmouse`
said, makes the word 8. What that case was for isn't known: the driver has said there is no
mouse, and the game goes on asking it for buttons. With a mouse the word is 10h, or 4 with
`xmouse`.

With an ordinary driver the game draws nothing for the mouse. `167E:02CF` shows the pointer and `167E:0407` hides it, by
the driver's functions 1 and 2, and the game hides it around everything it draws, as a driver
of the day needed: the arrow was in the video card's memory with the picture. With `xmouse`,
or with no driver, the same two routines save what is under the pointer and draw one; the
word is there, presumably, for a driver that couldn't draw on an EGA. `167E:059B` gives where the mouse is and
whether a button is down, and `167E:0618` whether it has moved, from the driver's count of
its steps.

A player at the prompt is in one of two states, and the pointer says which.

- **Typing**, at `0C35:4785`. The routine starts by hiding the pointer and showing the text's
  cursor, and then waits for a key or a button. The mouse can be moved all day and nothing
  shows. A click is taken as a Tab, and a Tab ends the typing.
- **Picking**, at `0C35:4922`. It shows the pointer and waits on the lists of verbs and
  objects, the compass and the buttons. If the pointer is down while it waits, it comes back
  when the mouse moves.

So the first click after typing picks nothing: it brings the arrow up. It looks like a fault
and is how the game was. A word at `4517:7764` is 1 in place of the mouse when the mode's
bits 02h or 08h are set, and sends the picking to a routine at `0C35:5719`, not yet read.

Keys typed early aren't lost, either. The game opens on two pages of writing, each up for
a time: some 35 seconds for the first and 20 for the second, and then the room, with nothing
pressed. A letter ends both: the wait, at `0C35:32F5`, asks whether a key is there
(`183B:0002`) and doesn't read it, the room's description then goes by without its prompt for
more, and the letter is the first at the command prompt. A player who knows the game types
the first command over the first page. A space is different, by what the game does and not
yet by its code: it's used up, and ends only the page it is pressed at.

## Still to do

- What picks the block in the three calls that take it from a table, and where blocks 19, 39
  and 40 are loaded.
- What a routine's answer means beyond zero, and 2 from performing; the reasons from 8 to 14.
- The rest of the actions' bytes.
- Of the timers: why the long line sends you to Batguano Court, what the two variables are that a timer forgets after a turn, and the rest of the routine that starts a day, at `07EC:0B9F`. Who has the timers run several turns at once, and what stops one from running for a turn.
- The rest of an object's record: the fields past the tree. Which of a thing's lists is which, beyond [the second being what's on it](#what-the-flags-are) when it has flag 33.
- What tells flag 26 from flag 27, and what the 80 things with flag 32 are.
- How the far pointers in a saved game are made right, and what a saved state's last part counts.
- Where the system ends and the game begins among the root's segments and the overlays.
- How the game names a picture, and the word in a picture's entry that isn't understood.
- The word a pair of songs shares, and which song and which sound go with what in the game.
