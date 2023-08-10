using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using Newtonsoft.Json;
using SoundpadConnector;

namespace Soundpad
{
    public class SoundpadManager
    {
        #region Private Members

        private static SoundpadManager instance;
        private static readonly object objLock = new object();
        private readonly SemaphoreSlim cacheCategoriesLock = new SemaphoreSlim(1, 1);
        private const int CACHE_SOUNDS_COOLDOWN_MS = 2000;
        private const int CONNECT_COOLDOWN_MS = 2000;

        private readonly SoundpadConnector.Soundpad soundpad;
        private static Dictionary<string, SoundpadCategory> categories = new Dictionary<string, SoundpadCategory>();
        private static Dictionary<string, SoundpadSound> sounds = new Dictionary<string, SoundpadSound>();
        private static readonly object connectAttemptLock = new object();
        private bool isProbablyConnected;
        private DateTime lastCacheCategories = DateTime.MinValue;
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
            soundpad = new SoundpadConnector.Soundpad { AutoReconnect = true };
            soundpad.Connected += Soundpad_Connected;
            soundpad.Disconnected += Soundpad_Disconnected;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Attempting to connect to Soundpad");
            Connect();
        }

        #endregion

        #region Public Methods

        public event EventHandler<EventArgs> SoundsUpdated;
        public event EventHandler<EventArgs> CategoriesUpdated;

        public bool IsConnected => soundpad != null && isProbablyConnected;

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
                Logger.Instance.LogMessage(TracingLevel.WARN, "Could not play sound - Soundpad not connected");
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
                Logger.Instance.LogMessage(TracingLevel.WARN, "Could not play sound - Soundpad not connected");
                return false;
            }

            if (sounds.TryGetValue(soundTitle, out var sound))
            {
                return (await soundpad.PlaySound(sound.SoundIndex)).IsSuccessful;
            }

            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Could not find sound to play: {soundTitle}");
            return false;
        }

        public async Task<bool> PlayRandomSound()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Play Random Sound Called");
            if (!IsConnected)
            {
                Connect();
                Logger.Instance.LogMessage(TracingLevel.WARN, "Could not play random sound - Soundpad not connected");
                return false;
            }

            var sounds = await GetAllSounds();
            if (sounds.Count > 0)
            {
                var randomSoundIndex = RandomGenerator.Next(sounds.Count) + 1; // Indecies start at 1
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Playing Random Sound: {randomSoundIndex}");
                await soundpad.PlaySound(randomSoundIndex);
                return true;
            }

            Logger.Instance.LogMessage(TracingLevel.WARN, "Could not play random sound - No sounds exists");
            return false;
        }

        public void Stop()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Stop Sound Called");
            soundpad.StopSound();
        }

        public async Task RemoveSound(int index)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Removing Sound called for index {index}");
            await Task.Run(async () =>
            {
                await soundpad.SelectIndex(index);
                await Task.Delay(500);
                await soundpad.RemoveSelectedEntries();
            });
        }

        public void RecordStart()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "RecordStart Called");
            soundpad.StartRecording();
        }

        public void RecordStop()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "RecordStop Called");
            soundpad.StopRecording();
        }

        public async Task<bool> LoadPlaylist(string fileName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"LoadPlaylist Called: {fileName}");

            if ((await soundpad.LoadSoundlist(fileName)).IsSuccessful)
            {
                Thread.Sleep(1000); // Takes about 1 sec to refresh
                await CacheAllSounds();
                return true;
            }

            return false;
        }

        public async Task<List<SoundpadSound>> GetAllSounds()
        {
            await CacheAllCategories();

            return sounds.Select(x => x.Value).ToList();
        }

        public async Task<List<SoundpadCategory>> GetAllCategories()
        {
            await CacheAllCategories();

            return categories.Select(x => x.Value).ToList();
        }

        public async Task CacheAllSounds()
        {
            // This method originally loaded sounds from Soundpad. Since the new functionality to load the categories
            // also includes loading the list of all sounds in the same request, this method simply redirects.
            // Provided for backward compatibility of code written before this change.
            await CacheAllCategories();
        }

        public async Task CacheAllCategories()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "CacheAllCategories Called");
            await cacheCategoriesLock.WaitAsync();
            try
            {
                if (IsConnected)
                {
                    if ((DateTime.Now - lastCacheCategories).TotalMilliseconds < CACHE_SOUNDS_COOLDOWN_MS)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, "CacheAllCategories in cooldown");
                        return;
                    }

                    var response = await soundpad.GetCategories(true);

                    if (response.IsSuccessful)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Returned category data = {JsonConvert.SerializeObject(response.Value)}");
                        
                        // Convert returned data into a new object that can be safely passed around the application.
                        // This dictionary is never modified, only it's reference replaced, so that any other
                        // consumer will not run into exceptions while possibly iterating over it.
                        categories = response.Value.Categories.ToDictionary(category => category.Name,
                            category => new SoundpadCategory
                            {
                                Name = category.Name,
                                Index = category.Index,
                                Sounds = category.Sounds.Select(sound => new SoundpadSound
                                    {
                                        SoundName = sound.Title,
                                        SoundIndex = sound.Index
                                    })
                                    .ToList()
                            });

                        // See above. Done separately for sounds aswell.
                        sounds = categories
                            .SelectMany(category => category.Value.Sounds)
                            .ToDictionary(sound => sound.SoundName, sound => sound);

                        lastCacheCategories = DateTime.Now;
                        
                        CategoriesUpdated?.Invoke(this, EventArgs.Empty);
                        SoundsUpdated?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN,
                            $"GetCategories failed from SoundPad: {response.ErrorMessage}");
                    }
                }

                Logger.Instance.LogMessage(TracingLevel.INFO,
                    $"CacheAllCategories Done. {categories.Count} categories loaded.");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CacheAllCategories Exception: {ex}");
            }
            finally
            {
                cacheCategoriesLock.Release();
            }
        }

        public void TogglePause()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "TogglePause Called");
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