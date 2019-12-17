using Sandbox.ModAPI.Ingame;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

using System;
using System.Collections.Generic;
using System.Text;

using IMySolarPanel = SpaceEngineers.Game.ModAPI.Ingame.IMySolarPanel;

namespace Ingame_Scripts.PowerControl {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "[PowerStatus]"; //Any LCD panel with this in its name is overridden to show the power status
		
		//const float SOLAR_MIN = 0.2F; //The fraction of power solar should be at to be considered "day"
		const float BATTERY_MIN = 0.125F; //The fraction of battery capacity at which reactors are enabled, regardless of load, to prevent a blackout
		const bool ALLOW_DISCHARGE_IN_DAY = true; //Should batteries be allowed to discharge during the day, if the solar power is insufficient to run the whole grid?
		const bool ALLOW_REACTORS_IN_DAY = true; //Should reactors be allowed to be online during the day, assuming that solar + battery output is insufficient?
		const bool ALLOW_REACTORS_TO_CHARGE_BATTERIES = false; //Should reactors be allowed to be online (at any time) to charge batteries? Note that doing so incurs a 20% loss of energy.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly PowerSourceCollection<IMySolarPanel> solars;
		private readonly BatteryCollection batteries;
		private readonly PowerSourceCollection<IMyReactor> reactors;
		private readonly PowerSourceCollection<IMyReactor> hydrogengines;
		
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			solars = new PowerSourceCollection<IMySolarPanel>(GridTerminalSystem, Me);
			batteries = new BatteryCollection(GridTerminalSystem, Me);
			reactors = new PowerSourceCollection<IMyReactor>(GridTerminalSystem, Me, b => b.CustomName.Contains("Reactor"));
			hydrogengines = new PowerSourceCollection<IMyReactor>(GridTerminalSystem, Me, b => !b.CustomName.Contains("Reactor"));
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			bool day = solars.getMaxGeneration() > 0;
			foreach (IMyTextPanel scr in displays) {
				scr.WritePublicText(""); //clear
			}
			
			if (!day || solars.isMaxLoad()) {
				if (batteries.areDischarging()) {
					string solarstatus = "Solar power "+(day ? "Exceeded" : "Unavailable")+". ";
					float stored = batteries.getStoredEnergy();
					if (stored <= BATTERY_MIN || batteries.isMaxLoad()) {
						show(solarstatus+"Batteries "+(stored <= 0 ? "Empty" : stored <= 0 ? "Critical" : "Exceeded")+". Reactors Enabled.");
						reactors.setEnabled(true);
					}
					else {
						show(solarstatus+"Batteries Discharging; Reactors Disabled.");
						reactors.setEnabled(false);
						hydrogengines.setEnabled(false);
					}
					showBatteryStatus(stored);
				}
				else {
					reactors.setEnabled(false);
					hydrogengines.setEnabled(false);
					if (!day || ALLOW_DISCHARGE_IN_DAY) {
						batteries.setCharging(false, true);
						return; //allow batteries to pick up the load and re-evaluate next cycle
					}
					else {
						batteries.setCharging(false, false);
					}
				}
			}
			else { //excess solar power; recharge batteries and disable reactors
				show("Excess solar power available. Batteries Recharging; Reactors Disabled.");
				batteries.setCharging(false, false); //not true-false
				reactors.setEnabled(false);
				hydrogengines.setEnabled(false);
			}
		}
		
		private void show(string text) {
			foreach (IMyTextPanel scr in displays) {
				scr.WritePublicText(text+"\n", true);
			}
			Echo(text);
		}
		
		private void showBatteryStatus(float stored) {
			Echo("Battery charge @ "+stored*100+" %");
			foreach (IMyTextPanel scr in displays) {
				displayPercentOnScreen(scr, (int)Math.Round(stored*100));
			}
		}
		
		private void displayPercentOnScreen(IMyTextPanel scr, int per) {  
		    int x = 100;  
		    int y = 0;
			scr.WritePublicText("Battery Charge:\n");
		    scr.WritePublicText("[", true);  
		    for (int i = per; i > 0; i -= 2) {  
		        scr.WritePublicText("|", true);  
		        y += 2;  
		    }  
		    for (int i = x - y; i > 0; i -= 2) {  
		        scr.WritePublicText("'", true);  
		    }  
		    scr.WritePublicText("] (" + per + "%)\n", true); 
			scr.ShowTextureOnScreen();
			scr.ShowPublicTextOnScreen();
		}
		
		/*
		private float getTotalPowerConsumption() {
			List<IMyFunctionalBlock> blocks = new List<IMyFunctionalBlock>();
			GridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock>(blocks, b => b.Enabled);
			float pow = 0;
			foreach (IMyFunctionalBlock block in blocks) {
				pow += block.BlockDefinition.
			}
			//shipGrid.Components.
		}*/
		
		internal class PowerSourceCollection<T> where T: class, IMyTerminalBlock { //f--- C#'s handling of generics
			
			protected readonly List<PowerSource> sources = new List<PowerSource>();
			
			internal PowerSourceCollection(IMyGridTerminalSystem grid, IMyProgrammableBlock cpu, Func<IMyTerminalBlock, bool> collect = null) {
				List<T> blocks = new List<T>();
				grid.GetBlocksOfType<T>(blocks, b => b.CubeGrid == cpu.CubeGrid && collect(b));
				foreach (IMyTerminalBlock block in blocks) {
					sources.Add(createSource(block));
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
			
			public float averageGeneration() {
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
		
		internal class BatteryCollection : PowerSourceCollection<IMyBatteryBlock> {
			
			private bool recharge;
			private bool discharge;
			
			internal BatteryCollection(IMyGridTerminalSystem grid, IMyProgrammableBlock cpu) : base(grid, cpu) {
			
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
				else if (block is IMySolarPanel) {
					return (block as IMySolarPanel).MaxOutput;
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
				else if (block is IMySolarPanel) {
					return (block as IMySolarPanel).CurrentOutput;
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
				(block as IMyBatteryBlock).OnlyRecharge = recharge;
				(block as IMyBatteryBlock).OnlyDischarge = discharge;
			}
			
		}
		
		//====================================================
	}
}