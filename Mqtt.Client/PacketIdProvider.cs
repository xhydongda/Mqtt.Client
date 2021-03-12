using System.Threading;

namespace Mqtt.Client
{
    internal class PacketIdProvider
    {
        private static int packetId = 1;
        
        public static int NewPacketId()
        {
            var retVal = Interlocked.Increment(ref packetId);
            if (retVal == ushort.MaxValue)
            {
                Interlocked.Exchange(ref packetId, 1);
            }
            return retVal - 1;
        }
    }
}