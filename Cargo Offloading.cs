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

namespace Ingame_Scripts.CargoOffloading {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		private string[] OFFLOAD_SHIPS = {"Anaconda"};
		//----------------------------------------------------------------------------------------------------------------

		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly HashSet<String> shipsToOffload = new HashSet<string>();				
		private readonly List<IMyCargoContainer> ourContainers = new List<IMyCargoContainer>();
		private readonly List<IMyCargoContainer> containersToFill = new List<IMyCargoContainer>();
		
		private readonly List<IMyCargoContainer> currentSources = new List<IMyCargoContainer>();
		private IMyCargoContainer currentPreferredTarget = null;
		
		private readonly Random rand = new Random();
		
		private int tick = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;			
			GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(ourContainers, b => b.CubeGrid == Me.CubeGrid);
			foreach (string s in OFFLOAD_SHIPS) {
				shipsToOffload.Add(s);
			}
		}
		
		public void Main() { //called each cycle
			//tick++;
			//if (tick%4 != 0)
			//	return;
			if (currentSources.Count == 0) {
				currentSources.AddList(ourContainers);
			}
			containersToFill.Clear();
			GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containersToFill, b => b.CubeGrid != Me.CubeGrid && shipsToOffload.Contains(b.CubeGrid.DisplayName));
			if (containersToFill.Count == 0) {
				Echo("No valid target inventories.");
				return;
			}
			Echo("Emptying "+ourContainers.Count+" containers into "+containersToFill.Count+" targets.");
			IMyCargoContainer box = getRandom(currentSources);
			if (isEmpty(box)) {
				Echo(box.CustomName+" is already empty.");
				currentSources.Remove(box);
			}
			else if (empty(box)) {
				Echo("Emptied "+box.CustomName);
				currentSources.Remove(box);
			}
			else {
				Echo("Could not empty "+box.CustomName);
			}
			tick++;
		}
		
		private T getRandom<T>(List<T> blocks) where T : class {
			if (blocks == null || blocks.Count == 0)
				return null;
			return blocks[rand.Next(blocks.Count)];
		}
		
		private bool isEmpty(IMyCargoContainer cube) {
			return cube.GetInventory().ItemCount == 0;
		}
		
		private bool empty(IMyCargoContainer cube) {
			List<MyInventoryItem> li = new List<MyInventoryItem>();
			IMyInventory src = cube.GetInventory();
			src.GetItems(li);
			foreach (MyInventoryItem item in li) {
				//Echo("Trying to move "+item.Amount.ToIntSafe()+" of "+item.Type.SubtypeId+" from "+cube.CustomName);
				IMyCargoContainer box = getTargetToFill();
				//Echo("Checking "+box.CustomName);
				IMyInventory tgt = box.GetInventory();
				if (src.TransferItemTo(tgt, item)) {
					Echo("Moved "+item.Amount.ToIntSafe()+" of "+item.Type.SubtypeId+" from "+cube.CustomName+" to "+box.CustomName);
					currentPreferredTarget = box;
					break;
				}
				else {
					currentPreferredTarget = null;
				}
			}
			return isEmpty(cube);
		}
		
		private IMyCargoContainer getTargetToFill() {
			return currentPreferredTarget != null ? currentPreferredTarget : getRandom(containersToFill);
		}		
		
		//====================================================
	}
}