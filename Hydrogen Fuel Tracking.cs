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

namespace Ingame_Scripts.H2Tracking {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "Hydrogen"; //Any LCD panel with this in its name is overridden to show the fill status
		
		static readonly Color textColor = new Color(60, 192, 255, 255);
		
		static bool isDedicatedDisplay(string name) {
			return name.Contains("Dedicated");
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
				
		private readonly List<Display> displays = new List<Display>();
		private readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			List<IMyTextPanel> li = new List<IMyTextPanel>();
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(li, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			foreach (IMyTextPanel scr in li) {
				displays.Add(new Display(scr, isDedicatedDisplay(scr.CustomName)));
			}
			
			GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks, b => b.CustomName.Contains("Hydrogen") && b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			double max = 0;
			double cur = 0;
			foreach (IMyGasTank io in tanks) {
				max += io.Capacity;
				cur += io.Capacity*io.FilledRatio;
			}
			double avg = cur/max*100;
			
			foreach (Display scr in displays) {
				scr.prepare();
				if (avg <= 15) {
					scr.setColor(Color.Red);
				}
				else if (avg <= 25) {
					scr.setColor(Color.Yellow);
				}
				scr.write("Hydrogen reserves at "+avg+"%.");				
				scr.show();
			}
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
		
		//====================================================
	}
}