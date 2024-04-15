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
    [PluginActionId("com.barraider.soundpadplayrand")]
    public class SoundpadPlayRandomAction : KeypadBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Categories = null,
                    Category = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "categories")]
            public List<SoundpadCategory> Categories { get; set; }

            [JsonProperty(PropertyName = "category")]
            public string Category { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;

        #endregion

        public SoundpadPlayRandomAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

            _ = InitializeSettings();
        }

        
        public override void Dispose() 
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated -= Instance_SoundsUpdated;
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            await SoundpadManager.Instance.PlayRandomSound(settings.Category);
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            if (!SoundpadManager.Instance.IsConnected)
            {
                Connection.SetImageAsync(Properties.Settings.Default.SoundPadNotRunning);
                return;
            }

            Connection.SetImageAsync((string)null);
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "refreshsounds":
                        _ = SoundpadManager.Instance.CacheAllSounds();
                        break;
                }
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override async void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            await InitializeSettings();
        }

        #region Private Methods

        private async void Instance_SoundsUpdated(object sender, EventArgs e)
        {
            await InitializeSettings();
        }

        private async Task InitializeSettings()
        {
            settings.Categories = await SoundpadManager.Instance.GetAllCategories();
            await SaveSettings();
        }
        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        #endregion
    }
}
