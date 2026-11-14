# Notes

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
  point at.
