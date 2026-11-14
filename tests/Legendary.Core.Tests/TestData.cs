namespace Legendary.Core.Tests;

internal static class TestData
{
    /// <summary>
    /// A game's folder, as the player's copy has it: looked for in LEGENDARY_GAMES or in a
    /// legend folder above the test binary. Null when there is none, and the tests that need
    /// the game pass without checking then.
    /// </summary>
    public static string? Game(string folder)
    {
        var fromEnv = Environment.GetEnvironmentVariable("LEGENDARY_GAMES");
        if (!string.IsNullOrEmpty(fromEnv) && In(fromEnv, folder) is { } found)
        {
            return found;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (In(Path.Combine(dir.FullName, "legend"), folder) is { } candidate)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The file with that name in a folder, its case aside; null when there is none.</summary>
    public static string? File(string folder, string name) =>
        Directory.EnumerateFiles(folder)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));

    private static string? In(string root, string relativePath)
    {
        string current = root;
        foreach (var part in relativePath.Split('/'))
        {
            if (!Directory.Exists(current))
            {
                return null;
            }

            var next = Directory.EnumerateDirectories(current)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), part, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}
