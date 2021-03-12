using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public class PendingSubscribe
    {
        private readonly RetransmissionAction<SubscribePacket> retransmissionAction;
        public PendingSubscribe(TaskCompletionSource<object> promise, string topic, SubscribePacket packet)
        {
            Promise = promise;
            Topic = topic;
            Packet = packet;
            Callbacks = new List<Action<Packet>>();

            retransmissionAction = new RetransmissionAction<SubscribePacket>()
            {
                OriginalPacket = packet
            };
        }

        public TaskCompletionSource<object> Promise { get; private set; }

        public string Topic { get; private set; }

        public bool Sent { get; set; }

        public SubscribePacket Packet { get; private set; }

        public void AddCallback(Action<Packet> callback)
        {
            Callbacks.Add(callback);
        }

        public List<Action<Packet>> Callbacks { get; private set; }

        public void Retransmit(IEventLoop eventLoop, Func<Packet, Task> sendPacket)
        {
            if (Sent)//If the packet is sent, we can start the retransmit timer
            {
                retransmissionAction.Action = sendPacket;
            }
            retransmissionAction.Start(eventLoop);
        }

        public void OnSubActReceived()
        {
            retransmissionAction.Stop();
        }
    }
}
