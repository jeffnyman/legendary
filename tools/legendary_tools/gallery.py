"""A page to look through what `extract` wrote for a game: its pictures, sounds and songs.

The page is one file of HTML beside the three folders, with nothing fetched from anywhere:
the pictures are shown from their PNG files, a file of the game's at a time, each under its
number and size; the sounds can be played where they're listed; and the songs are links,
since a browser doesn't play MIDI.
"""

from __future__ import annotations

import html
import os
import struct

_STYLE = """
:root { color-scheme: light dark; --ink: #1c1c1c; --dim: #666; --page: #fafafa; --card: #fff; --line: #ddd; }
@media (prefers-color-scheme: dark) {
  :root { --ink: #e8e8e8; --dim: #999; --page: #151515; --card: #1f1f1f; --line: #333; }
}
body { margin: 0; padding: 16px; background: var(--page); color: var(--ink);
  font: 14px/1.4 system-ui, sans-serif; }
h1 { font-size: 20px; margin: 0 0 4px; }
h2 { font-size: 16px; margin: 28px 0 8px; border-bottom: 1px solid var(--line); padding-bottom: 4px; }
h3 { font-size: 13px; margin: 18px 0 8px; color: var(--dim); font-weight: 600; }
p, nav { color: var(--dim); margin: 0 0 8px; }
a { color: inherit; }
.grid { display: flex; flex-wrap: wrap; gap: 10px; align-items: flex-end; }
figure { margin: 0; padding: 6px; background: var(--card); border: 1px solid var(--line);
  border-radius: 4px; }
figure img { display: block; max-width: min(100%, 640px); height: auto; image-rendering: pixelated;
  background: repeating-conic-gradient(#8883 0% 25%, transparent 0% 50%) 0 0 / 12px 12px; }
figcaption { font-size: 11px; color: var(--dim); margin-top: 4px; }
table { border-collapse: collapse; }
td { padding: 3px 12px 3px 0; vertical-align: middle; }
audio { height: 28px; }
"""


def _size(path: str) -> tuple[int, int]:
    """A PNG file's width and height, from its header."""
    with open(path, "rb") as file:
        header = file.read(24)
    return struct.unpack(">II", header[16:24]) if header[12:16] == b"IHDR" else (0, 0)


def _listed(folder: str, endings: tuple[str, ...]) -> list[str]:
    if not os.path.isdir(folder):
        return []
    return sorted(name for name in os.listdir(folder) if name.lower().endswith(endings))


def write_index(folder: str, name: str) -> str:
    """Write the page for a game's extracted folder, and give where it is."""
    pictures = _listed(os.path.join(folder, "pictures"), (".png",))
    sounds = _listed(os.path.join(folder, "sounds"), (".wav",))
    songs = _listed(os.path.join(folder, "music"), (".mid", ".mdi", ".rol"))
    title = html.escape(name)
    page = [
        "<!doctype html>",
        '<html lang="en"><head><meta charset="utf-8">',
        '<meta name="viewport" content="width=device-width, initial-scale=1">',
        f"<title>{title}</title><style>{_STYLE}</style></head><body>",
        f"<h1>{title}</h1>",
        f"<p>{len(pictures)} pictures, {len(sounds)} sounds, {len(songs)} songs.</p>",
        '<nav><a href="#pictures">Pictures</a> · <a href="#sounds">Sounds</a>'
        ' · <a href="#music">Music</a></nav>',
        '<h2 id="pictures">Pictures</h2>',
    ]
    # a file of the game's at a time: s101_e1_038.png is picture 38 of S101_E1.PIC
    files: dict[str, list[str]] = {}
    for picture in pictures:
        files.setdefault(picture.rsplit("_", 1)[0], []).append(picture)
    for file, members in files.items():
        page.append(f"<h3>{html.escape(file.upper())}.PIC, {len(members)}</h3>")
        page.append('<div class="grid">')
        for picture in members:
            width, height = _size(os.path.join(folder, "pictures", picture))
            number = int(picture.rsplit("_", 1)[1].split(".")[0])
            page.append(
                f'<figure><img loading="lazy" src="pictures/{html.escape(picture)}"'
                f' width="{width}" height="{height}" alt="{html.escape(file)} picture {number}">'
                f"<figcaption>{number} · {width} by {height}</figcaption></figure>"
            )
        page.append("</div>")
    page.append('<h2 id="sounds">Sounds</h2><table>')
    for sound in sounds:
        shown = html.escape(sound)
        page.append(
            f'<tr><td>{shown}</td><td><audio controls preload="none" src="sounds/{shown}"></audio></td></tr>'
        )
    page.append("</table>")
    page.append('<h2 id="music">Music</h2>')
    page.append(
        "<p>A .mid file is for the MT-32, and plays in anything that plays MIDI. A .rol or .mdi file is"
        " the AdLib's, which an ordinary player doesn't open.</p><table>"
    )
    for song in songs:
        shown = html.escape(song)
        kind = "MIDI, for the MT-32" if song.lower().endswith(".mid") else "for the AdLib"
        page.append(f'<tr><td><a href="music/{shown}">{shown}</a></td><td>{kind}</td></tr>')
    page.append("</table></body></html>")
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, "index.html")
    with open(path, "w", encoding="utf-8") as file:
        file.write("\n".join(page) + "\n")
    return path
