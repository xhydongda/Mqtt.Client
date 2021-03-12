using DotNetty.Buffers;
using DotNetty.Codecs.Mqtt;
using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Handlers.Logging;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    public class MqttClient : IMqttClient
    {
        volatile bool disconnected = false;
        volatile bool reconnecting = false;

        IEventLoopGroup eventLoopGroup;
        volatile IChannel channel;
        readonly ConcurrentDictionary<int, PendingPublish> pendingPublishes;        //publish   to   mqtt server with QoS>0.
        readonly ConcurrentDictionary<int, PendingQoS2Publish> pendingIncomingQoS2Publishes;//  from mqtt server with QoS=2.
        readonly ConcurrentDictionary<int, PendingSubscribe> pendingSubscribes;     //subscribe to   mqtt server with QoS=1 fixed.
        readonly ConcurrentDictionary<int, PendingUnsubscribe> pendingUnsubscribes; //unsubscribe to mqtt server with QoS=1 fixed.
        readonly HashMultimap<Action<Packet>, Subscription> callbackSubscriptions;  //callback-subscription 
        readonly HashMultimap<string, Subscription> topicSubscriptions;             //topic-subscription
        readonly List<string> subscribedTopics;                                     //current subscribed topics
        readonly List<string> pendingSubscribeTopics;                               //unconfirmed subscribe topics

        public MqttClient()
        {
            pendingPublishes = new ConcurrentDictionary<int, PendingPublish>();
            pendingIncomingQoS2Publishes = new ConcurrentDictionary<int, PendingQoS2Publish>();
            pendingSubscribes = new ConcurrentDictionary<int, PendingSubscribe>();
            pendingUnsubscribes = new ConcurrentDictionary<int, PendingUnsubscribe>();
            pendingSubscribeTopics = new List<string>();
            topicSubscriptions = new HashMultimap<string, Subscription>();
            subscribedTopics = new List<string>();
            callbackSubscriptions = new HashMultimap<Action<Packet>, Subscription>();
        }

        protected ILogger logger;
        public ILogger Logger
        {
            get
            {
                if (logger == null) logger = NullLogger<MqttClient>.Instance;
                return logger;
            }
            set
            {
                logger = value;
            }
        }

        public IConnectionChangeCallback OnConnectionChange { get; set; }

        /// <summary>
        /// sub class override to supply will message etc.
        /// </summary>
        /// <returns>ConnectPacket</returns>
        protected virtual ConnectPacket GetConnectPacket()
        {
            return new ConnectPacket()
            {
                ProtocolName = "MQTT",
                ProtocolLevel = 4,
                ClientId = ClientSettings.ClientId,
                CleanSession = ClientSettings.CleanSession,
                KeepAliveInSeconds = ClientSettings.KeepAlive,
                HasUsername = true,
                HasPassword = true,
                Username = ClientSettings.UserName,
                Password = ClientSettings.Password,
                HasWill = false
            };
        }

        /// <summary>
        /// sub class override to add handlers between MqttEncoder/MqttDecoder and MqttHandler.
        /// </summary>
        /// <param name="pipeline">Channel pipeline</param>
        protected virtual void AddHandlers(IChannelPipeline pipeline)
        {
            //NOOP, leave sub class to add.
        }

        private bool isConnected()
        {
            return !disconnected && channel != null && channel.Active;
        }

        TaskCompletionSource<ConnectResult> connectFuture; //create a Task start with Connect, end by ConnAct.
        public async Task<ConnectResult> ConnectAsync(string host, int port)
        {
            if (isConnected()) 
                return new ConnectResult(true, ConnectReturnCode.Accepted, channel.CloseCompletion);

            if (eventLoopGroup == null) eventLoopGroup = new MultithreadEventLoopGroup();

            connectFuture = new TaskCompletionSource<ConnectResult>();

            X509Certificate2 cert = null;
            string targetHost = null;
            if (ClientSettings.IsSsl)
            {
                cert = new X509Certificate2(Path.Combine(ClientSettings.ProcessDirectory, ClientSettings.KeyFile), ClientSettings.Password);
                targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
            }
            var bootstrap = new Bootstrap();
            bootstrap
                .Group(eventLoopGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    if (cert != null)
                    {
                        pipeline.AddLast("tls", new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)));
                    }
                    if (ClientSettings.EnableLoggingHandler) pipeline.AddLast(new LoggingHandler());
                    pipeline.AddLast(MqttEncoder.Instance);
                    pipeline.AddLast(new MqttDecoder(false, ClientSettings.MaxMessageSize));
                    AddHandlers(pipeline);
                    pipeline.AddLast(new MqttHandler(this));
                }));
            try
            {
                IChannel clientChannel = await bootstrap.ConnectAsync(new IPEndPoint(IPAddress.Parse(host), port));
                if (clientChannel.Open)
                {
                    channel = clientChannel;
                    _= clientChannel.CloseCompletion.ContinueWith((t, s) =>
                    {
                        var self = (MqttClient)s;
                        if(self.isConnected())
                        {
                            return;
                        }
                        if(self.OnConnectionChange != null)
                        {                            
                            if(connectFuture.Task.IsCompleted)
                            {
                                var result = connectFuture.Task.Result;
                                if(result.ReturnCode != ConnectReturnCode.Accepted)
                                {
                                    ChannelClosedException e = new ChannelClosedException(result.ReturnCode.ToString());
                                    self.OnConnectionChange.OnConnectionLost(e);
                                    connectFuture.TrySetException(e);
                                }//ConnAct with refused code.
                            }
                            else 
                            {
                                ChannelClosedException e = new ChannelClosedException("Channel is closed!", connectFuture.Task.Exception);
                                self.OnConnectionChange.OnConnectionLost(e);
                                connectFuture.TrySetException(e);
                            }//Channel closed before ConnAct.
                        }
                        pendingPublishes.Clear();
                        pendingIncomingQoS2Publishes.Clear();
                        pendingSubscribes.Clear();
                        pendingSubscribeTopics.Clear();
                        pendingUnsubscribes.Clear();
                        topicSubscriptions.Clear();
                        subscribedTopics.Clear();
                        callbackSubscriptions.Clear();
                        reconnecting = true;
                        scheduleReconnect(host, port);//auto reconnect when channel closed.
                    }, this, TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    var e = new ClosedChannelException();
                    if (OnConnectionChange != null)
                    {
                        OnConnectionChange.OnConnectionLost(e);
                    }
                    connectFuture.SetException(e);
                    scheduleReconnect(host, port);//auto reconnect when connect failed.
                }
            }
            catch(Exception e)
            {
                connectFuture.SetException(e);
                scheduleReconnect(host, port); //auto reconnect when connect error.
            }
            return await connectFuture.Task;
        }
        private void scheduleReconnect(string host, int port)
        {
            if (!disconnected)
            {
                eventLoopGroup.Schedule(async () => await ConnectAsync(host, port), TimeSpan.FromSeconds(ClientSettings.ReconnectInterval));
            }
        }

        public async Task DisconnectAsync()
        {
            disconnected = true;
            if(channel != null)
            {
                await sendAndFlushAsync(DisconnectPacket.Instance).ContinueWith(async (t) => await channel.CloseAsync());
            }
        }

        public async Task PublishAsync(string topic, byte[] payload, QualityOfService qos = QualityOfService.AtMostOnce, bool retain = false)
        {
            TaskCompletionSource<object> future = new TaskCompletionSource<object>();
            PublishPacket packet = new PublishPacket(qos, false, retain);
            packet.TopicName = topic;
            packet.PacketId = PacketIdProvider.NewPacketId();
            packet.Payload = Unpooled.WrappedBuffer(payload);
            packet.Retain();            
            WriteResult result = await sendAndFlushAsync(packet);
            if (!result.Success)
            {
                packet.Release();//needed?
                future.SetException(result.Exception);
            }
            else if(qos== QualityOfService.AtLeastOnce)
            {
                packet.Release();//needed?
                future.SetResult(null);//We don't get an ACK for QOS 0
            }
            else
            {
                PendingPublish pendingPublish = new PendingPublish(packet, future);//return after PubAct(QoS=1)/PubRel(QoS=2) received.
                pendingPublishes.TryAdd(packet.PacketId, pendingPublish);
                pendingPublish.RetransmitPublish(eventLoopGroup.GetNext(), sendAndFlushAsync);
            }
            await future.Task;
        }

        public async Task SubscribeAsync(string topic, QualityOfService qos, Action<Packet> callback)
        {
            if(pendingSubscribeTopics.Contains(topic))
            {
                foreach(var subscription in pendingSubscribes.Values)
                {
                    if(subscription.Topic == topic)
                    {
                        subscription.AddCallback(callback);
                        await subscription.Promise.Task;
                        return;
                    }
                }
            }//subscribe is pending, return pending task.
            if (subscribedTopics.Contains(topic))
            {
                Subscription subscription = new Subscription(topic, callback);
                topicSubscriptions.Put(topic, subscription);
                callbackSubscriptions.Put(callback, subscription );
                return;//channel.newSucceededFuture()?
            }//already subscribed, add callback to topic's subscription.
            //send SubscribePacket and complete Task when SubAck received.
            TaskCompletionSource<object> future = new TaskCompletionSource<object>();
            SubscribePacket subscribePacket = new SubscribePacket(PacketIdProvider.NewPacketId(), new SubscriptionRequest(topic, qos));

            var pendingSubscription = new PendingSubscribe(future, topic, subscribePacket);
            pendingSubscription.AddCallback(callback);
            pendingSubscribes.TryAdd(subscribePacket.PacketId, pendingSubscription);
            pendingSubscribeTopics.Add(topic);
            var result = await sendAndFlushAsync(subscribePacket);
            pendingSubscription.Sent = result.Success;//If not sent, we will send it when the connection is opened

            pendingSubscription.Retransmit(eventLoopGroup.GetNext(), sendAndFlushAsync);

            await future.Task;
        }

        private async Task<WriteResult> sendAndFlushAsync(Packet packet)
        {
            if (channel == null)
            {
                return WriteResult.ChannelNull;
            }

            if (channel.Active)
            {
                try
                {
                    await channel.WriteAndFlushAsync(packet);
                    return WriteResult.Succeed;
                }
                catch(Exception e)
                {
                    return new WriteResult(e);
                }
            }
            else
            {
                return WriteResult.ChannelClosed;
            }
        }

        public Task UnsubscribeAsync(string topic, Action<Packet> callback = null)
        {
            if (callback != null && callbackSubscriptions.ContainsKey(callback))
            {
                TaskCompletionSource<object> promise = new TaskCompletionSource<object>();
                foreach (var subscription in callbackSubscriptions.Get(callback))
                {
                    topicSubscriptions.Remove(topic, subscription);
                }
                callbackSubscriptions.Remove(callback);
                return checkSubscriptionsAsync(topic, promise);
            }
            else if(callback == null && topicSubscriptions.ContainsKey(topic))
            {
                TaskCompletionSource<object> promise = new TaskCompletionSource<object>();
                var subscriptions = topicSubscriptions.Get(topic);
                foreach (var subscription in subscriptions)
                {
                    callbackSubscriptions.Remove(subscription.Callback);
                }
                topicSubscriptions.Remove(topic);
                return checkSubscriptionsAsync(topic, promise);
            }
            return Task.CompletedTask;
        }

        private async Task checkSubscriptionsAsync(string topic, TaskCompletionSource<object> promise)
        {
            if(!topicSubscriptions.ContainsKey(topic) && subscribedTopics.Contains(topic))
            {
                UnsubscribePacket packet = new UnsubscribePacket(PacketIdProvider.NewPacketId(), topic);
                PendingUnsubscribe pendingUnsubscribe = new PendingUnsubscribe(promise, topic, packet);
                pendingUnsubscribes.TryAdd(packet.PacketId, pendingUnsubscribe);
                pendingUnsubscribe.Retransmit(eventLoopGroup.GetNext(), sendAndFlushAsync);
                await sendAndFlushAsync(packet);
            }
            else
            {
                promise.SetResult(null);
            }
        }
        #region Handle Received Message 

        public void OnChannelActive(IChannelHandlerContext context)
        {

            var packet = GetConnectPacket();
            context.WriteAndFlushAsync(packet);
        }

        public void OnChannelInActive(IChannelHandlerContext context)
        {

        }

        public void OnConnAck(IChannelHandlerContext context, ConnAckPacket packet)
        {
            switch (packet.ReturnCode)
            {
                case ConnectReturnCode.Accepted:
                    connectFuture.SetResult(new ConnectResult(true, ConnectReturnCode.Accepted, context.Channel.CloseCompletion));
                    foreach (var value in pendingSubscribes.Values)
                    {
                        if (!value.Sent)
                        {
                            context.WriteAsync(value.Packet).Wait();
                            value.Sent = true;
                        }
                    }
                    foreach (var value in pendingPublishes.Values)
                    {
                        if (value.Sent) continue;

                        context.WriteAsync(value.Packet).Wait();
                        value.Sent = true;
                        if (value.Packet.QualityOfService == QualityOfService.AtMostOnce)
                        {
                            value.Promise.SetResult(null);//We don't get an ACK for QOS 0
                            pendingPublishes.TryRemove(value.Packet.PacketId, out _);
                        }                        
                    }
                    context.Flush();
                    if (reconnecting)
                    {
                        if (OnConnectionChange != null) OnConnectionChange.OnReconnect();
                    }
                    break;

                default:
                    connectFuture.SetResult(new ConnectResult(false, packet.ReturnCode, context.Channel.CloseCompletion));
                    context.CloseAsync();
                    break;
            }
        }

        public void OnPublish(IChannelHandlerContext context, PublishPacket packet)
        {
            switch (packet.QualityOfService)
            {
                case QualityOfService.AtMostOnce:
                    publishCallback(packet);
                    break;

                case QualityOfService.AtLeastOnce:
                    publishCallback(packet);
                    if (packet.PacketId != -1)
                    {
                        context.WriteAndFlushAsync(PubAckPacket.InResponseTo(packet));
                    }
                    break;

                case QualityOfService.ExactlyOnce:
                    if (packet.PacketId != -1)
                    {
                        var pubRecPacket = PubRecPacket.InResponseTo(packet);
                        PendingQoS2Publish pendingQoS2Publish = new PendingQoS2Publish(packet, pubRecPacket);
                        pendingIncomingQoS2Publishes.TryAdd(pubRecPacket.PacketId, pendingQoS2Publish);
                        packet.Retain();
                        pendingQoS2Publish.Retransmit(eventLoopGroup.GetNext(),  sendAndFlushAsync);
                        context.WriteAndFlushAsync(pubRecPacket);
                    }
                    break;
            }
        }
        private void publishCallback(PublishPacket packet)
        {
            bool invoked = false;
            foreach (var subscription in topicSubscriptions.Values)
            {
                if (subscription.IsMatch(packet.TopicName))
                {
                    packet.Payload.MarkReaderIndex();
                    subscription.Callback(packet);
                    packet.Payload.ResetReaderIndex();
                    invoked = true;
                }
            }
            if (!invoked)
            {
                Logger.LogWarning($"Publish packet {packet} is not subscribed");
            }
            packet.Release();
        }

        public void OnPubAck(IChannelHandlerContext context, PubAckPacket packet)
        {
            pendingPublishes.TryGetValue(packet.PacketId, out PendingPublish pendingPublish);
            if (pendingPublish == null) return; 

            pendingPublish.Promise.SetResult(null);
            pendingPublish.OnPubAckReceived();
            pendingPublishes.TryRemove(packet.PacketId, out _);
            pendingPublish.Packet.Release();
        }

        public void OnPubRec(IChannelHandlerContext context, PubRecPacket packet)
        {
            pendingPublishes.TryGetValue(packet.PacketId, out PendingPublish pendingPublish);
            if (pendingPublish == null) return;

            pendingPublish.OnPubAckReceived();

            var pubRelPacket = PubRelPacket.InResponseTo(packet);
            context.WriteAndFlushAsync(pubRelPacket);

            pendingPublish.SetPubRelPacket(pubRelPacket);
            pendingPublish.RetransmitPubRel(eventLoopGroup.GetNext(),  sendAndFlushAsync);
        }

        public void OnPubRel(IChannelHandlerContext context, PubRelPacket packet)
        {
            if (pendingIncomingQoS2Publishes.ContainsKey(packet.PacketId))
            {
                var qos2Publish = pendingIncomingQoS2Publishes[packet.PacketId];
                publishCallback(qos2Publish.PublishPacket);
                qos2Publish.OnPubRelReceived();
                pendingIncomingQoS2Publishes.TryRemove(packet.PacketId, out _);
            }
            context.WriteAndFlushAsync(PubCompPacket.InResponseTo(packet));
        }

        public void OnPubComp(IChannelHandlerContext context, PubCompPacket packet)
        {
            if (pendingPublishes.TryGetValue(packet.PacketId, out PendingPublish pendingPublish))
            {
                pendingPublish.Promise.SetResult(null);
                pendingPublishes.TryRemove(packet.PacketId, out _);
                pendingPublish.Packet.Release();
                pendingPublish.OnPubCompReceived();
            }
        }

        public void OnSubAck(IChannelHandlerContext context, SubAckPacket packet)
        {
            pendingSubscribes.TryRemove(packet.PacketId, out PendingSubscribe value);
            if (value == null) return;

            value.OnSubActReceived();
            foreach (var callback in value.Callbacks)
            {
                var subscription = new Subscription(value.Topic, callback);
                topicSubscriptions.Put(value.Topic, subscription);
                callbackSubscriptions.Put(callback,subscription);
            }
            pendingSubscribeTopics.Remove(value.Topic);
            subscribedTopics.Add(value.Topic);
            value.Promise.TrySetResult(null);
        }

        public void OnUnsubAck(IChannelHandlerContext context, UnsubAckPacket packet)
        {
            pendingUnsubscribes.TryGetValue(packet.PacketId, out PendingUnsubscribe pendingUnsubscribe);
            if (pendingUnsubscribe == null)  return; 

            pendingUnsubscribe.OnUnsubActReceived();
            topicSubscriptions.Remove(pendingUnsubscribe.Topic);
            pendingUnsubscribe.Promise.SetResult(null);
            pendingUnsubscribes.TryRemove(packet.PacketId, out _);
        }
        #endregion
    }
}
