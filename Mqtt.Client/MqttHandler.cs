using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Transport.Channels;

namespace Mqtt.Client
{
    public class MqttHandler : SimpleChannelInboundHandler<Packet>
    {
        readonly IMqttClient client;
        public MqttHandler(IMqttClient client)
        {
            this.client = client;
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, Packet packet)
        {
            switch (packet.PacketType)
            {
                case PacketType.CONNACK:
                    client.OnConnAck(ctx,(ConnAckPacket)packet);
                    break;
                case PacketType.SUBACK:
                    client.OnSubAck(ctx, (SubAckPacket)packet);
                    break;
                case PacketType.PUBLISH:
                    client.OnPublish(ctx, (PublishPacket)packet);
                    break;
                case PacketType.UNSUBACK:
                    client.OnUnsubAck(ctx, (UnsubAckPacket)packet);
                    break;
                case PacketType.PUBACK:
                    client.OnPubAck(ctx, (PubAckPacket)packet);
                    break;
                case PacketType.PUBREC:
                    client.OnPubRec(ctx, (PubRecPacket)packet);
                    break;
                case PacketType.PUBREL:
                    client.OnPubRel(ctx, (PubRelPacket)packet);
                    break;
                case PacketType.PUBCOMP:
                    client.OnPubComp(ctx, (PubCompPacket)packet);
                    break;
            }
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            client.OnChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
            client.OnChannelInActive(context);
        }
    }
}
