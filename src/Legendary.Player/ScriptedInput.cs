using System.Globalization;
using Legendary.Pc;
using UsageException = Legendary.Player.Program.UsageException;

namespace Legendary.Player;

/// <summary>
/// Keys and clicks given ahead of time, for a game run without a window, each at a time in
/// seconds of the game's own: what <c>--input</c> takes. Events are separated by semicolons,
/// and each is a time, a colon, and one of these:
/// <list type="bullet">
/// <item><c>Enter</c>, <c>Escape</c>, <c>Space</c>, <c>Tab</c>, <c>Backspace</c>, an arrow as
/// <c>Up</c>, <c>F1</c> to <c>F10</c>, or a letter or digit: that key, pressed and let go</item>
/// <item><c>type</c> and text: the text typed, a key a twentieth of a second, ending with a line</item>
/// <item><c>click</c> and a place on the screen, across and down in its pixels: the pointer
/// taken there and the left button pressed and let go</item>
/// </list>
/// So <c>20:Enter;25:type look;30:click 160,100</c>.
/// </summary>
internal sealed class ScriptedInput
{
    // a key held this long, and the button this long, as a hand would
    private const double Held = 0.05, Clicked = 0.1;

    private readonly List<(double At, Action<Machine> Do)> _steps = [];
    private int _next;

    private ScriptedInput()
    {
    }

    /// <summary>Whether any of the events is a click, for which the PC needs its mouse.</summary>
    public bool Clicks { get; private set; }

    /// <exception cref="UsageException">The text isn't events as they're written.</exception>
    public static ScriptedInput Parse(string text)
    {
        var input = new ScriptedInput();
        foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = part.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0 || !double.TryParse(part[..colon], NumberStyles.Float, CultureInfo.InvariantCulture, out double at) || at < 0)
            {
                throw new UsageException($"--input: \"{part}\" isn't a time in seconds, a colon, and what to do");
            }

            string what = part[(colon + 1)..].Trim();
            if (what.StartsWith("type ", StringComparison.OrdinalIgnoreCase))
            {
                string typed = what[5..];
                for (int index = 0; index < typed.Length; index++)
                {
                    input.Key(at + (index * 2 * Held), Usage(typed[index]) ?? throw new UsageException($"--input: can't type \"{typed[index]}\""), char.IsUpper(typed[index]));
                }

                input.Key(at + (typed.Length * 2 * Held), Enter, false);
            }
            else if (what.StartsWith("click ", StringComparison.OrdinalIgnoreCase))
            {
                string[] place = what[6..].Split(',', StringSplitOptions.TrimEntries);
                if (place.Length != 2 || !int.TryParse(place[0], CultureInfo.InvariantCulture, out int x) || !int.TryParse(place[1], CultureInfo.InvariantCulture, out int y))
                {
                    throw new UsageException($"--input: \"{what}\" isn't a click at a place, as click 160,100");
                }

                input.Click(at, x, y);
            }
            else
            {
                input.Key(at, Named(what) ?? throw new UsageException($"--input: \"{what}\" isn't a key that's known"), false);
            }
        }

        input._steps.Sort((first, second) => first.At.CompareTo(second.At));
        return input;
    }

    /// <summary>Does what is due by a time, in seconds of the game's.</summary>
    public void Apply(Machine machine, double now)
    {
        ArgumentNullException.ThrowIfNull(machine);
        while (_next < _steps.Count && _steps[_next].At <= now)
        {
            _steps[_next++].Do(machine);
        }
    }

    private const int Enter = 0x28, Shift = 0xE1;

    private void Key(double at, int usage, bool shifted)
    {
        if (!ScanCodes.TryFromUsb(usage, out byte code, out bool extended))
        {
            throw new UsageException("--input: a key that has no scan code");
        }

        ScanCodes.TryFromUsb(Shift, out byte shift, out _);
        _steps.Add((at, machine =>
        {
            if (shifted)
            {
                machine.PressKey(shift);
            }

            machine.PressKey(code, extended);
        }
        ));
        _steps.Add((at + Held, machine =>
        {
            machine.ReleaseKey(code, extended);
            if (shifted)
            {
                machine.ReleaseKey(shift);
            }
        }
        ));
    }

    private void Click(double at, int x, int y)
    {
        Clicks = true;

        // to the top left corner, where the pointer stops, and from there by the mickeys that
        // many of the screen's pixels are, as the driver counts them now
        _steps.Add((at, machine =>
        {
            var (across, down) = machine.MickeysAcross;
            machine.MoveMouse(-4 * across, -4 * down);
            machine.MoveMouse(x * across / Math.Max(1, machine.DisplayWidth), y * down / Math.Max(1, machine.DisplayHeight));
            machine.SetMouseButton(0, true);
        }
        ));
        _steps.Add((at + Clicked, machine => machine.SetMouseButton(0, false)));
    }

    // a key's USB usage, which the scan codes are kept by
    private static int? Named(string name) => name.ToUpperInvariant() switch
    {
        "ENTER" or "RETURN" => Enter,
        "ESCAPE" or "ESC" => 0x29,
        "BACKSPACE" => 0x2A,
        "TAB" => 0x2B,
        "SPACE" => 0x2C,
        "RIGHT" => 0x4F,
        "LEFT" => 0x50,
        "DOWN" => 0x51,
        "UP" => 0x52,
        ['F', .. var number] when int.TryParse(number, CultureInfo.InvariantCulture, out int key) && key is >= 1 and <= 10 => 0x39 + key,
        [var letter] => Usage(letter),
        _ => null,
    };

    private static int? Usage(char letter) => char.ToLowerInvariant(letter) switch
    {
        >= 'a' and <= 'z' and var lower => 0x04 + (lower - 'a'),
        >= '1' and <= '9' and var digit => 0x1E + (digit - '1'),
        '0' => 0x27,
        ' ' => 0x2C,
        '-' => 0x2D,
        '\'' => 0x34,
        ',' => 0x36,
        '.' => 0x37,
        '/' => 0x38,
        _ => null,
    };
}
