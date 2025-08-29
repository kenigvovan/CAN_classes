using canclasses.src.charClassSystem.Network;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace canclasses.src.charClassSystem.PlayerProgression
{
    [ProtoContract]
    public class PlayerCharacterClassProgressInfo
    {
        [ProtoMember(1)]
        string plUID;
        [ProtoMember(2)]
        public Dictionary<SubClassType, PlayerSubClass> subClasses = new();
        public PlayerCharacterClassProgressInfo()
        {

        }
        public PlayerCharacterClassProgressInfo(string plUID)
        {
            this.plUID = plUID;
            subClasses.Add(SubClassType.SMITH, new PlayerSubClass() { SubClassType = SubClassType.SMITH, ExpToNextBorder = calculateNextLevelExp(0, SubClassType.SMITH), ExpToNextLeft = calculateNextLevelExp(0, SubClassType.SMITH), AlreadyCollectedExp = 0, LevelsGotThisDay = 0, WhichDay = 0 });
            subClasses.Add(SubClassType.FARMER, new PlayerSubClass() { SubClassType = SubClassType.FARMER, ExpToNextBorder = calculateNextLevelExp(0, SubClassType.FARMER), ExpToNextLeft = calculateNextLevelExp(0, SubClassType.FARMER), AlreadyCollectedExp = 0, LevelsGotThisDay = 0, WhichDay = 0 });
            subClasses.Add(SubClassType.MINER, new PlayerSubClass() { SubClassType = SubClassType.MINER, ExpToNextBorder = calculateNextLevelExp(0, SubClassType.MINER), ExpToNextLeft = calculateNextLevelExp(0, SubClassType.MINER), AlreadyCollectedExp = 0, LevelsGotThisDay = 0, WhichDay = 0 });
            subClasses.Add(SubClassType.HUNTER, new PlayerSubClass() { SubClassType = SubClassType.HUNTER, ExpToNextBorder = calculateNextLevelExp(0, SubClassType.HUNTER), ExpToNextLeft = calculateNextLevelExp(0, SubClassType.HUNTER), AlreadyCollectedExp = 0, LevelsGotThisDay = 0, WhichDay = 0 });
            subClasses.Add(SubClassType.SOLDIER, new PlayerSubClass() { SubClassType = SubClassType.SOLDIER, ExpToNextBorder = calculateNextLevelExp(0, SubClassType.SOLDIER), ExpToNextLeft = calculateNextLevelExp(0, SubClassType.SOLDIER), AlreadyCollectedExp = 0, LevelsGotThisDay = 0, WhichDay = 0 });
        }
        public List<string>GetAllTraits()
        {
            List<string> traits = new List<string>();
            foreach (var subClass in subClasses.Values)
            {
                foreach (var trait in subClass.AccuredTraits)
                {
                    if (!traits.Contains(trait))
                    {
                        traits.Add(trait);
                    }
                }
            }
            return traits;
        }
        public PlayerSubClass GetSubClass(SubClassType subClassType)
        {
            if (subClasses.TryGetValue(subClassType, out var subClass))
            {
                return subClass;
            }
            return null;
        }
        public void addExp(double val, SubClassType subClassType, bool force = false)
        {
            if(!this.subClasses.TryGetValue(subClassType, out var subClass))
            {
                return;
            }

            if((subClass.PercentsReached >= 100 || subClass.LevelsGotThisDay >= 5) && !force)
            {
                if (!canclasses.canCharSys.playerWasMensionedNoMoreExp.Contains(plUID))
                {
                    var pl = canclasses.sapi.World.PlayerByUid(plUID);
                    if (pl != null)
                    {
                        ((IServerPlayer)pl).SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("exp_tired"), EnumChatType.Notification);
                    }
                    canclasses.canCharSys.playerWasMensionedNoMoreExp.Add(plUID);
                }
                return;
            }
            
            if(subClass.ExpToNextLeft > val)
            {
                subClass.ExpToNextLeft -= val;
                return;
            }
            else if(subClass.ExpToNextLeft <= val)
            {
                while(true)
                {
                    subClass.PercentsReached++;
                    subClass.LevelsGotThisDay++;
                    if (subClass.PercentsReached >= 100)
                    {
                        subClass.ExpToNextBorder = subClass.ExpToNextLeft = 0;
                        break;
                    }
                    val -= subClass.ExpToNextLeft;
                    subClass.ExpToNextBorder = subClass.ExpToNextLeft = calculateNextLevelExp(subClass.PercentsReached + 1, subClass.SubClassType);
                    if (val <= 0 || val < subClass.ExpToNextLeft)
                    {
                        subClass.ExpToNextLeft = subClass.ExpToNextBorder - val;
                        break;
                    }
                }    
            }

            canclasses.canCharSys.ReapplyTraits(canclasses.sapi.World.PlayerByUid(plUID), subClassType);
            
            canclasses.sapi.Network.GetChannel("cancharactersystem").SendPacket(new CANCharacterProgressInfoPacket()
            {
                playerCharacterClassProgressInfo = this,
                packetType = EnumCANCharacterProgressInfoPacket.NewLevel
            }, canclasses.sapi.World.PlayerByUid(plUID) as IServerPlayer
            ); ;
            //send packet (new level achieved)
            //call reapply traits
        }
        private double calculateNextLevelExp(int curLevel, SubClassType subClass)
        {
            switch (subClass)
            {
                case SubClassType.SMITH:
                    return curLevel * 40 + 150;
                case SubClassType.SOLDIER:
                    return curLevel * 30 + 150;
                case SubClassType.FARMER:
                    return curLevel * 40 + 155;
                case SubClassType.HUNTER:
                    return curLevel * 36 + 130;
                default:
                    return curLevel * 150 + 150;
            }
        }
    }
}
