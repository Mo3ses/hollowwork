using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Hollowwork.Patches;

namespace Hollowwork.App.ViewModels;

public sealed class PatchItemViewModel : INotifyPropertyChanged
{
    private static readonly IBrush SealOn = new SolidColorBrush(Color.Parse("#7FE0D0"));
    private static readonly IBrush StrokeOn = new SolidColorBrush(Color.Parse("#7FE0D0"));
    private static readonly IBrush StrokeOff = new SolidColorBrush(Color.Parse("#397A73"));

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

    public IBrush SealFill => _isSelected ? SealOn : Brushes.Transparent;

    public IBrush SealStroke => _isSelected ? StrokeOn : StrokeOff;

    public string CompactSummary => IsMinimap
        ? string.Empty
        : Parameters.Count > 0
            ? Parameters[0].Display
            : string.Empty;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SealFill)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SealStroke)));
        }
    }

    private IBrush? BrushFor(string key) =>
        Parameters.FirstOrDefault(parameter => parameter.Key == key)?.PreviewBrush;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public IReadOnlyDictionary<string, string> Values() =>
        Parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Formatted);
}
