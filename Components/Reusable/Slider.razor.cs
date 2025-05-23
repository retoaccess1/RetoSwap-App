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
    [Parameter]
    public bool Disabled { get; set; }

    public double SliderProgress { get; set; }

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
        if (Disabled)
            return;

        IsDragging = true;
        startX = e.ClientX - offsetX;
    }

    private async Task OnPointerMove(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (!IsDragging || Disabled) 
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
    }

    private void OnPointerUp(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (Disabled)
            return;

        IsDragging = false;
        offsetX = left;
    }
}
