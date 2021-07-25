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

namespace Ingame_Scripts.StationProx {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------		
		const string SENSOR_GROUP = "Player Sensors"; //The name of the block group containing the sensors looking for nearby players to activate the station
		readonly string[] NEAR_LIGHTS = {"Indoor Lights"}; //The names of block groups for lights to be deactivated unless players are near
		readonly string[] FAR_LIGHTS = {"Ring Lights"}; //The names of block groups for lights to be deactivated if players are near
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyGravityGenerator> gravGens = new List<IMyGravityGenerator>();
		private readonly List<IMyLightingBlock> nearLights = new List<IMyLightingBlock>();
		private readonly List<IMyLightingBlock> farLights = new List<IMyLightingBlock>();
		private readonly List<IMySensorBlock> sensors = new List<IMySensorBlock>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			collectLights(NEAR_LIGHTS, nearLights);
			collectLights(FAR_LIGHTS, farLights);
			
			GridTerminalSystem.GetBlocksOfType<IMyGravityGenerator>(gravGens, b => b.CubeGrid == Me.CubeGrid);
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(SENSOR_GROUP);
			if (grp != null) {
				grp.GetBlocksOfType<IMySensorBlock>(sensors);
			}
		}
		
		private void collectLights(string[] groups, List<IMyLightingBlock> li) {
			foreach (string s in groups) {
			List<IMyLightingBlock> has = new List<IMyLightingBlock>();
				IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(s);
				if (grp != null) {
					grp.GetBlocksOfType<IMyLightingBlock>(has);
					li.AddList(has);
				}
			}
		}
		
		public void Main() {			
			bool near = isPlayerNear();
			Echo("Players are near: "+near);
			foreach (IMyGravityGenerator gen in gravGens) {
				gen.Enabled = near;
			}
			foreach (IMyLightingBlock light in nearLights) {
				light.Enabled = near;
			}
			foreach (IMyLightingBlock light in farLights) {
				light.Enabled = !near;
			}
		}
	
		private bool isPlayerNear() {
			List<MyDetectedEntityInfo> li = new List<MyDetectedEntityInfo>();
			foreach (IMySensorBlock sensor in sensors) {
				sensor.DetectEnemy = false;
				sensor.DetectFloatingObjects = false;
				sensor.DetectFriendly = true;
				sensor.DetectLargeShips = false;
				sensor.DetectSmallShips = false;
				sensor.DetectStations = false;
				sensor.DetectSubgrids = false;
				sensor.DetectPlayers = true;
				sensor.DetectNeutral = false;
				sensor.DetectAsteroids = false;
				sensor.DetectOwner = true;
				sensor.DetectedEntities(li);
				if (li.Count > 0)
					return true;
			}
			return false;
		}
		
		//====================================================
	}
}