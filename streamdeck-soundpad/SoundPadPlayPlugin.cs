using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundpad
{
    [PluginActionId("com.barraider.soundpadplay")]
    public class SoundpadPlayPlugin : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings();
                instance.SoundTitle = String.Empty;
                instance.ShowSoundTitle = false;
                instance.Sounds = null;
                return instance;
            }

            [JsonProperty(PropertyName = "sounds")]
            public List<SoundpadSound> Sounds { get; set; }

            [JsonProperty(PropertyName = "soundTitle")]
            public string SoundTitle { get; set; }

            [JsonProperty(PropertyName = "showSoundTitle")]
            public bool ShowSoundTitle { get; set; }
        }

        #region Private Members

        private PluginSettings settings;

        #endregion

        #region Public Methods

        public SoundpadPlayPlugin(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.StreamDeckConnection.OnSendToPlugin += StreamDeckConnection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated += Instance_SoundsUpdated;
            settings.Sounds = SoundpadManager.Instance.GetAllSounds();
            SaveSettings();
        }

        public override void Dispose()
        {
            Connection.StreamDeckConnection.OnSendToPlugin -= StreamDeckConnection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated -= Instance_SoundsUpdated;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor Called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            if (!String.IsNullOrEmpty(settings.SoundTitle) && SoundpadManager.Instance.IsConnected)
            {
                SoundpadManager.Instance.PlaySound(settings.SoundTitle);
                Connection.ShowOk();
            }
            else
            {
                Connection.ShowAlert();
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
            if (settings.ShowSoundTitle && !String.IsNullOrEmpty(settings.SoundTitle))
            {
                Connection.SetTitleAsync(settings.SoundTitle);
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        #endregion

        #region Private Methods

        private void Instance_SoundsUpdated(object sender, EventArgs e)
        {
            settings.Sounds = SoundpadManager.Instance.GetAllSounds();
            SaveSettings();
        }


        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void StreamDeckConnection_OnSendToPlugin(object sender, streamdeck_client_csharp.StreamDeckEventReceivedEventArgs<streamdeck_client_csharp.Events.SendToPluginEvent> e)
        {
            var payload = e.Event.Payload;
            if (Connection.ContextId != e.Event.Context)
            {
                return;
            }

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
