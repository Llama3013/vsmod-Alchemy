using System.Collections.Generic;
using ProtoBuf;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [ProtoContract]
    public class AlchemyConfigSyncPacket
    {
        [ProtoMember(1)]
        public Dictionary<string, bool> Bools { get; set; } = [];

        [ProtoMember(2)]
        public Dictionary<string, int> Ints { get; set; } = [];

        [ProtoMember(3)]
        public Dictionary<string, float> Floats { get; set; } = [];

        [ProtoMember(4)]
        public Dictionary<string, string> Strings { get; set; } = [];
    }
}
