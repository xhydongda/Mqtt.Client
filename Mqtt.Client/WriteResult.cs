using System;

namespace Mqtt.Client
{
    public class WriteResult
    {        
        public readonly static WriteResult Succeed = new WriteResult();
        public readonly static WriteResult ChannelNull = new WriteResult(new ArgumentNullException("Channel is null"));
        public readonly static WriteResult ChannelClosed = new WriteResult(new ChannelClosedException("Channel is closed!"));
        private WriteResult()
        {
            Success = true;
        }
        public WriteResult(Exception e)
        {
            Success = false;
            Exception = e;
        }

        public bool Success { get; private set; }
        public Exception Exception { get; private set; }
    }
}
