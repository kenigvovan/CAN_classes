using canmods.src.characterClassesSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace canclasses.src.characterClassesSystem
{
    public class CANCharacterSystem : ModSystem
    {
        private ICoreAPI api;
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;

        private CANGuiDialogCreateCharacter createCharDlg;
        private GuiDialogCharacterBase charDlg;
        private bool didSelect;
        public List<CharacterClass> characterClasses = new List<CharacterClass>();
        List<CANTrait> traits = new List<CANTrait>();
        public Dictionary<string, CharacterClass> characterClassesByCode = new Dictionary<string, CharacterClass>();
        public Dictionary<string, CANTrait> TraitsByCode = new Dictionary<string, CANTrait>();

        public Dictionary<string, PlayerCharacterClassProgressInfo> playersProgressInfos = new Dictionary<string, PlayerCharacterClassProgressInfo>();
        public Dictionary<string, Dictionary<string, PlayerCANTraitCraftInfo>> playersCraftTraitsInfos = new Dictionary<string, Dictionary<string, PlayerCANTraitCraftInfo>>();
        HashSet<string> traitConnectedCraftsNames = new HashSet<string>();
        Dictionary<string, string> craftNameToTraitCodeMap = new Dictionary<string, string>();
        public Dictionary<string, Dictionary<int, double>> blockIdToExpGain = new Dictionary<string, Dictionary<int, double>>();
        public Dictionary<string, double> killedEntityToExp = new Dictionary<string, double>();
        public HashSet<string> extraDurabilityReceivers;
        public HashSet<string> playerWasMensionedNoMoreExp;
        //way to gain exp: list of classes
        public Dictionary<string, HashSet<string>> expReceiversClasses;
        public static CANCharacterProgressGUI charactercProgressGui { get; set; }

        int clientCurrentLevel;
        double clientCurrentExpToNextLevel;
        double clientAllExpToNextLevel;
        int currentDayNumber;
        public override void Dispose()
        {
            if (this.api.Side == EnumAppSide.Server)
            {
                (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("canplayerprogressinfos", SerializerUtil.Serialize(playersProgressInfos));
                (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("canplayercrafttraitsinfos", SerializerUtil.Serialize(playersCraftTraitsInfos));
                (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("cancurrentdayofyear", SerializerUtil.Serialize(currentDayNumber));
                playersProgressInfos.Clear();
                characterClassesByCode.Clear();
                TraitsByCode.Clear();
                playersCraftTraitsInfos.Clear();
                traitConnectedCraftsNames.Clear();
                craftNameToTraitCodeMap.Clear();
                blockIdToExpGain.Clear();
                killedEntityToExp.Clear();
                extraDurabilityReceivers.Clear();
            }
            if (charactercProgressGui != null)
            {
                charactercProgressGui.TryClose();
                charactercProgressGui.Dispose();
                charactercProgressGui = null;
            }

        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Network.RegisterChannel("charselection").RegisterMessageType<CharacterSelectionPacket>().RegisterMessageType<CharacterSelectedState>();
            api.Network.RegisterChannel("cancharactersystem").RegisterMessageType<CANCharacterProgressInfoPacket>();
            api.Event.MatchesGridRecipe += new MatchGridRecipeDelegate(this.Event_MatchesGridRecipe);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            api.Network.GetChannel("charselection").SetMessageHandler<CharacterSelectedState>(new NetworkServerMessageHandler<CharacterSelectedState>(this.onSelectedState));
            //api.Network.GetChannel("cancharactersystem").SetMessageHandler<CharacterSelectedState>(new NetworkServerMessageHandler<CharacterSelectedState>(this.onSelectedState));
            api.Event.IsPlayerReady += new IsPlayerReadyDelegate(this.Event_IsPlayerReady);
            api.Event.PlayerJoin += new PlayerEventDelegate(this.Event_PlayerJoin);
            api.RegisterCommand("charsel", "", "", new ClientChatCommandDelegate(this.onCharSelCmd));
            api.Event.BlockTexturesLoaded += new Action(this.loadCharacterClasses);
            this.charDlg = api.Gui.LoadedGuis.Find((Predicate<GuiDialog>)(dlg => dlg is GuiDialogCharacterBase)) as GuiDialogCharacterBase;
            this.charDlg.Tabs.Add(new GuiTab()
            {
                Name = Lang.Get("charactertab-traits"),
                DataInt = 1
            });
            this.charDlg.RenderTabHandlers.Add(new Action<GuiComposer>(this.composeTraitsTab));

            this.charDlg.Tabs.Add(new GuiTab()
            {
                Name = "Progress",
                DataInt = 2
            });
            this.charDlg.RenderTabHandlers.Add(new Action<GuiComposer>(this.composeProgressTab));

            api.Network.GetChannel("cancharactersystem").SetMessageHandler<CANCharacterProgressInfoPacket>((packet) =>
            {
                {
                    this.clientCurrentLevel = packet.currentLevel;
                    this.clientCurrentExpToNextLevel = packet.currentExpToNextLevel;
                    this.clientAllExpToNextLevel = packet.allExpToNextLevel;
                    if(packet.packetType == EnumCANCharacterProgressInfoPacket.NewLevel)
                    {
                        api.World.Player.ShowChatNotification(Lang.Get("canclasses:new_level_achieved", clientCurrentLevel));
                        //write to player about new level
                    }
                }
            });
                ;
            api.Input.RegisterHotKey("cancharacterprogress", "Show main gui window", GlKeys.C, HotkeyType.GUIOrOtherControls, true);
            api.Input.SetHotKeyHandler("cancharacterprogress", new ActionConsumable<KeyCombination>(this.OnHotKeySkillDialog));

        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Network.GetChannel("charselection").SetMessageHandler<CharacterSelectionPacket>(new NetworkClientMessageHandler<CharacterSelectionPacket>(this.onCharacterSelection));
            api.Event.PlayerJoin += new PlayerDelegate(this.Event_PlayerJoinServer);
            api.Event.ServerRunPhase(EnumServerRunPhase.LoadGamePre, new Action(this.loadCharacterClasses));

            this.traitConnectedCraftsNames = this.api.Assets.Get("config/traitconnectedcraftnames.json").ToObject<HashSet<string>>();
            this.craftNameToTraitCodeMap = this.api.Assets.Get("config/craftnametotraitcodemap.json").ToObject<Dictionary<string, string>>();
            this.expReceiversClasses = this.api.Assets.Get("config/classtoexpgainways.json").ToObject<Dictionary<string, HashSet<string>>>();

            //Load exp values for entity killed, block broken
            killedEntityToExp = this.api.Assets.Get("config/killedentitytoexp.json").ToObject<Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, double>> tmpIdExp = this.api.Assets.Get("config/blocknametoexpgain.json").ToObject<Dictionary<string, Dictionary<string, double>>>();
            foreach (var itCode in tmpIdExp)
            {
                this.blockIdToExpGain.Add(itCode.Key, new Dictionary<int, double>());
            }
            foreach (var dictByClass in tmpIdExp)
            {
                foreach (var it in api.World.Blocks)
                {
                    foreach (var tmpBlock in dictByClass.Value)
                    {
                        string[] stripped = tmpBlock.Key.Split(':');
                        if (it.Code != null && it.Code.Domain.Equals(stripped[0]) && it.Code.Path.Contains(stripped[1]))
                        {
                            this.blockIdToExpGain[dictByClass.Key].Add(it.BlockId, tmpBlock.Value);
                            break;
                        }
                    }
                }
            }

            //Load progress data from world
            var progressDict = api.WorldManager.SaveGame.GetData("canplayerprogressinfos");
            if (progressDict != null)
            {
                playersProgressInfos = SerializerUtil.Deserialize<Dictionary<string, PlayerCharacterClassProgressInfo>>(progressDict);
            }
            var crafttraitinfos = api.WorldManager.SaveGame.GetData("canplayercrafttraitsinfos");
            if (crafttraitinfos != null)
            {
                playersCraftTraitsInfos = SerializerUtil.Deserialize<Dictionary<string, Dictionary<string, PlayerCANTraitCraftInfo>>>(crafttraitinfos);
            }
            int currentDayOfYear = api.WorldManager.SaveGame.GetData<int>("cancurrentdayofyear");

            var oldProgressDict = canclasses.sapi.WorldManager.SaveGame.GetData("cansavedoldplayercharacterclassprogressinfos");
            if(oldProgressDict == null)
            {
                (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("cansavedoldplayercharacterclassprogressinfos", SerializerUtil.Serialize(new Dictionary<string, Dictionary<string, PlayerCharacterClassProgressInfo>>()));
            }
            api.Event.DidBreakBlock += onBlockBroken;
            api.Event.DidUseBlock += onBlockUsed;
            api.Event.Timer((() =>
            {
                
                updateCraftsTimer ();
            }
            ), 3600);
            long timeToCheckDayChange = getSecondsBeforeNextDayStart();
            if(timeToCheckDayChange < 60 || currentDayOfYear != DateTime.Today.DayOfYear)
            {
                sapi.Event.RegisterCallback((dt =>
                {
                    new Thread(new ThreadStart(() =>
                    {
                        updateProgressWhichDay();
                    })).Start();
                }), (int)timeToCheckDayChange * 1000);
                sapi.Logger.Debug("Reset players \"whichDay\" of progress");
                 sapi.Event.Timer((() =>
                {
                    updateProgressWhichDay();
                    sapi.Logger.Debug("Reset players \"whichDay\" of progress");
                }), 86400);
            }
            extraDurabilityReceivers = new HashSet<string> { "pickaxehead", "cleaver", "axehead", "bladeheadfalx", "hammerhead", "knifeblade", "hoehead",
            "prospectingpickhead", "sawblade","scythehead" , "shovelhead", "spearhead", "rollingpin", "finishingchiselhead", "rubblehammerhead", "wedgechiselhead",
            "xhalberdhead", "xlongswordhead", "xmacdehead", "xmesserhead", "xrapierhead", "xspearhead", "xzweihanderhead", "wrench", "chisel"
            };
            playerWasMensionedNoMoreExp = new HashSet<string>();
            sapi.Event.Timer((() =>
            {
                sendPlayersInfos();
                sapi.Logger.Debug("Send players class info");
            }), 300);
        }

        //Reset levels player got this day
        private void updateProgressWhichDay()
        {
            this.playerWasMensionedNoMoreExp.Clear();
            currentDayNumber = DateTime.Today.DayOfYear;
            foreach (var it in playersProgressInfos)
            {
                if(it.Value.whichDay != currentDayNumber)
                {
                    it.Value.whichDay = currentDayNumber;
                    it.Value.levelsGotThisDay = 0;
                }
            }
        }

        private void sendPlayersInfos()
        {
            foreach(var pl in sapi.World.AllOnlinePlayers)
            {
                if(this.playersProgressInfos.TryGetValue(pl.PlayerUID, out var info))
                {
                    if(info == null)
                    { continue; }
                    canclasses.sapi.Network.GetChannel("cancharactersystem").SendPacket<CANCharacterProgressInfoPacket>(new CANCharacterProgressInfoPacket()
                    {
                        currentLevel = info.globalPercents,
                        currentExpToNextLevel = info.expToNextPercent,
                        packetType = EnumCANCharacterProgressInfoPacket.InfoUpdate,
                        allExpToNextLevel = info.expToNextPercentAll
                    }, pl as IServerPlayer
            );
                }
            }
        }
        private bool OnHotKeySkillDialog(KeyCombination comb)
        {
            if (charactercProgressGui == null)
            {
                charactercProgressGui = new CANCharacterProgressGUI(capi);
            }
            if (charactercProgressGui.IsOpened())
            {
                charactercProgressGui.TryClose();
            }
            else
                charactercProgressGui.TryOpen();
            return true;
        }
        private void composeTraitsTab(GuiComposer compo) => compo.AddRichtext(this.getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0.0, 25.0, 385.0, 200.0));
        private void composeProgressTab(GuiComposer compo)
        {
            compo.AddRichtext(Lang.Get("canclasses:can-level", this.clientCurrentLevel.ToString()), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), ElementBounds.Fixed(0.0, 25.0, 385.0, 200.0));
            compo.AddRichtext(Lang.Get("canclasses:can-curexp-allexp", String.Format("{0:0.0}",this.clientAllExpToNextLevel - this.clientCurrentExpToNextLevel), this.clientAllExpToNextLevel.ToString()), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), ElementBounds.Fixed(0.0, 55.0, 385.0, 200.0));
        }
        private string getClassTraitText()
        {
            string charClass = this.capi.World.Player.Entity.WatchedAttributes.GetString("characterClass", (string)null);
            CharacterClass characterClass = this.characterClasses.FirstOrDefault<CharacterClass>((System.Func<CharacterClass, bool>)(c => c.Code == charClass));
            StringBuilder stringBuilder1 = new StringBuilder();
            StringBuilder stringBuilder2 = new StringBuilder();
            foreach (CANTrait trait in (IEnumerable<CANTrait>)((IEnumerable<string>)characterClass.Traits).Select<string, CANTrait>((System.Func<string, CANTrait>)(code => this.TraitsByCode[code])).OrderBy<CANTrait, int>((System.Func<CANTrait, int>)(trait => (int)trait.Type)))
            {
                if(trait.ApplyType == EnumTraitApplyType.PerPercentAttributeApply)
                {
                    //string ifExists = Lang.GetIfExists("traitdesc-" + trait.Code);
                   // if (ifExists != null)
                    {
                        stringBuilder1.Append(Lang.Get("trait-" + trait.Code)).Append("\n");
                        foreach (var it in trait.Attributes)
                        {
                            bool wasFound = false;
                            foreach(var stat in this.capi.World.Player.Entity.Stats.ToArray())
                            {
                                if(stat.Key.Equals(it.Key))
                                {
                                    wasFound = true;
                                    break;
                                }
                            }
                            if(!wasFound)
                            {
                                this.capi.World.Player.Entity.Stats.Set(it.Key, "trait", 0);
                            }
                            //stringBuilder1.Append(ifExists).Append(" ");//trait-
                            if (this.capi.World.Player.Entity.Stats[it.Key].ValuesByKey.TryGetValue("trait", out var val))
                            {
                                stringBuilder1.Append(Lang.Get("charattribute-" + it.Key));
                                if (it.Key.Equals("maxhealthExtraPoints") || it.Key.Equals("weightmodweightbonus"))
                                {
                                    stringBuilder1.Append("(").Append((val.Value)).Append(")");
                                }
                                else
                                {
                                    stringBuilder1.Append("(").Append((val.Value * 100)).Append("%)");
                                }
                            }
                            stringBuilder1.Append("\n");
                        }
                        //stringBuilder1.Append(ifExists).Append();
                        //stringBuilder1.AppendLine(Lang.Get("traitwithattributes", (object)Lang.Get("trait-" + trait.Code), (object)ifExists));
                    }
                }
                else if(trait.ApplyType == EnumTraitApplyType.CraftWithResetTime)
                {
                    stringBuilder1.Append(Lang.Get("trait-" + trait.Code)).Append("\n");
                    stringBuilder1.Append(Lang.Get("traitdesc-" + trait.Code)).Append("\n");
                    foreach (var it in trait.Attributes.Values.ElementAt(0))
                    {
                        if (it.Key > clientCurrentLevel)
                        {
                            continue;
                        }
                        stringBuilder1.Append(Lang.Get("canclasses:n_uses_per_hours_interval", it.Value, trait.additionalInfo["resetInterval"])).Append("\n");
                        break;
                    }                    
                    //stringBuilder1.Append(trait.additionalInfo);
                }
                else if(trait.ApplyType == EnumTraitApplyType.NoAppliance)
                {
                    stringBuilder1.Append(Lang.Get("trait-" + trait.Code)).Append("\n");
                    stringBuilder1.Append(Lang.Get("traitdesc-" + trait.Code)).Append("\n");
                }

                stringBuilder2.Clear();
                /* foreach (KeyValuePair<string, double> attribute in trait.Attributes)
                 {
                     if (stringBuilder2.Length > 0)
                         stringBuilder2.Append(", ");
                     stringBuilder2.Append(Lang.Get(string.Format((IFormatProvider)GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", (object)attribute.Key, (object)attribute.Value)));
                 }*/
                /*if (stringBuilder2.Length > 0)
                {
                    stringBuilder1.AppendLine(Lang.Get("traitwithattributes", (object)Lang.Get("trait-" + trait.Code), (object)stringBuilder2));
                }
                else
                {
                    string ifExists = Lang.GetIfExists("traitdesc-" + trait.Code);
                    if (ifExists != null)
                        stringBuilder1.AppendLine(Lang.Get("traitwithattributes", (object)Lang.Get("trait-" + trait.Code), (object)ifExists));
                    else
                        stringBuilder1.AppendLine(Lang.Get("trait-" + trait.Code));
                }*/
            }
            if (characterClass.Traits.Length == 0)
                stringBuilder1.AppendLine(Lang.Get("No positive or negative traits"));
            return stringBuilder1.ToString();
        }

        private void loadCharacterClasses()
        {
            this.traits = this.api.Assets.Get("config/traits.json").ToObject<List<CANTrait>>();
            this.characterClasses = this.api.Assets.Get("config/characterclasses.json").ToObject<List<CharacterClass>>();
            foreach (CANTrait trait in this.traits)
                this.TraitsByCode[trait.Code] = trait;
            foreach (CharacterClass characterClass in this.characterClasses)
            {
                this.characterClassesByCode[characterClass.Code] = characterClass;
                foreach (JsonItemStack jsonItemStack in characterClass.Gear)
                {
                    if (!jsonItemStack.Resolve(this.api.World, "character class gear", false))
                        this.api.World.Logger.Warning("Unable to resolve character class gear " + jsonItemStack.Type.ToString() + " with code " + jsonItemStack.Code?.ToString() + " item/bloc does not seem to exist. Will ignore.");
                }
            }
        }

        public void setCharacterClass(EntityPlayer player, string classCode, bool initializeGear = true)
        { 
            
            CharacterClass characterClass = this.characterClasses.FirstOrDefault<CharacterClass>((System.Func<CharacterClass, bool>)(c => c.Code == classCode));
            if (characterClass == null)
            {
                player.WatchedAttributes.SetString("characterClass", this.characterClasses.First().Code);
                player.WatchedAttributes.MarkPathDirty("characterClass");
                characterClass = this.characterClasses.First();
                //throw new ArgumentException("Not a valid character class code!");
            }
            player.WatchedAttributes.SetString("characterClass", characterClass.Code);
            
            if (initializeGear)
            {

                if (this.capi?.World.Player.Entity.Properties.Client.Renderer is EntitySkinnableShapeRenderer renderer)
                {
                    renderer.doReloadShapeAndSkin = false;
                }
                else
                {
                    renderer = null;
                }
                IInventory gearInventory = player.GearInventory;
                if (gearInventory != null)
                {
                    for (int slotId = 0; slotId < gearInventory.Count && slotId < 12; ++slotId)
                    {
                        if (!player.GearInventory[slotId].Empty)
                            this.api.World.SpawnItemEntity(player.GearInventory[slotId].TakeOutWhole(), player.Pos.XYZ);
                     }
                    foreach (JsonItemStack jsonItemStack in characterClass.Gear)
                    {
                        if (!jsonItemStack.Resolve(this.api.World, "character class gear", false))
                        {
                            this.api.World.Logger.Warning("Unable to resolve character class gear " + jsonItemStack.Type.ToString() + " with code " + jsonItemStack.Code?.ToString() + " item/bloc does not seem to exist. Will ignore.");
                        }
                        else
                        {
                            ItemStack itemstack = jsonItemStack.ResolvedItemstack?.Clone();
                            if (itemstack != null)
                            {
                                EnumCharacterDressType result;
                                if (!Enum.TryParse<EnumCharacterDressType>(itemstack.ItemAttributes["clothescategory"].AsString(), true, out result))
                                    return;
                                gearInventory[(int)result].Itemstack = itemstack;
                                gearInventory[(int)result].MarkDirty();
                            }
                            else
                                player.TryGiveItemStack(itemstack);
                        }
                    }

                    if (renderer != null)
                    {
                        renderer.doReloadShapeAndSkin = true;
                        renderer.TesselateShape();
                    }
                }
            } 
            this.applyTraitAttributes(player);
        }

        private void applyTraitAttributes(EntityPlayer eplr)
        {
            string classcode = eplr.WatchedAttributes.GetString("characterClass", (string)null);
            CharacterClass characterClass = this.characterClasses.FirstOrDefault<CharacterClass>((System.Func<CharacterClass, bool>)(c => c.Code == classcode));
            if (characterClass == null)
                throw new ArgumentException("Not a valid character class code!");
            foreach (KeyValuePair<string, EntityFloatStats> stat in eplr.Stats)
            {
                foreach (KeyValuePair<string, EntityStat<float>> keyValuePair in stat.Value.ValuesByKey)
                {
                    if (keyValuePair.Key == "trait")
                    {
                        stat.Value.Remove(keyValuePair.Key);
                        break;
                    }
                }
            }
            string[] stringArray = eplr.WatchedAttributes.GetStringArray("extraTraits");
            foreach (string key1 in stringArray == null ? (IEnumerable<string>)characterClass.Traits : ((IEnumerable<string>)characterClass.Traits).Concat<string>((IEnumerable<string>)stringArray))
            {
                
                double playerProgress = this.clientCurrentLevel;
                if (canclasses.sapi != null)
                {
                    this.playersProgressInfos.TryGetValue(eplr.PlayerUID, out PlayerCharacterClassProgressInfo playerLevel);
                    if (playerLevel == null)
                    {
                        this.playersProgressInfos[eplr.PlayerUID] = new PlayerCharacterClassProgressInfo(classcode, eplr.PlayerUID);
                        playerLevel = this.playersProgressInfos[eplr.PlayerUID];
                    }
                    playerProgress = playerLevel.globalPercents;
                }
                
                CANTrait trait;
                if (this.TraitsByCode.TryGetValue(key1, out trait))
                {
                    if (trait.ApplyType == EnumTraitApplyType.PerPercentAttributeApply)
                    {
                        foreach (KeyValuePair<string, Dictionary<double, double>> attributeDict in trait.Attributes)
                        {
                            foreach (var it in attributeDict.Value)
                            {
                                if (it.Key > playerProgress)
                                {
                                    continue;
                                }
                                string key2 = attributeDict.Key;
                                double num = attributeDict.Value[it.Key];
                                eplr.Stats.Set(key2, "trait", (float)num, true);
                                break;
                            }
                        }
                    }
                    else if (trait.ApplyType == EnumTraitApplyType.OnceAttributeApply)
                    {
                        foreach (KeyValuePair<string, Dictionary<double, double>> attributeDict in trait.Attributes)
                        {
                            string key2 = attributeDict.Key;
                            foreach(KeyValuePair<double, double> it in attributeDict.Value)
                            {
                                if(it.Key <= playerProgress)
                                {
                                    double num = it.Value;
                                    eplr.Stats.Set(key2, "trait", (float)num, true);
                                    
                                    break;
                                }
                            }
                        }
                    }
                    else if (trait.ApplyType == EnumTraitApplyType.CraftWithResetTime)
                    {
                        if (canclasses.sapi == null)
                        {
                            continue;
                        }
                        this.playersCraftTraitsInfos.TryGetValue(eplr.PlayerUID, out var DictPlCANTrInfo);
                        
                        if (DictPlCANTrInfo == null)
                        {
                            DictPlCANTrInfo = new Dictionary<string, PlayerCANTraitCraftInfo>();
                            int possibleCrafts = 0;
                            foreach (var it in trait.Attributes["possibleCraftsPerReset"])
                            {
                                if (it.Key <= playerProgress)
                                {
                                    possibleCrafts = (int)it.Value;
                                    break;
                                }
                            }
                            
                            DictPlCANTrInfo.Add(trait.additionalInfo["craftName"].ToString(), new PlayerCANTraitCraftInfo(trait.Code, possibleCrafts, long.Parse(trait.additionalInfo["resetInterval"].ToString())));
                            playersCraftTraitsInfos.Add(eplr.PlayerUID, DictPlCANTrInfo);
                            
                        }
                        else
                        {
                            if (canclasses.sapi == null)
                            {
                                continue;
                            }
                            if (DictPlCANTrInfo.ContainsKey(trait.additionalInfo["craftName"].ToString()))
                            {
                                continue;
                            }
                            int possibleCrafts = 0;
                            foreach (var it in trait.Attributes["possibleCraftsPerReset"])
                            {
                                if (it.Key <= playerProgress)
                                {
                                    possibleCrafts = (int)it.Value;
                                    break;
                                }
                            }
                            DictPlCANTrInfo[trait.additionalInfo["craftName"].ToString()] = new PlayerCANTraitCraftInfo(trait.Code, possibleCrafts, long.Parse(trait.additionalInfo["resetInterval"].ToString()));
                            //we have info for trait if class was set before
                            /* if (DictPlCANTrInfo.TryGetValue(trait.additionalInfo["craftName"].ToString(), out var PlCANTrInfo))
                                {

                                }
                                else
                                { 

                                }*/
                        }
                        
                    }
                }
            }
            eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
            
        }

        public void reapplyTraitsNewPercent(EntityPlayer eplr, int newPercent, string classCode)
        {
            CharacterClass characterClass = this.characterClasses.FirstOrDefault<CharacterClass>((System.Func<CharacterClass, bool>)(c => c.Code == classCode));
            if (characterClass == null)
                throw new ArgumentException("Not a valid character class code!");
           /* foreach (KeyValuePair<string, EntityFloatStats> stat in eplr.Stats)
            {
                foreach (KeyValuePair<string, EntityStat<float>> keyValuePair in stat.Value.ValuesByKey)
                {
                    if (keyValuePair.Key == "trait")
                    {
                        stat.Value.Remove(keyValuePair.Key);
                        break;
                    }
                }
            }*/
            string[] stringArray = eplr.WatchedAttributes.GetStringArray("extraTraits");
            foreach (string key1 in stringArray == null ? (IEnumerable<string>)characterClass.Traits : ((IEnumerable<string>)characterClass.Traits).Concat<string>((IEnumerable<string>)stringArray))
            {
                double playerProgress = newPercent;
                CANTrait trait;
                if (this.TraitsByCode.TryGetValue(key1, out trait))
                {
                    if (trait.ApplyType == EnumTraitApplyType.PerPercentAttributeApply)
                    {
                        foreach (KeyValuePair<string, Dictionary<double, double>> attributeDict in trait.Attributes)
                        {
                            foreach (var it in attributeDict.Value)
                            {
                                if (it.Key > playerProgress)
                                {
                                    continue;
                                }
                                string key2 = attributeDict.Key;
                                double num = attributeDict.Value[it.Key];
                                eplr.Stats.Set(key2, "trait", (float)num, true);
                                break;
                            }
                        }
                    }
                    else if (trait.ApplyType == EnumTraitApplyType.OnceAttributeApply)
                    {
                        foreach (KeyValuePair<string, Dictionary<double, double>> attributeDict in trait.Attributes)
                        {
                            string key2 = attributeDict.Key;
                            foreach (KeyValuePair<double, double> it in attributeDict.Value)
                            {
                                if (it.Key <= playerProgress)
                                {
                                    double num = it.Value;
                                    eplr.Stats.Set(key2, "trait", (float)num, true);
                                    break;
                                }
                            }
                        }
                    }
                    else if (trait.ApplyType == EnumTraitApplyType.CraftWithResetTime)
                    {
                        if (canclasses.sapi.Side != EnumAppSide.Server)
                        {
                            continue;
                        }
                        this.playersCraftTraitsInfos.TryGetValue(eplr.PlayerUID, out var DictPlCANTrInfo);

                        if (DictPlCANTrInfo == null)
                        {
                            DictPlCANTrInfo = new Dictionary<string, PlayerCANTraitCraftInfo>();
                            int possibleCrafts = 0;
                            foreach (var it in trait.Attributes["possibleCraftsPerReset"])
                            {
                                if(it.Key <= playerProgress)
                                {
                                    possibleCrafts = (int)it.Value;
                                    break;
                                }
                            }
                            
                            DictPlCANTrInfo[trait.additionalInfo["craftName"].ToString()] = new PlayerCANTraitCraftInfo(trait.Code, possibleCrafts, long.Parse(trait.additionalInfo["resetInterval"].ToString()));
                            playersCraftTraitsInfos.Add(eplr.PlayerUID, DictPlCANTrInfo);
                        }
                        else
                        {
                            int possibleCrafts = 0;
                            foreach (var it in trait.Attributes["possibleCraftsPerReset"])
                            {
                                if (it.Key <= playerProgress)
                                {
                                    possibleCrafts = (int)it.Value;
                                    break;
                                }
                            }
                            if (DictPlCANTrInfo.ContainsKey(trait.additionalInfo["craftName"].ToString()))
                            {
                                DictPlCANTrInfo[trait.additionalInfo["craftName"].ToString()].possibleCraftsPerReset = possibleCrafts;
                                continue;
                            }
                            DictPlCANTrInfo[trait.additionalInfo["craftName"].ToString()] = new PlayerCANTraitCraftInfo(trait.Code, possibleCrafts, long.Parse(trait.additionalInfo["resetInterval"].ToString()));
                        }

                    }
                }
            }
            eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
        private void onCharSelCmd(int groupId, CmdArgs args)
        {
            if (this.createCharDlg == null)
            {
                this.createCharDlg = new CANGuiDialogCreateCharacter(this.capi, this);
                this.createCharDlg.PrepAndOpen();
            }
            if (this.createCharDlg.IsOpened())
                return;
            this.createCharDlg.TryOpen();
        }

        private void onSelectedState(CharacterSelectedState p) => this.didSelect = p.DidSelect;

        private void Event_PlayerJoin(IClientPlayer byPlayer)
        {
            if (this.didSelect || !(byPlayer.PlayerUID == this.capi.World.Player.PlayerUID))
                return;
            this.createCharDlg = new CANGuiDialogCreateCharacter(this.capi, this);
            this.createCharDlg.PrepAndOpen();
        }

        private bool Event_IsPlayerReady(ref EnumHandling handling)
        {
            if (this.didSelect)
                return true;
            handling = EnumHandling.PreventDefault;
            return false;
        }
         
        private bool Event_MatchesGridRecipe(
          IPlayer player,
          GridRecipe recipe,
          ItemSlot[] ingredients,
          int gridWidth)
        {
            if(!(player is IClientPlayer) && recipe.Name.Path.Contains("sew"))
            {
                var c = 3;
            }
            if (recipe.RequiresTrait == null)
                return true;
            string key = player.Entity.WatchedAttributes.GetString("characterClass", (string)null);
            if (key == null)
                return true;
            CharacterClass characterClass;
            if (this.characterClassesByCode.TryGetValue(key, out characterClass))
            {
                if (!characterClass.Traits.Contains<string>(recipe.RequiresTrait))
                    return false;
            }
            if (this.traitConnectedCraftsNames.Contains(recipe.Name.Path))
            {
                this.craftNameToTraitCodeMap.TryGetValue(recipe.Name.Path, out string traitCode);
                this.playersCraftTraitsInfos.TryGetValue(player.PlayerUID, out var plCrTrIn);
                if (plCrTrIn == null)
                {
                    return false;
                }
                if(!plCrTrIn.TryGetValue(recipe.Name.Path, out PlayerCANTraitCraftInfo currentTraitsInfo))
                {
                    return false;
                }
                int maxAmountResult = plCrTrIn[recipe.Name.Path].possibleCraftsPerReset - plCrTrIn[recipe.Name.Path].usedCraftsThisReset;

                foreach (var itemIt in ingredients)
                {
                    if (itemIt.Itemstack != null && itemIt.Itemstack.StackSize > maxAmountResult)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool HasTrait(IPlayer player, string trait)
        {
            string key = player.Entity.WatchedAttributes.GetString("characterClass", (string)null);
            if (key == null)
                return true;
            CharacterClass characterClass;
            if (this.characterClassesByCode.TryGetValue(key, out characterClass))
            {
                if (characterClass.Traits.Contains<string>(trait))
                    return true;
                string[] stringArray = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
                if (stringArray != null && stringArray.Contains<string>(trait))
                    return true;
            }
            return false;
        }

        
        public void onBlockUsed(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            var c = 3;
        }
        public void onBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            string classcode = byPlayer.Entity.WatchedAttributes.GetString("characterClass", null);
            if(classcode == null)
            {
                return;
            }
            else
            {
                if (this.blockIdToExpGain[classcode].TryGetValue(oldblockId, out var expVal))
                {
                    this.playersProgressInfos[byPlayer.PlayerUID].addExp(expVal);
                }
            }
            
        }
        private void Event_PlayerJoinServer(IServerPlayer byPlayer)
        {
            this.didSelect = SerializerUtil.Deserialize<bool>(byPlayer.GetModdata("createCharacter"), false);
            if (!this.didSelect)
            {
                this.randomizeSkin(byPlayer);
                this.setCharacterClass(byPlayer.Entity, this.characterClasses[0].Code, false);
            }
            IServerNetworkChannel channel = this.sapi.Network.GetChannel("charselection");
            CharacterSelectedState message = new CharacterSelectedState();
            message.DidSelect = this.didSelect;
            IServerPlayer[] serverPlayerArray = new IServerPlayer[1]
            {
        byPlayer
            };
            //TODO
            channel.SendPacket<CharacterSelectedState>(message, serverPlayerArray);
        }

        private void randomizeSkin(IServerPlayer byPlayer)
        {
            EntityBehaviorExtraSkinnable behavior = byPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            foreach (SkinnablePart availableSkinPart in behavior.AvailableSkinParts)
            {
                int index = this.api.World.Rand.Next(availableSkinPart.Variants.Length);
                if ((availableSkinPart.Code == "mustache" || availableSkinPart.Code == "beard") && this.api.World.Rand.NextDouble() < 0.5)
                    index = 0;
                string code = availableSkinPart.Variants[index].Code;
                behavior.selectSkinPart(availableSkinPart.Code, code);
            }
        }

        private void onCharacterSelection(IServerPlayer fromPlayer, CharacterSelectionPacket p)
        {
            bool flag = SerializerUtil.Deserialize<bool>(fromPlayer.GetModdata("createCharacter"), false);
            if (flag && fromPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                fromPlayer.BroadcastPlayerData(true);
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            }
            else 
            {
                if (p.DidSelect)
                {                                       
                    string charClass = fromPlayer.Entity.WatchedAttributes.GetString("characterClass", (string)null);
                    if (charClass != null)
                    {
                        var oldProgressDict = canclasses.sapi.WorldManager.SaveGame.GetData("cansavedoldplayercharacterclassprogressinfos");
                        if (oldProgressDict != null)
                        {
                            var dictsPlayersOldProgress = SerializerUtil.Deserialize<Dictionary<string, Dictionary<string, PlayerCharacterClassProgressInfo>>>(oldProgressDict);
                            if (dictsPlayersOldProgress.TryGetValue(fromPlayer.PlayerUID, out Dictionary<string, PlayerCharacterClassProgressInfo> dictPlayerOldClassesProgressInfos))
                            {
                                //if for this player we have old classes progresses
                                //does it have class we set now?
                                if (dictPlayerOldClassesProgressInfos.ContainsKey(p.CharacterClass))
                                {

                                    if (dictsPlayersOldProgress[fromPlayer.PlayerUID].ContainsKey(charClass))
                                    {
                                        dictsPlayersOldProgress[fromPlayer.PlayerUID][charClass] = canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID];
                                        (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("cansavedoldplayercharacterclassprogressinfos", SerializerUtil.Serialize(dictsPlayersOldProgress));
                                    }
                                    if(dictPlayerOldClassesProgressInfos[p.CharacterClass].classCode == null)
                                    {
                                        dictPlayerOldClassesProgressInfos[p.CharacterClass].classCode = p.CharacterClass;
                                    }
                                    canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID] = dictPlayerOldClassesProgressInfos[p.CharacterClass];
                                }
                                else
                                {
                                    if (dictsPlayersOldProgress[fromPlayer.PlayerUID].ContainsKey(charClass))
                                    {
                                        dictsPlayersOldProgress[fromPlayer.PlayerUID][charClass] = canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID];
                                    }
                                    else
                                    {
                                        dictsPlayersOldProgress[fromPlayer.PlayerUID].Add(charClass, canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID]);
                                    }
                                    (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("cansavedoldplayercharacterclassprogressinfos", SerializerUtil.Serialize(dictsPlayersOldProgress));
                                    canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID] = new PlayerCharacterClassProgressInfo(p.CharacterClass, fromPlayer.PlayerUID);
                                }

                            }
                            else
                            {
                                
                                dictsPlayersOldProgress.Add(fromPlayer.PlayerUID, new Dictionary<string, PlayerCharacterClassProgressInfo>{ { charClass, canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID].DeepCopy() } });
                                (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("cansavedoldplayercharacterclassprogressinfos", SerializerUtil.Serialize(dictsPlayersOldProgress));
                                canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID] = new PlayerCharacterClassProgressInfo(p.CharacterClass, fromPlayer.PlayerUID);
                            }
                        }
                    }
                    else
                    {
                        canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID] = new PlayerCharacterClassProgressInfo(p.CharacterClass, fromPlayer.PlayerUID);                       
                    }
                    
                    fromPlayer.SetModdata("createCharacter", SerializerUtil.Serialize<bool>(p.DidSelect));
                    this.setCharacterClass(fromPlayer.Entity, p.CharacterClass, !flag || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);
                    EntityBehaviorExtraSkinnable behavior = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                    behavior.ApplyVoice(p.VoiceType, p.VoicePitch, false);
                    foreach (KeyValuePair<string, string> skinPart in p.SkinParts)
                        behavior.selectSkinPart(skinPart.Key, skinPart.Value, false);
                }
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                fromPlayer.BroadcastPlayerData(true);
            }
        }

        internal void ClientSelectionDone(
          IInventory characterInv,
          string characterClass,
          bool didSelect)
        {
            List<ClothStack> clothStackList = new List<ClothStack>();
            for (int slotId = 0; slotId < characterInv.Count; ++slotId)
            {
                ItemSlot itemSlot = characterInv[slotId];
                if (itemSlot.Itemstack != null)
                    clothStackList.Add(new ClothStack()
                    {
                        Code = itemSlot.Itemstack.Collectible.Code.ToShortString(),
                        SlotNum = slotId,
                        Class = itemSlot.Itemstack.Class
                    });
            }
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            EntityBehaviorExtraSkinnable behavior = this.capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            foreach (AppliedSkinnablePartVariant appliedSkinPart in (IEnumerable<AppliedSkinnablePartVariant>)behavior.AppliedSkinParts)
                dictionary[appliedSkinPart.PartCode] = appliedSkinPart.Code;
            this.capi.Network.GetChannel("charselection").SendPacket<CharacterSelectionPacket>(new CharacterSelectionPacket()
            {
                Clothes = clothStackList.ToArray(),
                DidSelect = didSelect,
                SkinParts = dictionary,
                CharacterClass = characterClass,
                VoicePitch = behavior.VoicePitch,
                VoiceType = behavior.VoiceType
            });
            this.capi.Network.SendPlayerNowReady();
            this.createCharDlg = (CANGuiDialogCreateCharacter)null;
        }


        void updateCraftsTimer()
        {
            long timeNow = getEpochSeconds();
            foreach(var playerInfosDict in this.playersCraftTraitsInfos)
            {
               foreach(var itInfo in playerInfosDict.Value)
                {
                    if(itInfo.Value.lastResetTime == 0)
                    {
                        itInfo.Value.lastResetTime = timeNow;
                    }
                    if (itInfo.Value.lastResetTime + itInfo.Value.resetInterval * 60 <= timeNow)
                    {
                        itInfo.Value.lastResetTime = timeNow;
                        itInfo.Value.usedCraftsThisReset = 0;
                    }                   
                }
            }
        }
        public static readonly long secondsInADay = 86400;
        public static readonly long secondsInAnHour = 3600;
        static readonly long secondsStartsNewDay = 0;
        static readonly DateTime start = new DateTime(1970, 1, 1);
        public static long getEpochSeconds()
        {
            return (long)((DateTime.UtcNow - start).TotalSeconds);
        }
        public static long getSecondsBeforeNextDayStart()
        {
            long passedThisDay = getEpochSeconds() % secondsInADay;
            if (passedThisDay <= secondsStartsNewDay)
            {
                return secondsStartsNewDay - passedThisDay;
            }
            long gg = (secondsInADay - passedThisDay) + secondsStartsNewDay;
            return (secondsInADay - passedThisDay) + secondsStartsNewDay;
        }
    }
}
