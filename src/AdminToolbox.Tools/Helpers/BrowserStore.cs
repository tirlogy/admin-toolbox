// SPDX-License-Identifier: MIT
using Microsoft.JSInterop;

namespace AdminToolbox.Tools.Helpers;

/// <summary>Local history in localStorage. Storage may be unavailable (private mode), so every call tolerates failure.</summary>
public sealed class BrowserStore
{
    private const int MaxItems = 10;
    private readonly IJSRuntime _js;

    public BrowserStore(IJSRuntime js) => _js = js;

    public async Task<List<string>> GetHistoryAsync(string key)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("atb.get", "atb.history." + key);
            return string.IsNullOrEmpty(raw) ? new List<string>() : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(MaxItems).ToList();
        }
        catch (JSException)
        {
            return new List<string>();
        }
    }

    public async Task<List<string>> PushHistoryAsync(string key, string value)
    {
        var list = await GetHistoryAsync(key);
        value = value.Replace('\n', ' ').Trim();
        list.Remove(value);
        list.Insert(0, value);
        if (list.Count > MaxItems)
        {
            list.RemoveRange(MaxItems, list.Count - MaxItems);
        }

        await TryAsync("atb.set", "atb.history." + key, string.Join('\n', list));
        return list;
    }

    public Task ClearHistoryAsync(string key) => TryAsync("atb.remove", "atb.history." + key);

    public async Task<bool> CopyAsync(string text)
    {
        try
        {
            return await _js.InvokeAsync<bool>("atb.copy", text);
        }
        catch (JSException)
        {
            return false;
        }
    }

    private async Task TryAsync(string fn, params object[] args)
    {
        try
        {
            await _js.InvokeVoidAsync(fn, args);
        }
        catch (JSException)
        {
            // storage unavailable: history simply stays empty
        }
    }
}
