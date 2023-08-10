using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soundpad.Properties;

namespace Soundpad.Actions
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Tek_Soup
    //---------------------------------------------------
    [PluginActionId("com.barraider.soundpadplay")]
    public class SoundpadPlayAction : PluginBase
    {
        private class PluginSettings
        {
            [JsonProperty(PropertyName = "sounds")]
            public List<SoundpadSound> Sounds { get; set; }

            [JsonProperty(PropertyName = "soundTitle")]
            public string SoundTitle { get; set; }

            [JsonProperty(PropertyName = "showSoundTitle")]
            public bool ShowSoundTitle { get; set; }

            [JsonProperty(PropertyName = "soundIndex")]
            public string SoundIndex { get; set; }

            [JsonProperty(PropertyName = "pushToPlay")]
            public bool PushToPlay { get; set; }

            public static PluginSettings CreateDefaultSettings()
            {
                var instance = new PluginSettings
                {
                    SoundTitle = string.Empty,
                    ShowSoundTitle = false,
                    SoundIndex = string.Empty,
                    Sounds = null,
                    PushToPlay = false
                };
                return instance;
            }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private bool titleIsDrawn;

        #endregion

        #region Public Methods

        public SoundpadPlayAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated += Instance_SoundsUpdated;
            
            settings.Sounds = SoundpadManager.Instance.GetAllSounds()
                .GetAwaiter()
                .GetResult()
                .OrderBy(x => x.SoundName)
                .ToList();
            
            SaveSettings();
        }

        public override void Dispose()
        {
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            SoundpadManager.Instance.SoundsUpdated -= Instance_SoundsUpdated;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor Called");
        }

        public override async void KeyPressed(KeyPayload payload)
        {
            _ = SaveSettings();
            if (SoundpadManager.Instance.IsConnected &&
                (!string.IsNullOrEmpty(settings.SoundTitle) || !string.IsNullOrEmpty(settings.SoundIndex)))
            {
                var success = false;
                if (!string.IsNullOrEmpty(settings.SoundIndex))
                {
                    if (int.TryParse(settings.SoundIndex, out var index))
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
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"Failed to play sound! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.SoundTitle ?? ""}");
                    await Connection.ShowAlert();
                }
            }
            else
            {
                await Connection.ShowAlert();
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"Cannot play sound! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.SoundTitle ?? ""}");
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
            if (settings.PushToPlay)
            {
                SoundpadManager.Instance.Stop();
            }
        }

        public override void OnTick()
        {
            if (!SoundpadManager.Instance.IsConnected)
            {
                Connection.SetImageAsync(Settings.Default.SoundPadNotRunning);
                Connection.SetTitleAsync(null);
                titleIsDrawn = false;

                return;
            }

            Connection.SetImageAsync((string) null);

            if (settings.ShowSoundTitle && !string.IsNullOrEmpty(settings.SoundTitle) &&
                string.IsNullOrEmpty(settings.SoundIndex))
            {
                if (!titleIsDrawn)
                {
                    // Only draw the title if we haven't yet.
                    Connection.SetTitleAsync(Tools.SplitStringToFit(settings.SoundTitle, titleParameters, 5, 5));
                    titleIsDrawn = true;
                }
            }
            else
            {
                // If we drew the title but it's not supposed to be here anymore, then hide it.
                if (titleIsDrawn)
                {
                    Connection.SetTitleAsync(string.Empty);
                    titleIsDrawn = false;
                }
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (!string.IsNullOrEmpty(settings.SoundIndex) && !int.TryParse(settings.SoundIndex, out _))
            {
                settings.SoundIndex = string.Empty;
            }

            SaveSettings();
        }

        #endregion

        #region Private Methods

        private async void Instance_SoundsUpdated(object sender, EventArgs e)
        {
            settings.Sounds = (await SoundpadManager.Instance.GetAllSounds()).OrderBy(x => x.SoundName).ToList();

            await SaveSettings();
        }


        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void Connection_OnSendToPlugin(object sender, SDEventReceivedEventArgs<SendToPlugin> e)
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

        private void Connection_OnTitleParametersDidChange(
            object sender,
            SDEventReceivedEventArgs<TitleParametersDidChange> e)
        {
            titleParameters = e.Event?.Payload?.TitleParameters;
        }

        #endregion
    }
}