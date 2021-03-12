using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;
using Mqtt.Client;
using System;

namespace Mqtt.Handler
{
    public class MqttPingHandler : SimpleChannelInboundHandler<object>
    {
        public override bool IsSharable => false;

        private IScheduledTask pingRespTimeout = null;

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            if (msg is Packet packet)
            {
                switch (packet.PacketType)
                {
                    case PacketType.PINGREQ:
                        HandlePingReq(ctx.Channel);
                        break;
                    case PacketType.PINGRESP:
                        HandlePingResp();
                        break;
                    default:
                        ctx.FireChannelRead(ReferenceCountUtil.Retain(msg));
                        break;
                }
            }
            else
                ctx.FireChannelRead(msg);
        }

        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            base.UserEventTriggered(ctx, evt);

            if (evt is IdleStateEvent evt2)
            {
                switch (evt2.State)
                {
                    case IdleState.ReaderIdle:
                        break;
                    case IdleState.WriterIdle:
                        SendPingReq(ctx.Channel);
                        break;
                }
            }
        }

        private void SendPingReq(IChannel channel)
        {
            channel.WriteAndFlushAsync(PingReqPacket.Instance);
            if(pingRespTimeout  == null)
            {
                pingRespTimeout = channel.EventLoop.Schedule(() =>
                {
                    channel.WriteAndFlushAsync(DisconnectPacket.Instance);
                    //TODO: what do when the connection is closed ?
                }, TimeSpan.FromSeconds(ClientSettings.KeepAlive));
            }
        }

        private void HandlePingReq(IChannel channel)
        {
            channel.WriteAndFlushAsync(PingRespPacket.Instance);
        }

        private void HandlePingResp()
        {
            if(pingRespTimeout != null)
            {
                pingRespTimeout.Cancel();
                pingRespTimeout = null;
            }
        }
    }
}
