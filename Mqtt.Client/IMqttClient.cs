using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public interface IMqttClient
    {
        ILogger Logger { get; set; }

        IConnectionChangeCallback OnConnectionChange { get; set; }

        Task<ConnectResult> ConnectAsync(string host, int port);

        Task PublishAsync(string topic, byte[] payload, QualityOfService qos= QualityOfService.AtMostOnce, bool retain=false);

        Task SubscribeAsync(string topic, QualityOfService qos, Action<Packet> action);

        Task UnsubscribeAsync(string topic, Action<Packet> action=null);

        Task DisconnectAsync();

        void OnChannelActive(IChannelHandlerContext context);

        void OnChannelInActive(IChannelHandlerContext context);

        void OnConnAck(IChannelHandlerContext context,ConnAckPacket packet);

        void OnPublish(IChannelHandlerContext context, PublishPacket packet);

        void OnPubAck(IChannelHandlerContext context, PubAckPacket packet);

        void OnPubRec(IChannelHandlerContext context, PubRecPacket packet);

        void OnPubRel(IChannelHandlerContext context, PubRelPacket packet);

        void OnPubComp(IChannelHandlerContext context, PubCompPacket packet);

        void OnSubAck(IChannelHandlerContext context, SubAckPacket packet);

        void OnUnsubAck(IChannelHandlerContext context, UnsubAckPacket packet);
    }
}
