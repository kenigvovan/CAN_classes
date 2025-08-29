using Cairo;
using canclasses.src.charClassSystem.Guis;
using canclasses.src.charClassSystem.Network;
using canclasses.src.charClassSystem.PlayerProgression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace canclasses.src.characterClassesSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CharacterSelectedState
    {
        public bool DidSelect;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ClothStack
    {
        public EnumItemClass Class;
        public string Code;
        public int SlotNum;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CharacterSelectionPacket
    {
        public bool DidSelect;
        public ClothStack[] Clothes;
        public string CharacterClass;
        public Dictionary<string, string> SkinParts;
        public string VoiceType;
        public string VoicePitch;
    }

    public class SeraphRandomizerConstraints
    {
        public Dictionary<string, Dictionary<string, Dictionary<string, RandomizerConstraint>>> Constraints;
    }

    public class RandomizerConstraint
    {
        public string[] Allow;
        public string[] Disallow;

        public string SelectRandom(Random rand, SkinnablePartVariant[] variants)
        {
            if (Allow != null)
            {
                return Allow[rand.Next(Allow.Length)];
            }
            if (Disallow != null)
            {
                var allowed = variants.Where(ele => !Disallow.Contains(ele.Code)).ToArray();
                return allowed[rand.Next(allowed.Length)].Code;
            }

            return variants[rand.Next(variants.Length)].Code;
        }
    }
    public class CANCharacterSystem : ModSystem
    {

        ICoreAPI api;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        CANGuiDialogCreateCharacter createCharDlg;
        GuiDialogCharacterBase charDlg;

        bool didSelect;

        public List<CharacterClass> characterClasses = new List<CharacterClass>();
        public List<CANTrait> traits = new List<CANTrait>();
        public Dictionary<string, List<string>> TraitsToSubclass = new();

        public Dictionary<string, CharacterClass> characterClassesByCode = new Dictionary<string, CharacterClass>();

        public Dictionary<string, CANTrait> TraitsByCode = new Dictionary<string, CANTrait>();
        public Dictionary<string, SubClassType> TraitToSubClass = new();

        SeraphRandomizerConstraints randomizerConstraints;
        public Dictionary<string, PlayerCharacterClassProgressInfo> playersProgressInfos = new Dictionary<string, PlayerCharacterClassProgressInfo>();
        public Dictionary<string, Dictionary<string, PlayerCANTraitCraftInfo>> playersCraftTraitsInfos = new Dictionary<string, Dictionary<string, PlayerCANTraitCraftInfo>>();
        HashSet<string> traitConnectedCraftsNames = new HashSet<string>();
        Dictionary<string, string> craftNameToTraitCodeMap = new Dictionary<string, string>();
        public Dictionary<SubClassType, Dictionary<int, double>> blockIdToExpGain = new();
        public Dictionary<SubClassType, Dictionary<string, double>> killedEntityToExp = new();
        
        public HashSet<string> extraDurabilityReceivers;
        public HashSet<string> playerWasMensionedNoMoreExp;
        //way to gain exp: list of classes
        public Dictionary<string, HashSet<SubClassType>> expReceiversClasses;
        public static CANCharacterProgressGUI charactercProgressGui { get; set; }

        PlayerCharacterClassProgressInfo ClientPlayerProgressInfo;
        int currentDayNumber;
        public override void Start(ICoreAPI api)
        {
            this.api = api;

            api.Network
                .RegisterChannel("charselection")                               
                .RegisterMessageType<CharacterSelectionPacket>()
                .RegisterMessageType<CharacterSelectedState>()
            ;
            api.Network.RegisterChannel("cancharactersystem").RegisterMessageType<CANCharacterProgressInfoPacket>();

            api.Event.MatchesGridRecipe += Event_MatchesGridRecipe;
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            api.Event.BlockTexturesLoaded += onLoadedUniversal;

            api.Network.GetChannel("charselection")
                .SetMessageHandler<CharacterSelectedState>(onSelectedState)
            ;

            api.Event.IsPlayerReady += Event_IsPlayerReady;
            api.Event.PlayerJoin += Event_PlayerJoin;


            this.api.ChatCommands.Create("charsel")
                .WithDescription("Open the character selection menu")
                .HandleWith(onCharSelCmd);

            api.Event.BlockTexturesLoaded += loadCharacterClasses;


            charDlg = api.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            charDlg.Tabs.Add(new GuiTab() { Name = Lang.Get("charactertab-traits"), DataInt = 1 });
            charDlg.RenderTabHandlers.Add(composeTraitsTab);

            this.charDlg.Tabs.Add(new GuiTab()
            {
                Name = "Progress",
                DataInt = 2
            });
            this.charDlg.RenderTabHandlers.Add(new Action<GuiComposer>(this.composeProgressTab));
            api.Network.GetChannel("cancharactersystem").SetMessageHandler<CANCharacterProgressInfoPacket>((packet) =>
            {
                {
                    ClientPlayerProgressInfo = packet.playerCharacterClassProgressInfo;
                    if (packet.packetType == EnumCANCharacterProgressInfoPacket.NewLevel)
                    {
                        api.World.Player.ShowChatNotification(Lang.Get("canclasses:new_level_achieved", packet.subClassTypeNewLevel));
                        //write to player about new level
                    }
                }
            });
            ;
        }
        private void onLoadedUniversal()
        {
            randomizerConstraints = api.Assets.Get("config/seraphrandomizer.json").ToObject<SeraphRandomizerConstraints>();
        }

        private void composeTraitsTab(GuiComposer compo)
        {
            var mainBounds = ElementBounds.Fixed(0.0, 35.0, 355.0, 250);
            var textBounds = mainBounds.FlatCopy();
            mainBounds.Alignment = EnumDialogArea.LeftTop;
            ElementBounds scrollbarBounds = textBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7, -32).WithFixedWidth(20);
            compo.AddInset(textBounds);
            //compo.AddRichtext("hello", CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), mainBounds);
            compo.BeginChildElements(mainBounds)
                .BeginClip(textBounds);

            var sb = getClassTraitText();
            compo.AddRichtext(sb.ToString(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), ElementBounds.Fixed(0.0, 35.0, 350.0, 250), "credits")
            //.AddRichtext("hello", CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), ElementBounds.Fixed(0.0, 25.0 + 10, 100.0, 50))
            .EndClip()
            .AddVerticalScrollbar(new Action<float>(delegate (float value)
            {
                ElementBounds bounds = compo.GetRichtext("credits").Bounds;
                bounds.fixedY = (double)(10f - value);
                bounds.CalcWorldBounds();
            }), scrollbarBounds, "scrollbar")
            .EndChildElements();
            TextExtents textExtents = CairoFont.WhiteDetailText().GetTextExtents("");
            //currentBounds.fixedWidth = textExtents.Width;
            compo.GetScrollbar("scrollbar").SetHeights((float)textBounds.fixedHeight, (float)50 * 25);
            compo.GetScrollbar("scrollbar").SetScrollbarPosition(10);
           // compo
           //   .AddRichtext(getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0, 25, 385, 200));
        }
        string getClassTraitText()
        {
            string charClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
            CharacterClass chclass = characterClasses.FirstOrDefault(c => c.Code == charClass);

            StringBuilder stringBuilder1 = new StringBuilder();
            StringBuilder stringBuilder2 = new StringBuilder();

            foreach (CANTrait trait in (IEnumerable<CANTrait>)((IEnumerable<string>)chclass.Traits).Select<string, CANTrait>((System.Func<string, CANTrait>)(code => this.TraitsByCode[code])).OrderBy<CANTrait, int>((System.Func<CANTrait, int>)(trait => (int)trait.Type)))
            {
                if (trait.ApplyType == EnumTraitApplyType.PerPercentAttributeApply)
                {
                    //string ifExists = Lang.GetIfExists("traitdesc-" + trait.Code);
                    // if (ifExists != null)
                    {
                        stringBuilder1.Append(Lang.Get("trait-" + trait.Code)).Append("\n");
                        foreach (var it in trait.Attributes)
                        {
                            bool wasFound = false;
                            foreach (var stat in this.capi.World.Player.Entity.Stats.ToArray())
                            {
                                if (stat.Key.Equals(it.Key))
                                {
                                    wasFound = true;
                                    break;
                                }
                            }
                            if (!wasFound)
                            {
                                this.capi.World.Player.Entity.Stats.Set(it.Key, "trait-" + trait.Code, 0);
                            }
                            //stringBuilder1.Append(ifExists).Append(" ");//trait-
                            if (this.capi.World.Player.Entity.Stats[it.Key].ValuesByKey.TryGetValue("trait-" + trait.Code, out var val))
                            {
                                stringBuilder1.Append(Lang.Get("charattribute-" + it.Key));
                                if (it.Key.Equals("maxhealthExtraPoints") || it.Key.Equals("weightmodweightbonus"))
                                {
                                    stringBuilder1.Append(" (").Append((val.Value)).Append(")");
                                }
                                else
                                {
                                    stringBuilder1.Append(" (").Append((val.Value * 100)).Append("%)");
                                }
                            }
                            else
                            {
                                stringBuilder1.Append(Lang.Get("charattribute-" + it.Key));
                                if (it.Key.Equals("maxhealthExtraPoints") || it.Key.Equals("weightmodweightbonus"))
                                {
                                    stringBuilder1.Append(" (").Append("x").Append(")");
                                }
                                else
                                {
                                    stringBuilder1.Append(" (").Append("x").Append("%)");
                                }
                            }
                            stringBuilder1.Append("\n");
                        }
                        //stringBuilder1.Append(ifExists).Append();
                        //stringBuilder1.AppendLine(Lang.Get("traitwithattributes", (object)Lang.Get("trait-" + trait.Code), (object)ifExists));
                    }
                }
                else if (trait.ApplyType == EnumTraitApplyType.CraftWithResetTime)
                {
                    stringBuilder1.Append(Lang.Get("trait-" + trait.Code)).Append("\n");
                    stringBuilder1.Append(Lang.Get("traitdesc-" + trait.Code)).Append("\n");
                    if(!TraitToSubClass.TryGetValue(trait.Code, out SubClassType subClassType))
                    {
                        continue;
                    }
                    foreach (var it in trait.Attributes.Values.ElementAt(0))
                    {
                        if (it.Key > ClientPlayerProgressInfo.GetSubClass(subClassType).PercentsReached)
                        {
                            continue;
                        }
                        stringBuilder1.Append(Lang.Get("canclasses:n_uses_per_hours_interval", it.Value, trait.additionalInfo["resetInterval"])).Append("\n");
                        break;
                    }
                    //stringBuilder1.Append(trait.additionalInfo);
                }
                else if (trait.ApplyType == EnumTraitApplyType.NoAppliance)
                {
                    stringBuilder1.Append(Lang.Get("trait-" + trait.Code)).Append("\n");
                    stringBuilder1.Append(Lang.Get("traitdesc-" + trait.Code)).Append("\n");
                }

                stringBuilder2.Clear();

            }
            if (chclass.Traits.Length == 0)
                stringBuilder1.AppendLine(Lang.Get("No positive or negative traits"));
            return stringBuilder1.ToString();
        }

        private void loadCharacterClasses()
        {
            onLoadedUniversal();
            this.traits = this.api.Assets.Get("canclasses:config/traits.json").ToObject<List<CANTrait>>();
            this.TraitsToSubclass = this.api.Assets.Get("canclasses:config/traits_to_subclass.json").ToObject<Dictionary<string, List<string>>>();
            this.characterClasses = this.api.Assets.Get("canclasses:config/characterclasses.json").ToObject<List<CharacterClass>>();
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
            //LoadTraits();
            //LoadClasses();


            /*foreach (var trait in traits)
            {
                TraitsByCode[trait.Code] = trait;
            }

            foreach (var charclass in characterClasses)
            {
                characterClassesByCode[charclass.Code] = charclass;

                foreach (var jstack in charclass.Gear)
                {
                    if (!jstack.Resolve(api.World, "character class gear", false))
                    {
                        api.World.Logger.Warning("Unable to resolve character class gear " + jstack.Type + " with code " + jstack.Code + " item/block does not seem to exist. Will ignore.");
                    }
                }
            }*/
        }
        public void setCharacterClass(EntityPlayer eplayer, string classCode, bool initializeGear = true)
        {
            CharacterClass charclass = characterClasses.FirstOrDefault(c => c.Code == classCode);
            if(charclass == null)
            {
                charclass = characterClasses[0];
            }
            if (charclass == null) throw new ArgumentException("Not a valid character class code!");

            eplayer.WatchedAttributes.SetString("characterClass", charclass.Code);

            if (initializeGear)
            {
                var bh = eplayer.GetBehavior<EntityBehaviorPlayerInventory>();
                var essr = capi?.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;

                bh.doReloadShapeAndSkin = false;

                IInventory inv = bh.Inventory;
                if (inv != null)
                {
                    for (int i = 0; i < inv.Count; i++)
                    {
                        if (i >= 12) break; // Armor
                        /*if (!inv.Empty)
                        {
                            api.World.SpawnItemEntity(player.GearInventory[i].TakeOutWhole(), player.Pos.XYZ);
                        }*/
                        inv[i].Itemstack = null;
                    }


                    foreach (var jstack in charclass.Gear)
                    {
                        // no idea why this is needed here, it yields the wrong item otherwise
                        if (!jstack.Resolve(api.World, "character class gear", false))
                        {
                            api.World.Logger.Warning("Unable to resolve character class gear " + jstack.Type + " with code " + jstack.Code + " item/block does not seem to exist. Will ignore.");
                            continue;
                        }

                        ItemStack stack = jstack.ResolvedItemstack?.Clone();
                        if (stack == null) continue;

                        string strdress = stack.ItemAttributes["clothescategory"].AsString();
                        if (!Enum.TryParse(strdress, true, out EnumCharacterDressType dresstype))
                        {
                            eplayer.TryGiveItemStack(stack);
                        }
                        else
                        {
                            inv[(int)dresstype].Itemstack = stack;
                            inv[(int)dresstype].MarkDirty();
                        }
                    }

                    if (essr != null)
                    {
                        bh.doReloadShapeAndSkin = true;
                        essr.TesselateShape();
                    }
                }
            }

            ReapplyTraitsForAllSubclasses(eplayer.Player);
        }           
        private TextCommandResult onCharSelCmd(TextCommandCallingArgs textCommandCallingArgs)
        {
            var allowcharselonce = capi.World.Player.Entity.WatchedAttributes.GetBool("allowcharselonce") || capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;

            if (createCharDlg == null && allowcharselonce)
            {
                createCharDlg = new CANGuiDialogCreateCharacter(capi, this);
                createCharDlg.PrepAndOpen();
            }
            else if (createCharDlg == null)
            {
                return TextCommandResult.Success(Lang.Get("You don't have permission to change you character and class. An admin needs to grant you allowcharselonce permission"));
            }

            if (!createCharDlg.IsOpened())
            {
                createCharDlg.TryOpen();
            }
            return TextCommandResult.Success();
        }
        private void onSelectedState(CharacterSelectedState p)
        {
            didSelect = p.DidSelect;
        }

        private void Event_PlayerJoin(IClientPlayer byPlayer)
        {
            if (byPlayer.PlayerUID == capi.World.Player.PlayerUID)
            {
                if (!didSelect)
                {
                    createCharDlg = new CANGuiDialogCreateCharacter(capi, this);
                    createCharDlg.PrepAndOpen();
                    createCharDlg.OnClosed += () => capi.PauseGame(false);
                    capi.Event.EnqueueMainThreadTask(() => capi.PauseGame(true), "pausegame");
                    capi.Event.PushEvent("begincharacterselection");
                }
                else
                {
                    capi.Event.PushEvent("skipcharacterselection");
                }
            }
        }

        private bool Event_IsPlayerReady(ref EnumHandling handling)
        {
            if (didSelect) return true;

            handling = EnumHandling.PreventDefault;
            return false;
        }
        private bool Event_MatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
        {
            if (recipe.RequiresTrait == null) return true;

            string classcode = player.Entity.WatchedAttributes.GetString("characterClass");
            if (classcode == null) return true;

            if (characterClassesByCode.TryGetValue(classcode, out CharacterClass charclass))
            {
                if (charclass.Traits.Contains(recipe.RequiresTrait)) return true;

                string[] extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
                if (extraTraits != null && extraTraits.Contains(recipe.RequiresTrait)) return true;
            }
            if (this.traitConnectedCraftsNames.Contains(recipe.Name.Path))
            {
                this.craftNameToTraitCodeMap.TryGetValue(recipe.Name.Path, out string traitCode);
                this.playersCraftTraitsInfos.TryGetValue(player.PlayerUID, out var plCrTrIn);
                if (plCrTrIn == null)
                {
                    return false;
                }
                if (!plCrTrIn.TryGetValue(recipe.Name.Path, out PlayerCANTraitCraftInfo currentTraitsInfo))
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
            return false;
        }       
        public bool HasTrait(IPlayer player, string trait)
        {
            string classcode = player.Entity.WatchedAttributes.GetString("characterClass");
            if (classcode == null) return true;

            if (characterClassesByCode.TryGetValue(classcode, out CharacterClass charclass))
            {
                if (charclass.Traits.Contains(trait)) return true;

                string[] extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
                if (extraTraits != null && extraTraits.Contains(trait)) return true;
            }

            return false;
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Network.GetChannel("charselection")
                .SetMessageHandler<CharacterSelectionPacket>(onCharacterSelection)
            ;

            api.Event.PlayerJoin += Event_PlayerJoinServer;
            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadCharacterClasses);

            this.traitConnectedCraftsNames = this.api.Assets.Get("canclasses:config/traitconnectedcraftnames.json").ToObject<HashSet<string>>();
            this.craftNameToTraitCodeMap = this.api.Assets.Get("canclasses:config/craftnametotraitcodemap.json").ToObject<Dictionary<string, string>>();
            this.expReceiversClasses = this.api.Assets.Get("canclasses:config/classtoexpgainways.json").ToObject<Dictionary<string, HashSet<SubClassType>>>();

            //Load exp values for entity killed, block broken
            killedEntityToExp = this.api.Assets.Get("canclasses:config/killedentitytoexp.json").ToObject<Dictionary<SubClassType, Dictionary<string, double>>>();
            Dictionary<SubClassType, Dictionary<string, double>> tmpIdExp = this.api.Assets.Get("canclasses:config/blocknametoexpgain.json").ToObject<Dictionary<SubClassType, Dictionary<string, double>>>();
            foreach (var itCode in tmpIdExp)
            {
                this.blockIdToExpGain.Add(itCode.Key, new Dictionary<int, double>());
            }
            foreach (var dictByClass in tmpIdExp)
            {
                var dictKey = dictByClass.Key;
                foreach (var it in api.World.Blocks)
                {
                    foreach (var tmpBlock in dictByClass.Value)
                    {
                        string[] stripped = tmpBlock.Key.Split(':');
                        if (it.Code != null && it.Code.Domain.Equals(stripped[0]) && it.Code.Path.Contains(stripped[1]))
                        {
                            this.blockIdToExpGain[dictKey].Add(it.BlockId, tmpBlock.Value);
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
            if (oldProgressDict == null)
            {
                (this.api as ICoreServerAPI).WorldManager.SaveGame.StoreData("cansavedoldplayercharacterclassprogressinfos", SerializerUtil.Serialize(new Dictionary<string, Dictionary<string, PlayerCharacterClassProgressInfo>>()));
            }
            api.Event.DidBreakBlock += onBlockBroken;
            api.Event.DidUseBlock += onBlockUsed;
            api.Event.Timer((() =>
            {

                updateCraftsTimer();
            }
            ), 3600);
            long timeToCheckDayChange = getSecondsBeforeNextDayStart();
            if (timeToCheckDayChange < 60 || currentDayOfYear != DateTime.Today.DayOfYear)
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
            }), 30);
        }
        
        private void Event_PlayerJoinServer(IServerPlayer byPlayer)
        {
            didSelect = SerializerUtil.Deserialize(byPlayer.GetModdata("createCharacter"), false);

            if (!didSelect)
            {
                setCharacterClass(byPlayer.Entity, characterClasses[0].Code, false);
            }

            var classChangeMonths = sapi.World.Config.GetDecimal("allowClassChangeAfterMonths", -1);
            var allowOneFreeClassChange = sapi.World.Config.GetBool("allowOneFreeClassChange");

            // allow players that already played on the server to also reselect their character like new players
            if (allowOneFreeClassChange && byPlayer.ServerData.LastCharacterSelectionDate == null)
            {
                byPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
            }
            else if (classChangeMonths >= 0)
            {
                var date = DateTime.UtcNow;
                var lastDateChange = byPlayer.ServerData.LastCharacterSelectionDate ?? byPlayer.ServerData.FirstJoinDate ?? "1/1/1970 00:00 AM";
                var monthsPassed = date.Subtract(DateTimeOffset.Parse(lastDateChange).UtcDateTime).TotalDays / 30.0;
                if (classChangeMonths < monthsPassed)
                {
                    byPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
                }
            }


            sapi.Network.GetChannel("charselection").SendPacket(new CharacterSelectedState() { DidSelect = didSelect }, byPlayer);
        }

        public bool randomizeSkin(Entity entity, Dictionary<string, string> preSelection, bool playVoice = true)
        {
            if (preSelection == null) preSelection = new Dictionary<string, string>();

            var skinMod = entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            bool mustached = api.World.Rand.NextDouble() < 0.3;

            Dictionary<string, RandomizerConstraint> currentConstraints = new Dictionary<string, RandomizerConstraint>();

            foreach (var skinpart in skinMod.AvailableSkinParts)
            {
                var variants = skinpart.Variants.Where(v => v.Category == "standard").ToArray();

                int index = api.World.Rand.Next(variants.Length);

                if (preSelection.TryGetValue(skinpart.Code, out string variantCode))
                {
                    index = variants.IndexOf(val => val.Code == variantCode);
                }
                else
                {
                    if (currentConstraints.TryGetValue(skinpart.Code, out var partConstraints))
                    {
                        variantCode = partConstraints.SelectRandom(api.World.Rand, variants);
                        index = variants.IndexOf(val => val.Code == variantCode);
                    }

                    if ((skinpart.Code == "mustache" || skinpart.Code == "beard") && !mustached)
                    {
                        index = 0;
                        variantCode = "none";
                    }
                }

                if (variantCode == null) variantCode = variants[index].Code;

                skinMod.selectSkinPart(skinpart.Code, variantCode, true, playVoice);

                if (randomizerConstraints.Constraints.TryGetValue(skinpart.Code, out var partConstraintsGroup))
                {
                    if (partConstraintsGroup.TryGetValue(variantCode, out var constraints))
                    {
                        foreach (var val in constraints)
                        {
                            currentConstraints[val.Key] = val.Value;
                        }
                    }
                }

                if (skinpart.Code == "voicetype" && variantCode == "high") mustached = false;
            }

            return true;
        }
        private void onCharacterSelection(IServerPlayer fromPlayer, CharacterSelectionPacket p)
        {
            bool didSelectBefore = fromPlayer.GetModData<bool>("createCharacter", false);
            bool allowSelect = !didSelectBefore || fromPlayer.Entity.WatchedAttributes.GetBool("allowcharselonce") || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

            if (!allowSelect)
            {
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                fromPlayer.BroadcastPlayerData(true);
                return;
            }

            if (p.DidSelect)
            {
                string charClass = fromPlayer.Entity.WatchedAttributes.GetString("characterClass", (string)null);
                if (charClass == null)
                {
                    canclasses.canCharSys.playersProgressInfos[fromPlayer.PlayerUID] = new PlayerCharacterClassProgressInfo(fromPlayer.PlayerUID);
                }

                fromPlayer.SetModData<bool>("createCharacter", true);

                setCharacterClass(fromPlayer.Entity, p.CharacterClass, !didSelectBefore || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);

                var bh = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                bh.ApplyVoice(p.VoiceType, p.VoicePitch, false);

                foreach (var skinpart in p.SkinParts)
                {
                    bh.selectSkinPart(skinpart.Key, skinpart.Value, false);
                }

                var date = DateTime.UtcNow;
                fromPlayer.ServerData.LastCharacterSelectionDate = date.ToShortDateString() + " " + date.ToShortTimeString();

                // allow players that just joined to immediately re select the class
                var allowOneFreeClassChange = sapi.World.Config.GetBool("allowOneFreeClassChange");
                if (!didSelectBefore && allowOneFreeClassChange)
                {
                    fromPlayer.ServerData.LastCharacterSelectionDate = null;
                }
                else
                {
                    fromPlayer.Entity.WatchedAttributes.RemoveAttribute("allowcharselonce");
                }
            }
            fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            fromPlayer.BroadcastPlayerData(true);
        }      
        internal void ClientSelectionDone(IInventory characterInv, string characterClass, bool didSelect)
        {
            List<ClothStack> clothesPacket = new List<ClothStack>();
            for (int i = 0; i < characterInv.Count; i++)
            {
                ItemSlot slot = characterInv[i];
                if (slot.Itemstack == null) continue;

                clothesPacket.Add(new ClothStack()
                {
                    Code = slot.Itemstack.Collectible.Code.ToShortString(),
                    SlotNum = i,
                    Class = slot.Itemstack.Class
                });
            }

            Dictionary<string, string> skinParts = new Dictionary<string, string>();
            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            var applied = bh.AppliedSkinParts;
            foreach (var val in applied)
            {
                skinParts[val.PartCode] = val.Code;
            }
            if (didSelect) storePreviousSelection(skinParts);

            capi.Network.GetChannel("charselection").SendPacket(new CharacterSelectionPacket()
            {
                Clothes = clothesPacket.ToArray(),
                DidSelect = didSelect,
                SkinParts = skinParts,
                CharacterClass = characterClass,
                VoicePitch = bh.VoicePitch,
                VoiceType = bh.VoiceType
            });

            capi.Network.SendPlayerNowReady();

            createCharDlg = null;

            capi.Event.PushEvent("finishcharacterselection");
        }
        public Dictionary<string, string> getPreviousSelection()
        {
            Dictionary<string, string> lastSelection = new Dictionary<string, string>();
            if (capi == null || !capi.Settings.String.Exists("lastSkinSelection")) return lastSelection;

            var lastSele = capi.Settings.String["lastSkinSelection"];
            var parts = lastSele.Split(",");
            foreach (var part in parts)
            {
                var keyval = part.Split(":");
                lastSelection[keyval[0]] = keyval[1];
            }
            return lastSelection;
        }
        public void storePreviousSelection(Dictionary<string, string> selection)
        {
            List<string> parts = new List<string>();
            foreach (var val in selection)
            {
                parts.Add(val.Key + ":" + val.Value);
            }

            capi.Settings.String["lastSkinSelection"] = string.Join(",", parts);
        }
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

        //Reset levels player got this day
        private void updateProgressWhichDay()
        {
            this.playerWasMensionedNoMoreExp.Clear();
            currentDayNumber = DateTime.Today.DayOfYear;
            foreach (var it in playersProgressInfos)
            {
                foreach(var subClass in it.Value.subClasses.Values)
                {
                    if (subClass.WhichDay != currentDayNumber)
                    {
                        subClass.WhichDay = currentDayNumber;
                        subClass.LevelsGotThisDay = 0;
                    }
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
                    { 
                        continue; 
                    }
                    canclasses.sapi.Network.GetChannel("cancharactersystem").SendPacket<CANCharacterProgressInfoPacket>(new CANCharacterProgressInfoPacket()
                    {
                        playerCharacterClassProgressInfo = info,
                        packetType = EnumCANCharacterProgressInfoPacket.InfoUpdate
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
        private void composeProgressTab(GuiComposer compo)
        {
            

            
            ElementBounds bounds1 = new ElementBounds()
            {
                Alignment = EnumDialogArea.LeftTop,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = 200,
                fixedHeight = 10
            }.WithFixedAlignmentOffset(0.5, 45);

            ElementBounds currentBounds = bounds1;

            foreach (var it in ClientPlayerProgressInfo.subClasses)
            {
                ElementBounds bounds2 = ElementStdBounds.Statbar(EnumDialogArea.LeftTop, (double)200).WithFixedAlignmentOffset(0, -0);
                bounds2.WithFixedHeight(10.0);

                var expStatBar = new GuiElementStatbar(capi, bounds2, GuiStyle.XPBarColor, false, false);
                compo.BeginChildElements(currentBounds)
                                       .AddInteractiveElement(expStatBar, "statbar" + it.Key.ToString())
                                       .EndChildElements();
                var name = currentBounds.RightCopy(10);
                var level = currentBounds.BelowCopy(0, 5);
                name.fixedY -= 5;
                expStatBar.SetLineInterval(1f);
                expStatBar.SetValues((float)(it.Value.ExpToNextBorder - it.Value.ExpToNextLeft), 0.0f, (float)it.Value.ExpToNextBorder);

                compo.AddStaticText(it.Value.SubClassType.ToString(), CairoFont.SmallButtonText().WithOrientation(EnumTextOrientation.Left), name);
                compo.AddStaticText(it.Value.PercentsReached + " level", CairoFont.SmallButtonText().WithOrientation(EnumTextOrientation.Left), level);
                currentBounds = level.BelowCopy(0, 15);
            }


            
           
            //.Compose();
            //compo.AddRichtext(Lang.Get("canclasses:can-level", this.clientCurrentLevel.ToString()), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), ElementBounds.Fixed(0.0, 25.0, 385.0, 200.0));
            //compo.AddRichtext(Lang.Get("canclasses:can-curexp-allexp", String.Format("{0:0.0}",this.clientAllExpToNextLevel - this.clientCurrentExpToNextLevel), this.clientAllExpToNextLevel.ToString()), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), ElementBounds.Fixed(0.0, 55.0, 385.0, 200.0));
        }
        

        private void ReapplyTraitsForAllSubclasses(IPlayer player)
        {
            if(this.playersProgressInfos.TryGetValue(player.PlayerUID, out var info))
            {
                foreach(var subClass in info.subClasses)
                {
                    ReapplyTraits(player, subClass.Key);
                }
            }
        }

        public void ReapplyTraits(IPlayer player, SubClassType subClass)
        {
            if(!this.playersProgressInfos.TryGetValue(player.PlayerUID, out var info))
            {
                return;
            }
            PlayerSubClass playerSubClassInfo = info.GetSubClass(subClass);
            EntityPlayer eplr = player.Entity;
            string[] stringArray = eplr.WatchedAttributes.GetStringArray("extraTraits");
            foreach (string key1 in stringArray == null ? (IEnumerable<string>)playerSubClassInfo.AccuredTraits : ((IEnumerable<string>)playerSubClassInfo.AccuredTraits).Concat<string>((IEnumerable<string>)stringArray))
            {
                double playerProgress = playerSubClassInfo.PercentsReached;
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
                                eplr.Stats.Set(key2, "trait-" + trait.Code, (float)num, true);
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
                                    eplr.Stats.Set(key2, "trait-" + trait.Code, (float)num, true);
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
                                if (it.Key <= playerProgress)
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
                foreach(var tr in this.TraitsToSubclass)
                {
                    if (tr.Key.Equals(subClass))
                    {
                        foreach(var it in tr.Value)
                        {
                            if (this.TraitsByCode.TryGetValue(it, out trait) && trait.ApplyType == EnumTraitApplyType.OnceAttributeAddition)
                            {
                                if(trait.additionalInfo.TryGetValue("needLevel", out object needLevel))
                                {
                                    if(playerSubClassInfo.PercentsReached >= (int)needLevel)
                                    {
                                        if (stringArray != null)
                                        {
                                            stringArray = stringArray.Append(trait.Code);
                                            eplr.WatchedAttributes.SetStringArray("extraTraits", stringArray);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
        public void AddExperience(double val, IPlayer player, SubClassType subClass)
        {
            if (canclasses.sapi.Side != EnumAppSide.Server)
            {
                return;
            }
            if (!this.playersProgressInfos.TryGetValue(player.PlayerUID, out PlayerCharacterClassProgressInfo playerLevel))
            {
                this.playersProgressInfos[player.PlayerUID] = new PlayerCharacterClassProgressInfo(player.PlayerUID);
            }
                      
            playerLevel.addExp(val, subClass);
            
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
                                eplr.Stats.Set(key2, "trait-" + trait.Code, (float)num, true);
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
                                    eplr.Stats.Set(key2, "trait-" + trait.Code, (float)num, true);
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
            
        public void onBlockUsed(IServerPlayer byPlayer, BlockSelection blockSel)
        {
           // var c = 3;
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
                foreach(var it in this.blockIdToExpGain)
                {
                    if(it.Value.TryGetValue(oldblockId, out var expVal))
                    {
                        canclasses.canCharSys.AddExperience(expVal, byPlayer, it.Key);
                    }
                }
            }
            
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
