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
using System.Text.RegularExpressions;

using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyFunctionalBlock = Sandbox.ModAPI.Ingame.IMyFunctionalBlock;
using IMyShipConnector = Sandbox.ModAPI.Ingame.IMyShipConnector;
using IMyLandingGear = SpaceEngineers.Game.ModAPI.Ingame.IMyLandingGear;
using IMyAirVent = SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent;
using IMySoundBlock = SpaceEngineers.Game.ModAPI.Ingame.IMySoundBlock;

namespace Ingame_Scripts.AirlockControl {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------		
		const string VENT_ID = "Air Vent";
		
		const string EXTERNAL_BLOCK_ID = "External"; //Anything whose name contains this is assumed to be exposed to the ship/station exterior
		const string AIRLOCK_BLOCK_ID = "Airlock"; //Anything whose name contains this is assumed to be part of an airlock
		
		const float MAX_ATMOSPHERE = 0.5F; //The value (out of 1) below which atmospheric pressure is low enough to be considerered space/unbreathable and in which lockdowns apply
		const bool allowOpenIfNoO2Space = true; //Whether to allow the hangar to open when pressurized but there is no space to put the air in tanks
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		static bool isOuterAirlockDoor(string name) {
			return name.Contains(EXTERNAL_BLOCK_ID) || name.Contains("Outer");
		}
		
		static bool isInnerAirlockDoor(string name) {
			return name.Contains("Inner");
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly HashSet<Airlock> airlocks = new HashSet<Airlock>();
		private readonly List<IMyAirVent> externalVents = new List<IMyAirVent>();
		private readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(externalVents, b => b.CustomName.Contains(EXTERNAL_BLOCK_ID) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains("Oxygen"));
			
			List<IMyAirVent> li = new List<IMyAirVent>();
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(li, b => b.CustomName.Contains(AIRLOCK_BLOCK_ID) && b.CubeGrid == Me.CubeGrid);
			foreach (IMyAirVent vent in li) {
				string id = createID(vent);
				if (id != null)
					airlocks.Add(new Airlock(this, id));
			}
		}
		
		private static string createID(IMyAirVent vent) {
			const string id = VENT_ID;
			
			if (!vent.CustomName.Contains(id) || vent.CustomName.Contains(EXTERNAL_BLOCK_ID))
				return null;
			
			string repl = vent.CustomName.Replace(id, "");
			while (repl[0] == ' ' || repl[0] == ':' || repl[0] == '-') {
				repl = repl.Substring(1);
			}
			while (repl[repl.Length-1] == ' ' || repl[repl.Length-1] == ':' || repl[repl.Length-1] == '-' || char.IsNumber(repl[repl.Length-1])) {
				repl = repl.Substring(0, repl.Length-1);
			}
			return repl;
		}
		
		public void Main() { //called each cycle
			float f = getExternalAtmo();
			double f2 = getTankFill();
			foreach (Airlock a in airlocks) {
				a.tick(f, f2);
			}
		}
		
		private float getExternalAtmo() {
			float f = 0;
			foreach (IMyAirVent vent in externalVents) {
				f += vent.GetOxygenLevel();
			}
			return f/externalVents.Count;
		}
		
		private double getTankFill() {
			double sum = 0;		
			foreach (IMyGasTank tank in tanks) {
				sum += tank.FilledRatio;
			}			
			return sum/tanks.Count;
		}
			
		private bool noAtmo() {
			return getExternalAtmo() < MAX_ATMOSPHERE;
		}
		
		internal enum AirlockState {
			CLOSED,
			INNER,
			OUTER,
			OPEN
		}
		
		internal class Airlock {
			
			private readonly MyGridProgram caller;
			private readonly string airlockID;
			private readonly VRage.Game.ModAPI.Ingame.IMyCubeGrid shipGrid;
			
			private readonly List<IMyDoor> outerDoors = new List<IMyDoor>();
			private readonly List<IMyDoor> innerDoors = new List<IMyDoor>();
			private readonly List<IMyAirVent> vents = new List<IMyAirVent>();	
			private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
					
			private float atmoLastTick;
			
			internal Airlock(MyGridProgram p, string id) {
				caller = p;
				shipGrid = caller.Me.CubeGrid;
				airlockID = id;
				build();
			}
			
			internal void build() {
				List<IMyBlockGroup> groups = new List<IMyBlockGroup>();

				caller.GridTerminalSystem.GetBlocksOfType<IMyAirVent>(vents, b => b.CustomName.Contains(AIRLOCK_BLOCK_ID) && b.CustomName.Contains(airlockID) && b.CubeGrid == caller.Me.CubeGrid);
				caller.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(AIRLOCK_BLOCK_ID) && b.CustomName.Contains(airlockID) && b.CubeGrid == caller.Me.CubeGrid);
				
				List<IMyDoor> li = new List<IMyDoor>();
				caller.GridTerminalSystem.GetBlocksOfType<IMyDoor>(li, b => b.CustomName.Contains(AIRLOCK_BLOCK_ID) && b.CustomName.Contains(airlockID) && b.CubeGrid == caller.Me.CubeGrid);
				foreach (IMyDoor door in li) {
					if (isOuterAirlockDoor(door.CustomName)) {
						outerDoors.Add(door);
					}
					else if (isInnerAirlockDoor(door.CustomName)) {
						innerDoors.Add(door);
					}
				}
			}
			
			internal void setAirlockState(AirlockState state, bool setOpenState = true) {
				switch(state) {
					case AirlockState.OPEN:
						setInnerDoorStates(true, true, setOpenState);
						setOuterDoorStates(true, true, setOpenState);
						setVents(false);
						break;
					case AirlockState.CLOSED:
						setInnerDoorStates(true, false, setOpenState);
						setOuterDoorStates(true, false, setOpenState);
						setVents(true);
						break;
					case AirlockState.INNER:
						setInnerDoorStates(true, true, setOpenState);
						setOuterDoorStates(true, false, setOpenState);
						setVents(false);
						break;
					case AirlockState.OUTER:
						setInnerDoorStates(true, false, setOpenState);
						setOuterDoorStates(true, true, setOpenState);
						setVents(true);
						break;
				}
			}
			
			internal AirlockState getCurrentAirlockState() {
				bool inner = areInnerDoorsClosed();
				bool outer = areOuterDoorsClosed();
				if (inner && outer) {
					return AirlockState.CLOSED;
				}
				else if (inner) {
					return AirlockState.OUTER;
				}
				else if (outer) {
					return AirlockState.INNER;
				}
				else {
					return AirlockState.OPEN;
				}
			}
			
			internal float getAtmo() {
				float f = 0;
				foreach (IMyAirVent vent in vents) {
					f += vent.GetOxygenLevel();
				}
				return f/vents.Count;
			}
			
			internal void setVents(bool dep) {
				foreach (IMyAirVent vent in vents) {
					vent.Depressurize = dep;
				}
			}
			
			internal void setInnerDoorStates(bool active, bool open, bool setOpenState = true) {
				foreach (IMyDoor door in innerDoors) {
					if (setOpenState) {
						if (open)
							door.OpenDoor();
						else
							door.CloseDoor();
					}
					door.Enabled = active;
				}
			}
			
			internal void setOuterDoorStates(bool active, bool open, bool setOpenState = true) {
				foreach (IMyDoor door in outerDoors) {
					if (setOpenState) {
						if (open)
							door.OpenDoor();
						else
							door.CloseDoor();
					}
					door.Enabled = active;
				}
			}
			
			internal bool areInnerDoorsClosed() {
				foreach (IMyDoor door in innerDoors) {
					if (door.OpenRatio > 0.01) {
						return false;
					}
				}
				return true;
			}
			
			internal bool areOuterDoorsClosed() {
				foreach (IMyDoor door in outerDoors) {
					if (door.OpenRatio > 0.01) {
						return false;
					}
				}
				return true;
			}
			
			internal void tick(float atmo, double tankFill) {				
				foreach (IMyTextPanel scr in displays) {
					scr.WritePublicText(""); //clear
				}
				
				if (atmo < MAX_ATMOSPHERE) {					
					show("No external atmosphere present.");
					
					if (atmoLastTick > atmo) { //accidentally left outside doors open? But only want to fire this once, not continuously, or it keeps closing airlocks
						setAirlockState(AirlockState.CLOSED);
					}
						
					if (isDepressurized()) {
						show("Airlock '"+airlockID+"' is depressurized.");
					}
					else if (isBreached()) {
						show("Airlock '"+airlockID+"' is breached!");
						setAirlockState(AirlockState.CLOSED);
					}
					else {
						show("Airlock '"+airlockID+"' is pressurized.");
					}
					
					AirlockState s = getCurrentAirlockState();
					setAirlockState(s, false);
					if (s != AirlockState.CLOSED && s != AirlockState.OPEN) {
						preventAccidentalOpen(s);
					}
				
					if (atmo < MAX_ATMOSPHERE && getAtmo() > 0.02 && !(allowOpenIfNoO2Space && isDepressurized() && tankFill > 0.99)) {
						setOuterDoorStates(false, false);
					}
					
					show("Airlock '"+airlockID+"' is "+s+".");
				}
				else {
					show("Usable external atmosphere present.");
					setAirlockState(AirlockState.OPEN); //fresh air
					show("All airlocks fully open.");
				}
				
				atmoLastTick = atmo;
			}
			
			internal void preventAccidentalOpen(AirlockState s) {
				if (s == AirlockState.INNER) {
					setOuterDoorStates(false, false);
					//setInnerDoorStates(true, true);
				}
				else if (s == AirlockState.OUTER) {
					setInnerDoorStates(false, false);
					//setOuterDoorStates(true, true);
				}
			}
			
			internal bool isBreached() {
				foreach (IMyAirVent av in vents) {
					if (!av.CanPressurize) {
						return true;
					}
				}
				return false;
			}
			
			internal bool isDepressurized() {
				foreach (IMyAirVent av in vents) {
					if (av.Depressurize) {
						return true;
					}
				}
				return false;
			}
		
			private void show(string text) {
				foreach (IMyTextPanel scr in displays) {
					scr.WriteText(text+"\n", true);
					scr.FontSize = 0.707F;
					scr.ShowPublicTextOnScreen();
				}
				caller.Echo(text);
			}
			
		}
		
		
		//====================================================
	}
}