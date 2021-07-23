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
		
		private readonly Vector2 screenSize = new Vector2(1024, 512);
		private readonly float edgePadding = 16;
		private readonly float lineGap = 4;
		private readonly float lineSize = 32;
		private readonly Vector2 indicatorSize = new Vector2(0, 0);
		
		private readonly Color GREEN_COLOR = new Color(40, 255, 40, 255);
		private readonly Color YELLOW_COLOR = new Color(255, 192, 40, 255);
		private readonly Color RED_COLOR = new Color(255, 40, 40, 255);		
		private readonly Color BLUE_COLOR = new Color(20, 96, 255, 255);
		private readonly Color GRAY = new Color(40, 40, 40, 255);	
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			solars = new PowerSourceCollection(this, Me, b => b is IMyPowerProducer && !(b is IMyReactor || b is IMyBatteryBlock) && b.BlockDefinition.TypeId.ToString().Contains("Solar"));
			wind = new PowerSourceCollection(this, Me, b => b is IMyPowerProducer && !(b is IMyReactor || b is IMyBatteryBlock) && b.BlockDefinition.TypeId.ToString().Contains("Wind"));
			batteries = new BatteryCollection(this, Me);
			reactors = new PowerSourceCollection(this, Me, b => b is IMyReactor && b.BlockDefinition.TypeId.ToString().Contains("Reactor"));
			hydrogengines = new PowerSourceCollection(this, Me, b => b is IMyReactor && b.BlockDefinition.TypeId.ToString().Contains("Hydrogen"));
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			
			indicatorSize.X = (screenSize.X-edgePadding*2)/2;
			indicatorSize.Y = lineSize-lineGap;
		}
		
		public void Main() { //called each cycle
			List<MySpriteDrawFrame> li = new List<MySpriteDrawFrame>();
			foreach (IMyTextPanel scr in displays) {
				li.Add(prepareScreen(scr));
			}
			
			float has = batteries.getStoredEnergy();
			float cap = batteries.getCapacity();
			float f2 = has/cap;
			
			float load = getCurrentPowerDemand(Me.CubeGrid);
			float dy = 0;
			drawText(li, edgePadding, dy, "Grid power demand is "+String.Format("{0:0.000}", load)+"MW");
			dy += lineSize;
			float remaining = load;
			bool batteryUse = false;
			foreach (string s in SOURCE_PRIORITY) {
				PowerSourceCollection c = getCollection(s);
				float capacity = c.getMaxGeneration();
				if (capacity <= 0) {
					Echo("Power source type "+s+" is unavailable. Skipping.");
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
				float f = c.getTotalGeneration()/capacity;
				drawText(li, edgePadding, dy, s+": "+String.Format("{0:0.0}", f*100)+" % x "+String.Format("{0:0.000}", capacity)+"MW");
				drawBox(li, screenSize.X/2+edgePadding/2, dy+lineSize, indicatorSize.X-edgePadding, indicatorSize.Y, GRAY);
				drawBox(li, screenSize.X/2+edgePadding/2, dy+lineSize-4, indicatorSize.X-edgePadding, indicatorSize.Y-8, enable ? YELLOW_COLOR : RED_COLOR);
				if (f > 0)
					drawBox(li, screenSize.X/2+edgePadding/2, dy+lineSize-4, (indicatorSize.X-edgePadding)*f-4, indicatorSize.Y-8, GREEN_COLOR);
				Echo("Power source type "+s+": "+(enable ? "In Use, producing up to "+capacity+"MW" : "Offline"));
				dy += lineSize;
			}
			
			dy += lineSize*2;
			
			drawText(li, edgePadding, dy, "Batteries: "+String.Format("{0:0}", has)+"MWh / "+String.Format("{0:0}", cap)+"MWh");
			Color indicator = batteryUse ? YELLOW_COLOR : BLUE_COLOR;
			drawBox(li, screenSize.X/2+edgePadding/2, dy+lineSize, indicatorSize.X-edgePadding, indicatorSize.Y, GRAY);
			drawBox(li, screenSize.X/2+edgePadding/2, dy+lineSize-4, (indicatorSize.X-edgePadding)*f2-4, indicatorSize.Y-8, indicator);
			if (batteryUse) {
				Echo("Batteries are discharging.");
			}
			else {
				batteries.setCharging(true, false); 
				Echo("Batteries are recharging.");
			}
			
			dy += lineSize*2;
			
			if (remaining > 0) {
				drawText(li, edgePadding, dy, "Power supply exceeded by "+remaining+"MW!", Color.Red);
			}
			
			foreach (MySpriteDrawFrame frame in li) {
				frame.Dispose();
			}
		}
		
		private MySpriteDrawFrame prepareScreen(IMyTextPanel scr) {
			scr.ContentType = ContentType.SCRIPT;
       		MySpriteDrawFrame frame = scr.DrawFrame();
       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: screenSize, color: Color.Black));
       		return frame;
		}
		
		private void drawBox(List<MySpriteDrawFrame> li, float x, float y, float w, float h, Color c) {
			Vector2 box = new Vector2(w, h);
    		Vector2 ctr = new Vector2(x*2-screenSize.X/2+w/2, y-h/2);
    		MySprite sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: box, color: c);
       		sprite.Position = ctr;
       		foreach (MySpriteDrawFrame frame in li) {
				frame.Add(sprite);
       		}
		}
		
		private void drawText(List<MySpriteDrawFrame> li, float x, float y, string s) {
			drawText(li, x, y, s, Color.White);
		}
		
		private void drawText(List<MySpriteDrawFrame> li, float x, float y, string s, Color c) {
			MySprite text = MySprite.CreateText(s, "monospace", c, lineSize/24F);
			text.Alignment = TextAlignment.LEFT;
			text.Position = new Vector2(x, y);
       		foreach (MySpriteDrawFrame frame in li) {
				frame.Add(text);
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
		/*
		private void show(string text) {
			foreach (IMyTextPanel scr in displays) {
				scr.WriteText(text+"\n", true);
			}
			Echo(text);
		}*/
		
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
				float has = 0;
				foreach (BatterySource src in sources) {
					has += src.getStoredEnergy();
				}
				return has;
			}
			
			public float getCapacity() {
				float cap = 0;
				foreach (BatterySource src in sources) {
					cap += src.getCapacity();
				}
				return cap;
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
			
			public virtual float getMaxGeneration() {
				if (block is IMyReactor) {
					return (block as IMyReactor).MaxOutput;
				}
				else if (block is IMyPowerProducer) {
					return (block as IMyPowerProducer).MaxOutput;
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
			
			private float maxOutput = 0.001F;
			
			internal BatterySource(IMyBatteryBlock b) : base(b) {

			}
			
			public override float getMaxGeneration() {
				maxOutput = Math.Max((block as IMyBatteryBlock).MaxOutput, maxOutput);
				return maxOutput;
			}
			
			public float getStoredEnergy() {
				return (block as IMyBatteryBlock).CurrentStoredPower;
			}
			
			public float getCapacity() {
				return (block as IMyBatteryBlock).MaxStoredPower;
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