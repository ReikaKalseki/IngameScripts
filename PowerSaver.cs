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
//using MyDetectedEntityInfo = SpaceEngineers.Game.ModAPI.Ingame.MyDetectedEntityInfo;
using IMyAirVent = SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent;
using IMyLandingGear = SpaceEngineers.Game.ModAPI.Ingame.IMyLandingGear;
//using IMySpotlight = SpaceEngineers.Game.ModAPI.Ingame.IMySpotlight;
using IMyGravityGenerator = SpaceEngineers.Game.ModAPI.Ingame.IMyGravityGenerator;

namespace Ingame_Scripts.PowerSaver {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "[PowerSaveStatus]"; //Any LCD panel with this in its name is overridden to show the gravity status
		
		const string EXTERNAL_VENT_TAG = "External Air Vent"; //Any vent with this in its name will be counted as an external vent and will be consulted for atmospheric oxygen levels
		const string INTERIOR_LIGHT_GROUP = "Interior Lights"; //The name of the block group of all interior lights
		const string HYDROGEN_THRUSTER_GROUP = "H2 Thrusters"; //The name of the block group of the hydrogen thrusters
		const string ATMO_THRUSTER_GROUP = "Atmo Jets"; //The name of the block group of the atmospheric thrusters
		const string ION_THRUSTER_GROUP = "Ion Jets"; //The name of the block group of the ion thrusters
		
		const float ION_THRESHOLD = 0.75F; //The minimum allowable efficiency for ion engines to operate. Bottoms out at 0.3 (30%) in 100% atmosphere, and is 100% at 0% atmo. 
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyShipController> seats = new List<IMyShipController>();
		private readonly List<IMyAirVent> externalVents = new List<IMyAirVent>();
		private readonly List<IMyLandingGear> gear = new List<IMyLandingGear>();
		
		private readonly List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
		private readonly List<IMyGravityGenerator> gravGens = new List<IMyGravityGenerator>();
		
		private readonly List<IMyThrust> h2Thrusters = new List<IMyThrust>();
		private readonly List<IMyThrust> ionThrusters = new List<IMyThrust>();
		private readonly List<IMyThrust> atmoThrusters = new List<IMyThrust>();
		
		private const float ATMO_ZERO = 0.3F; //Atmo thrusters at zero power at 30% atmo
		private readonly float ION_ZERO = getIonEfficiencyInverse(ION_THRESHOLD);
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(externalVents, b => b.CustomName.Contains(EXTERNAL_VENT_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyShipController>(seats, b => b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyLandingGear>(gear, b => b.CubeGrid == Me.CubeGrid);
			
			GridTerminalSystem.GetBlocksOfType<IMyGravityGenerator>(gravGens, b => b.CubeGrid == Me.CubeGrid);
			
			IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(INTERIOR_LIGHT_GROUP);
			if (grp != null)
				grp.GetBlocksOfType<IMyLightingBlock>(lights, b => b.CubeGrid == Me.CubeGrid);
			
			grp = GridTerminalSystem.GetBlockGroupWithName(HYDROGEN_THRUSTER_GROUP);
			if (grp != null)
				grp.GetBlocksOfType<IMyThrust>(h2Thrusters, b => b.CubeGrid == Me.CubeGrid);
			grp = GridTerminalSystem.GetBlockGroupWithName(ATMO_THRUSTER_GROUP);
			if (grp != null)
				grp.GetBlocksOfType<IMyThrust>(atmoThrusters, b => b.CubeGrid == Me.CubeGrid);
			grp = GridTerminalSystem.GetBlockGroupWithName(ION_THRUSTER_GROUP);
			if (grp != null)
				grp.GetBlocksOfType<IMyThrust>(ionThrusters, b => b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			float atmo = getAtmoAirLevel();
			bool pilot = hasPilot();
			bool docked = landed();
		}
		
		private static float getIonEfficiency(float atmo) {
			return 1-atmo*0.7F;
		}
		
		private static float getIonEfficiencyInverse(float eff) {
			return -(eff-1)/0.7F;
		}
		
		private bool landed() {
			foreach (IMyLandingGear g in gear) {
				if (g.IsLocked)
					return true;
			}
			return false;
		}
		
		private bool hasPilot() {
			foreach (IMyShipController pit in seats) {
				if (pit.IsUnderControl)
					return true;
			}
			return false;
		}
		
		private float getNaturalGravity() {
			double gmax = 0;
			foreach (IMyShipController pit in seats) { 
				gmax = Math.Max(gmax, pit.GetNaturalGravity().Length());
			}
			return (float)(gmax/9.81F);
		}
		
		private float getAtmoAirLevel() {
			float sum = 0;			
			foreach (IMyAirVent vent in externalVents) {
				sum += vent.GetOxygenLevel();
			}			
			return sum*100F/externalVents.Count;
		}
		
		//====================================================
	}
}