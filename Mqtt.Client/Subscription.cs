using DotNetty.Codecs.Mqtt.Packets;
using System;
using System.Text.RegularExpressions;

namespace Mqtt.Client
{
    public class Subscription
    {
        private Regex regex;
        public Subscription(string topic, Action<Packet> callback)
        {
            Topic = topic;
            Callback = callback;
            regex = new Regex(topic.Replace("+", "[^/]+").Replace("#", ".+") + "$");
        }

        public string Topic { get; private set; }

        public Action<Packet> Callback { get; private set; }

        public bool IsMatch(string topic)
        {
            return regex.IsMatch(topic);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (this == obj) return true;
            if (!(obj is Subscription)) return false;
            var that = (Subscription)obj;
            return Topic == that.Topic && Callback == that.Callback;
        }

        public override int GetHashCode()
        {
            int result = Topic.GetHashCode();
            result = 31 * result + Callback.GetHashCode();
            return result;
        }
    }
}
