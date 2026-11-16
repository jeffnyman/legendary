# Timequest

Legend Entertainment, 1991. Bob Bates's game, and Legend's second, between
[*Spellcasting 101*](spellcasting-101.md) and
[*Spellcasting 201*](spellcasting-201.md) on the same system.

This is what the first look found: how much of it is the first game's, how much is already
the third's, and what is its own. What is known for certain is said plainly; what is
inferred says so. Addresses are `segment:offset` in hex, with segments relative to where the
program loads. Every one of them can be looked at with the [tools](../tools/README.md).

## How the program is built

As the first game is: **Microsoft C** in the large model, linked by **PLINK86**. `TQ.EXE` is
what the player runs, and `TIMEMAIN.EXE` is the game, with its overlays in `TIMEMAIN.OVL`.

| What | Where |
|---|---|
| The root | `0000` to `3646`: 222,304 bytes, with 6,761 relocations |
| The loader | `333D`, and the program starts in it, at `333D:04C0` |
| Its table | `33F0`, with 768 thunks after it |
| Overlays 1 to 18 | all loaded at `3646`, one at a time, from `TIMEMAIN.OVL` |
| Overlay 19 | `3C2B` to `51E7`, from `TIMEMAIN.EXE` itself, loaded at the start: the data |
| The data segment | `4333`, where Microsoft's notice is |

There is one area for the overlays' code where the first game had more, so only one
overlay of code is ever in memory.

## What is the first game's

These are read by what was written for *Spellcasting 101*, with nothing changed:

- **The text.** `TIMESTR.DAT` has 120 blocks and 1,475 strings, 202,598 bytes packed into
  101,330, by words and then by Huffman's codes, with 128 words and a tree of 210 nodes. A
  block is loaded into an array and a string is named by its place there. Most of the calls
  that load a block take its number from a variable here, where the first game's had
  constants.
- **The vocabulary.** `VOCAB.DAT` is plain, as the first game's is: 1,296 words.
- **The grammar.** 354 forms that make 165 actions, in the first game's narrower table.
- **The parser's own grammar**, with its rules still named.
- **The routines.** The switch that calls an action's routine and the eight that call an
  object's are found the same way: 165 actions, and 249 of the objects have a routine.
- **The names.** An object's two names are far pointers into the program, not places in a
  file. There is no `OBJECT.DAT`.
- **The flags.** A flag's number is its bit: there is no table of them.
- **The pictures, their regions, and the sounds.**

## What is already the third game's

- **The exits.** A room no longer has a far pointer to its own rows. Every room's exits are
  in one table, and the room has the number of its first row and how many.
- **The AdLib music.** A song for the AdLib is a `.MDI`, a MIDI file that carries its own
  instruments, though the table at the start of a `.MUS` file is still the first game's ten
  bytes to an entry. A file's table has 120 entries, where the first game's has 30.

## The objects

518 of them, in the first game's eight types: 79 rooms, 127 things, 73 that are only words,
and so on. The table of types is at `29E5:0000`, three bytes to a type; the table of objects
at `29E5:0018`, six bytes to an object; their count at `4333:05DA`; and the records from
`2EE7:0000`.

Four types' records are two bytes bigger than the first game's, and a room's is the same
size with different things in it:

| Type | Bytes | Bytes of flags | What follows the flags and the two lists of words |
|---|---|---|---|
| 1, a room | 28 | 4 | `10` its list, `12` not known, `14` what's also here, `16` what's seen from it, `18` how many exits, `1A` the first of them |
| 2, a thing | 36 | 6 | `12` its place, `14` relation, `16` next, `18` two lists, `1C` and `22` not known |
| 3 | 14 | 2 | nothing |
| 4 | 22 | 2 | `0E` two lists |
| 5 | 24 | 4 | `10` two lists |
| 6, a door | 28 | 4 | `10` two places, `14` two lists |
| 7, a person | 34 | 4 | `10` its place, `12` relation, `14` next, `16` three lists, `1C` not known |
| 8 | 24 | 4 | `10` its place, `12` relation, `14` next, `16` not known |

So the two bytes are flags: every type that had 16 of them has 32, but for the two that are
only words. A thing keeps its 48. The room paid for its own two by giving up the far pointer
to its exits for a word.

This is from the routines that read and write each field, which are in segment `01B9` from
`1B01` and switch on the object's type as the first game's do: the nouns at `1C24`, the
adjectives at `1CA8`, a place at `1D2C`, the relation at `1E71`, a list at `1F4D`, the next
at `2116`, and a room's four at `254F`, `25A6`, `25FD` and `2654`.

The words an object is called by are in two lists, the nouns at `29E5:0C3C` and the
adjectives at `29E5:18F8`. What's also in a room is at `29E5:1EE6`, and what's seen from it
at `4333:05DC`, both kept as the first game keeps them.

## The exits

294 rows of four bytes at `29E5:2352`: a direction, a kind, and a word. The routine that
follows one is at `0AAD:0442`, and it switches on the kind:

| Kind | Rows | The word is |
|---|---|---|
| 1 | 125 | the room it leads to |
| 2 | 5 | a door's row at `4333:05EC`: the room, and the door, which is passable when it has flag 11 |
| 3 | 16 | a row of far pointers at `29E5:28AF`: the text the exit says, and it leads nowhere |
| 4 | 121 | a routine's number, from 0 to 52: `0743:0006` is called with it, switches on it, and answers with a room |
| 5 | 27 | a row of eight bytes at `29E5:28EF`: a room, a variable in the data segment, and a far pointer to text |

Kind 5 is new, and *Spellcasting 201* doesn't have it. The exit leads to its room when the
byte the variable names isn't zero. When it is zero, the exit says its text, or `You can't go
that way.` if it has none. So a passage that opens during the game needs no routine: the
game sets a byte.

Kinds 1 to 4 are the first game's, with the far pointer each row had there replaced by a
number, which is how *Spellcasting 201* has them. That game dropped kind 3 as well, and
gives the text to a routine.

## Still to do

- The routines that keep the timers aren't found: they aren't where the first game's are,
  and the timers aren't numbers as the third's are.
- The flags have no names yet. Flag 11 is open, from the routine that follows an exit.
- A room's word at `12`, a thing's at `1C` and `22`, a person's at `1C`, and type 8's at `16`.
- The saved game's layout: there is no `RESTART.DAT` to check it against until the game
  is run.
- The player doesn't watch it: `--watch` and `--panel` need the map of where its world is.
