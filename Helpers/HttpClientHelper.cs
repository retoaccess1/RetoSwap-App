namespace Manta.Helpers;

public static class HttpClientHelper
{
    public static async Task<Stream> DownloadWithProgressAsync(string url, IProgress<double> progressCb, HttpClient httpClient)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? -1L;
        long totalRead = 0;
        var buffer = new byte[8192];

        using var stream = await response.Content.ReadAsStreamAsync();

        var ms = new MemoryStream();

        double lastPercent = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await ms.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            var currentPercent = (double)totalRead / contentLength * 100;
            if (currentPercent - lastPercent > 1f || currentPercent >= 100f)
            {
                lastPercent = currentPercent;
                progressCb?.Report(currentPercent);
            }
        }

        ms.Position = 0;

        return ms;
    }
}
