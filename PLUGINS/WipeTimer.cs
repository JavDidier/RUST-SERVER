using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Wipe Timer", "Rick", "1.1.0")]
    [Description("Allows players to check when the next wipe is")]
    class WipeTimer : CovalencePlugin
    {
        #region Configuration

        DefaultConfig config;

        class DefaultConfig
        {
            public int year = 2023;
            public int month = 1;
            public int day = 13;
            public int hour = 16;
            public int min = 30;
            public int sec = 0;
            public bool AutoRespond = true;
            public bool AnnouceOnConnect = true;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            config = new DefaultConfig();
            Config.WriteObject(config, true);
            SaveConfig();
        }

        private new void LoadDefaultMessages()
        {
            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandWipe"] = "Time until next wipe: {0}"
            }, this);
        }

        #endregion

        void Init()
        {
            try
            {
                config = Config.ReadObject<DefaultConfig>();
            }
            catch
            {
                PrintWarning("Could not read config, creating new default config");
                LoadDefaultConfig();
            }
        }

        [Command("wipe")]
        void cmdWipe(IPlayer p, string command, string[] args)
        {
            DateTime date1 = new DateTime(config.year, config.month, config.day, config.hour, config.min, config.sec);
            System.TimeSpan diff1 = date1.Subtract(DateTime.Now);

            string time = string.Format("{0}d:{1}h:{2}m", diff1.Days, diff1.Hours, diff1.Minutes);
            p.Reply("<color=#aaff55>" + Lang("CommandWipe", p.Id, time) + "</color>");
        }

        object OnUserChat(IPlayer p, string message)
        {
            DateTime date1 = new DateTime(config.year, config.month, config.day, config.hour, config.min, config.sec);
            System.TimeSpan diff1 = date1.Subtract(DateTime.Now);

            string time = string.Format("{0}d:{1}h:{2}m", diff1.Days, diff1.Hours, diff1.Minutes);
            if (message.Contains("wipe") && config.AutoRespond)
            {
                p.Reply("<color=#aaff55>" + Lang("CommandWipe", p.Id, time + "</color>"));
            }
            return null;
        }

        void OnUserConnected(IPlayer p)
        {
            DateTime date1 = new DateTime(config.year, config.month, config.day, config.hour, config.min, config.sec);
            System.TimeSpan diff1 = date1.Subtract(DateTime.Now);

            string time = string.Format("{0}d:{1}h:{2}m", diff1.Days, diff1.Hours, diff1.Minutes);
            if (config.AnnouceOnConnect)
            {
                p.Reply("<color=#aaff55>" + Lang("CommandWipe", p.Id, time + "</color>"));
            }
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}