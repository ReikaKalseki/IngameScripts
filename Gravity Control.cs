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

using IMyGravityGenerator = SpaceEngineers.Game.ModAPI.Ingame.IMyGravityGenerator;
using IMyArtificialMassBlock = SpaceEngineers.Game.ModAPI.Ingame.IMyArtificialMassBlock;

namespace Ingame_Scripts.GravityControl {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "[GravityStatus]"; //Any LCD panel with this in its name is overridden to show the gravity status
		
		const float TARGET_GRAVITY = 0.4F; //The desired net gravity level in Gs
		const float MAX_ORE_GRAVITY = 0.05F; //The maximum gravity at which Ore Detectors are enabled. Not recommended to be anything above 0.05 for long-range modded detectors.
		
		const bool SPLIT_GRAVITY = false; //Whether to split the desired artificial gravity equally among all generators. Ie, do their ranges substantially overlap?
		const bool MANNED_ORE = true; //Whether to require that the ship be manned for ore detectors to be active.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyGravityGenerator> gravGens = new List<IMyGravityGenerator>();
		private readonly List<IMyFunctionalBlock> gravDrives = new List<IMyFunctionalBlock>();
		private readonly List<IMyOreDetector> oreDetectors = new List<IMyOreDetector>();
		private readonly List<IMyShipController> cockpits = new List<IMyShipController>();
		
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		
		internal Base6Directions.Direction upDirection;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock>(gravDrives, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains("Gravity Drive"));
			List<IMyFunctionalBlock> li = new List<IMyFunctionalBlock>();
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName("Gravity Drive");
			if (grp != null) {
				grp.GetBlocksOfType<IMyFunctionalBlock>(li);
			}
			gravDrives.AddList(li);
			GridTerminalSystem.GetBlocksOfType<IMyGravityGenerator>(gravGens, b => b.CubeGrid == Me.CubeGrid && !gravDrives.Contains(b));
			GridTerminalSystem.GetBlocksOfType<IMyOreDetector>(oreDetectors, b => b.CubeGrid == Me.CubeGrid && !b.CustomName.Contains("Cloaking"));
			GridTerminalSystem.GetBlocksOfType<IMyShipController>(cockpits, b => b.CubeGrid == Me.CubeGrid);
			
			upDirection = Me.Orientation.Up;
			Dictionary<Base6Directions.Direction, int> counts = new Dictionary<Base6Directions.Direction, int>();
			foreach (IMyShipController control in li) {
				Base6Directions.Direction dir = control.Orientation.Up;
				int has = 0;
				counts.TryGetValue(dir, out has);
				has++;
				counts[dir] = has;
			}
			int max = 0;
			Base6Directions.Direction found = upDirection;
			foreach (var entry in counts) {
				if (entry.Value > max) {
					max = entry.Value;
					found = entry.Key;
				}
			}
			upDirection = found;
		}
		
		public void Main() {
			foreach (IMyTextPanel scr in displays) {
				scr.WritePublicText(""); //clear
			}
			
			bool manned;
			float natural = getNaturalGravity(out manned);
			float net = Math.Max(0, TARGET_GRAVITY-natural);
			setArtificialGravity(natural, net);
			if (net > 0)
				show("Natural gravity is "+natural+" G, of a target "+TARGET_GRAVITY+" G; Artificial gravity set to "+net+" G.");
			else
				show("Natural gravity is "+natural+" G, of a target "+TARGET_GRAVITY+" G; Artificial gravity disabled.");
			
			show("Ship is manned: "+manned);
			bool ores = natural <= MAX_ORE_GRAVITY && (!MANNED_ORE || manned);
			setOreDetectors(ores);
			show("Ore detectors enabled: "+ores);
		}
		
		private bool gravDriveOn() {
			foreach (IMyFunctionalBlock gen in gravDrives) {					
				if (gen.Enabled)
					return true;
			}
			return false;
		}
		
		private float getAGravEfficacy(float gnat) {
			return 1-(gnat/9.81F)*2; //"For every 1% of natural gravity, your gravity generators will be reduced by 2%"
		}
		
		private void setOreDetectors(bool on) {
			foreach (IMyOreDetector det in oreDetectors) { 
				det.Enabled = on;
			}
		}
		
		private void setArtificialGravity(float gnat, float g) {
			float eff = getAGravEfficacy(gnat);
			float f = eff <= 0 ? 0 : 1F/eff;
			if (SPLIT_GRAVITY) {
				f /= gravGens.Count;
			}
			if (gravDriveOn()) {
				Echo("Gravity Drive is working, all other gravity disabled.");
				f = 0;
			}
			else {
				Echo("Setting gravity generator level to "+f*100+"% in direction "+upDirection.ToString());
			}
			foreach (IMyGravityGenerator gen in gravGens) {					
				if (f <= 0 || g <= 0) {
					gen.Enabled = false;
				}
				else {
					float sign = 0;
					if (gen.Orientation.Up == upDirection) {
						sign = 1;
					}
					else if (gen.Orientation.Up == Base6Directions.GetFlippedDirection(upDirection)) {
						sign = -1;
					}
					float amt = g*9.81F*f*sign;
					gen.Enabled = Math.Abs(amt) > 0;
					gen.GravityAcceleration = amt;
				}
			}
		}
		
		private float getNaturalGravity(out bool manned) {
			manned = false;
			double gmax = 0;
			foreach (IMyShipController pit in cockpits) { 
				gmax = Math.Max(gmax, pit.GetNaturalGravity().Length());
				manned |= pit.IsUnderControl;
			}
			return (float)(gmax/9.81F);
		}
		
		private void show(string text) {
			foreach (IMyTextPanel scr in displays) {
				scr.WritePublicText(text+"\n", true);
				scr.ShowPublicTextOnScreen();
			}
			Echo(text);
		}
		
		//====================================================
	}
}