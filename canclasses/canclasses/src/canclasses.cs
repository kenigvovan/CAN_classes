using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using canclasses.src.characterClassesSystem;
using System.Linq;
using System;

namespace canclasses.src
{
    public class canclasses : ModSystem
    {

        public static Harmony harmonyInstance;
        public const string harmonyID = "canmods.Patches";
        public static ICoreClientAPI capi;
        public static ICoreServerAPI sapi;
        public static CANCharacterSystem canCharSys;
        public static Dictionary<string, Dictionary<string, float>> gemBuffValuesByLevel;
        public static Dictionary<string, HashSet<string>> buffNameToPossibleItem;
        //public GuiDialogSocketsInfo guiDialogSocketsInfo;
        public bool onMatchGridRecipeDelegate(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.Event.MatchesGridRecipe += new MatchGridRecipeDelegate(onMatchGridRecipeDelegate);
            harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(ModSystem).GetMethod("ShouldLoad", new Type[] { typeof(ICoreAPI)}), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ModSystemShouldLoad")));
            //TODO
            //harmonyInstance.Patch(typeof(Vintagestory.GameContent.BlockEntityAnvil).GetMethod("CheckIfFinished"), transpiler: new HarmonyMethod(typeof(harmPatch).GetMethod("Transpiler_check")));
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("GetHeldItemInfo"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_GetHeldItemInfo")));
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("OnCreatedByCrafting"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_OnCreatedByCrafting")));
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("ShouldDisplayItemDamage"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ShouldDisplayItemDamage_CollectibleObject")));
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("GetItemDamageColor"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_GetItemDamageColor_CollectibleObject")));

            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("GetMaxDurability"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_CollectibleObject_GetMaxDurability")));

        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            harmonyInstance = new Harmony(harmonyID);

            //ItemSlot inSlot, double posX, double posY, double posZ, float size, int color, float dt, bool shading = true, bool origRotate = false, bool showStackSize = true
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("Equals", new[] { typeof(ItemStack), typeof(ItemStack), typeof(string[]) }), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_TreeAttribute_Equal")));
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("GetHeldItemName"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_GetHeldItemName_CollectibleObject")));
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            gemBuffValuesByLevel = new Dictionary<string, Dictionary<string, float>>();
            buffNameToPossibleItem = new Dictionary<string, HashSet<string>>();
            harmonyInstance = new Harmony(harmonyID);
            sapi = api;
            harmonyInstance.Patch(typeof(CollectibleObject).GetMethod("Equals", new[] { typeof(ItemStack), typeof(ItemStack), typeof(string[]) }), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_TreeAttribute_Equal")));
            harmonyInstance.Patch(typeof(GridRecipe).GetMethod("ConsumeInput"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_OnCrafted")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.ItemAxe).GetMethod("OnBlockBrokenWith"), transpiler: new HarmonyMethod(typeof(harmPatch).GetMethod("Transpiler_ChoppedTree")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.BlockMicroBlock).GetMethod("OnCreatedByCrafting"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_OnCreatedByCrafting_MicroBlock")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.BlockEntityChisel).GetMethod("UpdateVoxel", BindingFlags.NonPublic | BindingFlags.Instance), transpiler: new HarmonyMethod(typeof(harmPatch).GetMethod("Transpiler_UpdateVoxel_BlockEntityChiselstatic")));

            harmonyInstance.Patch(typeof(Vintagestory.GameContent.ItemShears).GetMethod("breakMultiBlock", BindingFlags.NonPublic | BindingFlags.Instance), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_OnBlockBrokenWith_ItemShears")));

            canCharSys = api.ModLoader.GetModSystem<CANCharacterSystem>();
            canCharSys = api.ModLoader.GetModSystem<CANCharacterSystem>();
            //TODO
            //api.RegisterCommand("can", "", "", canHandlerCommand);
            //api.RegisterCommand("canadm", "", "", canAdmHandlerCommand);
            api.Event.PlayerJoin += reserClassPlayer;
            api.Event.OnEntityDeath += onEntityDeath;

            api.Event.PlayerNowPlaying += onPlayerPlaying;

            //TODO
            /*foreach (var it in api.World.Blocks)
            {
                if (it.Code != null && it.Code.Path.Contains("log-placed"))
                {
                    foreach (var itDr in it.Drops)
                    {
                        itDr.DropModbyStat = null;
                    }
                }
            }*/

        }
        public void onPlayerPlaying(IServerPlayer byPlayer)
        {
            IInventory charakterInv = byPlayer.InventoryManager.GetOwnInventory("character");
            InventoryBasePlayer playerHotbar = (InventoryBasePlayer)byPlayer.InventoryManager.GetOwnInventory("hotbar");
            charakterInv.SlotModified += (slotId) => {
                if (charakterInv[slotId].Itemstack != null && charakterInv[slotId].Itemstack.Attributes.HasAttribute("canencrusted"))
                {
                    //do stuff
                }
            };
            playerHotbar.SlotModified += (slotId) => {
                if (playerHotbar[slotId].Itemstack != null && playerHotbar[slotId].Itemstack.Attributes.HasAttribute("canencrusted"))
                {
                    ITreeAttribute tree = playerHotbar[slotId].Itemstack.Attributes.GetTreeAttribute("canencrusted");
                    for (int i = 0; i < tree.GetInt("socketsnumber"); i++)
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot" + i);
                        if (treeSocket.GetInt("size") > 0)
                        {

                        }
                    }
                }
            };
        }
        public void onEntityDeath(Entity entity, DamageSource damageSource)
        {
            //exp gain for soldier class
            if (damageSource != null && damageSource.Source == EnumDamageSource.Player)
            {
                if (damageSource.SourceEntity != null)
                {
                    if (entity.Code.Path.Equals("player"))
                    {
                        if ((entity as EntityPlayer).LastReviveTotalHours < Config.Current.EXP_FROM_KILLED_PLAYER_MIN_LAST_REVIVE_TOTAL_HOURS.Val)
                        {
                            return;
                        }
                    }
                    if (!canCharSys.killedEntityToExp.TryGetValue(entity.Code.Path, out var expVal))
                    {
                        return;
                    }
                    string key = damageSource.SourceEntity.WatchedAttributes.GetString("characterClass", null);
                    if (key == null)
                    {
                        return;
                    }
                    if (canCharSys.expReceiversClasses["entitieskilling"].Contains(key))
                    {
                        canCharSys.playersProgressInfos[(damageSource.SourceEntity as EntityPlayer).PlayerUID].addExp(expVal);
                    }
                }
            }
        }
        public static void canAdmHandlerCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return;
            }
            if (args.Length < 1)
            {
                return;
            }
            if (args[0].Equals("entitiesinfo"))
            {
                StringBuilder sb = new StringBuilder();
                Dictionary<string, int> countEntities = new Dictionary<string, int>();
                foreach (var it in sapi.World.LoadedEntities)
                {
                    if (countEntities.ContainsKey(it.Value.GetName()))
                    {
                        countEntities[it.Value.GetName()]++;
                    }
                    else
                    {
                        countEntities.Add(it.Value.GetName(), 1);
                    }
                }
                foreach (var it in countEntities)
                {
                    sb.Append(it.Key + " " + it.Value).Append("\n");
                }
                player.SendMessage(GlobalConstants.GeneralChatGroup, sb.ToString(), EnumChatType.Notification);

            }
        }
        public static void canHandlerCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return;
            }
            if (args.Length < 1)
            {
                return;
            }
            if (args[0].Equals("ap"))
            {
                if (args.Length < 3)
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, "more args needed", EnumChatType.Notification);
                    return;
                }
                foreach (var pl in sapi.World.AllPlayers)
                {
                    if (pl.PlayerName.Equals(args[2]))
                    {
                        if (canCharSys.playersProgressInfos.TryGetValue(player.PlayerUID, out var plPr))
                        {
                            plPr.addExp(double.Parse(args[1]), true);
                            player.SendMessage(GlobalConstants.GeneralChatGroup, pl.PlayerName + " got " + args[1] + " exp.", EnumChatType.Notification);
                            return;
                        }
                    }
                    player.SendMessage(GlobalConstants.GeneralChatGroup, pl.PlayerName + " no such player.", EnumChatType.Notification);
                }
            }
            else if (args[0].Equals("rename"))
            {
                if (player.InventoryManager.ActiveHotbarSlot.Itemstack != null)
                {
                    player.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetString("canrenameditem", "testname");

                }
            }
            else if (args[0].Equals("showlevel"))
            {
                StringBuilder sb = new StringBuilder();
                foreach (var plProgress in canCharSys.playersProgressInfos)
                {
                    sb.Append(plProgress.Key + " - " + plProgress.Value.globalExp);
                    sb.Append("\n");
                }
                 player.SendMessage(GlobalConstants.GeneralChatGroup, sb.ToString(), EnumChatType.Notification);
                return;
            }
        }
        public void reserClassPlayer(IServerPlayer byPlayer)
        {
            var plClassCode = byPlayer.Entity.WatchedAttributes.GetString("characterClass");
            bool found = false;
            foreach (var it in canCharSys.characterClasses)
            {
                if (it.Code.Equals(plClassCode))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                byPlayer.Entity.WatchedAttributes.SetString("characterClass", canCharSys.characterClasses.First().Code);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("characterClass");
            }
            canCharSys.playersProgressInfos.TryGetValue(byPlayer.PlayerUID, out var plProgress);
            if (plProgress == null)
            {
                plProgress = new PlayerCharacterClassProgressInfo(plClassCode, byPlayer.PlayerUID);
                canCharSys.playersProgressInfos.Add(byPlayer.PlayerUID, plProgress);
            }
            sapi.Network.GetChannel("cancharactersystem").SendPacket(new CANCharacterProgressInfoPacket()
            {
                currentLevel = plProgress.globalPercents,
                currentExpToNextLevel = plProgress.expToNextPercent,
                packetType = EnumCANCharacterProgressInfoPacket.OnJoin,
                allExpToNextLevel = plProgress.expToNextPercentAll
            }, sapi.World.PlayerByUid(byPlayer.PlayerUID) as IServerPlayer
            );
        }
        //this.capi.World.Player.Entity.WatchedAttributes.GetString("characterClass", (string) null);
        public override void Dispose()
        {
            if (harmonyInstance != null)
            {
                harmonyInstance.UnpatchAll(harmonyID);
            }
            harmonyInstance = null;
            capi = null;
            sapi = null;
            canCharSys = null;
        }

    }
}
