using Microsoft.Extensions.Configuration;
using System;

namespace Easy.Common
{
    public static class SettingsHelper
    {
        public static IConfigurationRoot Configuration { get; }
        static SettingsHelper()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(ProcessDirectory)
                .AddJsonFile("settings.json")
                .Build();
        }
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
    }
}
