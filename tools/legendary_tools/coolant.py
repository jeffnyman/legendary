"""Mission Critical's CSP.DAT: paths across a picture, which read as its reactor's coolant.

The file starts with 77 longs, where each path starts in it. A path is steps, each a byte
and what the byte takes, up to a step that goes on to another path:

    0, 1  nothing after them; not yet understood
    2     a word: go on to that path. Path 74 is this step and nothing else, to itself
    3     two words: be at a place, across and down
    4     two words: go to a place in a straight line
    5     two words, not yet understood: 13 and 13 for most, 0 and 0, or a number and 2
    6     a word: wait that long
    7     two words: where something is set a way, 0 to 6, go on to that path

The places are from 8 to 618 across and 93 to 946 down, and the lines are all straight up,
down or across: pipes. The routine that loads the file is beside the words `WARNING: System
breach` and `- Recalculating -`, and by the names `Engine Coolant Grid` and `Automatic
Valve`; the game has a puzzle of the reactor's coolant, and step 7 reads as a valve and which
way it is turned. That is a reading, and hasn't been followed in the program.
"""

from __future__ import annotations

import struct

END, PLACE, LINE, SET, WAIT, BRANCH = 2, 3, 4, 5, 6, 7
_WORDS = {0: 0, 1: 0, END: 1, PLACE: 2, LINE: 2, SET: 2, WAIT: 1, BRANCH: 2}


def read_paths(data: bytes) -> list[list[tuple[int, ...]]]:
    """Each path's steps: a step is its byte and its words."""
    if len(data) < 4:
        raise ValueError("not a file of paths: it is too short")
    (first,) = struct.unpack_from("<I", data, 0)
    if first % 4 or not 4 <= first <= len(data):
        raise ValueError("not a file of paths: its table doesn't end where its first path starts")
    paths = []
    for start in struct.unpack_from(f"<{first // 4}I", data, 0):
        steps: list[tuple[int, ...]] = []
        at = start
        while True:
            if at >= len(data) or data[at] not in _WORDS:
                raise ValueError("not a file of paths: a step isn't one that's known")
            words = _WORDS[data[at]]
            if at + 1 + 2 * words > len(data):
                raise ValueError("not a file of paths: a path goes past the file's end")
            steps.append((data[at], *struct.unpack_from(f"<{words}H", data, at + 1)))
            at += 1 + 2 * words
            if steps[-1][0] == END:
                break
        paths.append(steps)
    return paths
