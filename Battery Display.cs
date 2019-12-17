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

namespace Ingame_Scripts.BatteryDisplay {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		public static bool isBatteryDisplay(string name) { //whether a given LCD panel on the ship/station should be used to display battery levels. On a solar recharging station, that might be any. On 
			// some other structure, maybe only ones whose name contains "BatteryDisplay" (name.Contains("BatteryDisplay")).
			return true;
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();		
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CubeGrid == Me.CubeGrid && isBatteryDisplay(b.CustomName));
			GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, b => b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() {
			foreach (IMyTextPanel scr in displays) {
				scr.WritePublicText(""); //clear
			}
			
			int percent = getTotalPercent();
			foreach (IMyTextPanel scr in displays) {
				displayPercentOnScreen(scr, percent);
			}
		}
		
		/*
		private VRageMath.Color getBCGColor(int per) {
			if (per <= 10)
				return new VRageMath.Color(64, 0, 0, 255);
			else
				return new VRageMath.Color(0, 0, 0, 255);
		}
		
		private VRageMath.Color getDisplayColor(int per) {
			if (per <= 10)
				return new VRageMath.Color(255, 255, 255, 255);
			else if (per <= 25)
				return new VRageMath.Color(255, 0, 0, 255);
			else if (per <= 40)
				return new VRageMath.Color(255, 127, 0, 255);
			else if (per <= 60)
				return new VRageMath.Color(255, 255, 0, 255);
			else if (per <= 80)
				return new VRageMath.Color(127, 255, 0, 255);
			else if (per <= 95)
				return new VRageMath.Color(0, 255, 0, 255);
			else
				return new VRageMath.Color(64, 192, 255, 255);
		}*/
		
		private VRageMath.Color getDisplayColor(int per) {
			return new VRageMath.Color(255, 255, 255, 255);
		}
		
		private VRageMath.Color getBCGColor(int per) {
			if (per <= 10)
				return new VRageMath.Color(255/3, 0, 0, 255);
			else if (per <= 25)
				return new VRageMath.Color(255/3, 64/3, 0, 255);
			else if (per <= 40)
				return new VRageMath.Color(255/3, 144/3, 0, 255);
			else if (per <= 60)
				return new VRageMath.Color(255/3, 255/3, 0, 255);
			else if (per <= 80)
				return new VRageMath.Color(127/3, 255/3, 0, 255);
			else if (per <= 95)
				return new VRageMath.Color(0, 255/3, 0, 255);
			else
				return new VRageMath.Color(64/3, 192/3, 255/3, 255);
		}
		
		//Display text as a percentage bar and numeric text 
		private void displayPercentOnScreen(IMyTextPanel scr, int per) {  
		    int x = 100;  
		    int y = 0;
			scr.WritePublicText(""); //clear
			scr.FontSize = scr.BlockDefinition.SubtypeName.Contains("Wide") ? 2.713F : 1.3565F; //size of 2.713 for wide, half that for normal
			scr.FontColor = getDisplayColor(per);
			scr.BackgroundColor = getBCGColor(per);
			//scr.WritePublicTitle("Battery Fraction:");
			scr.WritePublicText("Battery Fraction:");
		    scr.WritePublicText("\n", true);
		    scr.WritePublicText("[", true);  
		    for (int i = per; i > 0; i -= 2) {  
		        scr.WritePublicText("|",true);  
		        y += 2;  
		    }  
		    for (int i = x - y; i > 0; i -= 2) {  
		        scr.WritePublicText("'", true);  
		    }  
		    scr.WritePublicText("] (" + per + "%)\n", true); 
			scr.ShowTextureOnScreen();
			scr.ShowPublicTextOnScreen();
		}
		
		private int getTotalPercent() {
			float c = 0;
			float s = 0;
			foreach (IMyBatteryBlock bat in batteries) {							
				c += bat.CurrentStoredPower;
				s += bat.MaxStoredPower;
			}
			return (int)(c*100/s);
		}
		
		//====================================================
	}
}