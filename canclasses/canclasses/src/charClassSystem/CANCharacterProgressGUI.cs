﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace canclasses.src.characterClassesSystem
{
    public class CANCharacterProgressGUI: GuiDialog
    {
        public float Width { get; private set; }
        public CANCharacterProgressGUI(ICoreClientAPI capi) : base(capi)
        {
            this.OnOpened += new Action(this.OnOpen);
            //this.OnClosed += new Action(this.OnClose);
            this.Width = 400;
            

        }
        public void buildWindow()
        {
            //int chosenGroupTab = groupOfInterests == null ? 0 : groupOfInterests.activeElement;
            int fixedY1 = 20;
            ElementBounds elementBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bounds1 = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds bounds2 = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0.0, (double)fixedY1).WithFixedHeight(24.0).WithFixedWidth((double)this.Width);
            int fixedY2 = fixedY1 + 28;
            ElementBounds bounds3 = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0.0, (double)fixedY2).WithFixedHeight(24.0).WithFixedWidth((double)this.Width);
            int fixedY3 = fixedY2 + 28;
            ElementBounds bounds4 = ElementBounds.Fixed(0.0, (double)fixedY3, 140.0, 200.0);
            int fixedY4 = fixedY3 + 4;
            ElementBounds bounds5 = ElementBounds.FixedOffseted(EnumDialogArea.LeftBottom, 20.0, -12.0, 100.0, 24.0);
            elementBounds.BothSizing = ElementSizing.FitToChildren;
            elementBounds.WithChild(bounds1);
            bounds1.BothSizing = ElementSizing.FitToChildren;

            bounds1.WithChildren(bounds2, bounds3, bounds4, bounds5);
           
            this.SingleComposer = this.capi.Gui.CreateCompo(
               "mainguiclaims", elementBounds).AddShadedDialogBG(bounds1);
            this.SingleComposer.Compose();
        }
        public override string ToggleKeyCombinationCode => "cancharacterprogress";
        private void OnOpen() => this.buildWindow();
    }
}
