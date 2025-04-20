using ZXing.Net.Maui.Controls;
using ZXing.Net.Maui;

namespace Manta;

public partial class CameraPage : ContentPage
{
    public CameraPage()
    {
        InitializeComponent();

        CameraBarcodeScannerView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false,
            TryInverted = true
        };
    }

    private TaskCompletionSource<BarcodeResult[]> ScanTCS = new();
    public Task<BarcodeResult[]> WaitForResultAsync()
    {
        return ScanTCS.Task;
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var barcode = e.Results.FirstOrDefault()?.Value;
        if (barcode is not null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ScanTCS.TrySetResult(e.Results);
            });
        }
    }
}