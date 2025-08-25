using canclasses.src.charClassSystem.PlayerProgression;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace canclasses.src.characterClassesSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CANCharacterProgressInfoPacket
    {      
        public PlayerCharacterClassProgressInfo playerCharacterClassProgressInfo;
        public EnumCANCharacterProgressInfoPacket packetType;
        public SubClassType subClassTypeNewLevel;

    }
    
}
