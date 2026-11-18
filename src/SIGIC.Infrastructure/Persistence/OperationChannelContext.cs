using SIGIC.Application.Security.Ports;

namespace SIGIC.Infrastructure.Persistence;

public enum OperationChannel
{
    Pos,
    Web,
    Worker
}

public interface IOperationChannelContext
{
    OperationChannel Channel { get; }
    string PoolName { get; }
    string ConnectionString { get; }
}

public class OperationChannelContext : IOperationChannelContext
{
    public OperationChannel Channel { get; }
    public string PoolName { get; }
    public string ConnectionString { get; }

    public OperationChannelContext(
        DatabasePoolOptions options,
        ICurrentUserContext? userContext = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool isWorker = false;
        bool isWeb = false;

        if (userContext != null)
        {
            var scope = userContext.TerminalScope ?? string.Empty;
            if (scope.Equals("WORKER", StringComparison.OrdinalIgnoreCase) ||
                userContext.HasRole("Worker") ||
                userContext.Roles.Any(r => r.Equals("Worker", StringComparison.OrdinalIgnoreCase)))
            {
                isWorker = true;
            }
            else if (scope.Contains("WEB", StringComparison.OrdinalIgnoreCase) ||
                     scope.Contains("WAN", StringComparison.OrdinalIgnoreCase) ||
                     userContext.HasRole("ClienteWeb") ||
                     userContext.Roles.Any(r => r.Contains("Web", StringComparison.OrdinalIgnoreCase)))
            {
                isWeb = true;
            }
        }

        if (isWorker)
        {
            Channel = OperationChannel.Worker;
            PoolName = "sigic_pos_pool";
            ConnectionString = options.PosConnection;
        }
        else if (isWeb)
        {
            Channel = OperationChannel.Web;
            PoolName = "sigic_web_pool";
            ConnectionString = options.WebConnection;
        }
        else
        {
            Channel = OperationChannel.Pos;
            PoolName = "sigic_pos_pool";
            ConnectionString = options.PosConnection;
        }
    }
}
