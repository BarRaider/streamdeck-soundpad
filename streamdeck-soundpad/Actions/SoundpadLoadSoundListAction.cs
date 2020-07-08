using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundpad.Actions
{
    [PluginActionId("com.barraider.soundpadloadsoundlist")]
    public class SoundpadLoadSoundListAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    SoundListFileName = String.Empty
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "soundListFileName")]
            public string SoundListFileName { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;

        #endregion

        #region Public Methods

        public SoundpadLoadSoundListAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            SaveSettings();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor Called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            if (SoundpadManager.Instance.IsConnected && 
               (!String.IsNullOrEmpty(settings.SoundListFileName)))
            {
                bool success = false;
                if (!File.Exists(settings.SoundListFileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"LoadSoundList - File not found: {settings.SoundListFileName}");
                }
                else
                {
                    success = await SoundpadManager.Instance.LoadPlaylist(settings.SoundListFileName);
                }

                if (success)
                {
                    await Connection.ShowOk();
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to Load SoundList! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.SoundListFileName ?? ""}");
                    await Connection.ShowAlert();
                }
            }
            else
            {
                await Connection.ShowAlert();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Cannot Load SoundList! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.SoundListFileName ?? ""}");
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
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        #endregion

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        #endregion
    }
}
