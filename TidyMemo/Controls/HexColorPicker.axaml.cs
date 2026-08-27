using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TidyMemo.Controls;

/// <summary>
/// Exposes the Avalonia color picker through the string format used by the application.
/// This keeps view models and domain services independent from Avalonia color types.
/// </summary>
public partial class HexColorPicker : UserControl
{
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<HexColorPicker, string>(
            nameof(Value),
            defaultValue: "#000000",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private bool _isSynchronizing;

    public HexColorPicker()
    {
        InitializeComponent();
        Picker.ColorChanged += (_, args) => SetValueFromPicker(args.NewColor);
        SyncPicker(Value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && !_isSynchronizing)
            SyncPicker(change.NewValue as string);
    }

    private void SyncPicker(string? value)
    {
        if (!Color.TryParse(value, out var color) || Picker.Color == color)
            return;

        _isSynchronizing = true;
        Picker.Color = color;
        _isSynchronizing = false;
    }

    private void SetValueFromPicker(Color color)
    {
        if (_isSynchronizing)
            return;

        var value = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (string.Equals(Value, value, StringComparison.OrdinalIgnoreCase))
            return;

        _isSynchronizing = true;
        SetCurrentValue(ValueProperty, value);
        _isSynchronizing = false;
    }
}
