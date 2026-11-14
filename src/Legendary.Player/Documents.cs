using System.Text;

namespace Legendary.Player;

/// <summary>A document a game keeps in its files, as the game would show it.</summary>
/// <param name="Number">Its number, counting from 1, in the order the game keeps them.</param>
/// <param name="Group">What kind of document it is, and the file it's from.</param>
/// <param name="Title">Its first line, cut short if it's long.</param>
/// <param name="Text">All of it, lines ending in a newline.</param>
internal sealed record GameDocument(int Number, string Group, string Title, string Text);

/// <summary>
/// What the games that keep their text rotated by a bit share: Cosmi's investigations, whose
/// files hold every byte rotated right by one bit, and the reading of documents from them.
/// </summary>
internal static class GameText
{
    // a title longer than this is cut short
    private const int TitleLength = 60;

    /// <summary>The games' encoding undone: each byte rotated left by one bit.</summary>
    public static byte[] Decode(byte[] bytes) => [.. bytes.Select(b => (byte)((b << 1) | (b >> 7)))];

    /// <summary>The games' encoding: each byte rotated right by one bit.</summary>
    public static byte[] Encode(byte[] bytes) => [.. bytes.Select(b => (byte)((b >> 1) | (b << 7)))];

    /// <summary>A file of the game's, decoded, as text.</summary>
    public static string ReadDecoded(string folder, string name) => Encoding.ASCII.GetString(Decode(File.ReadAllBytes(Find(folder, name))));

    /// <summary>
    /// A document, its lines ending in a newline, with no blank lines before or after; none when
    /// it's empty. Its title is its first line with a letter or digit in it, after its name if
    /// it has one, cut short if it's long.
    /// </summary>
    public static void Add(List<GameDocument> documents, string group, string text, string? name = null)
    {
        string body = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n');
        if (body.Trim().Length == 0)
        {
            return;
        }

        string first = body.Split('\n').FirstOrDefault(line => line.Any(char.IsLetterOrDigit))?.Trim() ?? "";
        string title = name is null ? first : $"{name}: {first}";
        if (title.Length > TitleLength)
        {
            // at the last word that fits
            int space = title.LastIndexOf(' ', TitleLength - 3);
            title = title[..(space > 0 ? space : TitleLength - 3)].TrimEnd(' ', ',', '.', '-', ':') + "...";
        }

        documents.Add(new GameDocument(documents.Count + 1, group, title, body + "\n"));
    }

    /// <summary>A file in the folder, whatever its case.</summary>
    public static string Find(string folder, string name)
        => Directory.EnumerateFiles(folder).FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"no {name} in {folder}, where the game keeps its documents");
}
