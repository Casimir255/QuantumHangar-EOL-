using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using static HangarStoreMod.ModCore;

namespace HangarStoreMod
{
    public class TerminalControls
    {
        //Create terminal controls for the block

        public static bool ControlsCreated;


        public static void CreateControls(IMyTerminalBlock blockCheck, List<IMyTerminalControl> controls)
        {
            if (!(blockCheck is IMyProjector) || ControlsCreated) return;


            ControlsCreated = true;

            var controlList = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<IMyProjector>(out controlList);
            controlList[9].Visible = HideControls;
            controlList[10].Visible = HideControls;
            controlList[11].Visible = HideControls;
            controlList[8].Visible = HideControls;

            controlList[29].Visible = HideControls;
            controlList[30].Visible = HideControls;
            controlList[31].Visible = HideControls;

            //controlList[28].Visible = Block => HideControls(Block);

            controlList[27].Visible = HideControls;
            controlList[26].Visible = HideControls;


            var saleList =
                MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyProjector>("HangerStoreNext");
            saleList.Title = MyStringId.GetOrCompute("Grids for sale: ");
            saleList.SupportsMultipleBlocks = false;
            saleList.VisibleRowsCount = 12;
            saleList.Multiselect = false;


            saleList.ListContent = GetForSaleGrids;
            saleList.ItemSelected = SetSelectedGrid;
            saleList.Enabled = block => true;
            saleList.Visible = ControlVisibility;
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(saleList);
            controls.Add(saleList);


            var filterList =
                MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyProjector>("SortType");
            filterList.Title = MyStringId.GetOrCompute("Sort by:");
            filterList.SupportsMultipleBlocks = false;
            filterList.Enabled = block => true;
            filterList.Visible = ControlVisibility;
            filterList.Setter = FilterSet;
            filterList.Getter = FilterGet;

            filterList.ComboBoxContent = list =>
            {
                list.Add(new MyTerminalControlComboBoxItem
                    { Key = (long)SortType.GridName, Value = MyStringId.GetOrCompute(SortType.GridName.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem
                    { Key = (long)SortType.Price, Value = MyStringId.GetOrCompute(SortType.Price.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem
                    { Key = (long)SortType.OwnerName, Value = MyStringId.GetOrCompute(SortType.OwnerName.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem
                    { Key = (long)SortType.Pcu, Value = MyStringId.GetOrCompute(SortType.Pcu.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem
                {
                    Key = (long)SortType.BlockCount, Value = MyStringId.GetOrCompute(SortType.BlockCount.ToString())
                });
                list.Add(new MyTerminalControlComboBoxItem
                {
                    Key = (long)SortType.ServerOffers, Value = MyStringId.GetOrCompute(SortType.ServerOffers.ToString())
                });
            };
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(filterList);
            controls.Add(filterList);


            var previewButton =
                MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("PreviewButton");
            previewButton.Title = MyStringId.GetOrCompute("Preview Selected Grid");
            previewButton.Tooltip = MyStringId.GetOrCompute("Previews the selected Grid");
            previewButton.SupportsMultipleBlocks = false;
            previewButton.Action = PreviewSelectedGrid;
            previewButton.Enabled = PreviewButtonEnabled;
            previewButton.Visible = ControlVisibility;

            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(previewButton);
            controls.Add(previewButton);


            var detailsButton =
                MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("Ship Details");
            detailsButton.Title = MyStringId.GetOrCompute("Selected Grid Details");
            detailsButton.Tooltip = MyStringId.GetOrCompute("Provides in-depth ship details");
            detailsButton.SupportsMultipleBlocks = false;
            detailsButton.Action = ShipDetails;
            detailsButton.Enabled = PreviewButtonEnabled;
            detailsButton.Visible = ControlVisibility;

            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(detailsButton);
            controls.Add(detailsButton);


            var purchaseButton =
                MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("PurchaseButton");
            purchaseButton.Title = MyStringId.GetOrCompute("Purchase Selected Grid");
            purchaseButton.Tooltip = MyStringId.GetOrCompute("Purchases the selected grid");
            purchaseButton.SupportsMultipleBlocks = false;
            purchaseButton.Action = PurchaseSlectedGrid;
            purchaseButton.Enabled = PurchaseButtonVisibility;
            purchaseButton.Visible = ControlVisibility;
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(purchaseButton);
            controls.Add(purchaseButton);
        }


        public static bool HideControls(IMyTerminalBlock block)
        {
            if (!(block is IMyProjector projector)) return true;
            return !projector.BlockDefinition.SubtypeName.Contains("HangarStoreBlock");
        }


        public static bool ControlVisibility(IMyTerminalBlock block)
        {
            if (!(block is IMyProjector projector)) return false;
            return projector.BlockDefinition.SubtypeName.Contains("HangarStoreBlock");
        }


        public static void ListControl(List<MyTerminalControlListBoxItem> a,
            List<MyTerminalControlListBoxItem> b)
        {
        }
    }
}