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
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: CyberlightGames
    // Geekie_Benji - Tip: $10.10
    //---------------------------------------------------
    [PluginActionId("com.barraider.soundpadremove")]
    public class SoundpadRemoveAction : KeypadBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    RemoveSoundIndex = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "removeSoundIndex")]
            public string RemoveSoundIndex { get; set; }
        }

        #region Private Members
        private const int DEFAULT_REMOVE_INDEX = 0;


        private readonly PluginSettings settings;
        private int removeIndex = DEFAULT_REMOVE_INDEX;

        #endregion

        #region Public Methods


        public SoundpadRemoveAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            SoundpadManager.Instance.Connect();
            _ = InitializeSettings();

        }

        public override void Dispose() { }

        public async override void KeyPressed(KeyPayload payload)
        {
            if (String.IsNullOrEmpty(settings.RemoveSoundIndex) || removeIndex == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Key pressed but invalid sound index {settings.RemoveSoundIndex}");
                await Connection.ShowAlert();
                return;
            }
            await SoundpadManager.Instance.RemoveSound(removeIndex);
            await Connection.ShowOk();            
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            if (!SoundpadManager.Instance.IsConnected)
            {
                Connection.SetImageAsync(Properties.Settings.Default.SoundPadNotRunning);
                return;
            }
            else if (SoundpadManager.Instance.IsTrial)
            {
                Connection.SetImageAsync(Properties.Settings.Default.SoundPadTrial);
                Connection.SetTitleAsync(null);
                return;
            }

            Connection.SetImageAsync((string)null);
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override async void ReceivedSettings(ReceivedSettingsPayload payload) 
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            await InitializeSettings();
        }

        #endregion

        #region Private Methods

        private async Task InitializeSettings()
        {
            if (!Int32.TryParse(settings.RemoveSoundIndex, out removeIndex))
            {
                settings.RemoveSoundIndex = DEFAULT_REMOVE_INDEX.ToString();
                await SaveSettings();
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }
        #endregion
    }


}
