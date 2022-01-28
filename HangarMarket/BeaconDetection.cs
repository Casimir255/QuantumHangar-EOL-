using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.Definitions;
using VRage.Game;
using Sandbox.Game.EntityComponents;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;

namespace BeaconDetection
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "DetectionSmallBlockBeacon", "DetectionLargeBlockBeacon")]
    public class BeaconDetect : MyGameLogicComponent
    {
        
        IMyTerminalBlock Beacon;
        private List<IMyPowerProducer> PowerProducers = new List<IMyPowerProducer>();
        private List<IMyEntity> ThermalProducers = new List<IMyEntity>();
        private MyObjectBuilder_EntityBase m_objectBuilder;
        private MyDefinitionId electricity = MyResourceDistributorComponent.ElectricityId;
        private IMyCubeGrid CubeGrid = null;
        private bool isLargeGrid;
        private bool unloadHandlers = false;
        private DateTime lastRun;
        private float lastOutput = 0;

        //Create public variables
        public static IMyTerminalBlock m_block = null;
        IMyBeacon m_beacon = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {


            m_objectBuilder = objectBuilder;


            //Bob fixes
            m_block = Entity as IMyTerminalBlock;
            m_beacon = m_block as IMyBeacon;

            //Initilize enabledChangedEvent
            m_beacon.EnabledChanged += M_beacon_EnabledChanged;
            
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

    
        private void M_beacon_EnabledChanged(IMyTerminalBlock obj)
        {

            m_beacon = obj as IMyBeacon;

            //Prevent calling myself
            if (m_beacon.Enabled == true)
                return;


            m_beacon.Enabled = true;
            //throw new NotImplementedException();
            //m_beacon.EnabledChanged -= M_beacon_EnabledChanged;
        }
        
         public override void Close()
        {
            m_beacon.EnabledChanged -= M_beacon_EnabledChanged;
        }

        
        private void CubeGrid_UpdatePowerGrid(IMySlimBlock obj)
        {

            CubeGrid = obj.CubeGrid;
            var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(CubeGrid);
            gts.GetBlocksOfType(PowerProducers, block =>
            {
                if (block.IsSameConstructAs(Beacon))
                {
                    return true;
                }else
                {
                    return false;
                }
            });

            gts.GetBlocksOfType(ThermalProducers, block =>
            {
                if (block is IMyThrust)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });


            if (PowerProducers.Count != 0 || ThermalProducers.Count != 0)
            {
                CubeGrid.OnBlockAdded += CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockRemoved += CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockIntegrityChanged += CubeGrid_RefreshThermalGenerators;
                unloadHandlers = true;
            }
        }

        private void CubeGrid_RefreshThermalGenerators(IMySlimBlock obj)
        {
            if (obj is IMyBeacon)
            {
                Beacon_CheckThermal((IMyEntity)obj);
            }
        }

        public override void UpdateAfterSimulation100()
        {
            if (lastRun == DateTime.MinValue || DateTime.Now > lastRun.AddSeconds(5))
            {
                lastRun = DateTime.Now;
                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
                if (Beacon == null)
                {
                    Beacon = Entity as IMyTerminalBlock;
                    //grid maintenance. 
                    CubeGrid = Beacon.CubeGrid;
                    isLargeGrid = CubeGrid.GridSizeEnum == MyCubeSize.Large;
                }

                if (PowerProducers.Count == 0 || ThermalProducers.Count == 0)
                {
                    CubeGrid_UpdatePowerGrid(Beacon.SlimBlock);
                }

                if (Beacon != null)
                {
                    try
                    {

                        if (Beacon != null && Beacon.IsWorking)
                        {
                            Beacon_CheckThermal(Beacon);
                        }
                    }
                    catch (Exception exc)
                    {
                    }

                }
            }

        }


        private void Beacon_CheckThermal(VRage.ModAPI.IMyEntity obj)
        {
            var subtype = Beacon.Name;
            if (obj is IMyBeacon)
            {
                var output = calculateRadius(GetThermalOutput(Beacon));
                var beacon = obj as IMyBeacon;
                if (lastOutput > output)
                {
                    output = lastOutput * 0.90f;
                }
                lastOutput = output;
                beacon.Radius = output;
                beacon.Enabled = true;
                beacon.HudText = "Thermal Signature";
            }
        }

        private float calculateRadius (float EnergyinMW)
        {
            float radius = 0.0f;
            if (isLargeGrid)
            {
                radius = EnergyinMW / 300 * 25000;
            }else
            {
                radius = EnergyinMW / 30 * 15000;
            }
            return radius;
        }
  
        public override void OnRemovedFromScene()
        {
         
            base.OnRemovedFromScene();
            if (unloadHandlers)
            {
                CubeGrid.OnBlockAdded -= CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockRemoved -= CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockIntegrityChanged -= CubeGrid_RefreshThermalGenerators;
            }
        }
        private float GetThermalOutput(IMyTerminalBlock block)
        {
            var thermalOutput = 0.0f;
            if (block.CubeGrid.IsStatic)
            {
                return 0.0f;
            }            
            
            if (PowerProducers.Count != 0)
            {
                foreach (var powerProducer in PowerProducers)
                {
                    if (powerProducer is IMyReactor)
                    {
                        thermalOutput += powerProducer.CurrentOutput;
                    }
                    else if (powerProducer is IMyBatteryBlock)
                    {
                        thermalOutput += powerProducer.CurrentOutput * 0.25f;
                    }
                    else if (powerProducer is IMySolarPanel)
                    {
                        thermalOutput += 0;
                    }
                    else if (powerProducer.BlockDefinition.SubtypeId.ToLower().Contains("engine")) //because thank you, Keen.
                    {
                        thermalOutput += powerProducer.CurrentOutput * 0.5f;
                    }
                    else  //Wind?
                    {
                        thermalOutput += 0;
                    }
                }
            }

            if (ThermalProducers.Count != 0)
            {
                foreach (var thermalProducer in ThermalProducers)
                {

                    //thruster logic
                    if (thermalProducer is IMyThrust)
                    {
                        var thrust = thermalProducer as IMyThrust;
                        if (thrust.BlockDefinition.SubtypeId.ToLower().Contains("hydrogen"))
                        {
                            thermalOutput += (thrust.CurrentThrust / 6000000) * 67; //large grid, large thruster
                        }
                    }
                }
            }
            return thermalOutput ;
        }

    }
}
