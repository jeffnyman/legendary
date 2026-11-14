using Legendary.Core.Text;

namespace Legendary.Core.Worlds;

/// <summary>A way a form of command goes on to a second object.</summary>
/// <param name="Preposition">The word before the second object, or 0.</param>
/// <param name="Flags">Where the second object may be found, and what's asked of it.</param>
/// <param name="Action">The action the command then is.</param>
public sealed record SecondObjectForm(int Preposition, int Flags, int Action);

/// <summary>A form a command can take: a verb, perhaps a preposition, and what may follow.</summary>
/// <param name="Verb">The verb, a word's number.</param>
/// <param name="Preposition">The preposition after the verb, or 0.</param>
/// <param name="Flags">Where the object may be found, and what's asked of it.</param>
/// <param name="ActionAlone">The action with no object, or 0 when there must be one.</param>
/// <param name="ActionWithObject">The action with one object, or 0.</param>
/// <param name="Seconds">The ways it goes on to a second object.</param>
public sealed record CommandForm(
    int Verb, int Preposition, int Flags, int ActionAlone, int ActionWithObject, IReadOnlyList<SecondObjectForm> Seconds);

/// <summary>
/// A game's grammar: the forms its commands can take, and from them the commands that make
/// each action, which is the nearest thing an action's number has to a name.
/// </summary>
public sealed class Grammar
{
    /// <summary>What stands for an object in a command's shape.</summary>
    public const string Object = "OBJECT";

    private readonly Dictionary<int, List<string>> _shapes = [];

    internal Grammar(IReadOnlyList<CommandForm> forms, IReadOnlyList<VocabularyWord> words, IReadOnlyDictionary<int, string> named)
    {
        Forms = forms;

        // what the map names itself comes first, so it is the name
        foreach (var (action, name) in named)
        {
            _shapes[action] = [name];
        }

        string Word(int number) => number >= 1 && number <= words.Count ? words[number - 1].Text : "";
        void Add(int action, params string[] parts)
        {
            if (action != 0 && !named.ContainsKey(action))
            {
                if (!_shapes.TryGetValue(action, out var shapes))
                {
                    _shapes[action] = shapes = [];
                }

                shapes.Add(string.Join(' ', parts.Where(part => part.Length > 0)));
            }
        }

        foreach (var form in forms)
        {
            string verb = Word(form.Verb), preposition = Word(form.Preposition);
            Add(form.ActionAlone, verb, preposition);
            Add(form.ActionWithObject, verb, preposition, Object);
            foreach (var second in form.Seconds)
            {
                Add(second.Action, verb, preposition, Object, Word(second.Preposition), Object);
            }
        }
    }

    /// <summary>The forms, in the order of the game's table: by verb, then by preposition.</summary>
    public IReadOnlyList<CommandForm> Forms { get; }

    /// <summary>
    /// The commands that make an action, as in <c>examine OBJECT</c> and <c>look at OBJECT</c>,
    /// in the table's order; none for an action no command makes.
    /// </summary>
    public IReadOnlyList<string> Shapes(int action) => _shapes.TryGetValue(action, out var shapes) ? shapes : [];

    /// <summary>
    /// A command that makes an action with so many objects: the first of its shapes with that
    /// many, or failing that its first; null for an action no command makes.
    /// </summary>
    public string? Shape(int action, int objects)
    {
        var shapes = Shapes(action);
        return shapes.FirstOrDefault(shape => Count(shape) == objects) ?? shapes.FirstOrDefault();
    }

    private static int Count(string shape) => shape.Split(' ').Count(part => part == Object);
}
