using Microsoft.AspNetCore.SignalR;

namespace UNSInfra.UI;

/// <summary>
/// Exception filter for SignalR to handle socket-related errors gracefully
/// </summary>
public class SignalRExceptionFilter : IHubFilter
{
    private readonly ILogger<SignalRExceptionFilter> _logger;

    public SignalRExceptionFilter(ILogger<SignalRExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, 
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("SocketAddress"))
        {
            _logger.LogWarning(ex, "Socket address error in SignalR hub method {Method}", invocationContext.HubMethodName);
            // Return null to gracefully handle the error
            return null;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("SocketAddress"))
        {
            _logger.LogWarning(ex, "Socket address argument error in SignalR hub method {Method}", invocationContext.HubMethodName);
            // Return null to gracefully handle the error
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SignalR hub method {Method}", invocationContext.HubMethodName);
            throw;
        }
    }
}