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

using VRage.Game.GUI.TextPanel;

using IMyInventoryOwner = VRage.Game.ModAPI.Ingame.IMyInventoryOwner;
using IMyInventory = VRage.Game.ModAPI.Ingame.IMyInventory;
using IMyInventoryItem = VRage.Game.ModAPI.Ingame.IMyInventoryItem;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;
using IMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

namespace Ingame_Scripts.CargoFill {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "CargoFillStatus"; //Any LCD panel with this in its name is overridden to show the fill status
		
		const bool TREAT_OVERFLOW_AS_MAIN = true; //If true, overflow containers (see below) are lumped in with "normal/main" cargo and treated as one unit. Otherwise, the overflow containers are not counted
		const bool SHOW_ITEM_BREAKDOWN = true; //Whether to show the item counts (as raw counts and % of total) as well as net cargo space used
		
		const bool COUNT_CONNECTORS = true; //Whether to count connectors as part of the main cargo
		const bool COUNT_SORTERS = true; //Whether to count conveyor sorters as part of the main cargo
		const bool COUNT_PROCESSING = true; //Whether to count processing inventories (refineries, oxygen generators, stone crushers, etc) (but not assemblers) as part of the main cargo
		// towards the total available cargo space.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		public static bool isOverflowContainer(IMyTerminalBlock block) { //overflow containers are ones that if they start filling, it means the main cargo is full. Eg drills.
			return block is IMyShipDrill || block is IMyShipGrinder;
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
				
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		private readonly List<IMyEntity> containers = new List<IMyEntity>();
		private readonly List<IMyEntity> overflow = new List<IMyEntity>();
		
		private Dictionary<string, int> counts = new Dictionary<string, int>();
		private Dictionary<string, int> countsLast = new Dictionary<string, int>();
		
		private int tick = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyEntity>(containers, b => isMainCargo(b) && b is IMyTerminalBlock && (b as IMyTerminalBlock).CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyEntity>(overflow, b => !TREAT_OVERFLOW_AS_MAIN && internalIsOverflowContainer(b) && b is IMyTerminalBlock && (b as IMyTerminalBlock).CubeGrid == Me.CubeGrid);
		}
		
		private bool isMainCargo(IMyEntity b) {
			if (b is IMyCargoContainer)
			    return true;
			if (COUNT_CONNECTORS && b is IMyShipConnector)
				return true;
			if (COUNT_SORTERS && b is IMyConveyorSorter)
				return true;
			if (COUNT_PROCESSING) {
				if (b is IMyRefinery || b is IMyGasGenerator)
					return true;
			}
			return TREAT_OVERFLOW_AS_MAIN && internalIsOverflowContainer(b);
		}
		
		private bool internalIsOverflowContainer(IMyEntity b) {
			return b is IMyTerminalBlock && !(b is IMyCargoContainer) && isOverflowContainer(b as IMyTerminalBlock);
		}
		
		public void Main() { //called each cycle	
			tick++;
			if (tick%4 != 0)
				return;
			counts.Clear();
			long capacity = 0;
			long used = 0;
			long totalItems = 0;
			long capacityOver = 0;
			long usedOver = 0;
			long totalItemsOver = 0;
			
			List<string> nonfull = new List<string>();
			
			foreach (IMyEntity io in containers) {
				IMyInventory inv = io.GetInventory(0);
				if (io is IMyProductionBlock) {
					inv = ((IMyProductionBlock)io).InputInventory;
				}
				int cap = inv.MaxVolume.ToIntSafe();
				int usedVol = inv.CurrentVolume.ToIntSafe();
				if (cap != usedVol) {
					string name = io.Name;
					if (io is IMyTerminalBlock) {
						name = ((IMyTerminalBlock)io).CustomName;
					}
					nonfull.Add(name);
				}
				capacity += cap;
				used += usedVol;
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				inv.GetItems(li);
				foreach (MyInventoryItem ii in li) {
					int amt = 0;
					string type = ii.Type.ToString();
					counts.TryGetValue(type, out amt);
					int has = ii.Amount.ToIntSafe();
					amt += has;
					counts[type] = amt;
					totalItems += has;
				}
			}
			
			if (!TREAT_OVERFLOW_AS_MAIN && used >= capacity) {
				foreach (IMyEntity io in overflow) {
					IMyInventory inv = io.GetInventory(0); //do not increment capacity, so fill fraction goes over 100%
					capacityOver += inv.MaxVolume.ToIntSafe();
					used += inv.CurrentVolume.ToIntSafe();
					usedOver += inv.CurrentVolume.ToIntSafe();
					List<MyInventoryItem> li = new List<MyInventoryItem>();
					inv.GetItems(li);
					foreach (MyInventoryItem ii in li) {
						int amt = 0;
						string type = ii.Type.ToString();
						counts.TryGetValue(type, out amt);
						int has = ii.Amount.ToIntSafe();
						amt += has;
						counts[type] = amt;
						totalItems += has;
						totalItemsOver += has;
					}
				}
			}
			
			double frac = used/(double)capacity;
			Color c = getDisplayColor(frac);
			
			foreach (IMyTextPanel scr in displays) {
				scr.WriteText("");
				scr.BackgroundColor = c;
				scr.ContentType = ContentType.TEXT_AND_IMAGE;
				scr.FontColor = new VRageMath.Color(255, 255, 255, 255);
				
				scr.WriteText("Cargo is "+Math.Round(frac*100, 1)+" % full.", true);
				if (!TREAT_OVERFLOW_AS_MAIN && frac > 1) {
					scr.WriteText("Cargo is overflowing!\n");
					double frac2 = usedOver/(double)capacityOver;
					scr.WriteText("Overflow is "+Math.Round(frac2*100, 1)+" % full.\n", true);
				}
				if (nonfull.Count > 0 && frac > 0.8)
					scr.WriteText("Space remains in: "+string.Join(", ", nonfull), true);
				if (SHOW_ITEM_BREAKDOWN) {
					scr.WriteText(" Contents:\n", true);
					List<KeyValuePair<string, int>> entries = new List<KeyValuePair<string, int>>();
					foreach (var entry in counts) {
						entries.Add(entry);
					}
					entries.Sort((e1, e2) => -e1.Value.CompareTo(e2.Value));
					foreach (var entry in entries) {
						int prev = 0;
						countsLast.TryGetValue(entry.Key, out prev);
						string delta = "   (No change)";
						if (prev < entry.Value) {
							delta = "   (+"+(entry.Value-prev)+" units)";
						}
						else if (prev > entry.Value) {
							delta = "   (-"+(prev-entry.Value)+" units)";
						}
						if (countsLast.Count == 0) {
							delta = "";
						}
						scr.WriteText(localize(entry.Key)+" x "+entry.Value+" ("+Math.Round(entry.Value*100F/totalItems, 1)+" %)"+delta, true);
						scr.WriteText("\n", true);
					}
				}
				else {
					scr.WriteText("\n", true);
				}
			}
			
			if (SHOW_ITEM_BREAKDOWN) {
				countsLast = new Dictionary<string, int>(counts);
			}
		}
		
		private string localize(string raw) {
			return raw.Substring(raw.IndexOf('/')+1);
		}
		
		private Color getDisplayColor(double f) {
			if (f < 0.5) {
				return new VRageMath.Color(0, 255/3, 0, 255);
			}
			else if (f < 0.75) {
				return new VRageMath.Color(255/3, 255/3, 0, 255);
			}
			else if (f < 0.9) {
				return new VRageMath.Color(255/3, 144/3, 0, 255);
			}
			else if (f < 1) {
				return new Color(255/3, 64/3, 0, 255);
			}
			else {
				return new Color(255/3, 0, 0, 255);
			}
		}
		
		//====================================================
	}
}