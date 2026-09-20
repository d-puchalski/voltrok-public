using Microsoft.AspNetCore.Components;
using Serilog;
using VoltrokServices.Services;
using VoltrokServices.Services.Translations;

namespace VoltrokWebApp.Components;

public abstract class AppStateComponentBase : ComponentBase, IDisposable
{
    [Inject] protected AppState AppState { get; set; } = null!;
    [Inject] protected TranslationService T { get; set; } = null!;

    protected override void OnInitialized()
    {
        AppState.OnChange += HandleStateChanged;
    }

    private async void HandleStateChanged()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                await OnAppStateChangedAsync();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            Log.Debug("{ComponentName} ignored AppState change because the component was already disposed.", GetType().Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled AppState change error in component {ComponentName}.", GetType().Name);
        }
    }

    protected virtual Task OnAppStateChangedAsync() => Task.CompletedTask;

    public void Dispose()
    {
        AppState.OnChange -= HandleStateChanged;
    }
}
