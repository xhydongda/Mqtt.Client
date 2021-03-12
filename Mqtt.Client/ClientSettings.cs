using Easy.Common;
using System;
using System.Net;

namespace Mqtt.Client
{
    public class ClientSettings
    {
        public static string ClientId => GetValue("clientid","mqttclient");

        public static bool IsSsl => GetValue("ssl", false);

        public static IPAddress Host => IPAddress.Parse(GetValue("host","127.0.0.1"));

        public static int Port => GetValue("port", 1883);

        public static string UserName => SettingsHelper.Configuration["username"];

        public static string Password => SettingsHelper.Configuration["password"];

        public static int KeepAlive => GetValue("keepalive", 60);

        public static bool CleanSession => GetValue("cleansession", true);

        public static int HeartBeat => GetValue("heartbeat",20);

        public static int ReconnectInterval => GetValue("reconnectinterval", 5);

        public static int MaxMessageSize => GetValue("maxmessagesize", 256*1024); 

        public static bool UseLibuv => GetValue("libuv", false);

        public static bool EnableLoggingHandler => GetValue("enablelogginghandler", true);

        public static string KeyFile => GetValue("keyfile", "easygetcloud.pfx");

        public static string ProcessDirectory
        {
            get
            {
#if NETSTANDARD1_3
                return AppContext.BaseDirectory;
#else
                return AppDomain.CurrentDomain.BaseDirectory;
#endif
            }
        }

        public static T GetValue<T>(string key, T defaultValue)
        {
            var str = SettingsHelper.Configuration[key];
            if (!string.IsNullOrEmpty(str))
            {
                return (T)Convert.ChangeType(str, typeof(T));
            }
            return defaultValue;
        }
    }
}
