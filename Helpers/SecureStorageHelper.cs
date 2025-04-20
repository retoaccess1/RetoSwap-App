using System.Text.Json;

namespace Manta.Helpers;

public class SecureStorageHelper
{
    public static async Task<T?> GetAsync<T>(string key)
    {
        var item = await SecureStorage.GetAsync(key);
        if (item is null)
            return default;

        return JsonSerializer.Deserialize<T>(item);
    }

    public static async Task SetAsync<T>(string key, T value)
    {
        await SecureStorage.SetAsync(key, JsonSerializer.Serialize(value));
    }
}
