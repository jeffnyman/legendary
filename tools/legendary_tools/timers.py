"""The game's timers: the routines it has put by to run on a later turn, or on every turn.

The game keeps a table of up to sixteen timers, six bytes each: a far pointer to a routine,
and a count. After each turn that takes time, one routine goes down the table. A timer whose
count is above zero has it taken down by one. A timer whose count is zero or less has its
routine called, and afterward is taken out of the table, unless its count is -1, which is a
routine that runs every turn, or the routine has put itself back with a new count.

Four routines beside that one keep the table: one empties it, one puts a routine in it with a
count or gives one already there a new count, one takes a routine out, and one says whether a
routine is in it. The game's source called them with the routine as a constant, so every call
says which timer it is about, and whose routine the call is in says who starts and stops it.

A timer's routine is in the root, where the table's far pointer can always find it. Most are
six bytes there, a call to the routine that does the work, in an overlay, and a return.

*Spellcasting 201* numbers its timers. Its table has four bytes for each of the sixteen, a
timer's number and its count, and a second table has a far pointer for each number, which
is the routine or a thunk to it. A fifth routine there gives a timer's count.

*Timequest*, between them, already numbers its timers and keeps the same table of four
bytes. It has no table of routines: the routine that runs the timers calls one that switches
on a timer's number, and each case is a far call to the timer's routine.
"""

from __future__ import annotations

import re
import struct
from dataclasses import dataclass

from .flags import CodeReader, routine_labels
from .handlers import _Code
from .program import Program

# Spellcasting 101's: the segment the routines are in, and where each starts
SEGMENT = 0x1272
ROUTINES = {0x000E: "empties", 0x0033: "starts", 0x00BF: "stops", 0x013E: "asks about"}
# where the table is, how many it holds, and where in the data its count of timers is
TABLE = (0x3B00, 0x4F30)
MOST = 16
COUNT_OFFSET = 0x7858
EVERY_TURN = -1
# a name for each, from reading its routine
TIMER_NAMES = {
    (0x07EC, 0x04B4): "the simulation ends",
    (0x07EC, 0x06D9): "the stepfather on the stairs",
    (0x07EC, 0x06DF): "the man with the whip",
    (0x07EC, 0x06E5): "the potty door closes",
    (0x07EC, 0x06EB): "the voice at the gate",
    (0x07EC, 0x06F1): "Miss Beancounter's spectacles",
    (0x07EC, 0x06F7): "the amazon leader",
    (0x07EC, 0x06FD): "the amazons' visits",
    (0x07EC, 0x0703): "the chambermaid",
    (0x07EC, 0x0709): "the waiter comes",
    (0x07EC, 0x070F): "the waiter brings it",
    (0x07EC, 0x0715): "the islanders gather",
    (0x07EC, 0x071B): "the islanders' thanks",
    (0x07EC, 0x0721): "Ocarina at home",
    (0x07EC, 0x0727): "Glockenspiel comes back",
    (0x07EC, 0x072D): "the gods quarrel",
    (0x07EC, 0x0733): "the gold gate shuts",
    (0x07EC, 0x0739): "the silver gate shuts",
    (0x07EC, 0x073F): "the bronze gate shuts",
    (0x07EC, 0x0745): "the ending",
    (0x07EC, 0x074B): "to Batguano Court",
    (0x07EC, 0x075A): "the role-players' game",
    (0x07EC, 0x0760): "meals at the Cafeteria",
    (0x07EC, 0x0766): "the Stadium's day",
    (0x07EC, 0x076C): "the professor tires",
    (0x07EC, 0x0772): "the party starts",
    (0x07EC, 0x0778): "the party's chatter",
    (0x07EC, 0x077E): "Gretchen with you",
    (0x07EC, 0x0784): "Gretchen arrives",
    (0x07EC, 0x078A): "the fratman",
    (0x07EC, 0x07D2): "dinner with Hillary",
    (0x07EC, 0x07D8): "the class bell",
    (0x07EC, 0x07DE): "the dinner invitation",
    (0x07EC, 0x07E4): "Tickingclock comes in",
    (0x07EC, 0x07EA): "the lecture",
    (0x07EC, 0x07F0): "the day's first look",
    (0x07EC, 0x089F): "a word forgotten",
    (0x07EC, 0x08AD): "a byte forgotten",
    (0x07EC, 0x08BA): "FRIMP wears off",
    (0x07EC, 0x09F3): "sleepiness",
    (0x07EC, 0x128A): "ZEM wears off",
    (0x07EC, 0x12A3): "the dragon's breath",
    (0x07EC, 0x12A9): "the pendulum",
    (0x09BB, 0x0D1E): "the red button resets",
    (0x09BB, 0x0D26): "the surfboard's trip",
    (0x3670, 0x0899): "the registration line",
}


# mov es,[segment variable] / call far es:[bx-4], with BX four times a timer's number: its
# routine, called from the table of them
# In Gateway II, from a later compiler, BX is made between the two: mov bx,[bp+6] /
# add bx,bx / add bx,bx
_BY_NUMBER = re.compile(
    rb"\x8e\x06(..)(?:\x8b\x5e.\x03\xdb\x03\xdb)?\x26\xff(?:\x9f\xfc\xff|\x5f\xfc)", re.DOTALL
)
# mov es,[segment variable] / cmp word es:[bx+count],-1: is the timer one that runs every
# turn? The routine that runs the timers asks it first
_EVERY_TURN = re.compile(rb"\x8e\x06..\x26\x83\xbf..\xff", re.DOTALL)
# push [bp-n] / call far / add sp,2: a routine called with one argument
_CALLED_WITH_ONE = re.compile(rb"\xff\x76.\x9a(....)\x83\xc4\x02", re.DOTALL)
_PROLOGUE = b"\x55\x8b\xec"
# what the routines are where timers are numbers, in the order they come in their segment,
# before the one that runs them
_KINDS = ("starts", "stops", "asks about", "asks the count of")
# xor ax,ax / call far: how the one before them starts, which empties the table and has no
# arguments or variables to make a frame for
_FRAMELESS = b"\x33\xc0\x9a"
# mov ax,40h / push ax / sub ax,ax / push ax: how it starts where the compiler was told to
# leave out the check of the stack, with the size of the table to clear
_FRAMELESS_UNCHECKED = b"\xb8\x40\x00\x50\x2b\xc0\x50"
# and the same from the later compiler, which clears a register with xor
_FRAMELESS_UNCHECKED_LATER = b"\xb8\x40\x00\x50\x33\xc0\x50"
# Companions of Xanth's clears the table itself: push di / xor ax,ax / mov es,[variable] /
# mov cx,20h / mov di,<table> / rep stosw
_CLEARS_ITSELF = re.compile(rb"\x57\x33\xc0\x8e\x06..\xb9\x20\x00\xbf..\xf3\xab", re.DOTALL)
_NOTICE = b"MS Run-Time Library"


@dataclass(frozen=True)
class Timers:
    """Where a game's timers are kept: the routines' segment, each routine, and the numbered ones."""

    segment: int
    routines: dict[int, str]  # where each routine starts in memory, and what it does
    numbered: (
        tuple[tuple[int, int], ...] | None
    )  # the routine of each number, from 1, where they have numbers


def find_timers(program: Program) -> Timers:
    """The routines that keep the timers, where Spellcasting 101 has them or by the timers' numbers."""
    image = program.image()
    memory = image.memory
    framed = [offset for offset, kind in ROUTINES.items() if kind != "empties"]
    if all(memory.startswith(_PROLOGUE, SEGMENT * 16 + offset) for offset in framed):
        return Timers(SEGMENT, {SEGMENT * 16 + offset: kind for offset, kind in ROUTINES.items()}, None)

    root = memory[: program.root_size]
    by_number = _BY_NUMBER.search(root)
    every_turn = _EVERY_TURN.search(root)
    notice = memory.find(_NOTICE)
    if not (by_number or every_turn) or notice < 0:
        raise ValueError("the routines that keep the timers weren't found")
    segments = sorted(image.segment_references())

    def segment_of(linear: int) -> int:
        return max(segment for segment in segments if segment * 16 <= linear)

    numbered: list[tuple[int, int]] = []
    if by_number:
        # the routine that calls a timer by its number, and the one call to it, from the
        # routine that runs the timers
        start = root.rindex(_PROLOGUE, 0, by_number.start())
        base = segment_of(start)
        call = root.find(bytes([0x9A, *struct.pack("<HH", start - base * 16, base)]))
        if call < 0:
            raise ValueError("the routine that runs the timers wasn't found")
        segment = segment_of(call)
        runs = root.rindex(_PROLOGUE, segment * 16, call)
        table = image.word(notice - 8 + int.from_bytes(by_number[1], "little")) * 16
        while table + 4 * len(numbered) + 2 in image.relocations:
            offset, target = struct.unpack_from("<HH", memory, table + 4 * len(numbered))
            numbered.append((target, offset))
    else:
        # the routine that runs the timers, by its test
        assert every_turn is not None
        runs = root.rindex(_PROLOGUE, 0, every_turn.start())
        segment = segment_of(runs)
        numbered = _switched(program, runs, root.index(b"\x5d\xcb", every_turn.start()))
    starts = [
        match.start()
        for match in re.finditer(re.escape(_PROLOGUE), root[:runs])
        if match.start() >= segment * 16
    ]
    if len(starts) > len(_KINDS):
        # Companions of Xanth has them in a segment with most of its code: the four before
        # the one that runs them, and before those the one that empties the table
        starts = starts[-len(_KINDS) :]
        cleared = [found.start() for found in _CLEARS_ITSELF.finditer(root, segment * 16, starts[0])]
        if not cleared:
            raise ValueError("the routine that empties the timers' table wasn't found")
        return Timers(segment, {cleared[-1]: "empties", **dict(zip(starts, _KINDS))}, tuple(numbered))
    empties = root.find(_FRAMELESS, segment * 16, starts[0] if starts else runs)
    for other in (_FRAMELESS_UNCHECKED, _FRAMELESS_UNCHECKED_LATER):
        if empties < 0:
            empties = root.find(other, segment * 16, starts[0] if starts else runs)
    # the one that gives a timer's count came later: Timequest has the three before it
    if len(starts) not in (len(_KINDS) - 1, len(_KINDS)) or empties < 0:
        raise ValueError("the routines that keep the timers aren't the ones that are known")
    routines = {empties: "empties", **dict(zip(starts, _KINDS))}
    return Timers(segment, routines, tuple(numbered))


def _switched(program: Program, runs: int, end: int) -> list[tuple[int, int]]:
    """
    Each numbered timer's routine, where the routine that runs the timers calls one that
    switches on a timer's number: each case is a far call to the timer's routine.
    """
    image = program.image()
    memory = image.memory
    code = _Code(program)
    cases: dict[int, int] = {}
    for called in _CALLED_WITH_ONE.finditer(memory, runs, end):
        offset, base = struct.unpack("<HH", called[1])
        if called.start(1) + 2 in image.relocations and (cases := code.cases(base * 16 + offset)):
            break
    if not cases or min(cases) != 1:
        raise ValueError("the routine that calls a timer by its number wasn't found")
    numbered = []
    for number in range(1, max(cases) + 1):
        at = cases.get(number, 0)
        if memory[at] == 0x9A and at + 3 in image.relocations:
            offset, target = struct.unpack_from("<HH", memory, at + 1)
            numbered.append((target, offset))
        else:
            numbered.append((0, 0))
    return numbered


# Spellcasting 201's, by number, from the same reading: most from what its routine prints
TIMER_NAMES_201 = {
    1: "the day's initiation",
    2: "the upperclassmen find you",
    3: "the serpent heads speak",
    4: "the simulation ends",
    5: "sleepiness",
    6: "the class bell",
    7: "the tower clock chimes",
    8: "nothing",
    9: "a word forgotten",
    10: "a byte forgotten",
    11: "what was raised sinks back",
    12: "the simpleberries wear off",
    13: "the speed potion wears off",
    14: "the mascot swells back",
    15: "the party starts",
    16: "you open your eyes",
    17: "the Brus make ready",
    18: "the Poolside Party",
    19: "Hiddenmolar and Cowpatty whisper",
    20: "word of Tickingclock's death",
    21: "Tickingclock's errand",
    22: "Tickingclock goes",
    23: "the sewer gurgles",
    24: "the last newspaper goes",
    25: "a second byte forgotten",
    26: "the Appliance runs",
    27: "the Sorcerer returns",
    28: "the banana fetches water",
    29: "the army of bananas",
    30: "a knock from the yard",
    31: "twelve bytes forgotten",
    32: "everyone out of the pool",
    33: "the Welcome Back Party fills",
    34: "the Royal Parade",
    35: "the hellhounds come",
    36: "the bier is brought in",
    37: "you are yourself again",
    38: "Hiddenmolar tells all",
    39: "the messenger",
    40: "the cloak is finished",
    41: "the cloak is brought",
    42: "the iron turns to copper",
    43: "the copper dissolves",
    44: "the hanging",
    45: "the cook and the hors d'oevres",
    46: "the tree is a sapling again",
    47: "the Social Hall, turn by turn",
    48: "a count down by sixes",
    49: "seen in the Ancient Lab",
}


# Timequest's, by number, the same way: from the strings its routine prints, of the block
# of text its place and time load, or from what it moves
TIMER_NAMES_TIMEQUEST = {
    1: "the sultan presents you to Yahya",
    2: "the sultan gives up on you",
    3: "the wives leave off",
    4: "the wives go to bathe",
    5: "the stolen veil is missed",
    6: "a rustling behind the wall",
    7: "the sultan is heard outside",
    8: "the sultan finds you under the bed",
    9: "the sultan spares you",
    10: "a soldier grabs you in the bazaar",
    11: "the mule lowers his ears",
    12: "the soldiers search the House",
    13: "Vettenmyer bursts in",
    14: "Vettenmyer tells the key's secret",
    15: "footsteps on the stairs",
    16: "another you comes in",
    17: "Vettenmyer grabs the laser",
    18: "Vettenmyer fires at you",
    19: "Vettenmyer fires at the other you",
    20: "Vettenmyer laughs last",
    21: "Vettenmyer waits for the other you",
    22: "the other you materializes",
    23: "the other you stops the Doomsday Device",
    24: "Vettenmyer is met in Rome",
    25: "the riddle's word was never said",
    26: "Drake goes up to the queen",
    27: "the strumpet is thrown out",
    28: "Churchill's broadcast",
    29: "what's seen at the Tavern",
    30: "the courier calls for a horse",
    31: "the courier gallops past",
    32: "the courier is ambushed",
    33: "the courier rides by",
    34: "the outlaws melt into the forest",
    35: "the courier stirs",
    36: "the courier comes to",
    37: "the cleric examines the letter",
    38: "the courier reaches Runnymede",
    39: "the cleric reads the letter to King John",
    40: "an arrow from the trees",
    41: "the Shed, turn by turn",
    42: "a hue and cry past the shed",
    43: "the sky brightens",
    44: "the sun comes up",
    45: "the pope's book, every ten turns",
    46: "nothing",
    47: "a man on a bench speaks",
    48: "the focaccia man comes by",
    49: "the focaccia man wanders off",
    50: "the parade of lions begins",
    51: "the lion breaks away",
    52: "the lion charges",
    53: "the lion kills",
    54: "the chariot race",
    55: "the Circus empties",
    56: "Napoleon's aide argues",
    57: "Napoleon is overheard",
    58: "Napoleon dismisses the aide",
    59: "Napoleon decides",
    60: "the hanky dries",
    61: "the priest misses the crown",
    62: "the pope comes for the crown",
    63: "the shout for Charlemagne",
    64: "the pope sets out",
    65: "the pope asks for Attila",
    66: "a barbarian strides into the tent",
    67: "Attila comes out of the tent",
    68: "war cries outside the tent",
    69: "Attila's patience ends",
    70: "Pope Leo comes out of the Vatican",
    71: "Pope Leo comes down the road",
    72: "the pope is shoved into the tent",
    73: "Hitler and Mussolini at the radio, turn by turn",
    74: "Hitler gets the radio going",
    75: "Hitler says to hop to it",
    76: "Hitler throws you out",
    77: "Mussolini asks the word",
    78: "Vettenmyer sneers",
    79: "the old man stares",
    80: "Vettenmyer grows agitated",
    81: "Vettenmyer's look of hatred",
    82: "Vettenmyer dies",
    83: "the old man's last breath",
    84: "the ether evaporates",
    85: "the surroundings melt away",
    86: "the bracelet glows and goes",
    87: "Vettenmyer steps out of the wall",
    88: "the general's orders",
    89: "the general says to stop wasting time",
    90: "the Olmec sneaks a look",
    91: "the Olmec knocks you out",
    92: "the Olmec's knife is sharp",
    93: "the Olmec raises his knife",
    94: "the eclipse",
    95: "the eclipse ends",
    96: "the Olmec throws his spear",
    97: "the spear hits",
    98: "a messenger to Montezuma",
    99: "Montezuma and the mural",
    100: "Montezuma's messenger returns",
    101: "the priest's sacrifice",
    102: "the priest sees an imposter",
    103: "the priest's knife",
    104: "the sacrificial priest comes in",
    105: "the mourners file into the cave",
    106: "the mourners tire of waiting",
    107: "the mourners drink the poison",
    108: "the Mongol leader leaves the tent",
    109: "Genghis storms back",
    110: "the women come back",
    111: "the funeral procession, every ten turns",
    112: "the barbarian army",
    113: "the fortune cookie",
    114: "nothing",
    115: "the girl",
    116: "Cleopatra's sedan chair, every hour",
    117: "nothing",
    118: "a word cleared",
    119: "Caesar congratulates you",
    120: "a rock from the window",
    121: "from the Royal Palace to the Royal compound",
    122: "Vettenmyer's welcome",
    123: "the vizier overthrows the sultan",
    124: "Napoleon has you taken away",
}


# Spellcasting 301's, by number, from the strings its routine prints
TIMER_NAMES_301 = {
    1: "sleepiness",
    2: "two words forgotten",
    3: "a byte forgotten",
    4: "what was raised settles back",
    5: "you open your eyes",
    6: "a count down by sixes",
    7: "the carpet is loaded, turn by turn",
    8: "the carpet flies east, turn by turn",
    9: "the carpet spirals down",
    10: "the world's most beautiful woman passes",
    11: "Fred fetches you to the lobby",
    12: "Sid takes you to the beach, turn by turn",
    13: "Moe calls it a day",
    14: "Gary says the bash is near",
    15: "the bimbos follow the studfinder",
    16: "The Judge passes through the lobby",
    17: "The Judge comes to the party",
    18: "the cell's bars rust away",
    19: "the studfinder hums",
    20: "The Judge judges the sand castles",
    21: "The Judge's event at the bandstand, turn by turn",
    22: "the mouse race",
    23: "the event at the pool, turn by turn",
    24: "everyone comes into the bar, turn by turn",
    25: "the Sheriff jails you",
    26: "BIGFINNO wears off",
    27: "the Soda-Barfing Contest, turn by turn",
    28: "the drawing of straws, turn by turn",
    29: "the laxative works",
    30: "the Sheriff jails you, from the arena",
    31: "the volleyball face-off, turn by turn",
    32: "the Yus swagger onto the beach",
    33: "the showdown on the boulevard, turn by turn",
    34: "the magic mushrooms come out",
    35: "the mushrooms take hold, turn by turn",
    36: "the tan fades",
    37: "Moe's turn",
    38: "Vince's turn",
    39: "Ralph's turn",
    40: "Ollie's turn",
    41: "Izzie's turn",
    42: "Fred's turn",
    43: "the Pharts come to the Dance Club",
    44: "the building shakes",
    45: "the baby squid is found",
    46: "the hunger pains",
    47: "the baby squid whimpers, every turn",
    48: "Joey shoots at your feet",
    49: "the dog digs under the fence",
    50: "Ollie says Fred has passed out",
    51: "Sid visits the cell",
    52: "a count down by fours",
    53: "the mama bull returns",
    54: "the carpet sputters",
    55: "your hostess in Room 1812 loses patience",
    56: "Snorty's nose clears",
    57: "Snorty pops the inflatable cow",
    58: "the crowds swarm to Blather Beach",
    59: "your order is called",
    60: "Moe says to get a move on",
    61: "the human pyramid comes apart",
    62: "the Copy Nymph flies in",
    63: "the moby berry shrinks",
    64: "the berry plant shrivels",
    65: "you feel filled with vigor",
}


# Gateway's, by number, from the strings its routine prints
TIMER_NAMES_GATEWAY = {
    1: "the woman leaves you the gold card",
    2: "the poker game, turn by turn",
    3: "the beast limps about",
    4: "the Dorman sun sets",
    5: "a byte changed",
    6: "two bytes forgotten",
    7: "it has dried off",
    8: "they have dried off",
    9: "the Ballroom, turn by turn",
    10: "the wheel turns, turn by turn",
    11: "the beast goes, with the last berries",
    12: "the boulder lands on you",
    13: "the portals flare",
    14: "the demon wanders, turn by turn",
    15: "the demons grow restless",
    16: "the hydra's head is knocked into the inferno",
    17: "the demon stops coughing",
    18: "the demon saws at the net",
    19: "tiredness, every thirty turns",
    20: "the hydra hunts for you",
    21: "the beast slams you against the wall",
    22: "the dust spills from the bag",
    23: "Thom Seldridge comes into the bar",
    24: "Terri Neilson comes to the bar",
    25: "the robot bartender comes over",
    26: "the robot bartender trundles away",
    27: "the robot bartender chirps",
    28: "Nubar snatches the drink",
    29: "Nubar plays the trivia game",
    30: "Terri Neilson at the bar, turn by turn",
    31: "the boots come round the planet",
    32: "nothing",
    33: "the prospectors fill the conference room",
    34: "the ship handling class ends",
    35: "a guard clears the room for a meeting",
    36: "the meeting ends",
    37: "a man comes down the corridor",
    38: "Perry scurries back",
    39: "Thom Seldridge leaves the bar",
    40: "Nubar Kamalian leaves the bar",
    41: "Terri Neilson leaves the bar",
    42: "a look at where you are, every thirty turns",
    43: "a word forgotten",
    44: "the MP arrests you at the Museum",
    45: "a creature scurries across the desert",
    46: "a yellow creature flits near",
    47: "the blue fog clears",
    48: "people stare below your navel",
    49: "the alien berries, turn by turn",
    50: "the old maintenance man appears",
    51: "the old man looks for his key",
    52: "the old man finishes and goes",
    53: "the portal disappears",
    54: "the Hydra lair's rock and path, every ten turns",
    55: "the spider robot comes for the waste",
    56: "the spider robot goes",
    57: "the robot's waste box",
    58: "the field actuator's hum rises",
    59: "something crashes through the anemones",
    60: "the spider decides",
    61: "the spider's pincers",
    62: "the enraged spider",
    63: "a worm bursts from the ground",
    64: "the worm chews at your helmet",
    65: "the pod's tongue",
    66: "your drink, every thirty turns",
    67: "the snake strikes",
    68: "the old man's room, every three turns",
    69: "it burrows out of sight",
    70: "it scampers out of view",
    71: "the bartender snores",
    72: "the salamander comes out of the lake",
    73: "the salamander goes",
    74: "a wailing from the east",
    75: "a wailing, further off",
    76: "a bubbling moan",
    77: "nothing",
    78: "the aliens file off",
    79: "the program insists, turn by turn",
    80: "the firestorm from the broken pyramid",
    81: "the tech goes back to his newsletter",
    82: "the chasm walls lurch, turn by turn",
    83: "at the Tunnel Antechamber",
    84: "the shoots grow around you",
    85: "the salamander comes up in the tank",
    86: "the cheeping stops",
    87: "Becker begins his story",
    88: "Becker's story ends",
    89: "a seed spins into the air",
    90: "it finishes the leaves and runs",
    91: "sleepiness, every ten turns",
    92: "the blister disappears",
    93: "Becker goes through the wooden door",
    94: "a leaf swats the air",
    95: "the raft sinks",
    96: "a sense of forgetting something",
    97: "an opening appears in the wall",
    98: "Becker sprints ahead",
    99: "what comes through Becker's door",
    100: "Becker asleep, turn by turn",
    101: "the tech explains the Guide",
    102: "your allotted time ends",
    103: "the banner, every ten turns",
    104: "Nubar Kamalian sits down next to you",
    105: "the Tanning Room, turn by turn",
    106: "Becker wants a nap",
    107: "Becker wakes",
    108: "the tan, every sixty turns",
}


# Eric the Unready's 72, each from what its routine prints: 3, 50 and 70 print nothing,
# and 65's routine wasn't read to its end
TIMER_NAMES_ERIC = {
    1: "the farmer fires you",
    2: "the farmer wants an answer",
    3: "a word forgotten",
    4: "Ed McDwarf gives back the sweepstakes notice",
    5: "you tire of walking on your knees",
    6: "the beard begins to itch",
    7: "the beard itches more",
    8: "you rip off the beard",
    9: "the wheel carries you round",
    10: "a gnu lumbers through",
    11: "the tavern's patrons and their Mead Lite",
    12: "Bruce the waiter",
    13: "the oaf's heads argue",
    14: "a raspberry at Blicester Castle",
    15: "the turtles close in",
    16: "the man in the stocks, turn by turn",
    17: "a boy and his father at the fair",
    18: "the storyteller's show",
    19: "Lily's show",
    20: "the jugglers come on",
    21: "the jugglers finish",
    22: "Sir Pectoral's thugs catch you",
    23: "the elf's shell game",
    24: "the musician wanders off",
    25: "the dragon flames, turn by turn",
    26: "the hanky dries out",
    27: "the virgin stops crying",
    28: "a unicorn comes for you",
    29: "the priestess searches, and the sacrifice begins",
    30: "a scream from the sanctuary",
    31: "Molochi smokes and burns, turn by turn",
    32: "words from the sanctuary",
    33: "the dragon's flame goes out",
    34: "the escalator carries you up",
    35: "a god greets you",
    36: "the cork pops",
    37: "Morty's door opens",
    38: "the receptionist speaks into her headset",
    39: "Clio comes back, and Morty finishes",
    40: "Morty shows you out",
    41: "the candles are put out",
    42: "the librarian fusses",
    43: "the library book disappears",
    44: "the library book is missed",
    45: "the slimewig wriggles away",
    46: "the slimewig kicks",
    47: "a salty burp",
    48: "the raft's voyage, turn by turn",
    49: "the whirlpool",
    50: "Gnoll Island's own",
    51: "the castaways of Milligan's Island talk",
    52: "a bottle floats in",
    53: "the hourglass's sand runs",
    54: "the wedding in the Great Hall, turn by turn",
    55: "the makeup eats through the chain",
    56: "the witch turns you inside out",
    57: "the castle falls",
    58: "the small voice hurries you",
    59: "your squire calls you out",
    60: "the Sergeant-At-Arms",
    61: "the knights' meeting",
    62: "the bard, for a copper penny",
    63: "the bard, for a silver penny",
    64: "the bard finishes his song",
    65: "the bard's song, which `listen` asks about",
    66: "the feasters' food fight",
    67: "the drunken knights fight",
    68: "the vanilla melts",
    69: "the delivery boy arrives",
    70: "the Shooting Gallery's own",
    71: "something is tossed to the jugglers",
    72: "the clock strikes noon, and the quest is over",
}


# Gateway II's 58, each from what its routine prints, or, for the ones that print nothing,
# from whose routines start it: 3, 4 and 21 are routines that do nothing at all
TIMER_NAMES_GATEWAY2 = {
    1: "a question, which yes and no end",
    2: "the pictures fade away",
    3: "nothing",
    4: "nothing",
    5: "the artist's own",
    6: "a diggle crawls by",
    7: "the hypo unit's own, every turn",
    8: "the crystal rod and the portal change color",
    9: "a button pops out",
    10: "a screen goes wild",
    11: "a tentacle grabs the kord",
    12: "the insect creatures attack, turn by turn",
    13: "the creatures enter the specimen cage",
    14: "the creatures leave the specimen cage",
    15: "the creatures in the cage slow and die",
    16: "the creatures scrabble again",
    17: "the green blob, turn by turn",
    18: "the carnivores in the clearing",
    19: "the fruit pod's own",
    20: "the lizard takes the red egg",
    21: "nothing",
    22: "the man with the thermos, turn by turn",
    23: "your Heechee escort hurries you on",
    24: "the lecture hall's light turns green",
    25: "your communicator pings",
    26: "your escort grows impatient",
    27: "the Heechee city's own",
    28: "the students and the hologram",
    29: "the ape men in the Cavern",
    30: "the ape men in the Grasslands",
    31: "the spear at the River Bank",
    32: "the alien robot comes, click-clack",
    33: "the guidething wakes and speaks",
    34: "the ship's computer speaks, turn by turn",
    35: "the Place of Seeing, turn by turn",
    36: "the field generator's own",
    37: "the metal disk seals, and you fall",
    38: "a probe crashes",
    39: "something falls from the sky",
    40: "the technicians flood out of the temple",
    41: "the technicians swarm in",
    42: "the speaker speaks",
    43: "the beams of light shift back",
    44: "the workthing and the case, turn by turn",
    45: "the workthing comes back from the vault",
    46: "the tram, turn by turn",
    47: "the prototype's power runs low",
    48: "the prototype's battery recharges",
    49: "sinking into the slime",
    50: "the PV goes dormant",
    51: "the escort and the red tag",
    52: "a voice in your condominium",
    53: "the terrorists' attack, turn by turn",
    54: "the grenade explodes",
    55: "the match's own",
    56: "the radio's messages, turn by turn",
    57: "the hijacking's own",
    58: "a strange creature emerges",
}


# Companions of Xanth's, by number: those whose routine says what it is, by what it prints
TIMER_NAMES_XANTH = {
    2: "the courier brings the package",
    3: "the telephone rings",
    5: "a companion beckons through the cavern door",
    6: "the cherry bomb goes off",
    8: "the butter melts",
    9: "doubts about the companion chosen",
    12: "a cough drop falls from the bush",
    13: "Edsel's offer refused: the game is lost",
    14: "the eye scream melts",
    15: "a door shimmers in the desert",
    20: "the firefly escapes in the barrow",
    25: "the agony moss's pain",
    26: "the cracker dough in the oven",
    27: "the firecracker explodes",
    28: "the wineskin, given and snatched back",
    31: "the well overflows",
    32: "the virus stops Com-Pewter",
    34: "the sword waved in the desert",
    35: "the castle's little door slams closed",
    36: "Jenny's song",
    37: "the cricket at the pier",
    38: "the Gap Dragon comes nearer",
    39: "Stanley's firestorm",
    40: "Fracto's snowstorm",
    42: "the telephone rings, as the game starts",
    43: "Kim takes The Prize",
    44: "a voice from Nada's arms in the dungeon",
}


@dataclass(frozen=True)
class TimerCall:
    """One call about a timer."""

    kind: str  # starts, stops or asks about
    timer: tuple[int, int] | None  # the timer's routine, in the root; None when it isn't a constant
    count: int | None  # for a start, the count it's given, when that is a constant
    overlay: int | None  # the overlay the call is in, or None for the root
    segment: int
    offset: int
    routine: str  # whose routine the call is in
    number: int | None = None  # the timer's number, where they have numbers


def timer_code(program: Program, timer: tuple[int, int]) -> str | None:
    """
    Where a timer's work is done, when its routine in the root is only a call to there and a
    return, or when what the table has for it is a thunk.
    """
    thunk = program.thunk_at(*timer)
    if thunk:
        return f"{thunk.target_segment:04x}:{thunk.target_offset:04x} in overlay {thunk.overlay}"
    image = program.image()
    at = timer[0] * 16 + timer[1]
    if image.memory[at] != 0x9A or image.memory[at + 5] != 0xCB or at + 3 not in image.relocations:
        return None
    offset, segment = struct.unpack_from("<HH", image.memory, at + 1)
    thunk = program.thunk_at(segment, offset)
    if thunk:
        return f"{thunk.target_segment:04x}:{thunk.target_offset:04x} in overlay {thunk.overlay}"
    return f"{segment:04x}:{offset:04x} in the root"


def _named(before: bytes, kind: str, timers: Timers) -> tuple[tuple[int, int] | None, int | None, int | None]:
    """The timer a call names, the count it starts it with, and its number, from the code before it."""
    timer = count = number = None
    if timers.numbered is None:
        # mov ax,<offset> / mov dx,<segment> / push dx / push ax: the routine, pushed last
        if before[4] == 0xB8 and before[7] == 0xBA and before[10:] == b"\x52\x50":
            offset, segment = struct.unpack_from("<HxH", before, 5)
            timer = (segment, offset)
    elif (before[8], before[11]) in ((0xB8, 0x50), (0xB9, 0x51)):
        # mov ax,<number> / push ax: the number, pushed last; or by CX, as a later compiler
        # does it after the count's AX
        (number,) = struct.unpack_from("<H", before, 9)
        if not 1 <= number <= len(timers.numbered):
            return None, None, None
        timer = timers.numbered[number - 1]
    # mov ax,<count> / push ax, before it
    at = 0 if timers.numbered is None else 4
    if timer and kind == "starts" and before[at] == 0xB8 and before[at + 3] == 0x50:
        count = struct.unpack_from("<h", before, at + 1)[0]
    return timer, count, number


def timer_calls(program: Program, names: dict[int, str], shapes: dict[int, str]) -> list[TimerCall]:
    """Every call that starts, stops or asks about a timer, in the root and in each overlay."""
    timers = find_timers(program)
    root = program.image()
    labels = routine_labels(program, names, shapes)
    readers = [CodeReader(root, 0, program.root_size, None, labels.get(None, {}))]
    for overlay in program.overlays:
        if not overlay.preloaded:
            start, end = overlay.load_segment * 16, overlay.end_segment * 16
            image = program.image(overlay.number)
            readers.append(CodeReader(image, start, end, overlay.number, labels.get(overlay.number, {})))
    calls = []
    for reader in readers:
        for at, kind in reader.calls(timers.routines, timers.segment):
            timer, count, number = _named(reader.memory[at - 12 : at], kind, timers)
            base = reader.segment_of(at)
            calls.append(
                TimerCall(
                    kind, timer, count, reader.overlay, base, at - base * 16, reader.routine(at), number
                )
            )
    return calls
