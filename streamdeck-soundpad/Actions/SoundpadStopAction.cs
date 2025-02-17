using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundpad.Actions
{
    [PluginActionId("com.barraider.soundpadstop")]
    public class SoundpadStopAction : KeypadBase
    {
        public SoundpadStopAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            SoundpadManager.Instance.Connect();
        }

        public override void Dispose() { }

        public override void KeyPressed(KeyPayload payload)
        {
            SoundpadManager.Instance.Stop();
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

        public override void ReceivedSettings(ReceivedSettingsPayload payload) { }
    }
}
