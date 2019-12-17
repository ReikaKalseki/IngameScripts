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

using IMyInventoryOwner = VRage.Game.ModAPI.Ingame.IMyInventoryOwner;
using IMyInventory = VRage.Game.ModAPI.Ingame.IMyInventory;
using IMyInventoryItem = VRage.Game.ModAPI.Ingame.IMyInventoryItem;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;
using IMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

namespace Ingame_Scripts.AmmoTracking {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "Ammo"; //Any LCD panel with this in its name is overridden to show the fill status
		
		static readonly Color textColor = new Color(60, 192, 255, 255);
		
		static bool isDedicatedDisplay(string name) {
			return name.Contains("Dedicated");
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		
		private const int SHOTS_PER_AMMOBOX = 140; //how many rounds per NATO ammo box
		private const int SHOTS_PER_MAGAZINE = 10; //how many rounds per ammo magazine
				
		private readonly List<Display> displays = new List<Display>();
		private readonly List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
		private readonly List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
		
		private readonly HashSet<TurretType> currentTurrets = new HashSet<TurretType>();
		private readonly Dictionary<string, int> itemCounts = new Dictionary<string, int>();
		private readonly Dictionary<string, string> locale = new Dictionary<string, string>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			List<IMyTextPanel> li = new List<IMyTextPanel>();
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(li, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			foreach (IMyTextPanel scr in li) {
				displays.Add(new Display(scr, isDedicatedDisplay(scr.CustomName)));
			}
			
			GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(containers, b => (b is IMyCargoContainer || b is IMyLargeTurretBase) && (b as IMyTerminalBlock).CubeGrid == Me.CubeGrid);
			
			List<IMyLargeTurretBase> li2 = new List<IMyLargeTurretBase>();
			GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(li2, b => b.CubeGrid == Me.CubeGrid);			
			foreach (IMyLargeTurretBase tur in li2) {
				currentTurrets.Add(getTurretType(tur));
				turrets.Add(tur);
			}
		}
		
		private TurretType getTurretType(IMyLargeTurretBase tur) {
			if (tur.DefinitionDisplayNameText.Contains("Missile"))
				return TurretType.MISSILE;
			if (tur.DefinitionDisplayNameText.Contains("Rocket"))
				return TurretType.MISSILE;
			if (tur.DefinitionDisplayNameText.Contains("Gatling"))
				return TurretType.GATLING;
			if (tur.DefinitionDisplayNameText.Contains("Interior"))
				return TurretType.INTERIOR;
			return TurretType.UNKNOWN;
		}
		
		private TurretType isAmmoItem(MyInventoryItem ii) {
			string type = ii.Type.ToString().ToLowerInvariant();
			if (type.Contains("missile") || type.Contains("rocket"))
				return TurretType.MISSILE;
			else if (type.Contains("184mm"))
			    return TurretType.GATLING;
			else if (type.Contains("magazine"))
			    return TurretType.INTERIOR;
			return TurretType.UNKNOWN;
		}
		
		public void Main() { //called each cycle			
			itemCounts.Clear();
			HashSet<TurretType> empties = new HashSet<TurretType>(currentTurrets);
			
			foreach (IMyEntity io in containers) {
				IMyInventory inv = io.GetInventory(0);
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				inv.GetItems(li);
				foreach (MyInventoryItem ii in li) {
					string type = ii.Type.ToString();
					locale[type] = ii.Type.SubtypeId;
					TurretType ammo = isAmmoItem(ii);
					empties.Remove(ammo);
					//Echo(ii.Type.ToString()+" > "+ammo);
					if (currentTurrets.Contains(ammo)) {
						int amt = 0;
						itemCounts.TryGetValue(type, out amt);
						int has = ii.Amount.ToIntSafe();
						amt += has;
						itemCounts[type] = amt;
						Echo(type+" > "+amt);
					}
				}
			}
			
			foreach (Display scr in displays) {
				scr.prepare();
				List<KeyValuePair<string, int>> entries = new List<KeyValuePair<string, int>>();
				foreach (var entry in itemCounts) {
					entries.Add(entry);
				}
				entries.Sort((e1, e2) => e1.Value.CompareTo(e2.Value));
				foreach (var entry in itemCounts) {
					scr.write(localize(entry.Key)+" x "+entry.Value);
				}
				foreach (TurretType empty in empties) {
					scr.setColor(Color.Red);
					scr.write(empty+" ammo is empty!!");
				}
				
				scr.show();
			}
		}
		
		private string localize(String key) {
			string ret = key;
			locale.TryGetValue(key, out ret);
			return ret;
		}
		
		internal class Display {
			
			private readonly IMyTextPanel block;
			private readonly bool isDedicated;
			
			internal Display(IMyTextPanel b, bool d) {
				block = b;
				isDedicated = d;
			}
			
			internal void setColor(Color c) {
				block.FontColor = c;
			}
			
			internal void prepare() {
				if (isDedicated) {
					block.WriteText("");
				}				
				block.BackgroundColor = Color.Black;
				block.FontColor = textColor;
				block.FontSize = 1.6F;
			}
			
			internal void show() {
				block.ShowPublicTextOnScreen();
			}
			
			internal void write(string s) {
				if (!isDedicated && block.GetText().Contains(s))
					return;
				block.WriteText(s, true);
				block.WriteText("\n", true);
			}
				
		}
		
		internal enum TurretType {
			INTERIOR,
			GATLING,
			MISSILE,
			UNKNOWN //Modded
		}
		
		//====================================================
	}
}