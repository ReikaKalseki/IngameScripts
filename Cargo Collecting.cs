﻿using Sandbox.ModAPI.Ingame;

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

namespace Ingame_Scripts.CargoCollecting {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		private static bool isInventory(IMyTerminalBlock b) { //which block types to attempt to empty
			return b.GetInventory() != null;
		}
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyTerminalBlock> containersToDrain = new List<IMyTerminalBlock>();
		private readonly List<IMyCargoContainer> ourContainers = new List<IMyCargoContainer>();
		
		private readonly List<IMyTerminalBlock> currentSources = new List<IMyTerminalBlock>();
		private IMyCargoContainer currentPreferredTarget = null;
		
		private long totalItems = 0;
		private long lastTotal = 0;
		
		private readonly Random rand = new Random();
		
		private int tick = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;			
			GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(ourContainers, b => b.CubeGrid == Me.CubeGrid);
			buildCounts();
			lastTotal = totalItems;
		}
		
		public void Main() { //called each cycle
			if (currentSources.Count == 0) {
				currentSources.AddList(containersToDrain);
			}
			if (tick%5 == 0) {
				containersToDrain.Clear();
				GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(containersToDrain, b => b.CubeGrid != Me.CubeGrid && isInventory(b));
			}
			if (currentSources.Count == 0) {
				Echo("No valid source inventories.");
				return;
			}
			Echo("Emptying "+containersToDrain.Count+" containers into "+ourContainers.Count+" targets.");
			IMyTerminalBlock box = getRandom(currentSources);
			if (isEmpty(box)) {
				Echo(box.CustomName+" is already empty.");
				currentSources.Remove(box);
			}
			else if (empty(box)) {
				if (isEmpty(box)) {
					Echo("Emptied "+box.CustomName);
					currentSources.Remove(box);
				}
				else {
					Echo(box.CustomName+" still has cargo occupying "+box.GetInventory().CurrentVolume*1000+" m3");
				}
			}
			else {
				Echo(box.CustomName+" could not have any cargo moved from it!");
			}
			buildCounts();
			Echo("Item total is "+totalItems);
			if (lastTotal != totalItems) {
				Echo("Item count changed from "+lastTotal+"!");
			}
			lastTotal = totalItems;
			tick++;
		}
		
		private void buildCounts() {
			totalItems = 0;
			
			foreach (IMyCargoContainer box in ourContainers) {
				countInventory(box);
			}
			foreach (IMyTerminalBlock box in containersToDrain) {
				countInventory(box);
			}
		}
		
		private void countInventory(IMyTerminalBlock b) {
			List<MyInventoryItem> li = new List<MyInventoryItem>();
			b.GetInventory().GetItems(li);
			if (li.Count > 0) {
				foreach (MyInventoryItem item in li) {
					totalItems += item.Amount.RawValue;
				}
			}
		}
		
		private T getRandom<T>(List<T> blocks) where T : class {
			if (blocks == null || blocks.Count == 0)
				return null;
			return blocks[rand.Next(blocks.Count)];
		}
		
		private bool isEmpty(IMyTerminalBlock cube) {
			return cube.GetInventory().ItemCount == 0;
		}
		
		private bool empty(IMyTerminalBlock cube) {
			List<MyInventoryItem> li = new List<MyInventoryItem>();
			IMyInventory src = cube.GetInventory();
			src.GetItems(li);
			foreach (MyInventoryItem item in li) {
				//Echo("Trying to move "+item.Amount.ToIntSafe()+" of "+item.Type.SubtypeId+" from "+cube.CustomName);
				IMyCargoContainer box = getTargetToFill();
				//Echo("Checking "+box.CustomName);
				IMyInventory tgt = box.GetInventory();
				if (tryMove(src, tgt, item)) {
					Echo("Moved "+item.Amount.ToIntSafe()+" of "+item.Type.SubtypeId+" from "+cube.CustomName+" to "+box.CustomName);
					currentPreferredTarget = box;
					return true;
				}
				else {
					currentPreferredTarget = null;
				}
			}
			return false;
		}
		
		private bool tryMove(IMyInventory src, IMyInventory tgt, MyInventoryItem item) {
			long filled = src.CurrentVolume.RawValue;
			bool ret = src.TransferItemTo(tgt, item);
			ret &= src.CurrentVolume.RawValue != filled;
			return ret;
		}
		
		private IMyCargoContainer getTargetToFill() {
			if (currentPreferredTarget != null && currentPreferredTarget.GetInventory().IsFull)
				currentPreferredTarget = null;
			return currentPreferredTarget != null ? currentPreferredTarget : getRandom(ourContainers);
		}
		
		//====================================================
	}
}