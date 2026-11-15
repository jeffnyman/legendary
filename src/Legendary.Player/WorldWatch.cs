using System.Globalization;
using Legendary.Core.Text;
using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player;

/// <summary>
/// The game's world, said as it changes: the time, the turn and the room, what is in the room
/// and what those things hold, the room's exits, each action as the game performs it, and
/// what the action changed: every object, anywhere in the game, that moved or had a flag set
/// or cleared. It reads the game's
/// memory and writes nothing to it, so the game is played as without it.
/// </summary>
/// <param name="machine">The PC the game is running on.</param>
/// <param name="map">Where the game keeps its world.</param>
/// <param name="output">Where to say it, a line at a time; null to say it nowhere, for the panel alone.</param>
/// <param name="words">
/// The game's vocabulary, to name actions by the commands that make them; without it, or when
/// it isn't this game's, they are said by number alone.
/// </param>
/// <remarks>
/// The room and what is in it are looked at once a frame. The actions aren't: the machine says
/// each time the game reaches its routine that performs one, so every action is said, the ones
/// a routine performs inside another's, set in under it, and the ones the game makes of a
/// command as well as the command.
/// <para>
/// It also keeps what a panel beside the game would show, for <see cref="Panel"/>: the world as
/// it is now, set out down a column, and under it the last of what was said.
/// </para>
/// </remarks>
internal sealed class WorldWatch(Machine machine, WorldMap map, TextWriter? output, IReadOnlyList<VocabularyWord>? words = null)
{
    // how many lines of what was said are kept for the panel
    private const int Kept = 400;

    // how many looks to let pass between searches for a game that isn't there yet
    private const int SearchEvery = 30;

    private World? _world;
    private Grammar? _grammar;
    private int _looks;
    private string _place = "";
    private string _here = "";
    private string _exits = "";
    private ObjectState[] _states = [];

    // for the panel: the world as it is, and what was said lately, the newest last
    private List<PanelLine> _now = [];
    private readonly List<PanelLine> _lately = [];

    /// <summary>Looks at the game, and says what has changed since the last look.</summary>
    public void Look()
    {
        if (_world is { IsThere: false })
        {
            // the game ended, or a saved state without it was loaded
            _world = null;
            _states = [];
            machine.Cpu.WatchNothing();
        }

        if (_world is null)
        {
            if (_looks++ % SearchEvery != 0 || (_world = World.Find(Read, 0xA0000, map)) is null)
            {
                return;
            }

            Say($"the game is in memory, loaded at {_world.LoadSegment:X4}h, with {map.ObjectCount} objects", PanelTone.Dim);
            machine.Cpu.Watch((uint)_world.PerformAddress, Performing);
            machine.Cpu.Watch((uint)_world.JumpBackAddress, JumpedBack);
            machine.Cpu.Watch((uint)_world.TokenAddress, () => Token(Taken.Next));
            machine.Cpu.Watch((uint)_world.AfterStopAddress, () => Token(Taken.AfterStop));
            machine.Cpu.Watch((uint)_world.AfterAndAddress, () => Token(Taken.AfterAnd));
            machine.Cpu.Watch((uint)_world.FormAddress, Form);
            machine.Cpu.Watch((uint)_world.FoundAddress, Found);
            foreach (var point in map.Answers)
            {
                machine.Cpu.Watch((uint)_world.AnswerAddress(point), () => Answered(point));
            }
            try
            {
                _grammar = words is null ? null : _world.ReadGrammar(words);
            }
            catch (InvalidDataException)
            {
                // the words aren't this game's: its actions go by number
                _grammar = null;
            }
        }

        var world = _world;
        int room = world.Room;
        if (room < 1 || room > map.ObjectCount)
        {
            // the game is loaded and hasn't put the player anywhere yet
            return;
        }

        SayChanges(world);
        string place = $"{Time(world.Minutes)}, turn {world.Moves}, in {Name(world, room)}";
        string here = $"  here: {Inside(world, room, depth: 0)}";
        string exits = $"  exits: {Exits(world, room)}";
        if (place == _place && here == _here && exits == _exits)
        {
            return;
        }

        // each when it changes: the first every turn, the others when something comes or goes
        // and when the room is another. The panel has them set out, so only the first is kept
        // for its list of what was said
        if (place != _place)
        {
            Say(_place = place, PanelTone.Dim);
        }

        if (here != _here)
        {
            Say(_here = here, kept: false);
        }

        if (exits != _exits)
        {
            Say(_exits = exits, kept: false);
        }

        _now = Now(world, room);
    }

    /// <summary>
    /// What a panel of so many columns and rows shows: the time, the turn and the room; what
    /// is in the room, each thing under what holds it; the exits; and in the rows left, the
    /// last of what was said, the newest at the bottom. Lines too long for it are folded.
    /// </summary>
    public IReadOnlyList<PanelLine> Panel(int columns, int rows)
    {
        if (_world is null || _now.Count == 0)
        {
            return [new PanelLine("Legendary", PanelTone.Title), new PanelLine("The game isn't in memory yet.", PanelTone.Dim)];
        }

        // the world gets what it needs, short of a few rows kept for what was said
        var lines = _now.SelectMany(line => Fold(line, columns)).ToList();
        int most = Math.Max(rows - 6, 1);
        if (lines.Count > most)
        {
            lines.RemoveRange(most - 1, lines.Count - (most - 1));
            lines.Add(new PanelLine("  ...", PanelTone.Dim));
        }

        int left = rows - lines.Count - 2;
        if (left > 0)
        {
            lines.Add(new PanelLine(""));
            lines.Add(new PanelLine("Lately", PanelTone.Dim));
            var lately = new List<PanelLine>();
            for (int at = _lately.Count - 1; at >= 0 && lately.Count < left; at--)
            {
                lately.InsertRange(0, Fold(_lately[at], columns));
            }

            lines.AddRange(lately.Skip(Math.Max(lately.Count - left, 0)));
        }

        return lines;
    }

    /// <summary>
    /// A line folded to a width, at its spaces where it has them: what doesn't fit goes on
    /// under it, set in a little more.
    /// </summary>
    internal static IEnumerable<PanelLine> Fold(PanelLine line, int columns)
    {
        string text = line.Text;
        if (text.Length <= columns || columns < 8)
        {
            yield return line;
            yield break;
        }

        string indent = text[..(text.Length - text.TrimStart().Length)];
        string under = new(' ', Math.Min(indent.Length + 2, columns / 2));
        string rest = text.TrimStart();
        for (string lead = indent; rest.Length > 0; lead = under)
        {
            int room = columns - lead.Length;
            int take = rest.Length;
            if (take > room)
            {
                // back to the last space that leaves the line no wider; a word longer than
                // the line is cut where the line ends
                int space = rest.LastIndexOf(' ', room);
                take = space > 0 ? space : room;
            }

            yield return line with { Text = lead + rest[..take] };
            rest = rest[take..].TrimStart();
        }
    }

    // says a line: to the output, and for the panel's list of what was said unless it's told not to
    private void Say(string line, PanelTone tone = PanelTone.Text, bool kept = true)
    {
        output?.WriteLine($"world: {line}");
        if (kept)
        {
            if (_lately.Count == Kept)
            {
                _lately.RemoveAt(0);
            }

            _lately.Add(new PanelLine(line.StartsWith("  ", StringComparison.Ordinal) ? line[2..] : line, tone));
        }
    }

    // the world as it is, down a column
    private List<PanelLine> Now(World world, int room)
    {
        var lines = new List<PanelLine>
        {
            new($"{Time(world.Minutes)}   turn {world.Moves}", PanelTone.Title),
            new(Name(world, room), PanelTone.Title),
            new(""),
            new("Here", PanelTone.Dim),
        };

        void Under(int holder, int depth)
        {
            foreach (int inside in world.Contents(holder))
            {
                // a thing on a list after its holder's first is held another way: worn, say
                int list = world.State(inside).Relation;
                lines.Add(new PanelLine(new string(' ', 2 * depth) + Name(world, inside) + (list > 0 ? $", list {list + 1}" : "") + How(world, inside)));
                if (depth < 4)
                {
                    Under(inside, depth + 1);
                }
            }
        }

        Under(room, 1);
        lines.Add(new PanelLine(""));
        lines.Add(new PanelLine("Exits", PanelTone.Dim));
        var exits = world.Exits(room);
        int width = exits.Count == 0 ? 0 : exits.Max(exit => DirectionName(exit.Direction).Length);
        lines.AddRange(exits.Select(exit => new PanelLine($"  {DirectionName(exit.Direction).PadRight(width)}  {Leads(world, exit).TrimStart(' ', ':')}")));
        return lines;
    }

    // every object as it is now against how it was at the last look: what moved, and which
    // flags changed. The first look only remembers
    private void SayChanges(World world)
    {
        var now = new ObjectState[map.ObjectCount];
        for (int number = 1; number <= now.Length; number++)
        {
            now[number - 1] = world.State(number);
        }

        for (int number = 1; number <= _states.Length; number++)
        {
            var (was, state) = (_states[number - 1], now[number - 1]);
            if (was == state)
            {
                continue;
            }

            if ((was.Place, was.SecondPlace, was.Relation) != (state.Place, state.SecondPlace, state.Relation))
            {
                Say($"  {Name(world, number)} moved: from {Where(world, was)} to {Where(world, state)}");
            }

            for (int flag = 1; flag <= 64; flag++)
            {
                if (was.Has(flag) != state.Has(flag))
                {
                    string name = map.FlagName(flag, world.Object(number).Type) is { } known ? $", {known}," : "";
                    Say($"  {Name(world, number)}: flag {flag}{name} {(state.Has(flag) ? "set" : "cleared")}");
                }
            }
        }

        _states = now;
    }

    // where an object is: what it's in and on which of that's lists, or the two it's between
    private string Where(World world, ObjectState state)
    {
        string In(int number) => number >= 1 && number <= map.ObjectCount ? Name(world, number) : "nowhere";
        return state.SecondPlace != 0 ? $"{In(state.Place)} and {In(state.SecondPlace)}"
            : state.Relation > 0 ? $"{In(state.Place)}, its list {state.Relation + 1}"
            : In(state.Place);
    }

    // a room's exits, each by its direction: where it goes, what is said, or that a routine decides
    private string Exits(World world, int room)
    {
        var exits = world.Exits(room);
        return exits.Count == 0 ? "none" : string.Join("; ", exits.Select(exit => DirectionName(exit.Direction) + Leads(world, exit)));
    }

    // where an exit leads, to follow its direction: " to Potty Chamber (2)", or ": a routine decides"
    private string Leads(World world, WorldExit exit)
    {
        string Room(int number) => number >= 1 && number <= map.ObjectCount ? Name(world, number) : $"object {number}";
        return exit.Kind switch
        {
            1 => $" to {Room(exit.Room)}",
            2 => $" to {Room(exit.Room)} by {Room(exit.Door)}",
            3 => $": \"{exit.Message}\"",
            4 => ": a routine decides",
            _ => string.Create(CultureInfo.InvariantCulture, $": an exit of kind {exit.Kind}"),
        };
    }

    private string DirectionName(int direction) =>
        direction >= 1 && direction < map.Directions.Count ? map.Directions[direction] : string.Create(CultureInfo.InvariantCulture, $"direction {direction}");

    // the game is at its routine that performs an action, about to: which, and how deep
    private void Performing()
    {
        // other code can be at the address before the game is loaded there, or after it has gone
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        var cpu = machine.Cpu;
        var (action, first, second) = world.PerformArguments((cpu.Ss * 16) + (ushort)cpu.Esp);
        string inside = new(' ', 2 * Math.Clamp(world.PerformDepth, 0, 8));
        Say($"  {inside}action {action}: {Said(world, action, first, second)}", PanelTone.Action);
    }

    // how the parser came to take a word off the line: as the command's next, or to see what
    // comes after a full stop or an "and"
    private enum Taken
    {
        Next,
        AfterStop,
        AfterAnd,
    }

    // the words of the command the parser is gathering, each as it will be said
    private readonly List<string> _tokens = [];

    // the parser has taken a word off the line and made it a token: the word, and what parts
    // of speech such a word can be. They are said together, when the command's last has come
    private void Token(Taken taken)
    {
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        int code = (short)machine.Cpu.Eax, number = world.TokenWord;
        var word = words is not null && number >= 1 && number <= words.Count ? words[number - 1] : null;
        bool ends = map.EndTokens.Contains(code);
        if (taken == Taken.AfterStop)
        {
            // what follows a full stop is another command's, and taken again for it
            ends = true;
            code = -1;
        }
        else if (taken == Taken.AfterAnd && word is { IsVerb: true } && (word.Parts & 6) == 0)
        {
            // so is a verb after "and", which ends the command there
            ends = true;
            code = -1;
        }

        if (code == 0)
        {
            _tokens.Add($"{Typed()} [not known]");
            ends = true;
        }
        else if (map.WordTokens.TryGetValue(code, out string? parts))
        {
            _tokens.Add($"{word?.Text ?? "a word"} [{parts}]");
        }
        else if (map.MarkTokens.TryGetValue(code, out string? mark))
        {
            _tokens.Add(mark);
        }
        else if (code != -1)
        {
            _tokens.Add(word?.Text ?? $"token {code}");
        }

        if (ends && _tokens.Count > 0)
        {
            Say($"  words: {string.Join(' ', _tokens)}");
            _tokens.Clear();
        }
    }

    // the main loop has searched the grammar for the form the command's verb and prepositions
    // fit, which says what action the command is
    private void Form()
    {
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        var (form, action, lots) = world.FormFound;
        Say((short)machine.Cpu.Eax > 0 && form >= 0
            ? $"  form {form}: {_grammar?.FormShape(form, action, lots) ?? "one"}, which is action {action}"
            : "  form: none in the grammar fits");
    }

    // the parser has looked for the objects each phrase of the command could be: each phrase
    // is said with them, and with where the form let them be looked for
    private void Found()
    {
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        var cpu = machine.Cpu;
        var (lot, where, phrases) = world.Found((cpu.Ss * 16) + (ushort)cpu.Esp);
        string[] places = [.. new[] { (1, "held"), (2, "carried"), (4, "in the room"), (8, "in something there") }.Where(place => (where & place.Item1) != 0).Select(place => place.Item2)];
        string looked = places.Length switch { 0 => "anywhere at all", 4 => "anywhere near", _ => string.Join(", ", places) };
        string Word(int number) => words is not null && number >= 1 && number <= words.Count ? words[number - 1].Text : "";
        foreach (var phrase in phrases)
        {
            string said = string.Join(' ', new[] { phrase.All ? "all" : "" }.Concat(phrase.Adjectives.Select(Word)).Append(Word(phrase.Noun)).Where(word => word.Length > 0));
            string found = phrase.Objects.Count > 0 ? string.Join(" or ", phrase.Objects.Select(number => number >= 1 && number <= map.ObjectCount ? Name(world, number) : $"object {number}"))
                : phrase.All ? "left to the action to gather"
                : "nothing";
            Say($"  {(lot == 0 ? "object" : "second object")} \"{said}\", looked for {looked}: {found}");
        }
    }

    // the word the parser has just taken, as it was typed: the line and how far along it the
    // parser is are the gathering routine's argument, and the word is what ends there
    private string Typed()
    {
        var cpu = machine.Cpu;
        int frame = (cpu.Ss * 16) + (ushort)cpu.Ebp;
        int line = (((Read(frame + 8) | (Read(frame + 9) << 8)) * 16) + (Read(frame + 6) | (Read(frame + 7) << 8))) & 0xFFFFF;
        int end = Read(line + 0x100);
        var text = new System.Text.StringBuilder();
        for (int at = 0; at < end && Read(line + at) != 0; at++)
        {
            text.Append((char)Read(line + at));
        }

        string[] typed = text.ToString().Split([' ', '.', ',', ';', '!', '?', '"'], StringSplitOptions.RemoveEmptyEntries);
        return typed.Length == 0 ? "something" : typed[^1];
    }

    // the main loop's setjmp has returned: by a routine's longjmp, when AX isn't 0. Such an
    // action is never answered, because the routine that jumped didn't go back to who asked it
    private void JumpedBack()
    {
        if (_world is not { IsThere: true })
        {
            return;
        }

        switch ((ushort)machine.Cpu.Eax)
        {
            case 0:
                break;
            case 1:
                Say("    given up by a routine, which starts over with another action");
                break;
            default:
                Say("    ended by a routine that jumped back to the main loop: done");
                break;
        }
    }

    // a routine the game asked about the action has answered: said when it's the answer that
    // ends the asking, which is any but zero, and for the action's own routine whatever it is
    private void Answered(AnswerPoint point)
    {
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        var cpu = machine.Cpu;
        int answer = (ushort)cpu.Eax;
        if (answer == 0 && point.Asked != Asked.Action)
        {
            return;
        }

        string who;
        if (point.Asked == Asked.Object)
        {
            var (number, why) = world.AskedArguments((cpu.Ss * 16) + (ushort)cpu.Esp);
            who = number >= 1 && number <= map.ObjectCount ? $"{Name(world, number)}, asked as {Why(why)}" : $"object {number}";
        }
        else
        {
            who = point.Asked == Asked.Before ? "the routine before the action's" : "the action's own routine";
        }

        string inside = new(' ', 2 * Math.Clamp(world.PerformDepth, 0, 8));
        Say($"  {inside}answered by {who}: {answer}");
    }

    /// <summary>Why an object's routine is asked about an action, from the number it's asked with.</summary>
    internal static string Why(int why) => why switch
    {
        1 => "where it happens",
        2 => "the actor",
        3 => "what the second object is in",
        4 => "the second object",
        5 => "what the object is in",
        6 => "the object",
        _ => string.Create(CultureInfo.InvariantCulture, $"{why}"),
    };

    /// <summary>A time of day as the game's own status line has it, as in <c>3:35 pm</c>.</summary>
    internal static string Time(int minutes)
    {
        int hour = minutes / 60 % 24;
        return string.Create(CultureInfo.InvariantCulture, $"{((hour + 11) % 12) + 1}:{minutes % 60:00} {(hour < 12 ? "am" : "pm")}");
    }

    private byte Read(int linear) => machine.Read((uint)linear);

    // how a thing is for now, after its name: ", open", ", locked"
    private string How(World world, int number)
    {
        var (state, type) = (world.State(number), world.Object(number).Type);
        return string.Concat(map.StateFlags.Where(state.Has).Select(flag => map.FlagName(flag, type)).OfType<string>().Select(name => $", {name}"));
    }

    private static string Name(World world, int number) => $"{world.Object(number).Name} ({number})";

    // the action as a command that makes it, with its objects where the command has them:
    // "examine rat (352)". Without a grammar, or for an action no command makes, its objects alone
    private string Said(World world, int action, int first, int second)
    {
        if (action == map.GoAction && world.Direction != 0)
        {
            // going has no object: where to is a direction, which the game keeps beside the action
            return $"go {DirectionName(world.Direction)}";
        }

        int[] objects = [.. new[] { first, second }.TakeWhile(number => number >= 1 && number <= map.ObjectCount)];
        if (_grammar?.Shape(action, objects.Length) is not { } shape)
        {
            return objects.Length == 0 ? "no command makes it" : string.Join(", ", objects.Select(number => Name(world, number)));
        }

        int next = 0;
        return string.Join(' ', shape.Split(' ').Select(part =>
            part != Grammar.Object ? part : next < objects.Length ? Name(world, objects[next++]) : "something"));
    }

    // what an object holds, and in brackets what each of those holds, a few deep
    private static string Inside(World world, int number, int depth)
    {
        var contents = world.Contents(number);
        if (contents.Count == 0)
        {
            return depth == 0 ? "nothing" : "";
        }

        return string.Join(", ", contents.Select(inside =>
        {
            string held = depth < 3 ? Inside(world, inside, depth + 1) : "";
            return held.Length == 0 ? Name(world, inside) : $"{Name(world, inside)} [{held}]";
        }));
    }
}
