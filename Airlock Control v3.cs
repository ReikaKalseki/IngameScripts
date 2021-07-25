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
using IMyAirVent = SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent;
using IMySoundBlock = SpaceEngineers.Game.ModAPI.Ingame.IMySoundBlock;

using VRage.Game.GUI.TextPanel;

namespace Ingame_Scripts.AirlockControlV3 {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------		
		const string VENT_ID = "Air Vent"; //The name of air vents to look for
		
		const string AIRLOCK_SCREEN_ID = "AirlockScreen"; //Anything whose name contains this is used for overall display of airlock states.		
		const string EXTERNAL_BLOCK_ID = "External"; //Anything whose name contains this is assumed to be exposed to the ship/station exterior
		const string AIRLOCK_BLOCK_ID = "Airlock"; //Anything whose name contains this is assumed to be part of an airlock
		
		const float MAX_ATMOSPHERE = 0.5F; //The value (out of 1) below which atmospheric pressure is low enough to be considerered space/unbreathable
		const bool allowOpenIfNoO2Space = true; //Whether to allow airlocks to open when pressurized but there is no space to put the air in tanks
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		static bool isOuterAirlockDoor(string name) { //Whether a door is part of the outer half of an airlock
			return name.Contains(EXTERNAL_BLOCK_ID) || name.Contains("Outer") || name.Contains("Exterior");
		}
		
		static bool isInnerAirlockDoor(string name) { //Whether a door is part of the inner half of an airlock
			return name.Contains("Inner") || name.Contains("Interior");
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly Dictionary<string, Airlock> airlocks = new Dictionary<string, Airlock>();
		private readonly List<IMyAirVent> externalVents = new List<IMyAirVent>();
		private readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
		private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
		
		private readonly Vector2 screenSize = new Vector2(1024, 512);
		private readonly float lineSize = 25;
		private readonly float edgePadding = 16;
			
		private static readonly Color GREEN = new Color(0, 255, 0, 255);
		private static readonly Color RED = new Color(255, 0, 0, 255);
		private static readonly Color YELLOW = new Color(255, 255, 0, 255);
		private static readonly Color BLUE = new Color(0, 80, 255, 255);
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(externalVents, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(EXTERNAL_BLOCK_ID));
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(AIRLOCK_SCREEN_ID));
			GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains("Oxygen"));
			
			List<IMyAirVent> li = new List<IMyAirVent>();
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(li, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(VENT_ID) && !b.CustomName.Contains(EXTERNAL_BLOCK_ID) && b.CustomName.Contains(AIRLOCK_BLOCK_ID));
			foreach (IMyAirVent vent in li) {
				string id = createID(vent);
				if (id != null) {
					if (!airlocks.ContainsKey(id))
						airlocks.Add(id, new Airlock(this, id));
				}
			}
		}
		
		private static string createID(IMyAirVent vent) {			
			string repl = vent.CustomName.Replace(VENT_ID, "").Replace("Airlock", "").Trim();
			while (repl[0] == ' ' || repl[0] == ':' || repl[0] == '-') {
				repl = repl.Substring(1).Trim();
			}
			while (repl[repl.Length-1] == ' ' || repl[repl.Length-1] == ':' || repl[repl.Length-1] == '-' || char.IsNumber(repl[repl.Length-1])) {
				repl = repl.Substring(0, repl.Length-1).Trim();
			}
			return repl;
		}
		
		public void Main() { //called each cycle
			float f = getExternalAtmo();
			double f2 = getTankFill();
			List<MySpriteDrawFrame> li = new List<MySpriteDrawFrame>();
			foreach (IMyTextPanel scr in displays) {
				li.Add(prepareScreen(scr));
			}
			float dy = 0;
			foreach (Airlock a in airlocks.Values) {
				AirlockState s = a.tick(f, f2);
				string name = s.ToString().ToUpperInvariant()[0]+s.ToString().Substring(1).ToLowerInvariant();
				Color? c = null;
				switch(s) {
					case AirlockState.CLOSED:
						c = BLUE;
						break;
					case AirlockState.INNER:
						c = YELLOW;
						break;
					case AirlockState.OUTER:
						c = YELLOW;
						break;
					case AirlockState.OPEN:
						c = GREEN;
						break;
				}
				if (s != AirlockState.OPEN && s != AirlockState.OUTER && a.isBreached()) {
					name = "breached!";
					c = RED;
				}
				string sg = "Airlock "+a.airlockID+" is "+name;
				drawText(li, edgePadding, dy, sg, c);
				dy += lineSize;
			}
			
			foreach (MySpriteDrawFrame frame in li) {
				frame.Dispose();
			}
		}
		
		private void drawText(List<MySpriteDrawFrame> li, float x, float y, string s, Color? c = null) {
			MySprite text = MySprite.CreateText(s, "monospace", c != null && c.HasValue ? c.Value : Color.White, lineSize/24F);
			text.Alignment = TextAlignment.LEFT;
			text.Position = new Vector2(x, y);
       		foreach (MySpriteDrawFrame frame in li) {
				frame.Add(text);
			}
		}
		
		private MySpriteDrawFrame prepareScreen(IMyTextPanel scr) {
			scr.ContentType = ContentType.SCRIPT;
       		MySpriteDrawFrame frame = scr.DrawFrame();
       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: screenSize, color: Color.Black));
       		return frame;
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
			internal readonly string airlockID;
			private readonly VRage.Game.ModAPI.Ingame.IMyCubeGrid shipGrid;
			
			private readonly List<IMyDoor> outerDoors = new List<IMyDoor>();
			private readonly List<IMyDoor> innerDoors = new List<IMyDoor>();
			private readonly List<IMyAirVent> vents = new List<IMyAirVent>();	
			private readonly List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
					
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
				caller.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, b => b.CustomName.Contains(AIRLOCK_BLOCK_ID) && (b.CustomName.Contains(airlockID) || b.CustomName.Contains(AIRLOCK_SCREEN_ID)) && b.CubeGrid == caller.Me.CubeGrid);
				
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
			
			internal void setAirlockState(AirlockState state, bool externalAtmo, float airLevel, bool setOpenState = true) {
				switch(state) {
					case AirlockState.OPEN:
						setDoorStates(innerDoors, true, true, setOpenState);
						setDoorStates(outerDoors, true, true, setOpenState);
						setVents(externalAtmo);
						if (externalAtmo)
							setLightState(false);
						else
							setLightState(true, RED, 1);
						break;
					case AirlockState.CLOSED:
						setDoorStates(innerDoors, true, false, setOpenState);
						setDoorStates(outerDoors, true, false, setOpenState);
						setVents(true);
						setLightState(true, Color.Lerp(YELLOW, GREEN, 1-airLevel));
						break;
					case AirlockState.INNER:
						setDoorStates(innerDoors, true, true, setOpenState);
						setDoorStates(outerDoors, true, false, setOpenState);
						setVents(false);
						setLightState(true, RED);
						break;
					case AirlockState.OUTER:
						setDoorStates(innerDoors, true, false, setOpenState);
						setDoorStates(outerDoors, true, true, setOpenState);
						setVents(true);
						setLightState(true, RED);
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
			
			internal void setLightState(bool on, Color? c = null, float flash = 0) {
				foreach (IMyLightingBlock light in lights) {
					light.Enabled = on;
					if (c != null && c.HasValue)
						light.Color = c.Value;
					if (flash > 0) {
						light.BlinkLength = 0.5F;
						light.BlinkIntervalSeconds = flash;
					}
					else {
						light.BlinkIntervalSeconds = 0;
						light.BlinkLength = 1;
					}
				}
			}
			
			internal void setDoorStates(List<IMyDoor> doors, bool active, bool open, bool setOpenState = true) {
				foreach (IMyDoor door in doors) {
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
			
			internal AirlockState tick(float atmo, double tankFill) {
				AirlockState? ret = null;
				if (atmo < MAX_ATMOSPHERE) {									
					float air = getAtmo();
					
					if (atmoLastTick > atmo) { //accidentally left outside doors open? But only want to fire this once, not continuously, or it keeps closing airlocks
						setAirlockState(AirlockState.CLOSED, false, air);
					}
						
					if (isDepressurized()) {
						//show("Airlock '"+airlockID+"' is depressurized.");
					}
					else if (isBreached()) {
						caller.Echo("Airlock '"+airlockID+"' is breached!");
						setAirlockState(AirlockState.CLOSED, false, air);
					}
					else {
						//show("Airlock '"+airlockID+"' is pressurized.");
					}
					
					AirlockState s = getCurrentAirlockState();
					setAirlockState(s, false, air, false);
					if (s != AirlockState.CLOSED && s != AirlockState.OPEN) {
						preventAccidentalOpen(s);
					}
				
					if (atmo < MAX_ATMOSPHERE && air > 0.02 && !(allowOpenIfNoO2Space && isDepressurized() && tankFill > 0.99)) {
						setDoorStates(outerDoors, false, false);
					}
					
					caller.Echo("Airlock '"+airlockID+"' is "+s+".");
				
					ret = s;
				}
				else {
					setAirlockState(AirlockState.OPEN, true, atmo); //fresh air
					ret = AirlockState.OPEN;
				}
				
				atmoLastTick = atmo;
				return ret.Value;
			}
			
			internal void preventAccidentalOpen(AirlockState s) {
				if (s == AirlockState.INNER) {
					setDoorStates(outerDoors, false, false);
					//setInnerDoorStates(true, true);
				}
				else if (s == AirlockState.OUTER) {
					setDoorStates(innerDoors, false, false);
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
			
		}
		
		
		//====================================================
	}
}