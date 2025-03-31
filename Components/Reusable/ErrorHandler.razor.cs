using Microsoft.AspNetCore.Components.Web;

namespace Manta.Components.Reusable;

public partial class ErrorHandler : ErrorBoundary
{

    protected override Task OnErrorAsync(Exception e)
    {
        return Task.CompletedTask;
    }
}
