using BarRaider.SdTools;
using SoundpadConnector;
using SoundpadConnector.XML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Soundpad
{
    public class SoundpadManager
    {
        #region Private Members
        private const string ALL_SOUNDS_CATEGORY = "All sounds";

        private static SoundpadManager instance = null;
        private static readonly object objLock = new object();
        private readonly SemaphoreSlim cacheSoundsLock = new SemaphoreSlim(1, 1);
        private const int CACHE_SOUNDS_COOLDOWN_MS = 2000;
        private const int CONNECT_COOLDOWN_MS = 2000;

        private readonly SoundpadConnector.Soundpad soundpad;
        private static Dictionary<string, int> dicSounds;
        private static readonly object dicSoundsLock = new object();
        private static Dictionary<string, Category> dicCategories;
        private static readonly object dicCategoriesLock = new object();
        private static readonly object connectAttemptLock = new object();
        private bool isProbablyConnected = false;
        private DateTime lastCacheSounds = DateTime.MinValue;
        private DateTime lastConnectAttempt = DateTime.MinValue;

        #endregion

        #region Constructors

        public static SoundpadManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new SoundpadManager();
                    }
                    return instance;
                }
            }
        }

        private SoundpadManager()
        {
            soundpad = new SoundpadConnector.Soundpad() { AutoReconnect = true };
            soundpad.Connected += Soundpad_Connected;
            soundpad.Disconnected += Soundpad_Disconnected;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Attempting to connect to Soundpad");
            Connect();
        }

        #endregion

        #region Public Methods

        public event EventHandler<EventArgs> SoundsUpdated;

        public bool IsConnected
        {
            get
            {
                return soundpad != null && isProbablyConnected;
            }
        }

        public void Connect()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Connect request received");
            if (!IsConnected)
            {
                if (soundpad.ConnectionStatus == ConnectionStatus.Connecting)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "Already trying to connect...");
                    return;
                }
                lock (connectAttemptLock)
                {
                    if (!IsConnected)
                    {
                        if ((DateTime.Now - lastConnectAttempt).TotalMilliseconds < CONNECT_COOLDOWN_MS)
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, "Connect in cooldown");
                            return;
                        }

                        if (soundpad.ConnectionStatus == ConnectionStatus.Connecting)
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, "Already trying to connect...");
                            return;
                        }
                        Logger.Instance.LogMessage(TracingLevel.INFO, "Attempting to connect to Soundpad");
                        lastConnectAttempt = DateTime.Now;
                        soundpad.ConnectAsync();
                    }
                }

            }
        }

        public async Task<bool> PlaySound(int soundIndex)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Play Sound in Index: {soundIndex}");
            if (!IsConnected)
            {
                Connect();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play sound - Soundpad not connected");
                return false;
            }

            return (await soundpad.PlaySound(soundIndex)).IsSuccessful;
        }

        public async Task<bool> PlaySound(string soundTitle)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Play Sound: {soundTitle}");
            if (!IsConnected)
            {
                Connect();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play sound - Soundpad not connected");
                return false;
            }

            if (dicSounds.ContainsKey(soundTitle))
            {
                return (await soundpad.PlaySound(dicSounds[soundTitle])).IsSuccessful;
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Could not find sound to play: {soundTitle}");
                return false;
            }
        }

        public async Task<bool> PlayRandomSound(string categoryName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Play Random Sound Called");
            if (!IsConnected)
            {
                Connect();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play random sound - Soundpad not connected");
                return false;
            }

            List<SoundpadSound> sounds = null;
            if (string.IsNullOrEmpty(categoryName))
            {
                sounds = await GetAllSounds();
            }
            else
            {
                sounds = await GetCategorySounds(categoryName);
            }

            if (sounds != null && sounds.Count > 0)
            {
                int randomSoundIndex = RandomGenerator.Next(sounds.Count);
                int allSoundsIndex = sounds[randomSoundIndex].SoundIndex;
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Playing Random Sound: {sounds[randomSoundIndex].SoundName}/{allSoundsIndex} from category {categoryName}");
                await soundpad.PlaySound(allSoundsIndex);
                return true;
            }

            Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play random sound - No sounds exists");
            return false;
        }

        public void Stop()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Stop Sound Called");
            _ = soundpad.StopSound();
        }

        public async Task RemoveSound(int index)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Removing Sound called for index {index}");
            await Task.Run(async () =>
            {
                await soundpad.SelectIndex(index);
                await Task.Delay(500);
                await soundpad.RemoveSelectedEntries(false);
            });
        }

        public void RecordStart()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"RecordStart Called");
            _ = soundpad.StartRecording();
        }

        public void RecordStop()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"RecordStop Called");
            _ = soundpad.StopRecording();
        }

        public async Task<bool> LoadPlaylist(string fileName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"LoadPlaylist Called: {fileName}");
            if ((await soundpad.LoadSoundlist(fileName)).IsSuccessful)
            {
                lastCacheSounds = DateTime.MinValue;
                Thread.Sleep(1000); // Takes about 1 sec to refresh
                await CacheAllSounds();
                return true;
            }
            return false;
        }

        public async Task<List<SoundpadSound>> GetAllSounds()
        {
            List<SoundpadSound> sounds = new List<SoundpadSound>();
            if (dicSounds != null)
            {
                if (dicSounds.Count == 0)
                {
                    await CacheAllSounds();
                }
                List<string> keys;
                lock (dicSoundsLock)
                {
                    keys = dicSounds?.Keys.ToList();
                    foreach (string title in keys)
                    {
                        sounds.Add(new SoundpadSound() { SoundName = title, SoundIndex = dicSounds[title] });
                    }
                }
            }

            return sounds.OrderBy(x => x.SoundName).ToList();
        }

        public async Task<List<SoundpadSound>> GetCategorySounds(string categoryName)
        {
            if (String.IsNullOrEmpty(categoryName) || categoryName == ALL_SOUNDS_CATEGORY)
            {
                return await GetAllSounds();
            }

            List<SoundpadSound> sounds = new List<SoundpadSound>();
            if (dicCategories != null)
            {
                if (dicCategories.Count == 0)
                {
                    await CacheAllSounds();
                }
                if (dicCategories.ContainsKey(categoryName))
                {
                    foreach (var sound in dicCategories[categoryName].Sounds)
                    {
                        sounds.Add(new SoundpadSound() { SoundName = sound.Title, SoundIndex = sound.Index });
                    }
                }
            }

            return sounds.OrderBy(x => x.SoundName).ToList();
        }

        public async Task<List<SoundpadCategory>> GetAllCategories()
        {
            List<SoundpadCategory> categories = new List<SoundpadCategory>();
            if (dicCategories != null)
            {
                if (dicCategories.Count == 0)
                {
                    await CacheAllSounds();
                }
                List<string> keys;
                lock (dicCategoriesLock)
                {
                    keys = dicCategories?.Keys.ToList();
                    foreach (string title in keys)
                    {
                        categories.Add(new SoundpadCategory() { CategoryName = title, CategoryIndex = dicCategories[title].Index });
                    }
                }
            }

            return categories.OrderBy(x => x.CategoryName).ToList();
        }

        public async Task CacheAllSounds()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"CacheAllSounds Called");
            await cacheSoundsLock.WaitAsync();
            try
            {
                bool soundsUpdated = false;
                if (IsConnected)
                {
                    if ((DateTime.Now - lastCacheSounds).TotalMilliseconds < CACHE_SOUNDS_COOLDOWN_MS)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"CacheAllSounds in cooldown");
                        return;
                    }
                    var response = await soundpad.GetSoundlist();
                    if (response.IsSuccessful)
                    {
                        lock (dicSoundsLock)
                        {
                            dicSounds = new Dictionary<string, int>();
                            foreach (var sound in response.Value.Sounds)
                            {
                                dicSounds[sound.Title] = sound.Index;
                            }
                        }
                        lastCacheSounds = DateTime.Now;
                        soundsUpdated = true;
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"GetSoundlist failed from SoundPad: {response.ErrorMessage}");
                    }

                    var catResponse = await soundpad.GetCategories(withSounds: true);
                    if (catResponse.IsSuccessful)
                    {
                        lock (dicCategoriesLock)
                        {
                            dicCategories = new Dictionary<string, Category>();
                            foreach (var cat in catResponse.Value.Categories)
                            {
                                dicCategories[cat.Name] = cat;
                            }
                        }
                        lastCacheSounds = DateTime.Now;
                        soundsUpdated = true;
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"GetCategories failed from SoundPad: {catResponse.ErrorMessage}");
                    }


                    if (soundsUpdated)
                    {
                        SoundsUpdated?.Invoke(this, EventArgs.Empty);
                    }
                }
                Logger.Instance.LogMessage(TracingLevel.INFO, $"CacheAllSounds Done. {dicSounds?.Keys?.Count ?? -1} sounds loaded. {dicCategories?.Keys?.Count ?? -1} categories loaded.");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CacheAllSounds Exception: {ex}");
            }
            finally
            {
                cacheSoundsLock.Release();
            }
        }

        public void TogglePause()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"TogglePause Called");
            soundpad.TogglePause();
        }

        #endregion

        #region Private Methods

        private void Soundpad_Disconnected(object sender, SoundpadConnector.Soundpad.OnDisconnectedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, "Disconnected from Soundpad");
            isProbablyConnected = false;
            lastConnectAttempt = DateTime.MinValue;
        }

        private void Soundpad_Connected(object sender, EventArgs e)
        {
            isProbablyConnected = true;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Connected to Soundpad");
            _ = CacheAllSounds();
        }

        #endregion


    }
}
