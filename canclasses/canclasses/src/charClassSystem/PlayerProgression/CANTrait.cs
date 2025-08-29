using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using canclasses.src.characterClassesSystem;
using Vintagestory.GameContent;

namespace canclasses.src.charClassSystem.PlayerProgression
{
    public class CANTrait
    {
        public string Code;
        public EnumTraitType Type;
        public Dictionary<string, Dictionary<double, double>> Attributes;
        public EnumTraitApplyType ApplyType;
        public Dictionary<string, object> additionalInfo;

        public virtual Dictionary<string, double> getAttributesToApply()
        {
            return new Dictionary<string, double>();
        }


    }
}
