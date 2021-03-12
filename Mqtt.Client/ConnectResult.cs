using DotNetty.Codecs.Mqtt.Packets;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public class ConnectResult
    {        
        public ConnectResult(bool success, ConnectReturnCode returnCode, Task closeFuture)
        {
            Success = success;
            ReturnCode = returnCode;
            CloseFuture = closeFuture;
        }

        public bool Success { get; private set; }
        public ConnectReturnCode ReturnCode { get; private set; }
        public Task CloseFuture { get; private set; }
    }
}
