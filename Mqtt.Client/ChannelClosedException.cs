using System;
using System.IO;

namespace Mqtt.Client
{
    public class ChannelClosedException : IOException
    {
        public ChannelClosedException() : base() { }

        public ChannelClosedException(string message) : base(message) { }

        public ChannelClosedException(string message, Exception cause) :base(message, cause) { }
    }
}
