using Microsoft.JSInterop;

namespace PingPong.Api.Services;

public sealed class UserIdentityService
{
    private const string IdentityNameKey = "pp.identity.name";
    private const string TutorialSeenKey = "pp.tutorial.seen";

    private readonly IJSRuntime _jsRuntime;

    public UserIdentityService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event EventHandler? IdentityChanged;

    public async Task<string?> GetIdentityNameAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("storageHelper.getItem", IdentityNameKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetIdentityNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _jsRuntime.InvokeVoidAsync("storageHelper.setItem", IdentityNameKey, name);
        IdentityChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearIdentityAsync()
    {
        await _jsRuntime.InvokeVoidAsync("storageHelper.removeItem", IdentityNameKey);
        IdentityChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> GetTutorialSeenAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("storageHelper.getItem", TutorialSeenKey);
            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    public async Task SetTutorialSeenAsync(bool seen = true)
    {
        await _jsRuntime.InvokeVoidAsync("storageHelper.setItem", TutorialSeenKey, seen ? "true" : "false");
    }
}

