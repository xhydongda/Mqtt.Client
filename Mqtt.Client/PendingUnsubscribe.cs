using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public class PendingUnsubscribe
    {
        private readonly RetransmissionAction<UnsubscribePacket> retransmissionAction;
        public PendingUnsubscribe(TaskCompletionSource<object> promise, string topic, UnsubscribePacket packet)
        {
            Promise = promise;
            Topic = topic;
            retransmissionAction = new RetransmissionAction<UnsubscribePacket>()
            {
                OriginalPacket = packet
            };
        }

        public string Topic { get; }

        public TaskCompletionSource<object> Promise { get; private set; }

        public void Retransmit(IEventLoop eventLoop, Func<Packet,Task> sendPacket)
        {
            retransmissionAction.Action = sendPacket;
            retransmissionAction.Start(eventLoop);
        }

        public void OnUnsubActReceived()
        {
            retransmissionAction.Stop();
        }
    }
}
