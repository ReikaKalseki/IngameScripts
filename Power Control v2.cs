using Sandbox.ModAPI.Ingame;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRageMath;

using System;
using System.Collections.Generic;
using System.Text;

using IMySolarPanel = SpaceEngineers.Game.ModAPI.Ingame.IMySolarPanel;
using MyResourceSinkComponent = Sandbox.Game.EntityComponents.MyResourceSinkComponent;
using MyObjectBuilder_GasProperties = VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties;

using VRage.Game.GUI.TextPanel;

namespace Ingame_Scripts.PowerControlV2 {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "PowerStatus"; //Any LCD panel with this in its name is overridden to show the power status
		
		const float BATTERY_REACTOR_MIN = 0.05F; //The fraction of battery capacity at which reactors are enabled, regardless of load, to prevent a blackout
		const bool ENABLE_BATTERY_CHARGE_IF_LOW = false; //Whether to allow reactors to charge batteries during the above condition
		readonly string[] SOURCE_PRIORITY = {"Solar", "Wind", "Battery", "Hydrogen", "Reactor"}; //The order to draw from power sources to meet demand
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly MyDefinitionId electricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
		
		private readonly PowerSourceCollection solars;
		private readonly PowerSourceCollection wind;
		private readonly BatteryCollection batteries;
		private readonly PowerSourceCollection reactors;
		private readonly PowerSourceCollection hydrogengines;
		
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			solars = new PowerSourceCollection(this, Me, b => !(b is IMyReactor || b is IMyBatteryBlock) && b.BlockDefinition.TypeId.ToString().Contains("Solar"));
			wind = new PowerSourceCollection(this, Me, b => !(b is IMyReactor || b is IMyBatteryBlock) && b.BlockDefinition.TypeId.ToString().Contains("Wind"));
			batteries = new BatteryCollection(this, Me);
			reactors = new PowerSourceCollection(this, Me, b => b.BlockDefinition.TypeId.ToString().Contains("Reactor"));
			hydrogengines = new PowerSourceCollection(this, Me, b => b.BlockDefinition.TypeId.ToString().Contains("Hydrogen"));
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			foreach (IMyTextPanel scr in displays) {
				scr.WriteText(""); //clear
				scr.ContentType = ContentType.TEXT_AND_IMAGE;
			}
			
			float load = getCurrentPowerDemand(Me.CubeGrid);
			show("Grid power demand is "+load+"MW");
			float remaining = load;
			bool batteryUse = false;
			foreach (string s in SOURCE_PRIORITY) {
				PowerSourceCollection c = getCollection(s);
				float capacity = c.getMaxGeneration();
				if (capacity <= 0) {
					show("Power source type "+s+" is unavailable. Skipping.");
					continue;
				}
				bool enable = remaining > 0;
				if (s == "Battery") {
					if (enable) {
						batteries.setCharging(false, true); 
						batteryUse = true;
					}
				}
				else {
					c.setEnabled(enable);
				}
				remaining -= capacity;
				show("Power source type "+s+": "+(enable ? "In Use, producing up to "+capacity+"MW" : "Offline"));
			}
			if (!batteryUse) {
				batteries.setCharging(true, false); 
				show("Batteries are recharging.");
			}
			else {
				show("Batteries are discharging.");
			}
			if (remaining > 0) {
				show("Power supply exceeded by "+remaining+"MW!");
			}
		}
		
		private PowerSourceCollection getCollection(string id) {
			switch(id) {
				case "Solar":
					return solars;
				case "Wind":
					return wind;
				case "Battery":
					return batteries;
				case "Hydrogen":
					return hydrogengines;
				case "Reactor":
					return reactors;
				default:
					return null;
			}
		}
		
		private float getCurrentPowerDemand(IMyCubeGrid grid) {
            float ret = 0;
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == grid);
            
            foreach (IMyTerminalBlock item in blocks) {
                MyResourceSinkComponent sink;
                if (item.Components.TryGet(out sink) && sink.AcceptedResources.IndexOf(electricityId) != -1) {
                	if (item is IMyBatteryBlock)
                        continue;
                	if (item is IMyFunctionalBlock && !((IMyFunctionalBlock)item).Enabled)
                		continue;
                    float amt = sink.RequiredInputByType(electricityId);
                    if (item is IMyProductionBlock) {
                    	if (!((IMyProductionBlock)item).IsQueueEmpty) {
                    		amt = Math.Max(amt, sink.MaxRequiredInputByType(electricityId));
                    	}
                    }
                    ret += amt;
                }
            }
            return ret;
        }
		
		private void show(string text) {
			foreach (IMyTextPanel scr in displays) {
				scr.WriteText(text+"\n", true);
			}
			Echo(text);
		}
		
		internal class PowerSourceCollection {
			
			protected readonly List<PowerSource> sources = new List<PowerSource>();
			
			internal PowerSourceCollection(MyGridProgram prog, IMyProgrammableBlock cpu, Func<IMyTerminalBlock, bool> collect = null) {
				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				prog.GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == cpu.CubeGrid && (collect == null || collect(b)));
				foreach (IMyTerminalBlock block in blocks) {
					sources.Add(createSource(block));
					prog.Echo("Created source "+sources[sources.Count-1]+" from "+block.CustomName);
				}
			}
			
			public float getMaxGeneration() {
				float max = 0;
				foreach (PowerSource src in sources) {
					max += src.getMaxGeneration();
				}
				return max;
			}
			
			public float getTotalGeneration() {
				float gen = 0;
				foreach (PowerSource src in sources) {
					gen += src.getCurrentGeneration();
				}
				return gen;
			}
			
			public bool isMaxLoad() {
				return getTotalGeneration() >= getMaxGeneration();
			}
			
			public int sourceCount() {
				return sources.Count;
			}
			
			public float averagePerBlockGeneration() {
				return getTotalGeneration()/sourceCount();
			}
			
			public virtual void setEnabled(bool enable) {
				foreach (PowerSource src in sources) {
					src.setEnabled(enable);
				}
			}
			
			protected virtual PowerSource createSource(IMyTerminalBlock block) {
				return new PowerSource(block);
			}
			
		}
		
		internal class BatteryCollection : PowerSourceCollection {
			
			private bool recharge;
			private bool discharge;
			
			internal BatteryCollection(MyGridProgram grid, IMyProgrammableBlock cpu) : base(grid, cpu, b => b is IMyBatteryBlock) {
			
			}
			
			protected override PowerSource createSource(IMyTerminalBlock block) {
				return new BatterySource(block as IMyBatteryBlock);
			}
			
			public override void setEnabled(bool enable) {
				base.setEnabled(enable); //no-op?
			}
			
			public void setCharging(bool recharge, bool discharge) {
				foreach (BatterySource src in sources) {
					src.setCharging(recharge, discharge);
				}
				this.recharge = recharge;
				this.discharge = discharge;
			}
			
			public float getStoredEnergy() {
				float e = 0;
				foreach (BatterySource src in sources) {
					e += src.getStoredEnergy();
				}
				return e/sourceCount();
			}
			
			public bool areRecharging() {
				return recharge;
			}
			
			public bool areDischarging() {
				return discharge;
			}
			
		}
		
		internal class PowerSource {
			
			protected readonly IMyTerminalBlock block;
			
			internal PowerSource(IMyTerminalBlock b) {
				block = b;
			}
			
			public float getMaxGeneration() {
				if (!isEnabled()) {
					return 0;
				}
				if (block is IMyReactor) {
					return (block as IMyReactor).MaxOutput;
				}
				else if (block is IMyPowerProducer) {
					return (block as IMyPowerProducer).MaxOutput;
				}
				else if (block is IMyBatteryBlock) {
					return (block as IMyBatteryBlock).MaxOutput;
				}
				else {
					return 0;
				}
			}
			
			public float getCurrentGeneration() {
				if (!isEnabled()) {
					return 0;
				}
				if (block is IMyReactor) {
					return (block as IMyReactor).CurrentOutput;
				}
				else if (block is IMyPowerProducer) {
					return (block as IMyPowerProducer).CurrentOutput;
				}
				else if (block is IMyBatteryBlock) {
					return (block as IMyBatteryBlock).CurrentOutput;
				}
				else {
					return 0;
				}
			}
			
			public void setEnabled(bool enable) {
				if (block is IMyReactor) {
					(block as IMyReactor).Enabled = enable;
				}
				else if (block is IMySolarPanel) {
					block.ApplyAction(enable ? "OnOff_On" : "OnOff_Off");
				}
				else if (block is IMyBatteryBlock) {
					(block as IMyBatteryBlock).Enabled = enable;
				}
			}
			
			public bool isEnabled() {
				if (block is IMyReactor) {
					return (block as IMyReactor).Enabled;
				}
				else if (block is IMySolarPanel) {
					return true;//Sandbox.ModAPI.Interfaces.TerminalPropertyExtensions.GetValueBool(block, "Enabled");
				}
				else if (block is IMyBatteryBlock) {
					return (block as IMyBatteryBlock).Enabled;
				}
				return true;
			}
			
			public bool isMaxLoad() {
				return getCurrentGeneration() >= getMaxGeneration();
			}
			
		}
		
		internal class BatterySource : PowerSource {
			
			internal BatterySource(IMyBatteryBlock b) : base(b) {

			}
			
			public float getStoredEnergy() {
				return (block as IMyBatteryBlock).CurrentStoredPower/(block as IMyBatteryBlock).MaxStoredPower;
			}
			
			public void setCharging(bool recharge, bool discharge) {
				if (recharge == discharge) {
					(block as IMyBatteryBlock).ChargeMode = ChargeMode.Auto;
				}
				else if (recharge) {
					(block as IMyBatteryBlock).ChargeMode = ChargeMode.Recharge;
				}
				else if (discharge) {
					(block as IMyBatteryBlock).ChargeMode = ChargeMode.Discharge;
				}
			}
			
		}
		
		//====================================================
	}
}