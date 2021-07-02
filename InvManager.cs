using Sandbox.ModAPI.Ingame;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
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
using MyFixedPoint = VRage.MyFixedPoint;

namespace Ingame_Scripts.CargoManager {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "CargoManagerDisplay"; //Any LCD panel with this in its name is overridden to show status
		const string EJECTION_GROUP = "Excess Cargo Ejection"; //The group with all collectors/ejectors which are set to eject their contents
		
		//const bool ENABLE_REACTOR_BALANCING = true; //Whether to balance the contents of all reactors
		//const bool ENABLE_ORE_PRIORITIES = true; //Whether to process ores in order of priority, determined primarily by what is in lowest supply but also by how much use that ore has
		const bool ENABLE_ORE_SORTING = true; //Whether ore should be sorted automatically between refinery types; useful if you have mods for specialized processing
		//const bool ENABLE_CARGO_COLLATION = true; //Whether items should be collated in cargo storage, i.e. putting all items of a type together, if possible
		const bool MOVE_FROM_SMALL_TO_LARGE = true; //Whether items should be moved from small to large containers if possible
		const bool SKIP_OFFLINE_REFINERIES = true; //Whether offline refineries should be ignored for the purposes of ore routing; this allows choosing between either holding onto the ore until a "better" refinery is available or ignoring those refineries and just using the most applicable enabled one.
		const bool ENABLE_O2_GENS = false; //Whether offline O2/H2 generators should be ignored, vs turned on as long as there is work for them. 
		readonly string[] EJECT_OVERFULL_ITEMS = {"ore/stone", "ingot/stone"}; //Which items to eject if they get too full. Empty list for none.
		const float EJECTION_THRESHOLD = 0.9F; //To be ejected, the cargo space must be at least this fraction full, and the item must represent at least this fraction of all stored items.
		
		readonly string[] ORE_PRIORITY = {"Iron/50000", "Nickel/10000", "Silicon/5000", "Cobalt/2500", "Silver/1000", "Gold/200"}; //Ore types and the ingot threshold at which they are given priority (ie if you have less than this amount of refined metal). If multiple "priority" ores are applicable, the ordering of this list determines priority among them.
		
		readonly string[] SORTING = {""}; //Which item types to sort, and where. Empty list for none.
		
		readonly string[] NO_PROCESS = {}; //Ores to keep as ore, preventing any processing. Useful if this script is running on a mining ship and needs to cart the ore back to base for specialized processing.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		public static bool isActualProcessingRefinery(string name) { //Some mods use blocks which are refineries internally, but have no cargo handling nor ore processing. Use this to filter them out.
			return !name.Contains("shieldgenerator"); //Shields ignored by default
		}
		
		public static int cargoBoxSortOrder(IMyCargoContainer box1, IMyCargoContainer box2) { //Standard Java/C# Comparator format, used to determine the sorting order of cargo containers, and thus filling priority.
			return box1.CustomName.CompareTo(box2.CustomName); //Default string comparison on their terminal names, which will end up being based on their autogenned numbers
		}
		
		//private static FlowDirection getActiveFlowDirection(string thisConnectorName, string otherConnectorName, string otherGridName) { //Whether to, and in what direction, attempt to move items across active connectors.
		//	return FlowDirection.INERT; //By default, do not actively attempt to move anything across any connector
		//}
		
		private static bool isItemValidForContainer(string itemCategory, string itemType, string containerName) { //Whether the given cargo container can accept this item when emptied from other locations. This allows item-type-specific sorting.
			return true;
		}
		
		private static bool isSharedGrid(string name) { //Whether cargo, refineries, etc on this grid should be counted as part of the main grid.
			return false;
		}
		
		private static bool shutdownWhenDockedTo(string connector, string other, string grid) { //Whether to pause all execution when the given connector is connected to the given grid by the given other connector, perhaps so its copy of this script can take precedence.
			return false;
		}
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
				
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		private readonly List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
		private readonly Dictionary<RefineryType, List<Refinery>> refineries = new Dictionary<RefineryType, List<Refinery>>();
		private readonly List<IMyGasGenerator> oxyGenerators = new List<IMyGasGenerator>();
		private readonly List<IMyReactor> reactors = new List<IMyReactor>();
		private readonly List<IMyAssembler> assemblers = new List<IMyAssembler>();
		
		private readonly List<ItemProfile> ejectionWatchers = new List<ItemProfile>();
		private readonly List<IMyShipConnector> ejectors = new List<IMyShipConnector>();
		private readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
		
		private readonly Dictionary<ItemProfile, List<IMyCargoContainer>> sourceCache = new Dictionary<ItemProfile, List<IMyCargoContainer>>();
		private readonly Dictionary<ItemProfile, int> inventoryAmounts = new Dictionary<ItemProfile, int>();
		private readonly Dictionary<string, OrePriorityCheck> orePriorityValues = new Dictionary<string, OrePriorityCheck>();
		private readonly Random rand = new Random();
		
		private int refineryCount = 0;
		
		private readonly Dictionary<ItemProfile, int> allItems = new Dictionary<ItemProfile, int>();
		private int maxCapacity = 0;
		private int usedVolume = 0;
		private int totalItems = 0;
		
		private int tick = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;			
			GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo, b => b.CubeGrid == Me.CubeGrid || isSharedGrid(b.CubeGrid.DisplayName));
			cargo.Sort(cargoBoxSortOrder);
			GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(oxyGenerators, b => (b.CubeGrid == Me.CubeGrid || isSharedGrid(b.CubeGrid.DisplayName)) && (b.BlockDefinition.SubtypeName.Length == 0 || b.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("oxygen")));
			GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers, b => b.CubeGrid == Me.CubeGrid || isSharedGrid(b.CubeGrid.DisplayName));
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => (b.CubeGrid == Me.CubeGrid || isSharedGrid(b.CubeGrid.DisplayName)) && b.CustomName.Contains(DISPLAY_TAG));
			GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors, b => (b.CubeGrid == Me.CubeGrid || isSharedGrid(b.CubeGrid.DisplayName)) && b.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("reactor"));
			GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors, b => b.CubeGrid == Me.CubeGrid || isSharedGrid(b.CubeGrid.DisplayName));
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(EJECTION_GROUP);
			if (grp != null) {
				grp.GetBlocksOfType<IMyShipConnector>(ejectors);
				foreach (IMyShipConnector conn in ejectors) {
					conn.CollectAll = false;
					//conn.ThrowOut = true;
					//conn.SetUseConveyorSystem(false);
				}
			}
			
			for (int i = 0; i < NO_PROCESS.Length; i++) {
				NO_PROCESS[i] = NO_PROCESS[i].ToLowerInvariant();
			}
			
			List<IMyRefinery> li = new List<IMyRefinery>();
			GridTerminalSystem.GetBlocksOfType<IMyRefinery>(li, b => b.CubeGrid == Me.CubeGrid && isActualProcessingRefinery(b.BlockDefinition.SubtypeName.ToLowerInvariant()));
			foreach (IMyRefinery b in li) {
				string id = b.BlockDefinition.SubtypeName.ToLowerInvariant().Replace(" ", "");
				RefineryType type = RefineryType.NORMAL;
				if (id.Contains("uranium")) {
					type = RefineryType.URANIUM;
				}
				else if (id.Contains("stone")/* && id.Contains("crusher")*/) {
					type = RefineryType.STONE;
				}
				else if ((id.Contains("blast") && id.Contains("furnace")) || id.Contains("arcfurnace")) {
					type = RefineryType.BLAST;
				}
				else if (id.Contains("platinum")) {
					type = RefineryType.PLATINUM;
				}
				else if (id.Contains("basic")) {
					type = RefineryType.BASIC;
				}
				Echo("Registered refinery "+b.CustomName+" as "+type+" from "+id);
				registerRefinery(new Refinery(b, type));
			}
			
			foreach (string s in EJECT_OVERFULL_ITEMS) {
				ejectionWatchers.Add(new ItemProfile(s));
			}
			
			for (int i = 0; i < ORE_PRIORITY.Length; i++) {
				string s = ORE_PRIORITY[i];
				string[] parts = s.Split('/');
				ItemProfile ingot = new ItemProfile("Ingot", parts[0]);
				ItemProfile ore = new ItemProfile("Ore", parts[0]);
				int amt = Int32.Parse(parts[1]);
				orePriorityValues[ore.itemSubType] = new OrePriorityCheck(i, ore, ingot, amt);
			}
		}
		
		private void registerRefinery(Refinery r) {
			List<Refinery> li = null;
			refineries.TryGetValue(r.type, out li);
			if (li == null)
				li = new List<Refinery>();
			li.Add(r);
			refineries[r.type] = li;
			refineryCount++;
		}
		
		public void Main() { //called each cycle
			//tick++;
			//if (tick%4 != 0)
			//	return;
			
			tick++;
			bool cancel = false;
			foreach (IMyShipConnector conn in connectors) {
				if (conn.Enabled && conn.Status == MyShipConnectorStatus.Connected) {
					if (shutdownWhenDockedTo(conn.DisplayName, conn.OtherConnector.DisplayName, conn.OtherConnector.CubeGrid.DisplayName)) {
					    cancel = true;
					    break;
					}
				}
			}
			if (cancel) {
				Echo("Docked to dominant grid; not managing inventories.");
				return;
			}
			
			Echo("Managing "+refineryCount+" refineries, "+assemblers.Count+" assemblers, "+oxyGenerators.Count+" O2 gens, and "+cargo.Count+" cargo containers.");
			if (tick%50 == 0)
				cacheSources();
			
			if (ENABLE_ORE_SORTING) {
				foreach (var entry in refineries) {
					Refinery r = getRandom<Refinery>(entry.Value);
					if (r != null && (r.refinery.Enabled || !SKIP_OFFLINE_REFINERIES)) {
						ICollection<string> li = r.validOres;
						if (ORE_PRIORITY.Length > 0) {
							li = applyPriorityRules(li);
						}
						foreach (string ore in li) {
							if (isOreValid(ore, r)) {
								tryMoveOre(ore, r);
							}
						}
						empty(r.refinery.OutputInventory);
						if (!SKIP_OFFLINE_REFINERIES)
							r.refinery.Enabled = r.hasWork();
					}
				}
				
				IMyAssembler ass = getRandom<IMyAssembler>(assemblers);
				if (ass != null) {
					empty(ass.Mode == MyAssemblerMode.Disassembly ? ass.InputInventory : ass.OutputInventory);
					List<MyProductionItem> li = new List<MyProductionItem>();
					ass.GetQueue(li);
					ass.Enabled = li.Count > 0 || ass.CooperativeMode || ass.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("survivalkit");
				}
				
				IMyGasGenerator gas = getRandom<IMyGasGenerator>(oxyGenerators);
				if (gas != null && (gas.Enabled || ENABLE_O2_GENS)) {
					tryMoveIce(gas);
					if (ENABLE_O2_GENS)
						gas.Enabled = true;// || gas.GetInventory().ItemCount > 0;
				}
			}
			if (MOVE_FROM_SMALL_TO_LARGE) {
				IMyCargoContainer box = getRandom<IMyCargoContainer>(cargo);
				if (box != null) {
					if (box.GetInventory().ItemCount > 0 && box.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("small")) {
						IMyInventory inv = box.GetInventory();
						empty(inv, false);
						//break;
					}
				}
			}
			if (tick%5 == 0) {
				bool flag = false;
				if (ejectionWatchers.Count > 0 && usedVolume/(float)maxCapacity >= EJECTION_THRESHOLD) {
					foreach (ItemProfile p in ejectionWatchers) {
						float f = getItemFraction(p);
						Echo("Item type "+p.ToString()+" represents "+f*100+"% of items.");
						if (f >= EJECTION_THRESHOLD) {
							Echo("Ejecting excess.");
							tryPrepareEjection(p);
							flag = true;
						}
					}
				}
				if (!flag) {
					Echo("No excess to eject.");
					foreach (IMyShipConnector conn in ejectors) {
						conn.ThrowOut = false;
					}
				}/*
				foreach (IMyShipConnector con in connectors) {
					if (con.Status == MyShipConnectorStatus.Connected) {
						FlowDirection flow = getActiveFlowDirection(con.CustomName, con.OtherConnector.CustomName, con.OtherConnector.CubeGrid.CustomName);
						if (flow != FlowDirection.INERT) {
							
						}
					}
				}*/
			}
		}
		
		private ICollection<string> applyPriorityRules(ICollection<string> li) {
			if (li == null || li.Count <= 1)
				return li;
			List<string> ret = new List<string>();
			List<string> priority = new List<string>();
			foreach (string s in li) {
				OrePriorityCheck ore = null;
				if (!orePriorityValues.TryGetValue(s, out ore))
					ore = null;
				if (ore != null) {
					int has = 0;
					if (!inventoryAmounts.TryGetValue(ore.ingot, out has))
						has = 0;
					//show("Ore '"+s+"' has priority check, have "+has+", thresh "+ore.threshold);
					if (has < ore.threshold) {
						priority.Add(s);
					}
					else {
						ret.Add(s);
					}
				}
			}
			//show("Sorting priority list "+string.Join(",", priority));
			priority.Sort(sortOrePriority);
			ret.InsertRange(0, priority);
			//show("Ore list '"+string.Join(",", li)+" sorted by priority to "+string.Join(",", ret));
			return ret;
		}
		
		private int sortOrePriority(string s1, string s2) {
			OrePriorityCheck ore1 = null;
			if (!orePriorityValues.TryGetValue(s1, out ore1))
				ore1 = null;
			OrePriorityCheck ore2 = null;
			if (!orePriorityValues.TryGetValue(s2, out ore2))
				ore2 = null;
			if (ore1 == ore2) {
				return 0;
			}
			else if (ore1 == null) {
				return 1;
			}
			else if (ore2 == null) {
				return -1;
			}
			else {
				return ore1.CompareTo(ore2);
			}
		}
		
		private void tryPrepareEjection(ItemProfile p) {
			FoundItem f = findItem(p);
			if (f != null) {
				foreach (IMyShipConnector conn in ejectors) {
					if (conn.Enabled && moveItem(f.source, conn.GetInventory(), f.item))
						conn.ThrowOut = true;
				}
			}
		}
		
		private float getItemFraction(ItemProfile p) {
			if (totalItems <= 0 || !allItems.ContainsKey(p))
				return 0;
			return allItems[p]/(float)totalItems;
		}
		
		private T getRandom<T>(List<T> blocks) where T : class {
			if (blocks == null || blocks.Count == 0)
				return null;
			return blocks[rand.Next(blocks.Count)];
		}
		
		private void cacheSources() {
			show("Rebuilding item cache.");
			sourceCache.Clear();
			inventoryAmounts.Clear();
			foreach (IMyCargoContainer box in cargo) {
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				box.GetInventory().GetItems(li);
				if (li.Count > 0) {
					foreach (MyInventoryItem item in li) {
						//if (prof.itemType == "ore") {
						addToCache(item, box);
						//}
						int has = 0;
						ItemProfile prof = new ItemProfile(item);
						if (!inventoryAmounts.TryGetValue(prof, out has))
							has = 0;
						has += item.Amount.ToIntSafe();
						inventoryAmounts[prof] = has;
					}
				}
				usedVolume += box.GetInventory().CurrentVolume.ToIntSafe();
				maxCapacity += box.GetInventory().MaxVolume.ToIntSafe();
			}
		}
		
		private void addToCache(MyInventoryItem item, IMyCargoContainer box) {
			ItemProfile prof = new ItemProfile(item);
			//show("Caching "+prof.ToString()+" in "+box.CustomName);
			List<IMyCargoContainer> li = null;
			sourceCache.TryGetValue(prof, out li);
			if (li == null)
				li = new List<IMyCargoContainer>();
			li.Add(box);
			sourceCache[prof] = li;
			
			int has = 0;
			allItems.TryGetValue(prof, out has);
			int amt = item.Amount.ToIntSafe();
			has += amt;
			allItems[prof] = has;
			totalItems += amt;
		}
		
		private void tryMoveIce(IMyGasGenerator target) {
			//show("Attempting to move ice into "+target.CustomName);
			FoundItem item = findItem(new ItemProfile("ore/ice"));
			if (item != null) {
				MyFixedPoint amt = min(item.item.Amount, 1000);
				moveItem(item.source, target.GetInventory(), item.item, amt);
			}
			else {
				//show("Not found.");
			}
		}
		
		private void tryMoveOre(string ore, Refinery refinery) {
			//show("Attempting to move "+ore+" into "+refinery.refinery.CustomName);
			FoundItem item = findItem(new ItemProfile("ore/"+ore));
			if (item == null && ore == "scrap") {
				item = findItem(new ItemProfile("ingot/scrap"));
			}
			if (item != null) {
				MyFixedPoint amt = min(item.item.Amount, 1000);
				moveItem(item.source, refinery.refinery.InputInventory, item.item, amt);
			}
			else {
				//show("Not found.");
			}
		}
		
		private FoundItem findItem(ItemProfile item) {
			List<IMyCargoContainer> cache = null;
			sourceCache.TryGetValue(item, out cache);
			if (cache == null || cache.Count == 0)
				return null;
			foreach (IMyCargoContainer box in cache) {
				IMyInventory inv = box.GetInventory();
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				//show("Found "+li.ToString()+" in "+box.CustomName+" for "+item.ToString());
				inv.GetItems(li, item.match);
				if (li.Count > 0) {
					return new FoundItem(li[0], inv);
				}
			}
			return null;
		}
		
		private bool isOreValid(string ore, Refinery refinery) {
			if (NO_PROCESS.Contains(ore))
				return false;
			if (!refinery.validOres.Contains(ore))
				return false;
			if (refinery.type != RefineryType.STONE) {
				if (ore == "stone") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.STONE, out li);
					if (SKIP_OFFLINE_REFINERIES)
						li = filterRefineryList(li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type != RefineryType.PLATINUM) {
				if (ore == "platinum") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.PLATINUM, out li);
					if (SKIP_OFFLINE_REFINERIES)
						li = filterRefineryList(li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type != RefineryType.URANIUM) {
				if (ore == "uranium") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.URANIUM, out li);
					if (SKIP_OFFLINE_REFINERIES)
						li = filterRefineryList(li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type != RefineryType.BLAST) {
				if (ore == "iron" || ore == "nickel" || ore == "silicon" || ore == "cobalt") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.BLAST, out li);
					if (SKIP_OFFLINE_REFINERIES)
						li = filterRefineryList(li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type == RefineryType.BASIC) {
				List<Refinery> li = null;
				refineries.TryGetValue(RefineryType.NORMAL, out li);
				if (SKIP_OFFLINE_REFINERIES)
					li = filterRefineryList(li);
				return li == null || li.Count == 0;
			}
			return true;
		}
		
		private List<Refinery> filterRefineryList(List<Refinery> li) {
			if (li == null)
				return li;
			List<Refinery> ret = new List<Refinery>();
			foreach (Refinery r in li) {
				if (r.refinery.Enabled) {
					ret.Add(r);
				}
			}
			return ret;
		}
		/*
		private void tallyItems() {
			maxCapacity = 0;
			usedVolume = 0;
			foreach (IMyCargoContainer box in cargo) {				
				IMyInventory inv = box.GetInventory(0);
				maxCapacity += inv.MaxVolume.ToIntSafe();
				usedVolume += inv.CurrentVolume.ToIntSafe();
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				inv.GetItems(li);
				foreach (MyInventoryItem ii in li) {
					int amt = 0;
					string type = ii.Type.ToString();
					allItems.TryGetValue(type, out amt);
					int has = ii.Amount.ToIntSafe();
					amt += has;
					allItems[type] = amt;
					totalItems += has;
				}
			}
		}
		
		private void balanceReactors() {
			int each = 0;
			allItems.TryGetValue("uranium", out each);
			each /= reactors.Count;
			List<IMyReactor> overfilled = new List<IMyReactor>();
		}
		
		private string getIngot(string ore) {
			
		}*/
		
		private bool empty(IMyInventory src, bool allowSmall = true) {
			List<MyInventoryItem> li = new List<MyInventoryItem>();
			src.GetItems(li);
			foreach (MyInventoryItem item in li) {
				foreach (IMyCargoContainer box in cargo) {
					if (!allowSmall && box.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("small"))
						continue;
					if (!isItemValidForContainer(item.Type.TypeId, item.Type.SubtypeId, box.CustomName)) {
						continue;
					}
					IMyInventory tgt = box.GetInventory();
					if (moveItem(src, tgt, item)) {
						break;
					}
				}
			}
			return src.ItemCount == 0;
		}
		
		private bool moveItem(IMyInventory src, IMyInventory tgt, MyInventoryItem item) {
			return moveItem(src, tgt, item, item.Amount);
		}
		
		private bool moveItem(IMyInventory src, IMyInventory tgt, MyInventoryItem item, MyFixedPoint amt) {
			bool ret = false;
			IMyRefinery prod1 = src as IMyRefinery;
			IMyRefinery prod2 = tgt as IMyRefinery;
			if (prod1 != null) {
				prod1.UseConveyorSystem = true;
			}
			if (prod2 != null) {
				prod2.UseConveyorSystem = true;
			}
			if (src.TransferItemTo(tgt, item, amt)) {
				//show("Moved "+amt+" of "+item.Type.SubtypeId+" from "+src.Owner.Name+" to "+tgt.Owner.Name);
				ret = true;
			}
			else {
				//show("Could not move "+item.Type.SubtypeId+" from "+src.Owner.Name+" to "+tgt.Owner.Name);
				ret = false;
			}
			if (prod1 != null) {
				prod1.UseConveyorSystem = false;
			}
			if (prod2 != null) {
				prod2.UseConveyorSystem = false;
			}
			return ret;
		}
		
		private MyFixedPoint min(MyFixedPoint val1, int val2) {
			int amt = val1.ToIntSafe();
			if (val2 <= amt)
				return val2;
			else
				return val1;
		}
		
		private void show(string text) {
			Echo(text);
			foreach (IMyTextPanel scr in displays) {
				scr.ContentType = ContentType.TEXT_AND_IMAGE;
				scr.WriteText(text, true);
			}
		}
		
		internal class Refinery {
			
			internal readonly IMyRefinery refinery;
			internal readonly RefineryType type;
			internal HashSet<string> validOres = new HashSet<string>();
			
			internal Refinery(IMyRefinery imr, RefineryType type) {
				refinery = imr;
				refinery.UseConveyorSystem = false;
				this.type = type;
				
				switch(type) {
					case RefineryType.NORMAL:
						validOres.Add("iron");
						validOres.Add("nickel");
						validOres.Add("silicon");
						validOres.Add("cobalt");
						validOres.Add("silver");
						validOres.Add("gold");
						validOres.Add("uranium");
						validOres.Add("platinum");
						validOres.Add("magnesium");
						validOres.Add("stone");
						validOres.Add("scrap");
						break;
					case RefineryType.BASIC:
						validOres.Add("iron");
						validOres.Add("nickel");
						validOres.Add("silicon");
						validOres.Add("cobalt");
						validOres.Add("stone");
						validOres.Add("scrap");
						break;
					case RefineryType.BLAST:
						validOres.Add("iron");
						validOres.Add("nickel");
						validOres.Add("silicon");
						validOres.Add("cobalt");
						validOres.Add("scrap");
						break;
					case RefineryType.STONE:
						validOres.Add("stone");
						break;
					case RefineryType.URANIUM:
						validOres.Add("uranium");
						break;
					case RefineryType.PLATINUM:
						validOres.Add("platinum");
						break;
				}
			}
			
			internal bool hasWork() {
				IMyInventory inv = refinery.InputInventory;
				return inv.ItemCount > 0;
			}
			
		}
		
		internal enum RefineryType {
			NORMAL,
			BASIC,
			BLAST,
			STONE,
			URANIUM,
			PLATINUM
		}
		/*
		internal enum FlowDirection {
			PULL,
			PUSH,
			INERT
		}*/
		
		internal class FoundItem {
			
			internal readonly MyInventoryItem item;
			internal readonly IMyInventory source;
			
			internal FoundItem(MyInventoryItem inv, IMyInventory src) {
				item = inv;
				source = src;
			}
			
			public ItemProfile getProfile() {
				return new ItemProfile(item);
			}
			
		}
		
		internal class OrePriorityCheck : IComparable, IComparable<OrePriorityCheck> {
			
			internal readonly ItemProfile ore;
			internal readonly ItemProfile ingot;
			internal readonly int threshold;
			private readonly int index;
			
			internal OrePriorityCheck(int idx, ItemProfile o, ItemProfile i, int t) {
				index = idx;
				ore = o;
				ingot = i;
				threshold = t;
			}
			
			public int CompareTo(OrePriorityCheck other) {
				return index.CompareTo(other.index);
			}
			
			public int CompareTo(object other) {
				OrePriorityCheck o = other as OrePriorityCheck;
				return o != null ? CompareTo(o) : -1;
			}
			
		}
		
		internal class ItemProfile : IEquatable<ItemProfile> {
			
			/** The root type, like ore, ingot, component, etc. */
			internal readonly string itemType;
			/** The specific item type, eg construction, girder, uranium, stone...*/
			internal readonly string itemSubType;
			
			internal ItemProfile(MyInventoryItem item) : this(item.Type.TypeId, item.Type.SubtypeId) {
				
			}
			
			internal ItemProfile(string split) {
				string[] parts = split.Split('/');
				itemType = strip(parts[0]);
				itemSubType = strip(parts[1]);
			}
			
			internal ItemProfile(string type, string sub) {
				itemType = strip(type);
				itemSubType = strip(sub);
			}
		
			private string strip(string s) {
				return s.ToLowerInvariant().Replace("myobjectbuilder_", "");
			}
			
			public override int GetHashCode() {
				return itemType.GetHashCode() ^ itemSubType.GetHashCode();
			}
			
			public override bool Equals(object o) {
				return o is ItemProfile && Equals((ItemProfile)o);
			}
			
			public bool Equals(ItemProfile p) {
				return p.itemSubType == itemSubType && p.itemType == itemType;
			}
			
			public bool match(MyInventoryItem item) {
				return strip(item.Type.TypeId) == itemType && strip(item.Type.SubtypeId) == itemSubType;
			}
			
			public override string ToString() {
				return this.itemType+"/"+this.itemSubType;
			}
			
		}
		
		//====================================================
	}
}