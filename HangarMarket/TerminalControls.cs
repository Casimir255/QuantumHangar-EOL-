using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.ModAPI;
using VRage.Utils;
using static HangarStoreMod.ModCore;

namespace HangarStoreMod
{
    public class TerminalControls
    {

        //Create terminal controls for the block

        public static bool controlsCreated = false;



        public static void CreateControls(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block as IMyProjector == null || controlsCreated == true)
            {
                return;
            }

          

            controlsCreated = true;

            var controlList = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<IMyProjector>(out controlList);
            controlList[9].Visible = Block => HideControls(Block);
            controlList[10].Visible = Block => HideControls(Block);
            controlList[11].Visible = Block => HideControls(Block);
            controlList[8].Visible = Block => HideControls(Block);

            controlList[29].Visible = Block => HideControls(Block);
            controlList[30].Visible = Block => HideControls(Block);
            controlList[31].Visible = Block => HideControls(Block);

            //controlList[28].Visible = Block => HideControls(Block);

            controlList[27].Visible = Block => HideControls(Block);
            controlList[26].Visible = Block => HideControls(Block);



            var SaleList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyProjector>("HangerStoreNext");
            SaleList.Title = MyStringId.GetOrCompute("Grids for sale: ");
            SaleList.SupportsMultipleBlocks = false;
            SaleList.VisibleRowsCount = 12;
            SaleList.Multiselect = false;

            
            SaleList.ListContent = ModCore.GetForSaleGrids;
            SaleList.ItemSelected = ModCore.SetSelectedGrid;
            SaleList.Enabled = Block => true;
            SaleList.Visible = Block => ControlVisibility(Block);
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(SaleList);
            controls.Add(SaleList);



            var FilterList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyProjector>("SortType");
            FilterList.Title = MyStringId.GetOrCompute("Sort by:");
            FilterList.SupportsMultipleBlocks = false;
            FilterList.Enabled = Block => true;
            FilterList.Visible = Block => ControlVisibility(Block);
            FilterList.Setter = ModCore.FilterSet;
            FilterList.Getter = ModCore.FilterGet;

            FilterList.ComboBoxContent = (list) =>
            {
                list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SortType.GridName, Value = MyStringId.GetOrCompute(SortType.GridName.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SortType.Price, Value = MyStringId.GetOrCompute(SortType.Price.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SortType.OwnerName, Value = MyStringId.GetOrCompute(SortType.OwnerName.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SortType.PCU, Value = MyStringId.GetOrCompute(SortType.PCU.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SortType.BlockCount, Value = MyStringId.GetOrCompute(SortType.BlockCount.ToString()) });
                list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SortType.ServerOffers, Value = MyStringId.GetOrCompute(SortType.ServerOffers.ToString()) });
            };
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(FilterList);
            controls.Add(FilterList);



            var PreviewButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("PreviewButton");
            PreviewButton.Title = MyStringId.GetOrCompute("Preview Selected Grid");
            PreviewButton.Tooltip = MyStringId.GetOrCompute("Previews the selected Grid");
            PreviewButton.SupportsMultipleBlocks = false;
            PreviewButton.Action = Block => ModCore.PreviewSelectedGrid(Block);
            PreviewButton.Enabled = Block => ModCore.PreviewButtonEnabled(block);
            PreviewButton.Visible = Block => ControlVisibility(Block);
            
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(PreviewButton);
            controls.Add(PreviewButton);



            var DetailsButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("Ship Details");
            DetailsButton.Title = MyStringId.GetOrCompute("Selected Grid Details");
            DetailsButton.Tooltip = MyStringId.GetOrCompute("Provides indepth ship details");
            DetailsButton.SupportsMultipleBlocks = false;
            DetailsButton.Action = Block => ModCore.ShipDetails(block);
            DetailsButton.Enabled = Block => ModCore.PreviewButtonEnabled(block);
            DetailsButton.Visible = Block => ControlVisibility(Block);

            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(DetailsButton);
            controls.Add(DetailsButton);



            var PurchaseButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("PurchaseButton");
            PurchaseButton.Title = MyStringId.GetOrCompute("Purchase Selected Grid");
            PurchaseButton.Tooltip = MyStringId.GetOrCompute("Purchases the selected grid");
            PurchaseButton.SupportsMultipleBlocks = false;
            PurchaseButton.Action = Block => ModCore.PurchaseSlectedGrid(Block);
            PurchaseButton.Enabled = Block => ModCore.PurchaseButtonVisibility(Block);
            PurchaseButton.Visible = Block => ControlVisibility(Block);
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(PurchaseButton);
            controls.Add(PurchaseButton);


        }


        public static bool HideControls(IMyTerminalBlock block)
        {
            if (block as IMyProjector != null)
            {
                var radar = block as IMyProjector;
                if (radar.BlockDefinition.SubtypeName.Contains("HangarStoreBlock"))
                {
                    return false;
                }
            }

            return true;
        }


        public static bool ControlVisibility(IMyTerminalBlock block)
        {

            if (block as IMyProjector != null)
            {

                var Hangar = block as IMyProjector;
                if (Hangar.BlockDefinition.SubtypeName.Contains("HangarStoreBlock"))
                {
                    return true;
                }

            }

            return false;
        }

        


        public static void ListControl(List<VRage.ModAPI.MyTerminalControlListBoxItem> A, List<VRage.ModAPI.MyTerminalControlListBoxItem> B)
        {

        }
    }
}
