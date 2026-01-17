using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace VIWI.Modules.KitchenSink;

[Serializable]
public class KitchenSinkConfig
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = false;

    public sealed class CharacterData
    {
        public ulong LocalContentId { get; set; }

        public bool IsGlamourDresserInitialized { get; set; }

        public HashSet<uint> GlamourDresserItems { get; set; } = new HashSet<uint>();

        public bool WarnAboutLeves { get; set; }
    }

    public List<CharacterData> Characters { get; set; } = new List<CharacterData>();
    public bool ShowOnlyMissingGlamourSets { get; set; } = true;
}
