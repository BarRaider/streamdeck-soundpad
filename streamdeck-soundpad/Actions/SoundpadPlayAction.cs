using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundpad.Actions
{
    [PluginActionId("com.barraider.soundpadplay")]
    public class SoundpadPlayAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    SoundTitle = String.Empty,
                    ShowSoundTitle = false,
                    SoundIndex = String.Empty,
                    Sounds = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "sounds")]
            public List<SoundpadSound> Sounds { get; set; }

            [JsonProperty(PropertyName = "soundTitle")]
            public string SoundTitle { get; set; }

            [JsonProperty(PropertyName = "showSoundTitle")]
            public bool ShowSoundTitle { get; set; }

            [JsonProperty(PropertyName = "soundIndex")]
            public string SoundIndex { get; set; }
        }

        #region Private Members

        private PluginSettings settings;

        #endregion

        #region Public Methods

        public SoundpadPlayAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated += Instance_SoundsUpdated;
            settings.Sounds = SoundpadManager.Instance.GetAllSounds().GetAwaiter().GetResult();
            SaveSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated -= Instance_SoundsUpdated;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor Called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            SaveSettings();
            if (SoundpadManager.Instance.IsConnected &&
               (!String.IsNullOrEmpty(settings.SoundTitle) || !String.IsNullOrEmpty(settings.SoundIndex)))
            {
                bool success = false;
                if (!String.IsNullOrEmpty(settings.SoundIndex))
                {
                    if (Int32.TryParse(settings.SoundIndex, out int index))
                    {
                        success = await SoundpadManager.Instance.PlaySound(index);
                    }
                }
                else
                {
                    success = await SoundpadManager.Instance.PlaySound(settings.SoundTitle);
                }


                if (success)
                {
                    await Connection.ShowOk();
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to play sound! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.SoundTitle ?? ""}");
                    await Connection.ShowAlert();
                }
            }
            else
            {
                await Connection.ShowAlert();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Cannot play sound! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.SoundTitle ?? ""}");
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            if (!SoundpadManager.Instance.IsConnected)
            {
                Connection.SetImageAsync(Properties.Settings.Default.SoundPadNotRunning);
                Connection.SetTitleAsync(null);
                return;
            }

            Connection.SetImageAsync((string)null);
            if (settings.ShowSoundTitle && !String.IsNullOrEmpty(settings.SoundTitle) && String.IsNullOrEmpty(settings.SoundIndex))
            {
                Connection.SetTitleAsync(settings.SoundTitle);
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (!String.IsNullOrEmpty(settings.SoundIndex) && !Int32.TryParse(settings.SoundIndex, out _))
            {
                settings.SoundIndex = string.Empty;
            }
            SaveSettings();
        }

        #endregion

        #region Private Methods

        private async void Instance_SoundsUpdated(object sender, EventArgs e)
        {
            settings.Sounds = await SoundpadManager.Instance.GetAllSounds();
            await SaveSettings();
        }


        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLower())
                {
                    case "refreshsounds":
                        SoundpadManager.Instance.CacheAllSounds();
                        break;
                }
            }
        }

        #endregion 
    }
}
