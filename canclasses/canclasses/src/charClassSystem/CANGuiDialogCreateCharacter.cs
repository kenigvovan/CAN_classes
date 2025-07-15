using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace canclasses.src.characterClassesSystem
{
    public class CANGuiDialogCreateCharacter : GuiDialog
    {
        private bool didSelect;
        protected IInventory characterInv;
        protected ElementBounds insetSlotBounds;
        private Dictionary<EnumCharacterDressType, int> DressPositionByTressType = new Dictionary<EnumCharacterDressType, int>();
        private Dictionary<EnumCharacterDressType, ItemStack[]> DressesByDressType = new Dictionary<EnumCharacterDressType, ItemStack[]>();
        private CANCharacterSystem modSys;
        private int currentClassIndex;
        private int curTab;
        private int rows = 7;
        private float charZoom = 1f;
        private bool charNaked = true;
        protected int dlgHeight = 483;
        protected float yaw = -1.270796f;
        protected bool rotateCharacter;
        private Vec4f lighPos = new Vec4f(-1f, -1f, 0.0f, 0.0f).NormalizeXYZ();
        private Matrixf mat = new Matrixf();

        public CANGuiDialogCreateCharacter(ICoreClientAPI capi, CANCharacterSystem modSys)
          : base(capi)
        {
            this.modSys = modSys;
        }

        protected void ComposeGuis()
        {
            double unscaledSlotPadding = GuiElementItemSlotGridBase.unscaledSlotPadding;
            double unscaledSlotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
            this.characterInv = this.capi.World.Player.InventoryManager.GetOwnInventory("character");
            ElementBounds bounds1 = ElementBounds.Fixed(0.0, -25.0, 450.0, 25.0);
            double fixedY1 = 20.0 + unscaledSlotPadding;
            ElementBounds bounds2 = ElementBounds.FixedSize(717.0, (double)this.dlgHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds bounds3 = ElementBounds.FixedSize(757.0, (double)(this.dlgHeight + 40)).WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0.0);
            GuiTab[] tabs = new GuiTab[2]
            {
        new GuiTab() { Name = "Skin & Voice", DataInt = 0 },
        new GuiTab() { Name = "Class", DataInt = 1 }
            };
            this.Composers["createcharacter"] = this.capi.Gui.CreateCompo("createcharacter", bounds3).AddShadedDialogBG(bounds2).AddDialogTitleBar(this.curTab == 0 ? Lang.Get("Customize Skin") : (this.curTab == 1 ? Lang.Get("Select character class") : Lang.Get("Select your outfit")), new Action(this.OnTitleBarClose)).AddHorizontalTabs(tabs, bounds1, new Action<int>(this.onTabClicked), CairoFont.WhiteSmallText().WithWeight((FontWeight)1), CairoFont.WhiteSmallText().WithWeight((FontWeight)1), "tabs").BeginChildElements(bounds2);
            this.capi.World.Player.Entity.hideClothing = false;
            if (this.curTab == 0)
            {
                EntityBehaviorExtraSkinnable behavior = this.capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                this.capi.World.Player.Entity.hideClothing = this.charNaked;
                (this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer).TesselateShape();
                CairoFont font = CairoFont.WhiteSmallText();
                TextExtents textExtents = font.GetTextExtents(Lang.Get("Show dressed"));
                int num = 22;
                ElementBounds refBounds = ElementBounds.Fixed(0.0, fixedY1, 204.0, (double)(this.dlgHeight - 59)).FixedGrow(2.0 * unscaledSlotPadding, 2.0 * unscaledSlotPadding);
                this.insetSlotBounds = ElementBounds.Fixed(0.0, fixedY1 + 2.0, 265.0, refBounds.fixedHeight - 2.0 * unscaledSlotPadding + 10.0).FixedRightOf(refBounds, 10.0);
                ElementBounds.Fixed(0.0, fixedY1, 54.0, (double)(this.dlgHeight - 59)).FixedGrow(2.0 * unscaledSlotPadding, 2.0 * unscaledSlotPadding).FixedRightOf(this.insetSlotBounds, 10.0);
                ElementBounds bounds4 = ElementBounds.Fixed((double)(int)this.insetSlotBounds.fixedX + this.insetSlotBounds.fixedWidth / 2.0 - ((TextExtents)textExtents).Width / (double)RuntimeEnv.GUIScale / 2.0 - 12.0, 0.0, ((TextExtents) textExtents).Width / (double)RuntimeEnv.GUIScale + 1.0, ((TextExtents) textExtents).Height / (double)RuntimeEnv.GUIScale).FixedUnder(this.insetSlotBounds, 4.0).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(12.0, 6.0);
                ElementBounds elementBounds1 = (ElementBounds)null;
                double fixedX = 0.0;
                foreach (SkinnablePart availableSkinPart in behavior.AvailableSkinParts)
                {
                    ElementBounds elementBounds2 = ElementBounds.Fixed(fixedX, elementBounds1 == null || elementBounds1.fixedY == 0.0 ? -10.0 : elementBounds1.fixedY + 8.0, (double)num, (double)num);
                    string code = availableSkinPart.Code;
                    AppliedSkinnablePartVariant skinnablePartVariant = behavior.AppliedSkinParts.FirstOrDefault<AppliedSkinnablePartVariant>((System.Func<AppliedSkinnablePartVariant, bool>)(sp => sp.PartCode == code));
                    ElementBounds elementBounds3;
                    if (availableSkinPart.Type == EnumSkinnableType.Texture && !availableSkinPart.UseDropDown)
                    {
                        int selectedIndex = 0;
                        int[] colors = new int[availableSkinPart.Variants.Length];
                        for (int index = 0; index < availableSkinPart.Variants.Length; ++index)
                        {
                            colors[index] = availableSkinPart.Variants[index].Color;
                            if (skinnablePartVariant?.Code == availableSkinPart.Variants[index].Code)
                                selectedIndex = index;
                        }
                        ElementBounds elementBounds4;
                        this.Composers["createcharacter"].AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), elementBounds4 = elementBounds2.BelowCopy(fixedDeltaY: 10.0).WithFixedSize(210.0, 22.0));
                        this.Composers["createcharacter"].AddColorListPicker(colors, (Action<int>)(index => this.onToggleSkinPartColor(code, index)), elementBounds3 = elementBounds4.BelowCopy().WithFixedSize((double)num, (double)num), 180, "picker-" + code);
                        for (int index = 0; index < colors.Length; ++index)
                        {
                            GuiElementColorListPicker colorListPicker = this.Composers["createcharacter"].GetColorListPicker("picker-" + code + "-" + index.ToString());
                            colorListPicker.ShowToolTip = true;
                            colorListPicker.TooltipText = Lang.Get("color-" + availableSkinPart.Variants[index].Code);
                        }
                        this.Composers["createcharacter"].ColorListPickerSetValue("picker-" + code, selectedIndex);
                    }
                    else
                    {
                        int selectedIndex = 0;
                        string[] names = new string[availableSkinPart.Variants.Length];
                        string[] values = new string[availableSkinPart.Variants.Length];
                        for (int index = 0; index < availableSkinPart.Variants.Length; ++index)
                        {
                            names[index] = Lang.Get("skinpart-" + code + "-" + availableSkinPart.Variants[index].Code);
                            values[index] = availableSkinPart.Variants[index].Code;
                            if (skinnablePartVariant?.Code == values[index])
                                selectedIndex = index;
                        }
                        ElementBounds elementBounds5;
                        this.Composers["createcharacter"].AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), elementBounds5 = elementBounds2.BelowCopy(fixedDeltaY: 10.0).WithFixedSize(210.0, 22.0));
                        string ifExists = Lang.GetIfExists("skinpartdesc-" + code);
                        if (ifExists != null)
                            this.Composers["createcharacter"].AddHoverText(ifExists, CairoFont.WhiteSmallText(), 300, elementBounds5 = elementBounds5.FlatCopy());
                        this.Composers["createcharacter"].AddDropDown(values, names, selectedIndex, (SelectionChangedDelegate)((variantcode, selected) => this.onToggleSkinPartColor(code, variantcode)), elementBounds3 = elementBounds5.BelowCopy().WithFixedSize(200.0, 25.0), "dropdown-" + code);
                    }
                    elementBounds1 = elementBounds3.FlatCopy();
                    if (availableSkinPart.Colbreak)
                    {
                        fixedX = this.insetSlotBounds.fixedX + this.insetSlotBounds.fixedWidth + 22.0;
                        elementBounds1.fixedY = 0.0;
                    }
                }
                this.Composers["createcharacter"].AddInset(this.insetSlotBounds, 2).AddToggleButton(Lang.Get("Show dressed"), font, new Action<bool>(this.OnToggleDressOnOff), bounds4, "showdressedtoggle").AddSmallButton(Lang.Get("Randomize"), new ActionConsumable(this.OnRandomizeSkin), ElementBounds.Fixed(0, this.dlgHeight - 30).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(12.0, 6.0)).AddSmallButton(Lang.Get("Confirm Skin"), new ActionConsumable(this.OnNext), ElementBounds.Fixed(0, this.dlgHeight - 30).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12.0, 6.0));
                this.Composers["createcharacter"].GetToggleButton("showdressedtoggle").SetValue(!this.charNaked);
            }
            if (this.curTab == 1)
            {
                (this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer).TesselateShape();
                double num1 = fixedY1 - 10.0;
                ElementBounds refBounds1 = ElementBounds.Fixed(0.0, num1, 0.0, (double)(this.dlgHeight - 47)).FixedGrow(2.0 * unscaledSlotPadding, 2.0 * unscaledSlotPadding);
                this.insetSlotBounds = ElementBounds.Fixed(0.0, num1 + 25.0, 190.0, refBounds1.fixedHeight - 2.0 * unscaledSlotPadding + 10.0).FixedRightOf(refBounds1, 10.0);
                ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, num1, 1, this.rows).FixedGrow(2.0 * unscaledSlotPadding, 2.0 * unscaledSlotPadding).FixedRightOf(this.insetSlotBounds, 10.0);
                ElementBounds refBounds2 = ElementBounds.Fixed(0.0, num1 + 25.0, 35.0, unscaledSlotSize - 4.0).WithFixedPadding(2.0).FixedRightOf(this.insetSlotBounds, 20.0);
                ElementBounds bounds5 = ElementBounds.Fixed(0.0, num1 + 25.0, 200.0, unscaledSlotSize - 4.0 - 8.0).FixedRightOf(refBounds2, 20.0);
                ElementBounds elementBounds6 = bounds5.ForkBoundingParent(4.0, 4.0, 4.0, 4.0);
                ElementBounds elementBounds7 = ElementBounds.Fixed(0.0, num1 + 25.0, 35.0, unscaledSlotSize - 4.0).WithFixedPadding(2.0).FixedRightOf(elementBounds6, 20.0);
                CairoFont cairoFont = CairoFont.WhiteMediumText();
                ElementBounds elementBounds8 = bounds5;
                double fixedY2 = elementBounds8.fixedY;
                double fixedHeight = bounds5.fixedHeight;
                FontExtents fontExtents = cairoFont.GetFontExtents();
                double num2 = ((FontExtents) fontExtents).Height / (double)RuntimeEnv.GUIScale;
                double num3 = (fixedHeight - num2) / 2.0;
                elementBounds8.fixedY = fixedY2 + num3;
                ElementBounds bounds6 = ElementBounds.Fixed(0.0, 0.0, 480.0, 100.0).FixedUnder(refBounds2, 20.0).FixedRightOf(this.insetSlotBounds, 20.0);
                this.Composers["createcharacter"].AddInset(this.insetSlotBounds, 2).AddIconButton("left", (Action<bool>)(on => this.changeClass(-1)), refBounds2.FlatCopy()).AddInset(elementBounds6, 2).AddDynamicText("Commoner", cairoFont.Clone().WithOrientation(EnumTextOrientation.Center), bounds5, "className").AddIconButton("right", (Action<bool>)(on => this.changeClass(1)), elementBounds7.FlatCopy()).AddRichtext("", CairoFont.WhiteDetailText(), bounds6, "characterDesc").AddSmallButton(Lang.Get("Confirm Class"), new ActionConsumable(this.OnConfirm), ElementBounds.Fixed(0, this.dlgHeight - 30).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12.0, 6.0));
                this.changeClass(0);
            }
            GuiElementHorizontalTabs horizontalTabs = this.Composers["createcharacter"].GetHorizontalTabs("tabs");
            horizontalTabs.unscaledTabSpacing = 20.0;
            horizontalTabs.unscaledTabPadding = 10.0;
            horizontalTabs.activeElement = this.curTab;
            this.Composers["createcharacter"].Compose();
        }

        private void OnToggleDressOnOff(bool on)
        {
            this.charNaked = !on;
            this.capi.World.Player.Entity.hideClothing = this.charNaked;
            (this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer).TesselateShape();
        }

        private void onToggleSkinPartColor(string partCode, string variantCode) => this.capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>().selectSkinPart(partCode, variantCode);

        private void onToggleSkinPartColor(string partCode, int index)
        {
            EntityBehaviorExtraSkinnable behavior = this.capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            string code = behavior.AvailableSkinPartsByCode[partCode].Variants[index].Code;
            behavior.selectSkinPart(partCode, code);
        }

        private bool OnNext()
        {
            this.curTab = 1;
            this.ComposeGuis();
            return true;
        }

        private void onTabClicked(int tabid)
        {
            this.curTab = tabid;
            this.ComposeGuis();
        }

        public override void OnGuiOpened()
        {
            string classCode = this.capi.World.Player.Entity.WatchedAttributes.GetString("characterClass", (string)null);
            if (classCode != null)
                this.modSys.setCharacterClass(this.capi.World.Player.Entity, classCode);
            else
                this.modSys.setCharacterClass(this.capi.World.Player.Entity, this.modSys.characterClasses[0].Code);
            this.ComposeGuis();
            (this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer).TesselateShape();
            if (this.capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Guest && this.capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Survival || this.characterInv == null)
                return;
            this.characterInv.Open((IPlayer)this.capi.World.Player);
        }

        public override void OnGuiClosed()
        {
            if (this.characterInv != null)
            {
                this.characterInv.Close((IPlayer)this.capi.World.Player);
                this.Composers["createcharacter"].GetSlotGrid("leftSlots")?.OnGuiClosed(this.capi);
                this.Composers["createcharacter"].GetSlotGrid("rightSlots")?.OnGuiClosed(this.capi);
            }
            this.modSys.ClientSelectionDone(this.characterInv, this.modSys.characterClasses[this.currentClassIndex].Code, this.didSelect);
            this.capi.World.Player.Entity.hideClothing = false;
            (this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer).TesselateShape();
        }

        private bool OnConfirm()
        {
            this.didSelect = true;
            this.TryClose();
            return true;
        }

        protected virtual void OnTitleBarClose() => this.TryClose();

        protected void SendInvPacket(object packet) => this.capi.Network.SendPacketClient(packet);

        private bool OnRandomizeSkin()
        {
            EntityBehaviorExtraSkinnable behavior = this.capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            EntitySkinnableShapeRenderer renderer = this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer;
            renderer.doReloadShapeAndSkin = false;
            foreach (SkinnablePart availableSkinPart in behavior.AvailableSkinParts)
            {
                int selectedIndex = this.capi.World.Rand.Next(availableSkinPart.Variants.Length);
                if ((availableSkinPart.Code == "mustache" || availableSkinPart.Code == "beard") && this.capi.World.Rand.NextDouble() < 0.65)
                    selectedIndex = 0;
                string code1 = availableSkinPart.Variants[selectedIndex].Code;
                behavior.selectSkinPart(availableSkinPart.Code, code1);
                string code2 = availableSkinPart.Code;
                if (availableSkinPart.Type == EnumSkinnableType.Texture && !availableSkinPart.UseDropDown)
                    this.Composers["createcharacter"].ColorListPickerSetValue("picker-" + code2, selectedIndex);
                else
                    this.Composers["createcharacter"].GetDropDown("dropdown-" + code2).SetSelectedIndex(selectedIndex);
            }
            renderer.doReloadShapeAndSkin = true;
            renderer.TesselateShape();
            return true;
        }

        private void changeClass(int dir)
        {
            this.currentClassIndex = GameMath.Mod(this.currentClassIndex + dir, this.modSys.characterClasses.Count);
            CharacterClass characterClass = this.modSys.characterClasses[this.currentClassIndex];
            this.Composers["createcharacter"].GetDynamicText("className").SetNewText(Lang.Get("characterclass-" + characterClass.Code));
            StringBuilder stringBuilder1 = new StringBuilder();
            StringBuilder stringBuilder2 = new StringBuilder();
            //stringBuilder1.AppendLine(Lang.Get("characterdesc-" + characterClass.Code));
            stringBuilder1.AppendLine();
            stringBuilder1.AppendLine(Lang.Get("traits-title"));
            foreach (CANTrait trait in (IEnumerable<CANTrait>)((IEnumerable<string>)characterClass.Traits).Select<string, CANTrait>((System.Func<string, CANTrait>)(code => this.modSys.TraitsByCode[code])).OrderBy<CANTrait, int>((System.Func<CANTrait, int>)(trait => (int)trait.Type)))
            {
                stringBuilder2.Clear();
                /*foreach (KeyValuePair<string, double> attribute in trait.Attributes)
                {
                    if (stringBuilder2.Length > 0)
                        stringBuilder2.Append(", ");
                    stringBuilder2.Append(Lang.Get(string.Format((IFormatProvider)GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", (object)attribute.Key, (object)attribute.Value)));
                }*/
                if (stringBuilder2.Length > 0)
                {
                    stringBuilder1.AppendLine(Lang.Get("traitwithattributes", (object)Lang.Get("trait-" + trait.Code), (object)stringBuilder2));
                }
                else
                {
                    string ifExists = Lang.GetIfExists("traitdesc-" + trait.Code);
                    if (ifExists != null)
                    {
                        stringBuilder1.AppendLine(Lang.Get("traitwithattributes", (object)Lang.Get("trait-" + trait.Code), (object)ifExists));
                    }
                    else
                        stringBuilder1.AppendLine(Lang.Get("trait-" + trait.Code));
                    //string aa = stringBuilder1.ToString();
                }
            }
            if (characterClass.Traits.Length == 0)
                stringBuilder1.AppendLine("No positive or negative traits");
            stringBuilder1.Append("\n\n");
            stringBuilder1.AppendLine(Lang.Get("characterdesc-" + characterClass.Code));
            this.Composers["createcharacter"].GetRichtext("characterDesc").SetNewText(stringBuilder1.ToString(), CairoFont.WhiteDetailText());

            this.modSys.setCharacterClass(this.capi.World.Player.Entity, characterClass.Code);
            (this.capi.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer).TesselateShape();
        }

        public void PrepAndOpen()
        {
            this.GatherDresses(EnumCharacterDressType.Foot);
            this.GatherDresses(EnumCharacterDressType.Hand);
            this.GatherDresses(EnumCharacterDressType.Shoulder);
            this.GatherDresses(EnumCharacterDressType.UpperBody);
            this.GatherDresses(EnumCharacterDressType.LowerBody);
            this.TryOpen();
        }

        private void GatherDresses(EnumCharacterDressType type)
        {
            List<ItemStack> itemStackList = new List<ItemStack>();
            itemStackList.Add((ItemStack)null);
            string lowerInvariant = type.ToString().ToLowerInvariant();
            IList<Item> items = this.capi.World.Items;
            for (int index = 0; index < items.Count; ++index)
            {
                Item obj = items[index];
                if (obj != null && !(obj.Code == (AssetLocation)null) && obj.Attributes != null)
                {
                    string str = obj.Attributes["clothescategory"]?.AsString();
                    JsonObject attribute = obj.Attributes["inCharacterCreationDialog"];
                    if ((attribute != null ? (attribute.AsBool() ? 1 : 0) : 0) != 0 && str?.ToLowerInvariant() == lowerInvariant)
                        itemStackList.Add(new ItemStack(obj, 1));
                }
            }
            this.DressesByDressType[type] = itemStackList.ToArray();
            this.DressPositionByTressType[type] = 0;
        }

        public override bool CaptureAllInputs() => this.IsOpened();

        public override string ToggleKeyCombinationCode => (string)null;

        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            base.OnMouseWheel(args);
            if (!this.insetSlotBounds.PointInside(this.capi.Input.MouseX, this.capi.Input.MouseY) || this.curTab != 0)
                return;
            this.charZoom = GameMath.Clamp(this.charZoom + args.deltaPrecise / 5f, 0.5f, 1f);
        }

        public override bool PrefersUngrabbedMouse => true;

        public override void OnMouseDown(MouseEvent args)
        {
            base.OnMouseDown(args);
            this.rotateCharacter = this.insetSlotBounds.PointInside(args.X, args.Y);
        }

        public override void OnMouseUp(MouseEvent args)
        {
            base.OnMouseUp(args);
            this.rotateCharacter = false;
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);
            if (!this.rotateCharacter)
                return;
            this.yaw -= (float)args.DeltaX / 100f;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            this.capi.Render.GlPushMatrix();
            if (this.focused)
                this.capi.Render.GlTranslate(0.0f, 0.0f, 150f);
            this.capi.Render.GlRotate(-14f, 1f, 0.0f, 0.0f);
            this.mat.Identity();
            this.mat.RotateXDeg(-14f);
            Vec4f vec4f = this.mat.TransformVector(this.lighPos);
            double num = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);
            this.capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(vec4f.X, vec4f.Y, vec4f.Z));
            this.capi.Render.PushScissor(this.insetSlotBounds);
            if (this.curTab == 0)
                this.capi.Render.RenderEntityToGui(deltaTime, (Entity)this.capi.World.Player.Entity, this.insetSlotBounds.renderX + num - GuiElement.scaled(195.0) * (double)this.charZoom + GuiElement.scaled(115.0 * (1.0 - (double)this.charZoom)), this.insetSlotBounds.renderY + num + GuiElement.scaled(10.0 * (1.0 - (double)this.charZoom)), GuiElement.scaled(230.0), this.yaw, (float)GuiElement.scaled(330.0 * (double)this.charZoom), -1);
            else
                this.capi.Render.RenderEntityToGui(deltaTime, (Entity)this.capi.World.Player.Entity, this.insetSlotBounds.renderX + num - GuiElement.scaled(95.0), this.insetSlotBounds.renderY + num - GuiElement.scaled(0.0), GuiElement.scaled(230.0), this.yaw, (float)GuiElement.scaled(180.0), -1);
            this.capi.Render.PopScissor();
            this.capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(1f, -1f, 0.0f).Normalize());
            this.capi.Render.GlPopMatrix();
        }

        public override float ZSize => (float)GuiElement.scaled(280.0);
    }
}
