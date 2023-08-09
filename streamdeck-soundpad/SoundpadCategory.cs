using System.Collections.Generic;
using Newtonsoft.Json;

namespace Soundpad
{
    public class SoundpadCategory
    {
        [JsonProperty(PropertyName = "categoryName")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "categoryIndex")]
        public int Index { get; set; }
        
        [JsonProperty]
        public List<SoundpadSound> Sounds { get; set; }
    }
}