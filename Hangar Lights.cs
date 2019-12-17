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

using IMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;
using IMyAirVent = SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent;
//using IMySpotlight = SpaceEngineers.Game.ModAPI.Ingame.IMySpotlight;

namespace Ingame_Scripts.HangarLights {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "Hangar Beam"; //Any spotlight with this in its name will be color-controlled
		const string AUX_GROUP_TAG = "Hangar Lights"; //Any lighting block in this group will be counted as an auxiliary light
		const string VENT_GROUP_TAG = "Hangar Vent"; //Any vent with this in its name will be c	ounted as a Hangar supply vent and will be consulted for oxygen levels
		const float RED_THRESHOLD = 10; //Spotlights will be red if air level is below this percentage
		const float GREEN_THRESHOLD = 98; //And green if it is above this; they will be yellow if it is in between
		const float AMBIENT_LIGHT_THRESHOLD = GREEN_THRESHOLD; //Any auxiliary lights in the hangar will be toggled on and off, being on if the oxygen level is above this
		const bool RED_IF_PRESSURIZED = true; //Enable this to flip red and green, so that instead of an air level indicator, you get a "safe to open the door" indicator.
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyLightingBlock> auxLights = new List<IMyLightingBlock>();
		private readonly List<IMyLightingBlock> spotlights = new List<IMyLightingBlock>();
		private readonly List<IMyAirVent> vents = new List<IMyAirVent>();
		
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(AUX_GROUP_TAG);
			GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(spotlights, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == Me.CubeGrid);
			if (grp != null)
				grp.GetBlocksOfType<IMyLightingBlock>(auxLights, b => b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(vents, b => b.CustomName.Contains(VENT_GROUP_TAG) && b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			/*
			foreach (IMySpotlight light in spotlights) {
				light.SetValue("Color", new VRageMath.Color(255, 0, 0, 255));
			}*/
			
			float f = getAirLevel();	
			Echo("Hangar Air Level: "+f+"%");		
			Color clr = getColor(f);		
			Echo("Light is: "+clr.ToString());
			foreach (IMyLightingBlock light in spotlights) {
				light.Color = clr;
			}
			
			foreach (IMyLightingBlock light in auxLights) {
				light.Enabled = f >= AMBIENT_LIGHT_THRESHOLD;
			}
		}
		
		private float getAirLevel() {
			float sum = 0;			
			foreach (IMyAirVent vent in vents) {
				sum += vent.GetOxygenLevel();
			}			
			return sum*100F/vents.Count;
		}
		
		private VRageMath.Color getColor(float f) {
			if (f <= RED_THRESHOLD)
				return RED_IF_PRESSURIZED ? new VRageMath.Color(0, 255, 0, 255) : new VRageMath.Color(255, 0, 0, 255);
			else if (f <= GREEN_THRESHOLD)
				return new VRageMath.Color(255, 255, 0, 255);
			else
				return RED_IF_PRESSURIZED ? new VRageMath.Color(255, 0, 0, 255) : new VRageMath.Color(0, 255, 0, 255);
		}
		
		//====================================================
	}
}