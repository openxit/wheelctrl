using G29ControlCenter.ViewModels;

namespace G29ControlCenter.Models;

public sealed class ButtonItemModel : ObservableObject
{
    private bool _isPressed;

    public ButtonItemModel(int index, string label, string category)
    {
        Index = index;
        Label = label;
        Category = category;
    }

    public int Index { get; }
    public string Label { get; }
    public string Category { get; }

    public bool IsPressed
    {
        get => _isPressed;
        set => SetProperty(ref _isPressed, value);
    }

    public static IReadOnlyList<ButtonItemModel> CreateG29DefaultMap()
    {
        return
        [
            new(0, "✕", "Aktion"),
            new(1, "□", "Aktion"),
            new(2, "○", "Aktion"),
            new(3, "△", "Aktion"),
            new(4, "Wippe R", "Wippe"),
            new(5, "Wippe L", "Wippe"),
            new(6, "R2", "Schulter"),
            new(7, "L2", "Schulter"),
            new(8, "Share", "Menü"),
            new(9, "Opt", "Menü"),
            new(10, "R3", "Stick"),
            new(11, "L3", "Stick"),
            new(12, "G1", "Shifter"),
            new(13, "G2", "Shifter"),
            new(14, "G3", "Shifter"),
            new(15, "G4", "Shifter"),
            new(16, "G5", "Shifter"),
            new(17, "G6", "Shifter"),
            new(18, "R", "Shifter"),
            new(19, "+", "Plus/Minus"),
            new(20, "−", "Plus/Minus"),
            new(21, "Rad ↻", "Drehrad"),
            new(22, "Rad ↺", "Drehrad"),
            new(23, "Enter", "Drehrad"),
            new(24, "PS", "System")
        ];
    }
}
