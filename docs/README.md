# Notes

[The PC](the-pc.md) is the machine the games are run on to be watched: where it came from, and
what each game has asked of it.

How each game was dug into: what it is on disk, how its program is built, and what its files
hold.

- [Spellcasting 101: Sorcerers Get All the Girls](spellcasting-101.md): Legend's first game,
  1990. A C program with its story compiled into it, too big for the PC's memory and so cut
  into overlays by PLINK86, with even its data loaded as one; text packed twice over, by
  word and then by Huffman's codes, and all of it unpacked, with each string named only by its
  place in an array a block is loaded into; a vocabulary of 1,736 words with their parts of speech and synonyms; a parser that
  matches commands against a grammar of English kept as data, its rules still named as its
  writers named them, and finds its objects by Infocom's own rules of where to look; and a grammar of 376
  forms that turns a command into one of 207 actions, each performed by asking the room's,
  the actor's and the objects' own routines before the action's, in Infocom's order, from a main loop that takes
  what's needed first and runs the clock after; and a world of 427 objects in eight
  types, kept in a tree as Infocom's were but with several lists to a parent, with its first
  state in a file; and 362 pictures, each drawn for the EGA and again for the CGA, packed by
  an LZW of pixels and by Huffman's codes, with the rectangles of each that the mouse can
  point at; music kept as MIDI files and as AdLib's own, and sounds in RealSound for the
  PC's speaker.
- [Timequest](timequest.md): 1991, between the two. Built as the first game is, and read
  by what reads it: its text, vocabulary, grammar and routines. Its 518 objects have twice the
  flags, and its rooms' exits are already in one table, as the next game's are, with a kind
  of exit of its own that a variable opens.
- [Spellcasting 301: Spring Break](spellcasting-301.md): 1992. A first look: linked by a
  newer .RTLink whose file is the older one's exactly, with an overlay manager that keeps its
  thunks outside its own segment; 36 overlays and 949 thunks. A vocabulary of 1,801 words, a grammar of 458 forms that
  makes 249 actions, and 517 objects kept as the game before keeps them, each with the
  number of its routine, and the actions' routines in a table too. Its text and pictures are each packed a new way, and aren't read yet.
- [Spellcasting 201: The Sorcerer's Appliance](spellcasting-201.md): 1991. The same system,
  linked by .RTLink/Plus in place of PLINK86: a program DOS can't load by itself, kept as
  records of its segments with their fixups, which the linker's own startup code puts in
  place; a run-time library the launcher and the game share, linked in as the program loads;
  and 22 overlays reached by 790 thunks; a vocabulary of 1,609 words, packed by
  Huffman's codes; and a world of 482 objects, with their names moved out to a file, their
  flags looked up in a table, and every room's exits in one; a grammar of 405 forms that makes
  226 actions; the routine of each action and of 445 of the objects; timers that are numbers;
  and text mostly left plain in the overlays, with 1,815 longer strings packed in a file and
  named by one number each; and music whose AdLib songs are MIDI files that carry their own
  instruments.
