using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using canclasses.src.characterClassesSystem;
using ProtoBuf;

namespace canclasses.src.charClassSystem.PlayerProgression
{
    public enum SubClassType 
    {
        SMITH, MINER, FARMER, SOLDIER, HUNTER
    }
    [ProtoContract]
    public class PlayerSubClass
    {
        [ProtoMember(1)]
        public double AlreadyCollectedExp { get; set; }
        [ProtoMember(2)]
        public int PercentsReached { get; set; }
        [ProtoMember(3)]
        public double ExpToNextLeft{ get; set; }
        [ProtoMember(4)]
        public double ExpToNextBorder { get; set; }
        [ProtoMember(5)]
        public int LevelsGotThisDay { get; set; }
        [ProtoMember(6)]
        public int WhichDay { get; set; }
        [ProtoMember(7)]
        public SubClassType SubClassType { get; set; }
        [ProtoMember(8)]
        public HashSet<string> AccuredTraits { get; set; } = new HashSet<string>();
        public PlayerSubClass()
        {

        }
    }
}
