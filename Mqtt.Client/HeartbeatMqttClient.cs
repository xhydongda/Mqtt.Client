using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;
using Mqtt.Handler;

namespace Mqtt.Client
{
    public class HeartbeatMqttClient : MqttClient
    {
        readonly static IdleStateHandler idleHandler = new IdleStateHandler(0, ClientSettings.HeartBeat, 0);
        readonly static MqttPingHandler pingHandler = new MqttPingHandler();
        protected override void AddHandlers(IChannelPipeline pipeline)
        {
            pipeline.AddLast(idleHandler);
            pipeline.AddLast(pingHandler);
        }
    }
}
