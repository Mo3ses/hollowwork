using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Hollowwork.Patches;

namespace Hollowwork.App.ViewModels;

public sealed class PatchItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public event Action? ParameterChanged;

    public PatchItemViewModel(PatchDefinition definition)
    {
        Definition = definition;
        var target = definition.Targets[0];
        Meta = $"{target.Root} · {string.Join(", ", target.Extensions)}";
        foreach (var parameter in definition.Parameters ?? [])
        {
            var viewModel = new PatchParameterViewModel(parameter);
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PatchParameterViewModel.Value))
                {
                    ParameterChanged?.Invoke();
                }

                if (args.PropertyName == nameof(PatchParameterViewModel.PreviewBrush))
                {
                    OnPropertyChanged(nameof(ExploredBrush));
                    OnPropertyChanged(nameof(EdgeBrush));
                    OnPropertyChanged(nameof(UnexploredBrush));
                }
            };
            Parameters.Add(viewModel);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PatchDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Description => Definition.Description;

    public string Meta { get; }

    public ObservableCollection<PatchParameterViewModel> Parameters { get; } = [];

    public bool IsMinimap => Definition.Id == "poe1.minimap-colors";

    public IBrush? ExploredBrush => BrushFor("explored");

    public IBrush? EdgeBrush => BrushFor("edge");

    public IBrush? UnexploredBrush => BrushFor("unexplored");

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

    private IBrush? BrushFor(string key) =>
        Parameters.FirstOrDefault(parameter => parameter.Key == key)?.PreviewBrush;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public IReadOnlyDictionary<string, string> Values() =>
        Parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Formatted);
}
