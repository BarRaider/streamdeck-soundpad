using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundpad
{
    public class SoundpadSound
    {
        [JsonProperty(PropertyName = "soundName")]
        public string SoundName { get; set; }

        [JsonProperty(PropertyName = "soundIndex")]
        public int SoundIndex { get; set; }
    }
}
