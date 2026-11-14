using Legendary.Core.Programs;
using Legendary.Core.Text;
using Legendary.Core.Worlds;

namespace Legendary.Core.Tests;

/// <summary>
/// The vocabulary and the grammar of <i>Spellcasting 101</i>, and the names the grammar gives
/// its actions. Without the game (in LEGENDARY_GAMES, or legend/sc101/game above the tests),
/// the tests pass without checking.
/// </summary>
public sealed class GrammarTests
{
    private static (IReadOnlyList<VocabularyWord> Words, World World)? Load()
    {
        if (TestData.Game("sc101/game") is not { } folder || TestData.File(folder, "s101main.exe") is not { } program
            || TestData.File(folder, "vocab.dat") is not { } vocabulary)
        {
            return null;
        }

        var memory = Plink86Program.Load(program).Image().Memory;
        var world = World.Find(at => at < memory.Length ? memory.Span[at] : (byte)0, memory.Length, WorldMap.Spellcasting101)!;
        return (Vocabulary.Read(File.ReadAllBytes(vocabulary)), world);
    }

    [Fact]
    public void The_vocabulary_has_its_words_by_number()
    {
        if (Load() is not { } game)
        {
            return;
        }

        Assert.Equal(1736, game.Words.Count);
        Assert.Equal((1, "'n'", 4), (game.Words[0].Number, game.Words[0].Text, game.Words[0].Parts));
        Assert.Equal("apply", game.Words[42].Text);
        Assert.True(game.Words[42].IsVerb);
    }

    [Fact]
    public void A_file_that_isnt_a_vocabulary_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => Vocabulary.Read([1, 2]));
        Assert.Throws<InvalidDataException>(() => Vocabulary.Read([2, 0, (byte)'a', 0, 1, 0]));
        Assert.Throws<InvalidDataException>(() => Vocabulary.Read([200, 0, (byte)'a', 0, 1, 0]));
    }

    [Fact]
    public void The_grammar_has_its_forms_and_their_second_objects()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var grammar = game.World.ReadGrammar(game.Words);
        Assert.Equal(376, grammar.Forms.Count);
        Assert.Equal(119, grammar.Forms.Sum(form => form.Seconds.Count));

        // the first form is APPLY with an object, action 1
        var first = grammar.Forms[0];
        Assert.Equal((43, 0, 63, 0, 1), (first.Verb, first.Preposition, first.Flags, first.ActionAlone, first.ActionWithObject));
        Assert.Empty(first.Seconds);
    }

    [Fact]
    public void An_action_is_named_by_the_commands_that_make_it()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var grammar = game.World.ReadGrammar(game.Words);
        Assert.Equal(["examine OBJECT", "look at OBJECT", "look on OBJECT"], grammar.Shapes(60));
        Assert.Equal(["apply OBJECT", "put on OBJECT", "wear OBJECT"], grammar.Shapes(1));
        Assert.Equal(["ask OBJECT about OBJECT"], grammar.Shapes(3));
        Assert.Equal("apply OBJECT to OBJECT", grammar.Shapes(2)[0]);

        // the shape with as many objects as the action has, when there is one
        Assert.Equal("climb", grammar.Shape(25, 0));
        Assert.Equal("climb OBJECT", grammar.Shape(25, 1));
        Assert.Equal("climb", grammar.Shape(25, 2));

        // going is named by the map: every direction's word makes it
        Assert.Equal(["go"], grammar.Shapes(197));

        // no command makes action 113, and there is no action 250
        Assert.Empty(grammar.Shapes(113));
        Assert.Null(grammar.Shape(113, 1));
        Assert.Null(grammar.Shape(250, 0));
    }

    [Fact]
    public void Words_that_arent_the_games_are_refused()
    {
        if (Load() is not { } game)
        {
            return;
        }

        // every word a noun: no form can start with a verb
        var nouns = game.Words.Select(word => word with { Parts = 2 }).ToList();
        Assert.Throws<InvalidDataException>(() => game.World.ReadGrammar(nouns));
    }
}
