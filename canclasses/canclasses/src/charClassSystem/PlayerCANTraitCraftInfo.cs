using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace canclasses.src.characterClassesSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PlayerCANTraitCraftInfo
    {
        public string traitCode;
        public int possibleCraftsPerReset;
        public int usedCraftsThisReset;
        public long lastResetTime;
        public long resetInterval;

        public PlayerCANTraitCraftInfo()
        {

        }
        public PlayerCANTraitCraftInfo(string code, int pcpr, long ri)
        {
            traitCode = code;
            possibleCraftsPerReset = pcpr;
            resetInterval = ri;
            usedCraftsThisReset = 0;
            lastResetTime = 0;
        }
    }
}
