# Notes

How each game was dug into: what it is on disk, how its program is built, and what its files
hold.

- [Spellcasting 101: Sorcerers Get All the Girls](spellcasting-101.md): Legend's first game,
  1990. A C program with its story compiled into it, too big for the PC's memory and so cut
  into overlays by PLINK86, with even its data loaded as one; text packed twice over, by
  word and then by Huffman's codes, and all of it unpacked, with each string named only by its place in an
  array a block is loaded into; a vocabulary, and the world's first state kept in a file.
