using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class NotesViewModel : ObservableObject, INavigationAware
{
    private readonly NotesFloatViewModel _notesFloatVm;
    private readonly FloatWindowService _floatWindowService;

    public NotesFloatViewModel FloatVm => _notesFloatVm;

    public NotesViewModel(
        NotesFloatViewModel notesFloatVm,
        FloatWindowService floatWindowService)
    {
        _notesFloatVm = notesFloatVm;
        _floatWindowService = floatWindowService;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = _notesFloatVm.InitializeAsync();
    }

    [RelayCommand]
    public void ToggleFloatDrawer()
    {
        _floatWindowService.Toggle(FloatWindowType.Notes);
    }
}
