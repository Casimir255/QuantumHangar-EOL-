
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;


namespace HangarStoreMod
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Projector), false, "HangarStoreBlock")]
    public class ModCore : MyGameLogicComponent
    {
        public enum SortType
        {
            GridName,
            Price,
            OwnerName,
            PCU,
            BlockCount,
            ServerOffers
        }



        //Static Logic Components
        public static List<IMyProjector> AllBlocks = new List<IMyProjector>();
        public static List<MarketListing> MarketOffers = new List<MarketListing>();
        public static List<MyTerminalControlListBoxItem> List = new List<MyTerminalControlListBoxItem>();
        public static int SelectedBoxItem = -1;
        public static MarketListing SelectedOffer;



        //BlockComponents
        private bool BlockInitilized = false;
        private static SortType Filter = SortType.GridName;
        private IMyProjector ProjectorBlock = null;

        



        private void InitilizeMarket()
        {
            BlockInitilized = true;
            MyAPIGateway.TerminalControls.CustomControlGetter += CreateControlsNew;
            ProjectorBlock.RefreshCustomInfo();

            AllBlocks.Add(ProjectorBlock);
            Utils.Log("Market Initilized");
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {

            var m_block = Entity as IMyTerminalBlock;
            ProjectorBlock = m_block as IMyProjector;

            ProjectorBlock.AppendingCustomInfo += ProjectorBlock_AppendingCustomInfo;



            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME;
           
        }

        public override void Close()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CreateControlsNew;
            ProjectorBlock.AppendingCustomInfo -= ProjectorBlock_AppendingCustomInfo;
            AllBlocks.Remove(ProjectorBlock);
        }



        public override void UpdateBeforeSimulation100()
        {

            if (MyAPIGateway.Utilities.IsDedicated) //only client pls
                return;


            if (MyAPIGateway.Session == null)
                return;


            if (!BlockInitilized)
            {
                InitilizeMarket();
            }
        }


        /* Transparency Update */
        public override void UpdateAfterSimulation10()
        {

            if (MyAPIGateway.Utilities.IsDedicated) //only client pls
                return;


            if (MyAPIGateway.Session == null)
                return;


            var projectionEnt = ProjectorBlock.ProjectedGrid as IMyEntity;
            //var projectionGrid = Session.Instance.consoleBlocks[index].ProjectedGrid as IMyCubeGrid;
            if (projectionEnt == null) return;
            var transparency = .25f;



            if (projectionEnt.Render.Transparency != transparency)
            {
                //MyLog.Default.WriteLineAndConsole("Updating Projection transparency!");
                Utils.SetTransparency(projectionEnt, transparency);
            }
        }


        private void CreateControlsNew(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            //on creation of new terminal controls
            
            if (block as IMyProjector != null)
            {
                TerminalControls.CreateControls(block, controls);
            }
        }


        /* Get and set of filter types */
        internal static long FilterGet(IMyTerminalBlock arg)
        {
            return (long)Filter;
        }

        internal static void FilterSet(IMyTerminalBlock arg1, long arg2)
        {
            Filter = (SortType)arg2;
        }

        internal static void PreviewSelectedGrid(IMyTerminalBlock block)
        {
            if (SelectedOffer != null) {

                GridDefinition Def = new GridDefinition();

                Def.GridName = SelectedOffer.Name;
                Def.OwnerSteamID = SelectedOffer.SteamID;
                Def.ProjectorEntityID = block.EntityId;

                MarketSessionComponent.SendGridPreviewRequest(Def);
            }
        }

        internal static bool PreviewButtonEnabled(IMyTerminalBlock block)
        {
            if(SelectedOffer != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static void ShipDetails(IMyTerminalBlock block)
        {
            //throw new NotImplementedException();
        }


        internal static void PurchaseSlectedGrid(IMyTerminalBlock block)
        {
            if (SelectedOffer == null)
                return;

            BuyGridRequest request = new BuyGridRequest();
            request.BuyerSteamID = MyAPIGateway.Session.LocalHumanPlayer.SteamUserId;
            request.OwnerSteamID = SelectedOffer.SteamID;
            request.GridName = SelectedOffer.Name;

            MarketSessionComponent.PurchaseGrid(request);


        }

        internal static bool PurchaseButtonVisibility(IMyTerminalBlock block)
        {

            if (SelectedOffer == null)
                return false;

      
            if (SelectedOffer.SteamID == MyAPIGateway.Session.LocalHumanPlayer.SteamUserId)
                return false;


            long Balance; 
            MyAPIGateway.Session.LocalHumanPlayer.TryGetBalanceInfo(out Balance);
            if (Balance < SelectedOffer.Price)
                return false;


            return true;
        }

        internal static void GetForSaleGrids(IMyTerminalBlock arg1, List<MyTerminalControlListBoxItem> listItems, List<MyTerminalControlListBoxItem> selectedItems)
        {
            //Utils.Log("GetForSaleGrids");
            listItems.Clear();

            foreach(var item in List)
            {
                listItems.Add(item);
            }

            if(SelectedBoxItem >= 0)
            {
                //Utils.Log("Updating selected item");
                selectedItems.Clear();
                selectedItems.Add(listItems[SelectedBoxItem]);
            }
        }

        internal static void SetSelectedGrid(IMyTerminalBlock arg1, List<MyTerminalControlListBoxItem> newSelectedItems)
        {
            //We defined in terminal controls that only one item can be selected;
            SelectedBoxItem = -1;
            for(int i = 0; i < List.Count; i++)
            {
                MarketListing Offer = (MarketListing)List[i].UserData;
                if(newSelectedItems[0].UserData as MarketListing == Offer)
                {
                    SelectedBoxItem = i;
                    SelectedOffer = Offer;
                    break; 
                }
            }


            //Utils.Log("Set selected item");
            arg1.RefreshCustomInfo();
            UpdateGUI(arg1);
        }

        public static void UpdateAllBlocks()
        {
            foreach(var Block in AllBlocks)
            {
                Block.RefreshCustomInfo();
                UpdateGUI(Block);
            }
        }
        public static void MergeNewCollection(List<MarketListing> NewOffers)
        {


            //No need to do any merge checks if the new list is null
            if (NewOffers == null || NewOffers.Count == 0)
            {
                //Remove any selected items
                SelectedBoxItem = -1;
                SelectedOffer = null;


                MarketOffers.Clear();
                List.Clear();
                UpdateAllBlocks();
                return;
            }


            //First, lets see if we need to remove any items
            for (int i = MarketOffers.Count - 1; i >= 0; i--)
            {
                if (!NewOffers.Contains(MarketOffers[i]))
                {
                    if (SelectedOffer == MarketOffers[i])
                    {
                        //Remove any selected items
                        SelectedBoxItem = -1;
                        SelectedOffer = null;
                    }

                    MarketOffers.RemoveAt(i);
                }

            }


            //Now see if we need to add any items
            for(int i = NewOffers.Count - 1; i >= 0; i--)
            {
                if (!MarketOffers.Contains(NewOffers[i]))
                {
                    MarketOffers.Add(NewOffers[i]);
                }
            }


            //Clear the listbox view
            List.Clear();
            for (int i = 0; i < MarketOffers.Count; i++)
            {
                MyStringId Name = MyStringId.GetOrCompute(MarketOffers[i].Name);
                MyStringId ToolTip = MyStringId.GetOrCompute("Price: " + MarketOffers[i].Price);

                MyTerminalControlListBoxItem NewItem = new MyTerminalControlListBoxItem(Name, ToolTip, MarketOffers[i]);
                List.Add(NewItem);

                if (MarketOffers[i] == SelectedOffer)
                {
                    SelectedBoxItem = i;
                }
            }

            UpdateAllBlocks();
        }
        public static void UpdateGUI(IMyTerminalBlock block)
        {

            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                var myCubeBlock = block as MyCubeBlock;

                if (myCubeBlock.IDModule != null)
                {

                    var share = myCubeBlock.IDModule.ShareMode;
                    var owner = myCubeBlock.IDModule.Owner;
                    myCubeBlock.ChangeOwner(owner, share == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
                    myCubeBlock.ChangeOwner(owner, share);
                }
            }
        }
        private void ProjectorBlock_AppendingCustomInfo(IMyTerminalBlock arg1, StringBuilder stringBuilder)
        {

            if(SelectedOffer == null)
            {
                stringBuilder.AppendLine("_______•°•(Hangar Market)•°•_______");
                return;
            }



            if(SelectedOffer.SteamID != 0)
            {
                long ID = MyAPIGateway.Players.TryGetIdentityId(SelectedOffer.SteamID);

                if(ID != 0)
                {
                    List<IMyIdentity> Identities = new List<IMyIdentity>();
                    MyAPIGateway.Players.GetAllIdentites(Identities);

                    IMyIdentity Seller = Identities.FirstOrDefault(x => x.IdentityId == ID);

                    SelectedOffer.Seller = Seller.DisplayName;

                    var Fac = MyAPIGateway.Session.Factions.GetObjectBuilder().Factions.FirstOrDefault(x => x.Members.Any(c => c.PlayerId == Seller.IdentityId));


                    if (Fac != null)
                    {
                        SelectedOffer.SellerFaction = Fac.Tag;
                    }
                }
                else
                {
                    SelectedOffer.Seller = "Unknown";
                }

            }

          


            stringBuilder.AppendLine("______•°•(Hangar Market)•°•______");
            stringBuilder.AppendLine("Grid Name: " + SelectedOffer.Name);
            stringBuilder.AppendLine("Price: " + SelectedOffer.Price + " [sc]");
            stringBuilder.AppendLine("Market Value: " + SelectedOffer.MarketValue + " [sc]");
            stringBuilder.AppendLine("Seller: " + SelectedOffer.Seller);
            stringBuilder.AppendLine("Seller Faction: " + SelectedOffer.SellerFaction);
            

            stringBuilder.AppendLine("Seller Description: " + SelectedOffer.Description);
            stringBuilder.AppendLine();


            stringBuilder.AppendLine("_______•°•[ Ship Properties ]•°•_______");
            stringBuilder.AppendLine("GridMass: " + SelectedOffer.GridMass + "kg");
            stringBuilder.AppendLine("Num of Small Grids: " + SelectedOffer.SmallGrids);
            stringBuilder.AppendLine("Num of Large Grids: " + SelectedOffer.LargeGrids);
            stringBuilder.AppendLine("Max Power Output: " + SelectedOffer.MaxPowerOutput);
            stringBuilder.AppendLine("Build Percentage: " + Math.Round(SelectedOffer.GridBuiltPercent * 100, 2) + "%");
            stringBuilder.AppendLine("Max Jump Distance: " + SelectedOffer.JumpDistance);
            stringBuilder.AppendLine("Ship PCU: " + SelectedOffer.PCU);

        }
    }



    public static class Utils
    {
        public static void Log(string Log)
        {

            //MyAPIGateway.Utilities.ShowMessage("QuantumHangarMOD", Log);
            MyLog.Default.WriteLineAndConsole(Log);
            //MyLog.Default.WriteLineAndConsole("QuantumHangarMOD: " + Log);
        }



        public static void SetTransparency(IMyEntity myEntity, float transparency)
        {
            var ren = myEntity.Render;
            if (ren == null) return;
            ren.Transparency = transparency;
            ren.CastShadows = false;
            ren.UpdateTransparency();

            IMyCubeGrid cubeGrid = (IMyCubeGrid)myEntity;
            if (cubeGrid == null) return;

            List<IMySlimBlock> slimList = new List<IMySlimBlock>();
            cubeGrid.GetBlocks(slimList);

            foreach (var block in slimList)
            {
                if (block == null) continue;
                block.Dithering = transparency;
                block.UpdateVisual();

                IMyCubeBlock fatBlock = block.FatBlock;
                if (fatBlock != null)
                {
                    fatBlock.Render.CastShadows = false;
                    SetTransparencyForSubparts(fatBlock, transparency);
                }
            }

            ren.UpdateTransparency();
        }

        private static void SetTransparencyForSubparts(IMyCubeBlock renderEntity, float transparency)
        {
            if (renderEntity == null) return;
            var cubeBlock = renderEntity as MyEntity;
            cubeBlock.Render.CastShadows = false;
            if (cubeBlock.Subparts == null)
            {
                return;
            }
            foreach (KeyValuePair<string, MyEntitySubpart> current in cubeBlock.Subparts)
            {
                current.Value.Render.Transparency = 0f;
                current.Value.Render.CastShadows = false;
                //current.Value.Render.RemoveRenderObjects();
                //current.Value.Render.AddRenderObjects();
                current.Value.Render.UpdateTransparency();


                var subpartBlock = current.Value as MyEntity;
                if (subpartBlock == null) continue;

                if (subpartBlock.Subparts != null)
                {
                    // SetTransparencyForSubparts(current.Value as IMyCubeBlock, transparency);
                    foreach (KeyValuePair<string, MyEntitySubpart> current2 in subpartBlock.Subparts)
                    {
                        current2.Value.Render.Transparency = 0f;
                        current2.Value.Render.CastShadows = false;
                        current2.Value.Render.UpdateTransparency();

                    }
                }

            }
        }


    }






}
