using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Transport.Channels;
using System;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public class PendingQoS2Publish
    {
        private readonly RetransmissionAction<PubRecPacket> retransmissionAction;
        public PendingQoS2Publish(PublishPacket publishPacket, PubRecPacket pubRecPacket)
        {
            PublishPacket = publishPacket;
            retransmissionAction = new RetransmissionAction<PubRecPacket>()
            {
                OriginalPacket = pubRecPacket
            };
        }

        public PublishPacket PublishPacket { get; private set; }

        public void Retransmit(IEventLoop eventLoop, Func<Packet, Task> sendPacket)
        {
            retransmissionAction.Action = sendPacket;
            retransmissionAction.Start(eventLoop);
        }

        public void OnPubRelReceived()
        {
            retransmissionAction.Stop();
        }
    }
}
