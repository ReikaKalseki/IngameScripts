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

using VRage.Game.GUI.TextPanel;

using IMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;
//using MyDetectedEntityInfo = SpaceEngineers.Game.ModAPI.Ingame.MyDetectedEntityInfo;
using IMyAirVent = SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent;
using IMySoundBlock = SpaceEngineers.Game.ModAPI.Ingame.IMySoundBlock;
//using IMySpotlight = SpaceEngineers.Game.ModAPI.Ingame.IMySpotlight;

namespace Ingame_Scripts.AlienBaseMonitoring {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string SENSOR_TAG = "Alien Sensor"; //Any sensor with this in its name will be checked for hostiles
		const string SCREEN_TAG = "AlienStatus"; //Any LCD panel with this in its name will show the status of hostile detection
		const string LOCKDOWN_TAG = "Lockdown"; //Any door with this in its name will be closed if hostiles are detected.
		const string ALARM_TAG = "AlienSound"; //Any sound block with this in its name will play alarm sounds when hostiles are detected.
		const string LIGHT_TAG = "AlienLight"; //Any light with this in its name will be activated if hostiles are detected.
		
		const int MAXIMUM_RADIUS = 500; //The maximum distance to detect enemies
		const int LOCKDOWN_RADIUS = 200; //The distance at which to initiate a lockdown
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------

		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//---------------------------------------------------------------------------------------------------------------
			
		private readonly List<IMySensorBlock> sensors = new List<IMySensorBlock>();
		private readonly List<IMyTextPanel> screens = new List<IMyTextPanel>();
		private readonly List<IMyDoor> lockdown = new List<IMyDoor>();
		private readonly List<IMySoundBlock> alarms = new List<IMySoundBlock>();
		private readonly List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, b => b.CustomName.Contains(SENSOR_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens, b => b.CustomName.Contains(SCREEN_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyDoor>(lockdown, b => b.CustomName.Contains(LOCKDOWN_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(alarms, b => b.CustomName.Contains(ALARM_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, b => b.CustomName.Contains(LIGHT_TAG) && b.CubeGrid == Me.CubeGrid);
			
			foreach (IMySensorBlock sensor in sensors) {
				sensor.DetectEnemy = true;
				sensor.DetectFloatingObjects = false;
				sensor.DetectAsteroids = false;
				sensor.DetectOwner = false;
				sensor.DetectFriendly = false;
				sensor.DetectLargeShips = false;
				sensor.DetectSmallShips = false;
				sensor.DetectStations = false;
				sensor.DetectSubgrids = false;
				sensor.DetectPlayers = true;
				sensor.DetectNeutral = false;
			}
		}
		
		public void Main() { //called each cycle
			
		}
			
		private int getEnemyDistance(int max) {
			List<MyDetectedEntityInfo> li = new List<MyDetectedEntityInfo>();
			foreach (IMySensorBlock sensor in sensors) {
				sensor.DetectedEntities(li);
				if (li.Count > 0) {
					
				}
			}
			return -1;
		}
		
		//====================================================
	}
}