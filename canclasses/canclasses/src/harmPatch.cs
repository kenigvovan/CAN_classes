using Cairo;
using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace canclasses.src
{
    [HarmonyPatch]
    public class harmPatch
    {       
        public static bool Prefix_CollectibleObject_GetMaxDurability(Vintagestory.API.Common.CollectibleObject __instance, ItemStack itemstack, ref int __result)
        {
            if (itemstack.Attributes.HasAttribute("candurabilitymax"))
            {
                __result = itemstack.Attributes.GetInt("candurabilitymax");
                return false;
                //return false;
            }
            return true;
        }
        public static MeshRef quadModel;
        public static float[] pMatrix = Mat4f.Create();
       
      
        [HarmonyPriority(500)]
        public static void Prefix_OnBlockBrokenWith_ItemShears(Vintagestory.GameContent.ItemShears __instance, BlockPos pos, IPlayer plr)
        {           
            Block block = plr.Entity.World.BlockAccessor.GetBlock(pos);
            if(block == null)
            {
                return;
            }
            if (block is BlockCrop)
            {
                string classcode = plr.Entity.WatchedAttributes.GetString("characterClass", null);
                if (classcode == null)
                {
                    return;
                }
                else
                {
                    if (canclasses.canCharSys.blockIdToExpGain.ContainsKey(classcode) && canclasses.canCharSys.blockIdToExpGain[classcode].TryGetValue(block.Id, out var expVal))
                    {
                        if (canclasses.canCharSys.playersProgressInfos.ContainsKey(plr.PlayerUID)){
                            canclasses.canCharSys.playersProgressInfos[plr.PlayerUID].addExp(expVal);
                        }
                    }
                }
            }
        }
       
        public static MethodInfo GetItemStackFromItemSlot = typeof(ItemSlot).GetMethod("get_Itemstack");
        public static MethodInfo GetAttributesFromItemStack = typeof(ItemStack).GetMethod("get_Attributes");
        public static MethodInfo HasAttributeITreeAttribute = typeof(ITreeAttribute).GetMethod("HasAttribute");

        public static FieldInfo ElementBoundsSlotGrid = typeof(GuiElementItemSlotGridBase).GetField("slotBounds", BindingFlags.NonPublic | BindingFlags.Instance);
        //slotQuantityTextures
        public static FieldInfo slotQuantityTexturesSlotGrid = typeof(GuiElementItemSlotGridBase).GetField("slotQuantityTextures", BindingFlags.NonPublic | BindingFlags.Instance);
        //NOT USED
       
        public static Dictionary<string, AssetLocation> preparedEncrustedGemsImages;
        public static Dictionary<string, AssetLocation> socketsTextureDict;

        private static LoadedTexture zoomed = new LoadedTexture(canclasses.capi);
        static MethodInfo dynMethodGenContext = typeof(GuiElementItemSlotGridBase).GetMethod("genContext",
                   BindingFlags.NonPublic | BindingFlags.Instance);
        static MethodInfo dynMethodGenerateTexture = typeof(GuiElementItemSlotGridBase).GetMethod("generateTexture",
                  BindingFlags.NonPublic | BindingFlags.Instance, null,  new Type[] { typeof(ImageSurface), typeof(LoadedTexture).MakeByRefType(), typeof(bool) }, null);
        //NOT USED
        public static void addExpChiseled(IPlayer player)
        {
            if(player.Entity.Api.Side == EnumAppSide.Client)
            {
                return;
            }
            string key = player.Entity.WatchedAttributes.GetString("characterClass", (string)null);
            if (key == null)
                return;
            if (canclasses.canCharSys.expReceiversClasses["chiseling"].Contains(key))
            {
                canclasses.canCharSys.playersProgressInfos[player.PlayerUID].addExp(Config.Current.EXP_CHISEL_USE.Val);
            }
        }
        public static IEnumerable<CodeInstruction> Transpiler_UpdateVoxel_BlockEntityChiselstatic(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            var codes = new List<CodeInstruction>(instructions);
            var proxyMethod = AccessTools.Method(typeof(harmPatch), "addExpChiseled");

            for (int i = 0; i < codes.Count; i++)
            {
                if (!found &&
                    codes[i].opcode == OpCodes.Ldarg_1 && codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 2].opcode == OpCodes.Callvirt && codes[i - 1].opcode == OpCodes.Callvirt)
                {
                    
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    yield return codes[i];
                    found = true;
                    continue;
                }
                yield return codes[i];
            }
        }
        public static bool Prefix_GetHeldItemName_CollectibleObject(Vintagestory.API.Common.CollectibleObject __instance, ItemStack itemStack, ref string __result)
        {
            if (itemStack.Attributes.GetString("canrenameditem", "") != "")
            {
                __result = itemStack.Attributes.GetString("canrenameditem");
                return false;
            }
            return true;
        }

        public static bool Prefix_GetItemDamageColor_CollectibleObject(Vintagestory.API.Common.CollectibleObject __instance, ItemStack itemstack, ref int __result)
        {
            if (itemstack.Attributes.GetInt("candurabilitymax", -42) != -42)
            {
                if(itemstack.Attributes.GetInt("candurabilitymax") == itemstack.Attributes.GetInt("durability"))
                {
                    __result = 14315734;
                    return false;
                }
                //return false;
            }
            return true;
        }
        public static double testWidth(double width)
        {

            if(width > 1.0)
            {
                width = 1.0;
            }
           return width;
        }
        public static IEnumerable<CodeInstruction> Transpiler_ComposeSlotOverlays(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            var codes = new List<CodeInstruction>(instructions);
            var proxyMethod = AccessTools.Method(typeof(harmPatch), "testWidth");

            for (int i = 0; i < codes.Count; i++)
            {
                if (!found &&
                    codes[i].opcode == OpCodes.Conv_R8 && codes[i + 1].opcode == OpCodes.Ldarg_0 && codes[i + 2].opcode == OpCodes.Ldfld && codes[i - 1].opcode == OpCodes.Div)
                {
                    yield return codes[i];
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    found = true;
                    continue;
                }
                yield return codes[i];
            }
        }
        public static void Prefix_ShouldDisplayItemDamage_CollectibleObject(Vintagestory.API.Common.CollectibleObject __instance, IItemStack itemstack, ref bool __result)
        {
           if(itemstack.Attributes.GetInt("candurabilitymax", -42) != -42)
            {
                __result = true;
            }
        }
        public static bool Prefix_OnCreatedByCrafting_MicroBlock(Vintagestory.GameContent.BlockMicroBlock __instance, ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            if (byRecipe.Name.Path.Equals("chiselblockcopy"))
            {
                foreach(var it in allInputslots)
                {
                    if(it.Itemstack != null && it.Itemstack.Block != null && it.Itemstack.Block is BlockMicroBlock)
                    {
                        outputSlot.Itemstack = it.Itemstack.Clone();
                    }
                }               
                outputSlot.Itemstack.StackSize = byRecipe.Output.Quantity;
                outputSlot.MarkDirty();
                return false;
            }
            return true;
        }
        public static MethodInfo Field_Count = typeof(Stack<BlockPos>).GetMethod("get_Count");
        public static IEnumerable<CodeInstruction> Transpiler_ChoppedTree(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            var codes = new List<CodeInstruction>(instructions);
            var decMethod = AccessTools.GetDeclaredMethods(typeof(Stack<BlockPos>))
            .Where(m => m.Name == "get_Count")
            .Single();
            var proxyMethod = AccessTools.Method(typeof(harmPatch), "choppedMethod");
            
            for (int i = 0; i < codes.Count; i++)
            {
                if (!found &&
                    codes[i].opcode == OpCodes.Stloc_2 && codes[i + 1].opcode == OpCodes.Ldloc_2 && codes[i + 2].opcode == OpCodes.Callvirt && codes[i - 1].opcode == OpCodes.Call)
                {
                    yield return codes[i];
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Callvirt, Field_Count);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    found = true;
                    continue;
                }
                yield return codes[i];
            }
        }
        public static void choppedMethod(int num, Entity byEntity)
        {
            if (byEntity.Api.Side == EnumAppSide.Client)
            {
                return;
            }
            if(num == 0)
            {
                return;
            }
            string key = byEntity.WatchedAttributes.GetString("characterClass", (string)null);
            if (key == null)
            {
                return;
            }
            if (canclasses.canCharSys.expReceiversClasses["treecutting"].Contains(key))
            {
                canclasses.canCharSys.playersProgressInfos[(byEntity as EntityPlayer).PlayerUID].addExp(num * Config.Current.EXP_CHOPPED_LOG.Val);
            }      
        }
        public static FieldInfo BlockEntityAnvilworkItemStack = typeof(BlockEntityAnvil).GetField("workItemStack", BindingFlags.NonPublic | BindingFlags.Instance);
        public static IEnumerable<CodeInstruction> Transpiler_check(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            var codes = new List<CodeInstruction>(instructions);
            var decMethod = AccessTools.GetDeclaredMethods(typeof(IWorldAccessor))
          .Where(m => m.Name == "SpawnItemEntity" && m.GetParameters().Types().Contains(typeof(ItemStack)) && m.GetParameters().Types().Contains(typeof(Vec3d)) && m.GetParameters().Types().Contains(typeof(Vec3d))).ElementAt(1)
         ;

            var proxyMethod = AccessTools.Method(typeof(harmPatch), "addNameAndProcessMadeTool");
            for (int i = 0; i < codes.Count; i++)
            {
                if (!found &&
                    codes[i].opcode == OpCodes.Ldarg_1 && codes[i + 1].opcode == OpCodes.Brfalse_S && codes[i + 2].opcode == OpCodes.Ldarg_1 && codes[i - 1].opcode == OpCodes.Stfld)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    //yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, BlockEntityAnvilworkItemStack);
                    //get workingitem
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    found = true;
                }
                yield return codes[i];
            }
        }

        public static void addName(IServerPlayer byPlayer, ItemStack itemStack, ItemStack workItemStack)
        {

            if (byPlayer != null)
            {
                if (byPlayer.Entity.Api.Side == EnumAppSide.Client)
                    return;

                string key = byPlayer.Entity.WatchedAttributes.GetString("characterClass", (string)null);
                if (key == null)
                    return;
                CharacterClass characterClass;
                if (canclasses.canCharSys.characterClassesByCode.TryGetValue(key, out characterClass))
                {
                    if(canclasses.canCharSys.expReceiversClasses.ContainsKey("smithing") && canclasses.canCharSys.expReceiversClasses["smithing"].Contains(characterClass.Code))
                    {
                        var plProgress = canclasses.canCharSys.playersProgressInfos[byPlayer.PlayerUID];
                        if (plProgress != null)
                        {
                            if (canclasses.canCharSys.extraDurabilityReceivers.Contains(itemStack.Collectible.Code.Path.Split('-')[0]))
                            {
                                //itemStack.Attributes.SetInt("candurabilitybonus", plProgress.globalPercents);
                                double pp = (1.0 + plProgress.globalPercents / 100.0);
                                itemStack.Attributes.SetInt("durability", (int)(itemStack.Item.Durability * pp));
                                itemStack.Attributes.SetInt("candurabilitymax", (int)(itemStack.Item.Durability * pp));
                                itemStack.Attributes.SetInt("candurabilitybonus", plProgress.globalPercents);
                            }
                            canclasses.canCharSys.playersProgressInfos[byPlayer.PlayerUID].addExp(Config.Current.EXP_FORGED_TOOL.Val);
                        }
                    }
                    
                }
                //if player's class is smith we continue
                //get player progress
                //set attribute on toolhead(later we set bonus durability(during craft))
                //add info about how much bonus was added to tool
                //add exp to player's progress
                //during storm set a chance to get better tool/weapon
                //int num3 = itemStack.Attributes.GetInt("durability");
                //itemStack.Item.Durability = 3333;
                if (itemStack.Item != null && (itemStack.Item.Shape.Base.Path.StartsWith("item/tool")
                    || itemStack.Item.Shape.Base.Path.StartsWith("item/spytube")
                    || itemStack.Item.Code.Domain.Equals("xmelee")))
                {
                    itemStack.Attributes.SetString("smithname", byPlayer.PlayerName);
                }
            }
        }
        public static void Postfix_GetHeldItemInfo(Vintagestory.API.Common.CollectibleObject __instance, ItemSlot inSlot,
      StringBuilder dsc,
      IWorldAccessor world,
      bool withDebugInfo)
        {
            ItemStack itemstack = inSlot.Itemstack;
            string smithName = itemstack.Attributes.GetString("smithname");
            if (smithName != null && !(smithName.Length == 0))
            {
                dsc.Append(Lang.Get("canmods" + ":smithed_by", "<font color=\"" + Lang.Get("canmods" + ":playername_color") + "\">" + smithName + "</strong>")).Append("\n");
            }
            int durBonus = itemstack.Attributes.GetInt("candurabilitybonus");
            if(durBonus != 0 && durBonus > 0)
            {
                dsc.Append("Durability bonus: +").Append(durBonus).Append("%\n");
            }
            if(itemstack.Attributes.HasAttribute("canencrusted"))
            {
                var tree = itemstack.Attributes.GetTreeAttribute("canencrusted");
                dsc.Append(Lang.Get("canmods:item-can-have-n-sockets", tree.GetInt("socketsnumber")));
                dsc.Append("\n");
                for (int i = 0; i < tree.GetInt("socketsnumber"); i++)
                {
                    var treeSlot = tree.GetTreeAttribute("slot" + i);
                    dsc.Append(Lang.Get("canmods:item-socket-tier", treeSlot.GetAsInt("sockettype")));
                    dsc.Append("\n");
                    if(treeSlot.GetString("gemtype") != "")
                    {
                        if (treeSlot.GetString("attributeBuff").Equals("maxhealthExtraPoints"))
                        {
                            dsc.Append(Lang.Get("canmods:socket-has-attribute", i, treeSlot.GetFloat("attributeBuffValue"))).Append(Lang.Get("canmods:buff-name-" + treeSlot.GetString("attributeBuff")));
                        }
                        else
                        {
                            dsc.Append(Lang.Get("canmods:socket-has-attribute-percent", i, treeSlot.GetFloat("attributeBuffValue") * 100)).Append(Lang.Get("canmods:buff-name-" + treeSlot.GetString("attributeBuff")));
                        }
                        dsc.Append('\n');
                    }
                }

            }
            else if(itemstack.ItemAttributes != null && itemstack.ItemAttributes.KeyExists("canhavesocketsnumber"))
            {
                dsc.Append(Lang.Get("canmods:item-can-have-n-sockets", itemstack.ItemAttributes["canhavesocketsnumber"].AsInt()));
                dsc.Append("\n");
            }

            return;
        }

        public static void Prefix_OnCreatedByCrafting(Vintagestory.API.Common.CollectibleObject __instance, ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {

            if (!(outputSlot.Itemstack?.Item?.Shape?.Base?.Path?.StartsWith("item/tool")).HasValue ||
                !(outputSlot.Itemstack?.Item?.Shape?.Base?.Path?.StartsWith("item/tool")).Value
                && !(outputSlot.Itemstack.Item.Code.Domain.Equals("xmelee")))
            {
                return;
            }

            //outputSlot.Itemstack.Item.Durability = 3333;
            int durBonus = 0;
            string twoAuthors = "";
            foreach (var it in allInputslots)
            {
                if(it.Itemstack != null && it.Itemstack.Attributes.HasAttribute("candurabilitybonus"))
                {
                    durBonus = it.Itemstack.Attributes.GetInt("candurabilitybonus");
                }
                if (it.Itemstack != null && it.Itemstack.Attributes.GetString("smithname") != null)
                {
                    if (it.Itemstack.Item.Code.Path.StartsWith("xweapongrip"))
                    {
                        //it.Itemstack.Attributes.SetString("smithname", "notme");
                        if (twoAuthors != "" && !it.Itemstack.Attributes.GetString("smithname").Equals(twoAuthors))
                        {
                            twoAuthors += " & " + it.Itemstack.Attributes.GetString("smithname");
                        }
                        outputSlot.Itemstack.Attributes.SetString("smithname", twoAuthors);
                        if(durBonus > 0)
                        {
                            double pp = (1.0 + durBonus / 100.0);
                            outputSlot.Itemstack.Attributes.SetInt("durability", (int)(outputSlot.Itemstack.Item.Durability * pp));
                            outputSlot.Itemstack.Attributes.SetInt("candurabilitymax", (int)(outputSlot.Itemstack.Item.Durability * pp));
                            outputSlot.Itemstack.Attributes.SetInt("candurabilitybonus", durBonus);
                        }
                        return;
                    }
                    if (it.Itemstack.Item.Code.Path.StartsWith("xspearhead") || it.Itemstack.Item.Code.Path.StartsWith("xhalberdhead"))
                    {
                        outputSlot.Itemstack.Attributes.SetString("smithname", it.Itemstack.Attributes.GetString("smithname"));
                        if (durBonus > 0)
                        {
                            double pp = (1.0 + durBonus / 100.0);
                            outputSlot.Itemstack.Attributes.SetInt("durability", (int)(outputSlot.Itemstack.Item.Durability * pp));
                            outputSlot.Itemstack.Attributes.SetInt("candurabilitymax", (int)(outputSlot.Itemstack.Item.Durability * pp));
                            outputSlot.Itemstack.Attributes.SetInt("candurabilitybonus", durBonus);
                        }
                        return;
                    }
                    if (it.Itemstack.Item.Code.Domain.StartsWith("xmelee"))
                    {
                        twoAuthors = it.Itemstack.Attributes.GetString("smithname");
                        //continue;
                    }
                    outputSlot.Itemstack.Attributes.SetString("smithname", it.Itemstack.Attributes.GetString("smithname"));
                    if (durBonus > 0)
                    {
                        double pp = (1.0 + durBonus / 100.0);
                        outputSlot.Itemstack.Attributes.SetInt("durability", (int)(outputSlot.Itemstack.Item.Durability * pp));
                        outputSlot.Itemstack.Attributes.SetInt("candurabilitymax", (int)(outputSlot.Itemstack.Item.Durability * pp));
                        outputSlot.Itemstack.Attributes.SetInt("candurabilitybonus", durBonus);
                    }
                    return;
                }
            }            
        }
        public static bool Prefix_OnCrafted(Vintagestory.API.Common.GridRecipe __instance, IPlayer byPlayer, ItemSlot[] inputSlots, int gridWidth, ref bool __result)
        {
            //__result = true;
           if(__instance.RequiresTrait == null)
            {
                return true;
            }
            if (byPlayer.Entity.Api.Side == EnumAppSide.Server)
            {
                canclasses.canCharSys.playersCraftTraitsInfos.TryGetValue(byPlayer.PlayerUID, out var traitInfoDict);
                if(traitInfoDict == null)
                {
                    return true;
                }
                if (traitInfoDict.ContainsKey(__instance.Name.Path))
                {
                    traitInfoDict[__instance.Name.Path].usedCraftsThisReset = traitInfoDict[__instance.Name.Path].usedCraftsThisReset + 1;
                }
                return true;
            }
            return true;
        }
        //we don't want default characterclasses system
        public static bool Prefix_ModSystemShouldLoad(Vintagestory.API.Common.ModSystem __instance, ref bool __result)
        {
            if (__instance is CharacterSystem)
            {
                //__instance.Dispose();
                __result = false;
                return false;
            }
            return true;
        }
        public static void Postfix_TreeAttribute_Equal(Vintagestory.API.Common.CollectibleObject __instance, ItemStack thisStack, ItemStack otherStack, ref bool __result, params string[] ignoreAttributeSubTrees)
        {
            if(thisStack.Block != null && otherStack.Block != null)
            {
                if (thisStack.Block is BlockLiquidContainerBase && otherStack.Block is BlockLiquidContainerBase)
                {
                    if(((BlockLiquidContainerBase)thisStack.Collectible).GetCurrentLitres(thisStack) != ((BlockLiquidContainerBase)otherStack.Collectible).GetCurrentLitres(otherStack))
                    {
                        __result = false;
                    }
                }
            }
        }
    }
}
