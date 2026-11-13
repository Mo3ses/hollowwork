using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hollowwork.Patches;

namespace Hollowwork.App.ViewModels;

public sealed class PatchItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public PatchItemViewModel(PatchDefinition definition)
    {
        Definition = definition;
        var target = definition.Targets[0];
        Meta = $"{target.Root} · {string.Join(", ", target.Extensions)}";
        foreach (var parameter in definition.Parameters ?? [])
        {
            Parameters.Add(new PatchParameterViewModel(parameter));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PatchDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Description => Definition.Description;

    public string Meta { get; }

    public ObservableCollection<PatchParameterViewModel> Parameters { get; } = [];

    public string Tag => Definition.Game.ToUpperInvariant();

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public IReadOnlyDictionary<string, string> Values() =>
        Parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Formatted);
}
