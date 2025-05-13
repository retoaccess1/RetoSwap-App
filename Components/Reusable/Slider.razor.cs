using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Manta.Components.Reusable;

public class Size
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public partial class Slider : ComponentBase
{
    private const double left = 4;

    private ElementReference containerElement;
    private ElementReference dragElement;
    private double startX;
    private double offsetX = left;


    private string LeftPx => $"{offsetX}px";

    public bool IsDragging { get; set; }
    public double ContainerWidth { get; private set; }
    public double CircleWidth { get; private set; }

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    public bool ReachedEnd { get; set; }

    [Parameter]
    public EventCallback<bool> OnReachedEnd { get; set; }

    [Parameter] 
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Parameter]
    public string? Text { get; set; }

    public double SliderProgress { get; set; }

    //public string StartColour = "#131313";
    //public string EndColour = "#ffffff";

    //public string CurrentColour = "#ffffff";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var size = await JS.InvokeAsync<Size>("GetSizeOfElement", containerElement);
            ContainerWidth = size.Width;

            size = await JS.InvokeAsync<Size>("GetSizeOfElement", dragElement);
            CircleWidth = size.Width;
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private void OnPointerDown(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        IsDragging = true;
        startX = e.ClientX - offsetX;
    }

    private async Task OnPointerMove(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (!IsDragging) 
            return;

        offsetX = e.ClientX - startX;

        if (ReachedEnd && offsetX <= left + 20)
        {
            ReachedEnd = false;
        }

        if (offsetX < left)
        {
            offsetX = left;
        }
        else if (offsetX > ContainerWidth - CircleWidth - (left + 1))
        {
            offsetX = ContainerWidth - CircleWidth - (left + 1);

            if (!ReachedEnd)
            {
                ReachedEnd = true;
                await OnReachedEnd.InvokeAsync(true);
            }
        }

        //var percentage = (100 / (ContainerWidth - CircleWidth)) * offsetX;

        //percentage = Math.Clamp(percentage / 100.0, 0.0, 1.0);

        //Color start = ColorFromHex(StartColour);
        //Color end = ColorFromHex(EndColour);

        //byte r = (byte)(start.Red + (end.Red - start.Red) * percentage);
        //byte g = (byte)(start.Green + (end.Green - start.Green) * percentage);
        //byte b = (byte)(start.Blue + (end.Blue - start.Blue) * percentage);

        //CurrentColour = $"#{r:X2}{g:X2}{b:X2}";
    }

    private static Color ColorFromHex(string hex)
    {
        hex = hex.Replace("#", "");

        if (hex.Length == 6)
        {
            float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;

            return new Color(r, g, b);
        }

        throw new Exception();
    }

    private void OnPointerUp(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        IsDragging = false;
        offsetX = left;
    }
}
