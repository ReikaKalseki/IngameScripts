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

namespace Ingame_Scripts.RunwayLights {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const int tickRate = 1; //how often the script should tick, measured in this many game ticks (which are 1/60th of a second apart). Valid values are 1, 10, 100. Lower is smoother but harder on performance.
		const String LIGHT_RUNWAY_GROUP = "Runway"; //The name of the block group all runway lights are in.
		const int xAxisSpeed = 1; //How much time (in ticks) offset per block X to add 
		const int yAxisSpeed = 0; //How much time (in ticks) offset per block Y to add 
		const int zAxisSpeed = 0; //How much time (in ticks) offset per block Z to add 
		
		//The list of colors to cycle through, defined with a color and a duration. Note that black will disable the light.
		ColorStage[] colorList = {new ColorStage(0xffffff, 5), new ColorStage(0x000000, 55)};
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------		
		private readonly List<Light> lights = new List<Light>();
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
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(LIGHT_RUNWAY_GROUP);
			if (grp != null) {
				List<IMyLightingBlock> li = new List<IMyLightingBlock>();
				grp.GetBlocksOfType<IMyLightingBlock>(li, b => b.CubeGrid == Me.CubeGrid);
				foreach (IMyLightingBlock b in li) {
					lights.Add(new Light(b));
				}
			}
		}
		
		public void Main() { //called each cycle					
			foreach (Light l in lights) {
				l.tick(colorList);
			}
			tick++;
		}
		
		private static VRageMath.Color convertColor(int hex) {
			int blue = hex & 0xFF;
			int green = (hex >> 8) & 0xFF;
			int red = (hex >> 16) & 0xFF;
			return new VRageMath.Color(red, green, blue, 255);
		}
		
		internal struct ColorStage {
			
			internal readonly int color;
			internal readonly int duration;
			
			internal ColorStage(int c) : this(c, 1) {
				
			}
			
			internal ColorStage(int c, int dur) {
				color = c;
				duration = dur;
			}
			
		}
		
		internal class Light {
			
			private readonly IMyLightingBlock light;
			private readonly int offset;
			
			private ColorStage color;
			private int index;
			private int ticksOnStage;
			
			public Light(IMyLightingBlock b) {
				light = b;
				Vector3I pos = b.Position;
				offset = (pos.X*xAxisSpeed+pos.Y*yAxisSpeed+pos.Z*zAxisSpeed);
			}
			
			public void tick(ColorStage[] colors) {
				ticksOnStage++;
				if (ticksOnStage >= color.duration) {
					index++;
					if (index >= colors.Length)
						index = 0;
					color = colors[index];
				}
			}
			
			private void apply() {
				light.Color = convertColor(color.color);
				light.Enabled = color.color > 0;
			}
			
		}
		
		//====================================================
	}
}