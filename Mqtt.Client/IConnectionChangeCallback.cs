using System;

namespace Mqtt.Client
{
    public interface IConnectionChangeCallback
    {
        /// <summary>
        /// This method is called when the connection to the server is lost.
        /// </summary>
        /// <param name="cause">the reason behind the loss of connection</param>
        void OnConnectionLost(Exception cause);

        /// <summary>
        /// This method is called when the connection to the server is recovered.
        /// </summary>
        void OnReconnect();
    }
}
