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
using IMySolarPanel = SpaceEngineers.Game.ModAPI.Ingame.IMySolarPanel;

namespace Ingame_Scripts.ConstructionStationControl {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------		
		//Whether to automatically determine the direction of the gate by counting grav gens.
		//If false, assumes directionality inline wiht the programmable block running this script.
		readonly string[] WELDER_LIGHT_GROUPS = {"Welder"};
		readonly string[] GRINDER_LIGHT_GROUPS = {"Grinder"};
		
		const float BLINK_RATE = 1.5F; //How fast the lights should blink.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMySensorBlock> grinderSensors = new List<IMySensorBlock>();
		private readonly List<IMySensorBlock> welderSensors = new List<IMySensorBlock>();
		
		private readonly List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
		private readonly List<IMyShipWelder> welders = new List<IMyShipWelder>();
		
		private readonly List<Light> welderLights = new List<Light>();
		private readonly List<Light> grinderLights = new List<Light>();
		
		private bool grinderActive;
		private bool welderActive;
		private int tick;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update1;
			
			foreach (string s in GRINDER_LIGHT_GROUPS) {
				IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(s);
				if (grp != null) {
					List<IMyLightingBlock> li = new List<IMyLightingBlock>();
					grp.GetBlocksOfType<IMyLightingBlock>(li);
					foreach (IMyLightingBlock b in li) {
						grinderLights.Add(new Light(b));
					}
				}
			}
			
			foreach (string s in WELDER_LIGHT_GROUPS) {
				IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(s);
				if (grp != null) {
					List<IMyLightingBlock> li = new List<IMyLightingBlock>();
					grp.GetBlocksOfType<IMyLightingBlock>(li);
					foreach (IMyLightingBlock b in li) {
						welderLights.Add(new Light(b));
					}
				}
			}
			
			IMyBlockGroup sns = GridTerminalSystem.GetBlockGroupWithName("Welder Sensors");
			if (sns != null)
				sns.GetBlocksOfType<IMySensorBlock>(welderSensors);
			
			sns = GridTerminalSystem.GetBlockGroupWithName("Grinder Sensors");
			if (sns != null)
				sns.GetBlocksOfType<IMySensorBlock>(grinderSensors);
			
			GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(welders, b => b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyShipGrinder>(grinders, b => b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() {
			bool welder = welderActive;
			bool grinder = grinderActive;
			if (tick%60 == 0) { //every 1s
				grinderActive = isSensorActiveNeeded(false);
				welderActive = isSensorActiveNeeded(true);
				Echo("Grinders Active: "+grinderActive);
				Echo("Welders Active: "+welderActive);
				setState(grinderActive, false);
				setState(welderActive, true);
			}
			if (grinder != grinderActive) {
				foreach (Light l in grinderLights) {
					if (!l.isSpotlight) {
						//l.light.Radius = l.calcIntensity(tick);
						l.light.BlinkIntervalSeconds = grinderActive ? BLINK_RATE : 0;
					}
				}
			}
			if (welder != welderActive) {
				foreach (Light l in welderLights) {
					if (!l.isSpotlight) {
						//l.light.Radius = l.calcIntensity(tick);
						l.light.BlinkIntervalSeconds = welderActive ? BLINK_RATE : 0;
					}
				}
			}
			tick++;
		}
			
		private bool isSensorActiveNeeded(bool welder) {
			List<MyDetectedEntityInfo> li = new List<MyDetectedEntityInfo>();
			foreach (IMySensorBlock sensor in welder ? welderSensors : grinderSensors) {
				sensor.DetectEnemy = true;
				sensor.DetectNeutral = true;
				sensor.DetectFriendly = true;
				sensor.DetectOwner = true;
				sensor.DetectFloatingObjects = false;
				sensor.DetectLargeShips = true;
				sensor.DetectSmallShips = true;
				sensor.DetectStations = true;
				sensor.DetectSubgrids = false;
				sensor.DetectPlayers = false;
				sensor.DetectedEntities(li);
				if (li.Count > 0)
					return true;
			}
			return false;
		}
		
		private void setState(bool on, bool welder) {
			if (welder) {
				foreach (IMyTerminalBlock b in welders) {
					if (on) {
						b.ApplyAction("OnOff_On");
					}
					else {
						b.ApplyAction("OnOff_Off");
					}
				}
			}
			else {
				foreach (IMyTerminalBlock b in grinders) {
					if (on) {
						b.ApplyAction("OnOff_On");
					}
					else {
						b.ApplyAction("OnOff_Off");
					}
				}
			}
		}
		
		internal class Light {
			
			internal readonly IMyLightingBlock light;
			internal readonly bool isSpotlight;
			
			private readonly float maxIntensity;
			private readonly float maxRadius;
			
			private static readonly Random rand = new Random();
			
			internal Light(IMyLightingBlock b) {
				light = b;
				b.BlinkIntervalSeconds = 0;
				b.BlinkLength = 100*0.5F;
				b.BlinkOffset = 100*(float)rand.NextDouble();
				
				isSpotlight = b.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("frontlight");
				
				b.Intensity = 20;
				b.Radius = 200;
				b.Falloff = 4;
				
				maxIntensity = b.Intensity;
				maxRadius = b.Radius;
			}
		}
		
		//====================================================
	}
}