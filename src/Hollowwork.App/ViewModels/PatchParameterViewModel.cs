using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Hollowwork.Patches;

namespace Hollowwork.App.ViewModels;

public sealed class PatchParameterViewModel : INotifyPropertyChanged
{
    private double _value;

    public PatchParameterViewModel(PatchParameter parameter)
    {
        Key = parameter.Key;
        Label = parameter.Label;
        Min = parameter.Min;
        Max = parameter.Max;
        Step = parameter.Step;
        _value = parameter.Default;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string Label { get; }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

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

    public string Display => Value.ToString("F1", CultureInfo.InvariantCulture) + "x";

    public string Formatted => Value.ToString("F1", CultureInfo.InvariantCulture);

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
