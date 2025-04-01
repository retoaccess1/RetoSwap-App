using Grpc.Core;
using Microsoft.AspNetCore.Components.Web;

namespace Manta.Components.Reusable;

public partial class ErrorHandler : ErrorBoundary
{
    public string? Message { get; set; }
    public Exception? Exception { get; set; }
    public bool IsOpen 
    { 
        get; 
        set 
        {
            field = value;
            if (!value)
            {
                Exception = null;
            }
        } 
    }

    protected override Task OnErrorAsync(Exception e)
    {
        Exception = e;
        if (e is RpcException)
            Message = Exception.Message.Split("Detail=\"")[1].TrimEnd("\")").ToString();
        else
            Message = e.Message;
        IsOpen = true;
        return Task.CompletedTask;
    }
}
