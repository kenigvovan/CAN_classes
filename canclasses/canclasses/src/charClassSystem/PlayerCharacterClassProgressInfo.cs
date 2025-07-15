using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace canclasses.src.characterClassesSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PlayerCharacterClassProgressInfo
    {
        public string classCode;
        public double globalExp;
        public int globalPercents;
        public double expToNextPercent;
        public double expToNextPercentAll;
        public int levelsGotThisDay;
        public int whichDay;
        string plUID;
        public PlayerCharacterClassProgressInfo()
        {

        }
        public PlayerCharacterClassProgressInfo(string classCode, string plUID)
        {
            this.plUID = plUID;
            this.classCode = classCode;
            globalExp = 0;
            globalPercents = 0;
            expToNextPercent = globalPercents * 150 + 150;
            expToNextPercentAll = globalPercents * 150 + 150;
        }
        public void addExp(double val, bool force = false)
        {
            if((globalPercents >= 100 || levelsGotThisDay >= 5) && !force)
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
            
            if(expToNextPercent > val)
            {
                expToNextPercent -= val;
                return;
            }
            else if(expToNextPercent <= val)
            {
                while(true)
                {
                    globalPercents++;
                    levelsGotThisDay++;
                    if (globalPercents >= 100)
                    {
                        expToNextPercentAll = expToNextPercent = 0;
                        break;
                    }
                    val -= expToNextPercent;
                    expToNextPercentAll = expToNextPercent = calculateNextLevelExp(globalPercents);
                    if (val <= 0 || val < expToNextPercent)
                    {
                        expToNextPercent = expToNextPercentAll - val;
                        break;
                    }
                }    
            }
            if(this.classCode == null)
            {
                var checkedClass = canclasses.sapi.World.PlayerByUid(this.plUID).Entity.WatchedAttributes.GetString("characterClass", (string)null);
                this.classCode = checkedClass;
                canclasses.sapi.Logger.Debug(this.plUID + " player had null classcode in progressInfo" + " set to " + checkedClass);
            }
            canclasses.canCharSys.reapplyTraitsNewPercent(canclasses.sapi.World.PlayerByUid(this.plUID).Entity, globalPercents, this.classCode);
            
            canclasses.sapi.Network.GetChannel("cancharactersystem").SendPacket<CANCharacterProgressInfoPacket>(new CANCharacterProgressInfoPacket()
            {
                currentLevel = globalPercents,
                currentExpToNextLevel = expToNextPercent,
                packetType = EnumCANCharacterProgressInfoPacket.NewLevel,
                allExpToNextLevel = expToNextPercentAll
            }, canclasses.sapi.World.PlayerByUid(this.plUID) as IServerPlayer
            ); ;
            //send packet (new level achieved)
            //call reapply traits
        }
        private double calculateNextLevelExp(int curLevel)
        {
            switch (classCode)
            {
                case "smith":
                    return curLevel * 40 + 150;
                case "soldier":
                    return curLevel * 30 + 150;
                case "farmer":
                    return curLevel * 40 + 155;
                case "handy":
                    return curLevel * 36 + 130;
                default:
                    return curLevel * 150 + 150;
            }
        }
        public PlayerCharacterClassProgressInfo DeepCopy()
        {
            var tmp = new PlayerCharacterClassProgressInfo();
            tmp.expToNextPercent = this.expToNextPercent;
            tmp.expToNextPercentAll = this.expToNextPercentAll;
            tmp.globalPercents = this.globalPercents;
            tmp.whichDay = this.whichDay;
            tmp.classCode = this.classCode;
            tmp.levelsGotThisDay = this.levelsGotThisDay;
            tmp.plUID = this.plUID;
            return tmp;
        }
    }
}
