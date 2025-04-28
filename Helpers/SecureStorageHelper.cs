using System.Text.Json;

namespace Manta.Helpers;

public class SecureStorageHelper
{
    public static async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var item = await SecureStorage.GetAsync(key);
            if (item is null)
                return default;

            return JsonSerializer.Deserialize<T>(item);
        }
        catch
        {
            return default;
        }
    }

    public static async Task SetAsync<T>(string key, T value)
    {
        try
        {
            await SecureStorage.SetAsync(key, JsonSerializer.Serialize(value));
        }
        catch
        {

        }
    }

    public static T? Get<T>(string key)
    {
        return Task.Run(() => GetAsync<T>(key)).GetAwaiter().GetResult();
    }

    public static void Set<T>(string key, T value)
    {
        Task.Run(() => SetAsync(key, value)).GetAwaiter().GetResult();
    }
}
