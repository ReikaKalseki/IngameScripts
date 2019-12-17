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
		const bool MOVE_FROM_SMALL_TO_LARGE = true; //Whether items should be moved frmo small to large containers if possible
		readonly string[] EJECT_OVERFULL_ITEMS = {"Ore/Stone", "Ingot/Stone"}; //Which items to eject if they get too full. Empty list for none.
		const float EJECTION_THRESHOLD = 0.9F; //To be ejected, the cargo space must be at least this fraction full, and the item must represent at least this fraction of all stored items.
		//----------------------------------------------------------------------------------------------------------------
		
		public static bool isActualProcessingRefinery(string name) { //Some mods use blocks which are refineries internally, but have no cargo handling nor ore processing. Use this to filter them out.
			return !name.Contains("shieldgenerator"); //Shields ignored by default
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
		
		private readonly Dictionary<ItemProfile, List<IMyCargoContainer>> sourceCache = new Dictionary<ItemProfile, List<IMyCargoContainer>>();
		private readonly Random rand = new Random();
		
		private int refineryCount = 0;
		
		private readonly Dictionary<ItemProfile, int> allItems = new Dictionary<ItemProfile, int>();
		private int maxCapacity = 0;
		private int usedVolume = 0;
		private int totalItems = 0;
		
		private int tick = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;			
			GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo, b => b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(oxyGenerators, b => b.CubeGrid == Me.CubeGrid && (b.BlockDefinition.SubtypeName.Length == 0 || b.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("oxygen")));
			GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers, b => b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(DISPLAY_TAG));
			GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors, b => b.CubeGrid == Me.CubeGrid && b.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("reactor"));
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(EJECTION_GROUP);
			if (grp != null) {
				grp.GetBlocksOfType<IMyShipConnector>(ejectors);
				foreach (IMyShipConnector conn in ejectors) {
					conn.CollectAll = false;
					conn.ThrowOut = true;
					//conn.SetUseConveyorSystem(false);
				}
			}
			
			List<IMyRefinery> li = new List<IMyRefinery>();
			GridTerminalSystem.GetBlocksOfType<IMyRefinery>(li, b => b.CubeGrid == Me.CubeGrid && isActualProcessingRefinery(b.BlockDefinition.SubtypeName.ToLowerInvariant()));
			foreach (IMyRefinery b in li) {
				string id = b.BlockDefinition.SubtypeName.ToLowerInvariant();
				RefineryType type = RefineryType.NORMAL;
				if (id.Contains("uranium")) {
					type = RefineryType.URANIUM;
				}
				else if (id.Contains("stone")/* && id.Contains("crusher")*/) {
					type = RefineryType.STONE;
				}
				else if (id.Contains("blast") && id.Contains("furnace")) {
					type = RefineryType.BLAST;
				}
				else if (id.Contains("platinum")) {
					type = RefineryType.PLATINUM;
				}
				else if (id.Contains("basic")) {
					type = RefineryType.BASIC;
				}
				registerRefinery(new Refinery(b, type));
			}
			
			foreach (string s in EJECT_OVERFULL_ITEMS) {
				ejectionWatchers.Add(new ItemProfile(s));
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
			Echo("Managing "+refineryCount+" refineries, "+assemblers.Count+" assemblers, "+oxyGenerators.Count+" O2 gens, and "+cargo.Count+" cargo containers.");
			if (tick%50 == 0)
				cacheSources();
			tick++;
			
			if (ENABLE_ORE_SORTING) {
				foreach (var entry in refineries) {
					Refinery r = getRandom<Refinery>(entry.Value);
					if (r != null) {
						foreach (string ore in r.validOres) {
							if (isOreValid(ore, r)) {
								tryMoveOre(ore, r);
							}
						}
						empty(r.refinery.OutputInventory);
						r.refinery.Enabled = r.hasWork();
					}
				}
				
				IMyAssembler ass = getRandom<IMyAssembler>(assemblers);
				if (ass != null) {
					empty(ass.OutputInventory);
					List<MyProductionItem> li = new List<MyProductionItem>();
					ass.GetQueue(li);
					ass.Enabled = li.Count > 0 || ass.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("survivalkit");
				}
				
				IMyGasGenerator gas = getRandom<IMyGasGenerator>(oxyGenerators);
				if (gas != null) {
					tryMoveIce(gas);
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
				if (ejectionWatchers.Count > 0 && usedVolume/(float)maxCapacity >= EJECTION_THRESHOLD) {
					foreach (ItemProfile p in ejectionWatchers) {
						float f = getItemFraction(p);
						if (f >= EJECTION_THRESHOLD) {
							tryPrepareEjection(p);
						}
					}
				}
			}
		}
		
		private void tryPrepareEjection(ItemProfile p) {
			FoundItem f = findItem(p);
			if (f != null) {
				foreach (IMyShipConnector conn in ejectors) {
					if (moveItem(f.source, conn.GetInventory(), f.item))
						break;
				}
			}
		}
		
		private float getItemFraction(ItemProfile p) {
			if (totalItems <= 0)
				return 0;
			int has = 0;
			allItems.TryGetValue(p, out has);
			return has/totalItems;
		}
		
		private T getRandom<T>(List<T> blocks) where T : class {
			if (blocks == null || blocks.Count == 0)
				return null;
			return blocks[rand.Next(blocks.Count)];
		}
		
		private void cacheSources() {
			sourceCache.Clear();
			foreach (IMyCargoContainer box in cargo) {
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				box.GetInventory().GetItems(li);
				if (li.Count > 0) {
					foreach (MyInventoryItem item in li) {
						//if (prof.itemType == "ore") {
						addToCache(item, box);
						//}
					}
				}
				usedVolume += box.GetInventory().CurrentVolume.ToIntSafe();
				maxCapacity += box.GetInventory().MaxVolume.ToIntSafe();
			}
		}
		
		private void addToCache(MyInventoryItem item, IMyCargoContainer box) {
			ItemProfile prof = new ItemProfile(item);
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
				int amt = Math.Min(item.item.Amount.ToIntSafe(), 1000);
				moveItem(item.source, target.GetInventory(), item.item, amt);
			}
			else {
				//show("Not found.");
			}
		}
		
		private void tryMoveOre(string ore, Refinery refinery) {
			//show("Attempting to move "+ore+" into "+refinery.refinery.CustomName);
			FoundItem item = findItem(new ItemProfile("ore/"+ore));
			if (item != null) {
				int amt = Math.Min(item.item.Amount.ToIntSafe(), 1000);
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
				inv.GetItems(li, b => item.match(b));
				if (li.Count > 0) {
					return new FoundItem(li[0], inv);
				}
			}
			return null;
		}
		
		private bool isOreValid(string ore, Refinery refinery) {
			if (!refinery.validOres.Contains(ore))
				return false;
			if (refinery.type != RefineryType.STONE) {
				if (ore == "stone") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.STONE, out li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type != RefineryType.PLATINUM) {
				if (ore == "platinum") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.PLATINUM, out li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type != RefineryType.URANIUM) {
				if (ore == "uranium") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.URANIUM, out li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type != RefineryType.BLAST) {
				if (ore == "iron" || ore == "nickel" || ore == "silicon" || ore == "cobalt") {
					List<Refinery> li = null;
					refineries.TryGetValue(RefineryType.BLAST, out li);
					if (li != null && li.Count > 0)
						return false;
				}
			}
			if (refinery.type == RefineryType.BASIC) {
				List<Refinery> li = null;
				refineries.TryGetValue(RefineryType.NORMAL, out li);
				return li == null || li.Count == 0;
			}
			return true;
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
					IMyInventory tgt = box.GetInventory();
					if (src.TransferItemTo(tgt, item)) {
						break;
					}
				}
			}
			return src.ItemCount == 0;
		}
		
		private bool moveItem(IMyInventory src, IMyInventory tgt, MyInventoryItem item) {
			if (src.TransferItemTo(tgt, item, item.Amount)) {
				//show("Moved "+item.Amount.ToIntSafe()+" of "+item.Type.SubtypeId+" from "+src.Owner.Name+" to "+tgt.Owner.Name);
				return true;
			}
			else {
				//show("Could not move "+item.Type.SubtypeId+" from "+src.Owner.Name+" to "+tgt.Owner.Name);
				return false;
			}
		}
		
		private bool moveItem(IMyInventory src, IMyInventory tgt, MyInventoryItem item, int amt) {
			if (src.TransferItemTo(tgt, item, amt)) {
				//show("Moved "+amt+" of "+item.Type.SubtypeId+" from "+src.Owner.Name+" to "+tgt.Owner.Name);
				return true;
			}
			else {
				//show("Could not move "+item.Type.SubtypeId+" from "+src.Owner.Name+" to "+tgt.Owner.Name);
				return false;
			}
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
				this.type = type;
				
				switch(type) {
					case RefineryType.NORMAL:
						validOres.Add("iron");
						validOres.Add("nickel");
						validOres.Add("silicon");
						validOres.Add("cobalt");
						validOres.Add("magnesium");
						validOres.Add("silver");
						validOres.Add("gold");
						validOres.Add("uranium");
						validOres.Add("platinum");
						validOres.Add("stone");
						break;
					case RefineryType.BASIC:
						validOres.Add("stone");
						validOres.Add("iron");
						validOres.Add("nickel");
						validOres.Add("silicon");
						validOres.Add("cobalt");
						break;
					case RefineryType.BLAST:
						validOres.Add("iron");
						validOres.Add("nickel");
						validOres.Add("silicon");
						validOres.Add("cobalt");
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
		
		internal class ItemProfile : IEquatable<ItemProfile> {
			
			internal readonly string itemType;
			internal readonly string itemSubType;
			
			internal ItemProfile(MyInventoryItem item) : this(item.Type.TypeId, item.Type.SubtypeId) {
				
			}
			
			internal ItemProfile(string split) {
				string[] parts = split.Split('/');
				itemType = strip(parts[0]);
				itemType = strip(parts[1]);
			}
			
			internal ItemProfile(string type, string sub) {
				itemType = strip(type);
				itemSubType = strip(sub);
			}
		
			private string strip(string s) {
				return s.ToLowerInvariant().Replace("MyObjectBuilder_", "");
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
			
		}
		
		//====================================================
	}
}