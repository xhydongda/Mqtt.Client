using DotNetty.Codecs.Mqtt.Packets;
using DotNetty.Common.Concurrency;
using DotNetty.Transport.Channels;
using System;
using System.Threading.Tasks;

namespace Mqtt.Client
{
    /// <summary>
    /// retry action after a keep growing delay.
    /// </summary>
    /// <typeparam name="T">Mqtt Packet</typeparam>
    public class RetransmissionAction<T> where T:Packet
    {
        int timeout = 10;
        IScheduledTask timer;
        public void Start(IEventLoop eventLoop)
        {
            if(eventLoop == null)
            {
                throw new ArgumentNullException("eventLoop");
            }
            if(Action == null)
            {
                throw new ArgumentNullException("action");
            }
            timeout = 10;
            startTimer(eventLoop);
        }

        private void startTimer(IEventLoop eventLoop)
        {
            timer = eventLoop.Schedule(async () =>
            {
                timeout += 5;
                await Action(OriginalPacket);
                startTimer(eventLoop);
            },TimeSpan.FromSeconds(timeout));
        }

        public void Stop()
        {
            if(timer != null)
            {
                timer.Cancel();
            }
        }

        public T OriginalPacket { get; set; }

        public Func<T,Task> Action { get; set; }
    }
}
