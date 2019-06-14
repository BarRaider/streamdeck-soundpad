using BarRaider.SdTools;
using SoundpadConnector;
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
        private static SoundpadManager instance = null;
        private static readonly object objLock = new object();

        private SoundpadConnector.Soundpad soundpad;
        private static Dictionary<string, int> dicSounds;
        private static readonly object dicSoundsLock = new object();
        private bool isProbablyConnected = false;
        private Random rand = new Random();

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
            soundpad.ConnectAsync();

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
                Logger.Instance.LogMessage(TracingLevel.INFO, "Attempting to connect to Soundpad");
                soundpad.ConnectAsync();
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

        public bool PlayRandomSound()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Play Random Sound Called");
            if (!IsConnected)
            {
                Connect();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play random sound - Soundpad not connected");
                return false;
            }

            var sounds = GetAllSounds();
            if (sounds.Count > 0)
            {
                int randomSoundIndex = rand.Next(sounds.Count) + 1; // Indecies start at 1
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Playing Random Sound: {randomSoundIndex}");
                soundpad.PlaySound(randomSoundIndex);
                return true;
            }

            Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play random sound - No sounds exists");
            return false;
        }

        public void Stop()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Stop Sound Called");
            soundpad.StopSound();
        }

        public void RecordStart()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"RecordStart Called");
            soundpad.StartRecording();
        }

        public void RecordStop()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"RecordStop Called");
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

        public List<SoundpadSound> GetAllSounds()
        {
            List<SoundpadSound> sounds = new List<SoundpadSound>();
            if (dicSounds != null)
            {
                List<string> keys;
                lock (dicSoundsLock)
                {
                    keys = dicSounds?.Keys.ToList();
                }
                foreach (string title in keys)
                {
                    sounds.Add(new SoundpadSound() { SoundName = title, SoundIndex = dicSounds[title] });
                }
            }

            return sounds.OrderBy(x => x.SoundName).ToList();
        }

        public async Task CacheAllSounds()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"CacheAllSounds Called");
            if (IsConnected)
            {
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
                    SoundsUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"CacheAllSounds Done. {dicSounds?.Keys?.Count ?? -1} sounds loaded.");
        }

        #endregion

        #region Private Methods

        private void Soundpad_Disconnected(object sender, SoundpadConnector.Soundpad.OnDisconnectedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, "Disconnected from Soundpad");
            isProbablyConnected = false;
        }

        private void Soundpad_Connected(object sender, EventArgs e)
        {
            isProbablyConnected = true;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Connected to Soundpad");
            CacheAllSounds();
        }

        #endregion


    }
}
