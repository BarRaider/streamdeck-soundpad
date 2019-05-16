using BarRaider.SdTools;
using SoundpadConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundpad
{
    public class SoundpadManager
    {
        #region Private Members
        private static SoundpadManager instance = null;
        private static readonly object objLock = new object();

        private SoundpadConnector.Soundpad soundpad;
        private Dictionary<string, int> dicSounds;
        private bool isProbablyConnected = false;

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
            soundpad = new SoundpadConnector.Soundpad() { AutoReconnect = true};
            soundpad.Connected += Soundpad_Connected;
            soundpad.Disconnected += Soundpad_Disconnected;
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
            if (!IsConnected)
            {
                soundpad.ConnectAsync();
            }
        }

        public bool PlaySound(string soundTitle)
        {
            
            if (!IsConnected)
            {
                Connect();
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not play sound - Soundpad not connected");
                return false;
            }

            if (dicSounds.ContainsKey(soundTitle))
            {
                soundpad.PlaySound(dicSounds[soundTitle]);
                return true;
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Could not find sound to play: {soundTitle}");
                return false;
            }
        }

        public void Stop()
        {
            soundpad.StopSound();
        }

        public List<SoundpadSound> GetAllSounds()
        {
            List<SoundpadSound> sounds = new List<SoundpadSound>();
            if (dicSounds != null)
            {
                foreach (string title in dicSounds?.Keys)
                {
                    sounds.Add(new SoundpadSound() { SoundName = title, SoundIndex = dicSounds[title] });
                }
            }

            return sounds;
        }

        public async void CacheAllSounds()
        {
            if (IsConnected)
            {
                var response = await soundpad.GetSoundlist();
                if (response.IsSuccessful)
                {
                    int soundIndex;
                    dicSounds = new Dictionary<string, int>();
                    foreach (var sound in response.Value.Sounds)
                    {
                        if (Int32.TryParse(sound.Index, out soundIndex))
                        {
                            dicSounds[sound.Title] = soundIndex;
                        }
                        else
                        {
                            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to convert sound index {sound.Index} to integer");
                        }
                    }
                    SoundsUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
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
