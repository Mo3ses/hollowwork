using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Hollowwork.Patches;

namespace Hollowwork.App.ViewModels;

public sealed class PatchParameterViewModel : INotifyPropertyChanged
{
    private double _value;
    private string _colorText;
    private IBrush? _previewBrush;

    public PatchParameterViewModel(PatchParameter parameter)
    {
        Key = parameter.Key;
        Label = parameter.Label;
        Min = parameter.Min;
        Max = parameter.Max;
        Step = parameter.Step;
        IsColor = string.Equals(parameter.Type, "color", StringComparison.OrdinalIgnoreCase);
        _value = parameter.Default;
        _colorText = parameter.DefaultText ?? "#FFFFFFFF";
        UpdateBrush();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string Label { get; }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    public bool IsColor { get; }

    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) < double.Epsilon)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }

    public string ColorText
    {
        get => _colorText;
        set
        {
            if (string.Equals(_colorText, value, StringComparison.Ordinal))
            {
                return;
            }

            _colorText = value ?? string.Empty;
            UpdateBrush();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(IsValidColor));
            OnPropertyChanged(nameof(Color));
        }
    }

    public Color Color
    {
        get => ShaderColor.TryParse(ColorText, out var r, out var g, out var b, out var a)
            ? Color.FromArgb(a, r, g, b)
            : Colors.Transparent;
        set => ColorText = $"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}";
    }

    public IBrush? PreviewBrush
    {
        get => _previewBrush;
        private set
        {
            _previewBrush = value;
            OnPropertyChanged();
        }
    }

    public bool IsValidColor => !IsColor || ShaderColor.TryParse(ColorText, out _, out _, out _, out _);

    public string Display => IsColor
        ? ColorText.ToUpperInvariant()
        : Value.ToString("F1", CultureInfo.InvariantCulture) + "x";

    public string Formatted => IsColor
        ? ShaderColor.ToFloat4(ColorText)
        : Value.ToString("F1", CultureInfo.InvariantCulture);

    public string Persisted => IsColor ? ColorText : Formatted;

    public void ApplyStored(string value)
    {
        if (IsColor)
        {
            ColorText = value;
            return;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            Value = parsed;
        }
    }

    private void UpdateBrush()
    {
        if (ShaderColor.TryParse(_colorText, out var r, out var g, out var b, out var a))
        {
            PreviewBrush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
        else
        {
            PreviewBrush = null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
