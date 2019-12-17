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

namespace Ingame_Scripts.LightWaves {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const int tickRate = 1; //how often the script should tick, measured in this many game ticks (which are 1/60th of a second apart). Valid values are 1, 10, 100. Lower is smoother but harder on performance.
		const String LIGHT_WAVE_GROUP = "Lightwave"; //The name of the block group all light-wave lights are in.
		const float xAxisSpeed = 1; //How fast the waves should move along the X axis; zero means X value has no effect on phase 
		const float yAxisSpeed = 0; //How fast the waves should move along the Y axis; zero means Y value has no effect on phase 
		const float zAxisSpeed = 0; //How fast the waves should move along the Z axis; zero means Z value has no effect on phase 
		const float cycleSpeed = 1/20F; //Simple speed multiplier
		
		int[] colorList = {0xff0000, 0xff7f00, 0xffff00, 0x00ff00, 0x00ffff, 0x22aaff, 0x0000ff, 0x7f00f};
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		private LightState getLightState(bool piloted, bool landed, bool inSpace) { //Automatic light control based on ship state
			return LightState.COLORS;
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private enum LightState {
			OFF,
			WHITE,
			COLORS,
		};
		
		private readonly List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
		private int tick;
		
		public Program() {
			switch(tickRate) {
				case 1:
					Runtime.UpdateFrequency = UpdateFrequency.Update1;
					break;
				case 10:
					Runtime.UpdateFrequency = UpdateFrequency.Update10;
					break;
				case 100:
					Runtime.UpdateFrequency = UpdateFrequency.Update100;
					break;
			}
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(LIGHT_WAVE_GROUP);
			if (grp != null)
				grp.GetBlocksOfType<IMyLightingBlock>(lights, b => b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			/*
			foreach (IMySpotlight light in spotlights) {
				light.SetValue("Color", new VRageMath.Color(255, 0, 0, 255));
			}*/
					
			foreach (IMyLightingBlock light in lights) {
				light.Color = getColor(light as IMyCubeBlock);
			}
			tick++;
		}
		
		private VRageMath.Color getColor(IMyCubeBlock block) {
			Vector3I pos = block.Position;
			float phase = tick*cycleSpeed+(pos.X*xAxisSpeed+pos.Y*yAxisSpeed+pos.Z*zAxisSpeed);
			float f = ((phase%colorList.Length)+colorList.Length)%colorList.Length;
			int ceil = (int)Math.Ceiling(f);
			int floor = (int)Math.Floor(f);
			f -= floor;
			//Echo(tick+" > "+f+" between "+floor+" and "+ceil+" of "+colorList.Length);
			Color c1 = convertColor(colorList[floor]);
			Color c2 = convertColor(ceil == colorList.Length ? colorList[0] : colorList[ceil]);
			return Color.Lerp(c1, c2, f);
		}
		
		private VRageMath.Color convertColor(int hex) {
			int blue = hex & 0xFF;
			int green = (hex >> 8) & 0xFF;
			int red = (hex >> 16) & 0xFF;
			return new VRageMath.Color(red, green, blue, 255);
		}
		
		//====================================================
	}
}