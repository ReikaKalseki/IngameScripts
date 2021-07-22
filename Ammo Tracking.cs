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

using IMyLargeMissileTurret = SpaceEngineers.Game.ModAPI.Ingame.IMyLargeMissileTurret;
using IMyLargeGatlingTurret = SpaceEngineers.Game.ModAPI.Ingame.IMyLargeGatlingTurret;
using IMyLargeInteriorTurret = SpaceEngineers.Game.ModAPI.Ingame.IMyLargeInteriorTurret;
using IMySmallMissileLauncher = Sandbox.ModAPI.Ingame.IMySmallMissileLauncher;

namespace Ingame_Scripts.AmmoTracking {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string AMMO_DISPLAY_TAG = "AmmoTracker"; //Any LCD panel with this in its name is overridden to show the fill status
		const string TURRET_DISPLAY_TAG = "TurretTracker"; //Any LCD panel with this in its name is overridden to show the fill status
		
		const int interiorRoundsFull = 10000; //How many rounds of interior turret ammo count as 100% fully loaded. Each magazine is worth 10 rounds.
		const int gatlingRoundsFull = 14000; //How many rounds of gatling ammo count as 100% fully loaded. Each box is worth 140 rounds.
		const int missileRoundsFull = 200; //How many missiles count as 100% fully loaded.
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------		
		private const int SHOTS_PER_AMMOBOX = 140; //how many rounds per NATO ammo box
		private const int SHOTS_PER_MAGAZINE = 10; //how many rounds per ammo magazine
		
		private readonly Dictionary<TurretType, int> roundValues = new Dictionary<TurretType, int>();
		private readonly Dictionary<TurretType, int> baseValues = new Dictionary<TurretType, int>();
				
		private readonly List<Display> ammoDisplays = new List<Display>();
		private readonly List<Display> turretDisplays = new List<Display>();
		
		private readonly List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
		private readonly List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
		
		private readonly HashSet<TurretType> currentTurrets = new HashSet<TurretType>();
		private readonly Dictionary<TurretType, int> itemCounts = new Dictionary<TurretType, int>();
		private readonly Dictionary<TurretType, float> fractions = new Dictionary<TurretType, float>();
		private readonly Dictionary<TurretType, string> locale = new Dictionary<TurretType, string>();
		
		const string whiteArrow = "";
		const string redArrow = "";
		const string greenArrow = "";
		const string blueArrow = "";
		const string yellowArrow = "";
		const string magentaArrow = "";
		const string cyanArrow = "";
		const string dot = "‧";
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			baseValues[TurretType.INTERIOR] = interiorRoundsFull;
			baseValues[TurretType.GATLING] = gatlingRoundsFull;
			baseValues[TurretType.MISSILE] = missileRoundsFull;
			
			roundValues[TurretType.INTERIOR] = SHOTS_PER_MAGAZINE;
			roundValues[TurretType.GATLING] = SHOTS_PER_AMMOBOX;
			roundValues[TurretType.MISSILE] = 1;
			
			List<IMyTextPanel> li = new List<IMyTextPanel>();
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(li, b => b.CustomName.Contains(AMMO_DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			foreach (IMyTextPanel scr in li) {
				ammoDisplays.Add(new Display(scr));
			}
			li = new List<IMyTextPanel>();
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(li, b => b.CustomName.Contains(TURRET_DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			foreach (IMyTextPanel scr in li) {
				turretDisplays.Add(new Display(scr));
			}
			
			GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(containers, b => (b is IMyCargoContainer || b is IMyLargeTurretBase) && b.CubeGrid == Me.CubeGrid);
			
			List<IMyLargeTurretBase> li2 = new List<IMyLargeTurretBase>();
			GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(li2, b => b.CubeGrid == Me.CubeGrid);			
			foreach (IMyLargeTurretBase tur in li2) {
				TurretType type = getTurretType(tur);
				if (type != TurretType.UNKNOWN) {
					currentTurrets.Add(type);
					turrets.Add(tur);
				}
			}
		}
		
		private TurretType getTurretType(IMyLargeTurretBase tur) {
			if (tur is IMyLargeMissileTurret || tur is IMySmallMissileLauncher)
				return TurretType.MISSILE;
			if (tur is IMyLargeGatlingTurret)
				return TurretType.GATLING;
			if (tur is IMyLargeInteriorTurret)
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
			fractions.Clear();
			
			foreach (IMyEntity io in containers) {
				IMyInventory inv = io.GetInventory();
				List<MyInventoryItem> li = new List<MyInventoryItem>();
				inv.GetItems(li);
				foreach (MyInventoryItem ii in li) {
					TurretType ammo = isAmmoItem(ii);
					if (ammo != TurretType.UNKNOWN) {
						locale[ammo] = ii.Type.SubtypeId;
						//Echo(ii.Type.ToString()+" > "+ammo);
						if (currentTurrets.Contains(ammo)) {
							int amt = 0;
							itemCounts.TryGetValue(ammo, out amt);
							int has = ii.Amount.ToIntSafe();
							amt += has;
							itemCounts[ammo] = amt;
							//Echo(ammo+" > "+amt);
						}
					}
				}
			}
			
			foreach (Display scr in ammoDisplays) {
				scr.prepare();
			}	
			
			foreach (Display scr in turretDisplays) {
				scr.prepare();
			}	
			
			foreach (TurretType type in itemCounts.Keys) {
				fractions[type] = Math.Min(1F, itemCounts[type]*roundValues[type]/(float)baseValues[type]);
				Echo(type+" > "+itemCounts[type]+" > "+fractions[type]);
				showStatus(type);
			}
			
			foreach (Display scr in ammoDisplays) {
				scr.write("");
			}
			
			foreach (IMyLargeTurretBase tur in turrets) {
				IMyInventory inv = tur.GetInventory();
				float f = inv.CurrentVolume == 0 ? 0 : inv.CurrentVolume.RawValue/(float)inv.MaxVolume.RawValue;
				if (tur is IMyLargeInteriorTurret) {
					f *= (float)inv.MaxVolume.RawValue/0.01F;
				}
				if (f <= 0.01) {
					//Echo(tur.CustomName+" > "+inv.CurrentVolume+"/"+inv.MaxVolume);
					foreach (Display scr in turretDisplays) {
						scr.setColor(Color.Red);
						scr.write("Turret "+tur.CustomName+" is empty!");
					}
				}
				else if (f <= 0.25) {
					//Echo(tur.CustomName+" > "+inv.CurrentVolume+"/"+inv.MaxVolume);
					foreach (Display scr in turretDisplays) {
						scr.setColor(Color.Yellow);
						scr.write("Turret "+tur.CustomName+" is low!");
					}
				}
			}
		}
		
		private string localize(TurretType key) {
			string ret = key.ToString();
			locale.TryGetValue(key, out ret);
			return ret;
		}
			
		private void showStatus(TurretType t) {
			string s = locale[t];
			int lines = currentTurrets.Count;
			float size = 1;
			if (lines > 18) {
				size -= (lines-18)*0.04F;
			}
			size = Math.Min(size, 0.67F);
			int maxSections = 40; //assuming zero padding and zero name length
			float ds = size-0.5F;
			maxSections = (int)(maxSections-16F*ds);
			int maxw = (maxSections/2);
			int pad = maxw-s.Length;
			int barSize = maxw;
			float f = fractions[t];
			int fill = (int)(f*barSize+0.5);
			int red = barSize/4;
			int yellow = barSize/2;
			foreach (Display scr in ammoDisplays) {
				scr.addLine(s, f, barSize, size, pad, red, yellow, fill);
			}
		}
		
		internal class Display {
			
			private readonly IMyTextPanel block;
			private bool tick;
			
			internal Display(IMyTextPanel b) {
				block = b;
			}
			
			internal void setColor(Color c) {
				block.FontColor = c;
			}
			
			internal void prepare() {
				block.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
				block.WriteText("");
				block.BackgroundColor = Color.Black;
				block.FontColor = Color.White;
				block.FontSize = 1.6F;
			}
			
			internal void write(string s) {
				block.WriteText(s, true);
				block.WriteText("\n", true);
			}
			
			internal void addLine(string s, float frac, int barSize, float size, int pad, int red, int yellow, int fill) {
				tick = !tick;
				block.FontSize = size;
				block.Font = "Monospace";
				String line = s+":";
				for (int i = 0; i < pad; i++) {
					int p = i+s.Length;
					line = line+(i == 0 || i == pad-1 || p%2 == 0 ? " " : dot);
				}
				for (int i = 0; i < barSize; i++) {
					bool has = i < fill;
					string color = has ? (i < red ? redArrow : (i < yellow ? yellowArrow : greenArrow)) : whiteArrow;
					if (frac <= 0.05)
						color = tick ? redArrow : whiteArrow;
					line = line+color;
				}
				block.WriteText(line+"\n", true);
			}
				
		}
		
		internal enum TurretType {
			INTERIOR,
			GATLING,
			MISSILE,
			UNKNOWN,
		}
		
		//====================================================
	}
}