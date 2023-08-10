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
    [PluginActionId("com.barraider.soundpadplayrandfromcategory")]
    public class SoundpadPlayRandomActionFromCategory : PluginBase
    {
        private class PluginSettings
        {
            [JsonProperty(PropertyName = "categories")]
            public List<SoundpadCategory> Categories { get; set; }

            [JsonProperty(PropertyName = "categoryTitle")]
            public string CategoryTitle { get; set; }

            [JsonProperty(PropertyName = "showCategoryTitle")]
            public bool ShowCategoryTitle { get; set; }

            [JsonProperty(PropertyName = "categoryIndex")]
            public string CategoryIndex { get; set; }

            [JsonProperty(PropertyName = "avoidRepetition")]
            public bool AvoidRepetition { get; set; }

            [JsonProperty(PropertyName = "pushToPlay")]
            public bool PushToPlay { get; set; }

            public static PluginSettings CreateDefaultSettings()
            {
                var instance = new PluginSettings
                {
                    CategoryTitle = string.Empty,
                    ShowCategoryTitle = false,
                    CategoryIndex = string.Empty,
                    Categories = null,
                    PushToPlay = false
                };
                return instance;
            }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private bool titleIsDrawn;
        private Queue<int> shuffledQueue = new Queue<int>();
        private int shuffledQueueOriginalSize = -1;

        #endregion

        #region Public Methods

        public SoundpadPlayRandomActionFromCategory(SDConnection connection, InitialPayload payload) : base(connection,
            payload)
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
            SoundpadManager.Instance.CategoriesUpdated += InstanceOnCategoriesUpdated;

            settings.Categories = SoundpadManager.Instance.GetAllCategories()
                .GetAwaiter()
                .GetResult()
                .OrderBy(x => x.Name)
                .ToList();

            SaveSettings();
        }

        public override void Dispose()
        {
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            SoundpadManager.Instance.CategoriesUpdated -= InstanceOnCategoriesUpdated;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor Called");
        }

        public override async void KeyPressed(KeyPayload payload)
        {
            _ = SaveSettings();

            if (SoundpadManager.Instance.IsConnected &&
                (!string.IsNullOrEmpty(settings.CategoryTitle) || !string.IsNullOrEmpty(settings.CategoryIndex)))
            {
                var categories = await SoundpadManager.Instance.GetAllCategories();

                SoundpadCategory categoryToPlayFrom = default;

                if (!string.IsNullOrEmpty(settings.CategoryIndex))
                {
                    if (int.TryParse(settings.CategoryIndex, out var requestedCategoryIndex))
                    {
                        categoryToPlayFrom =
                            categories.FirstOrDefault(category => category.Index == requestedCategoryIndex);
                    }
                }
                else
                {
                    categoryToPlayFrom = categories.FirstOrDefault(category => category.Name == settings.CategoryTitle);
                }

                var success = false;

                if (categoryToPlayFrom != default)
                {
                    if (settings.AvoidRepetition)
                    {
                        // If the amount of sounds changed, the user probably updated their sound list in SoundPad.
                        // In that case, force a regeneration.
                        if (shuffledQueueOriginalSize != categoryToPlayFrom.Sounds.Count)
                        {
                            shuffledQueue = new Queue<int>();
                        }

                        bool didAppend = false;
                        
                        // If the queue is smaller than a threshold, then append a new random order to the queue.
                        while (shuffledQueue.Count < categoryToPlayFrom.Sounds.Count + 1)
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO,
                                $"Queue of random sounds of category={categoryToPlayFrom.Index} is smaller than the threshold. Appending elements.");
                            
                            // Here, we want to take the last X elements from the queue and avoid them in the collection
                            // we are appending to the queue. This is so that the appended collection does not instantly
                            // repeat the last X elements of the current queue at it's start.
                            var avoidCountFromPreviousIteration = 1;

                            if (categoryToPlayFrom.Sounds.Count <= avoidCountFromPreviousIteration)
                            {
                                avoidCountFromPreviousIteration = 0;
                            }

                            var lastTwoElements = shuffledQueue.Reverse().Take(avoidCountFromPreviousIteration);

                            var iterationResult = categoryToPlayFrom.Sounds
                                .Where(x => !lastTwoElements.Contains(x.SoundIndex))
                                .OrderBy(x => RandomGenerator.Next(0, int.MaxValue))
                                .Select(x => x.SoundIndex)
                                .ToList();

                            foreach (var element in iterationResult)
                            {
                                shuffledQueue.Enqueue(element);
                            }
                            
                            shuffledQueueOriginalSize = categoryToPlayFrom.Sounds.Count;
                            didAppend = true;
                        }

                        if (didAppend)
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO,
                                $"New shuffled indices of sound queue are={string.Join(",", shuffledQueue.Select(x => x.ToString()))}");
                        }

                        var nextSoundIndex = shuffledQueue.Dequeue();

                        Logger.Instance.LogMessage(TracingLevel.INFO,
                            $"Playing shuffled index={nextSoundIndex} of category={categoryToPlayFrom.Index}");

                        success = await SoundpadManager.Instance.PlaySound(nextSoundIndex);
                    }
                    else
                    {
                        var randomSoundLocalIndex = RandomGenerator.Next(categoryToPlayFrom.Sounds.Count);

                        Logger.Instance.LogMessage(TracingLevel.INFO,
                            $"Playing random index={categoryToPlayFrom.Sounds[randomSoundLocalIndex].SoundIndex} of category={categoryToPlayFrom.Index}");

                        success = await SoundpadManager.Instance.PlaySound(categoryToPlayFrom
                            .Sounds[randomSoundLocalIndex]
                            .SoundIndex);
                    }
                }

                if (success)
                {
                    await Connection.ShowOk();
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"Failed to play sound! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.CategoryTitle ?? ""}");
                    await Connection.ShowAlert();
                }
            }
            else
            {
                await Connection.ShowAlert();
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"Cannot play sound! Connected: {SoundpadManager.Instance.IsConnected} File: {settings.CategoryTitle ?? ""}");
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

            if (settings.ShowCategoryTitle && !string.IsNullOrEmpty(settings.CategoryTitle) &&
                string.IsNullOrEmpty(settings.CategoryIndex))
            {
                if (!titleIsDrawn)
                {
                    // Only draw the title if we haven't yet.
                    Connection.SetTitleAsync(Tools.SplitStringToFit(settings.CategoryTitle, titleParameters, 5, 5));
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

            if (!string.IsNullOrEmpty(settings.CategoryIndex) && !int.TryParse(settings.CategoryIndex, out _))
            {
                settings.CategoryIndex = string.Empty;
            }

            SaveSettings();
        }

        #endregion

        #region Private Methods

        private async void InstanceOnCategoriesUpdated(object sender, EventArgs e)
        {
            settings.Categories = (await SoundpadManager.Instance.GetAllCategories()).OrderBy(x => x.Name).ToList();

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
                    case "refreshSounds":
                        _ = SoundpadManager.Instance.CacheAllCategories();
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