namespace Legendary.Player;

/// <summary>How a line of the panel beside the game is shown.</summary>
internal enum PanelTone
{
    /// <summary>Ordinary text.</summary>
    Text,

    /// <summary>The top of the panel: the time, the turn and the room.</summary>
    Title,

    /// <summary>The name of a part of the panel, and what matters less.</summary>
    Dim,

    /// <summary>An action the game performed.</summary>
    Action,
}

/// <summary>A line of the panel beside the game: its text, no wider than the panel, and how it's shown.</summary>
internal readonly record struct PanelLine(string Text, PanelTone Tone = PanelTone.Text);
