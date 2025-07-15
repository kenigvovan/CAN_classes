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
        public int currentLevel;
        public double currentExpToNextLevel;
        public double allExpToNextLevel;
        public EnumCANCharacterProgressInfoPacket packetType;

    }
    
}
