using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Transport.Channels;
using System;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public class PendingPublish
    {
        private readonly RetransmissionAction<PublishPacket> publishRetransmissionAction;
        private readonly RetransmissionAction<PubRelPacket> pubRelRetransmissionAction;
        public PendingPublish(PublishPacket packet, TaskCompletionSource<object> promise)
        {
            Packet = packet;
            Promise = promise;
            publishRetransmissionAction = new RetransmissionAction<PublishPacket>();
            pubRelRetransmissionAction = new RetransmissionAction<PubRelPacket>();
            publishRetransmissionAction.OriginalPacket = packet;
        }

        public PublishPacket Packet { get; private set; }

        public TaskCompletionSource<object> Promise { get; private set; }

        public bool Sent { get; set; }

        //RetransmitPublish->Timer->WriteAndFlush
        public void RetransmitPublish(IEventLoop eventLoop, Func<Packet, Task> sendPacket)
        {
            publishRetransmissionAction.Action = (publishPacket) =>
            {
                return sendPacket(duplicateRetainPublishPacket(publishPacket));
            };
            publishRetransmissionAction.Start(eventLoop);
        }

        private PublishPacket duplicateRetainPublishPacket(PublishPacket packet)
        {
            var qos = packet.QualityOfService;
            var retain = packet.RetainRequested;
            PublishPacket result = new PublishPacket(qos, true, retain)
            {
                PacketId = packet.PacketId,
                Payload = packet.Payload,
                TopicName = packet.TopicName,
            };
            result.Retain();
            return packet;
        }

        public void OnPubAckReceived()
        {
            publishRetransmissionAction.Stop();
        }

        public void SetPubRelPacket(PubRelPacket pubRelPacket)
        {
            pubRelRetransmissionAction.OriginalPacket = pubRelPacket;
        }

        public void RetransmitPubRel(IEventLoop eventLoop, Func<Packet, Task> sendPacket)
        {
            pubRelRetransmissionAction.Action = sendPacket;
            pubRelRetransmissionAction.Start(eventLoop);
        }

        public void OnPubCompReceived()
        {
            pubRelRetransmissionAction.Stop();
        }
    }
}
