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
//using IMySpotlight = SpaceEngineers.Game.ModAPI.Ingame.IMySpotlight;

namespace Ingame_Scripts.HangarControl {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "Beam"; //Any spotlight with this (and a hangar ID) in its name will be color-controlled
		const string AUX_DISPLAY_TAG = "Color Lights"; //Any lighting block in a group named "<HangarID> - <AUX_DISPLAY_TAG>" will be counted as an auxiliary color-controlled light
		const string AUX_GROUP_TAG = "Lights"; //Any lighting block in a group named "<HangarID> - <AUX_GROUP_TAG>" will be counted as an auxiliary light for that hangar
		const string VENT_GROUP_TAG = "Air Vent"; //Any vent with this (and a hangar ID) in its name will be counted as a Hangar supply vent and will be consulted for oxygen levels
		const string EXTERNAL_VENT_TAG = "External Air Vent"; //Any vent with this in its name will be counted as an external vent and will be consulted for atmospheric oxygen levels
		const string SENSOR_TAG = "Door Sensor"; //Any sensor with this (and a hangar ID) in its name will be checked for what grids it detects to know when to open the door of the hangar
		const float RED_THRESHOLD = 10; //Spotlights will be red if air level is below this percentage
		const float GREEN_THRESHOLD = 98; //And green if it is above this; they will be yellow if it is in between
		const float AMBIENT_LIGHT_THRESHOLD = GREEN_THRESHOLD; //Any auxiliary lights in the hangar will be toggled on and off, being on if the oxygen level is above this
		const bool RED_IF_PRESSURIZED = true; //Enable this to flip red and green, so that instead of an air level indicator, you get a "safe to open the door" indicator.
		const float COLOR_FADE_DURATION = 60; //How many update cycles (in 1/60th of a second) should be spent fading between colors.
		const float OPEN_ATMO_THRESHOLD = 80F; //If atmospheric O2 is above this percentage, hangars will be left open; set this over 100 to disable that behavior
		const bool allowOpenIfNoO2Space = true; //Whether to allow the hangar to open when pressurized but there is no space to put the air in tanks
		const string SCREEN_TAG = "AirLevel"; //Any LCD panel with this (and a hangar ID) in its name will show the air level in its hangar
		
		readonly string[] HANGAR_GROUPS = {"Hangar"}; //Each string in this list is treated as a separate hangar group ('ID').
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		 //whether a given vent or internal door is connected to the hangar of a given ID;
		 //This is usually "hangar and vent/door are part of the same group", but might be different if for example the hangars are all connected, sharing doors and/or vents
		internal static bool isConnectedToHangar(string blockName, string id) {
		 	return blockName.Contains(id);
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<Hangar> hangars = new List<Hangar>();
		
		private readonly List<IMyAirVent> externalVents = new List<IMyAirVent>();
		private readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
		
		private readonly HashSet<IMyDoor> closedDoors = new HashSet<IMyDoor>();
		private readonly HashSet<IMyAirVent> depressurizing = new HashSet<IMyAirVent>();
		
		private float externalAtmo;
		private double o2TankFill;
		private int tickIndex = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update1;
			
			GridTerminalSystem.GetBlocksOfType<IMyAirVent>(externalVents, b => b.CustomName.Contains(EXTERNAL_VENT_TAG) && b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains("Oxygen"));
			
			foreach (string id in HANGAR_GROUPS) {
				hangars.Add(new Hangar(this, id));
			}
		}
		
		public void Main() { //called each cycle
			bool full = tickIndex%10 == 0 || tickIndex < 4;
			if (full) {
				externalAtmo = getAtmoAirLevel();
				o2TankFill = getTankFill();
			}
			closedDoors.Clear();
			depressurizing.Clear();
			if (full)
				Echo("Ship O2 reserves at "+o2TankFill*100+"%. Currently depressurizing vents: "+depressurizing.Count+"=["+string.Join(", ", depressurizing)+"]");
			foreach (Hangar h in hangars) {
				h.tick(full, externalAtmo, o2TankFill, closedDoors, depressurizing);
			}
			tickIndex++;
		}
		
		private float getAtmoAirLevel() {
			float sum = 0;			
			foreach (IMyAirVent vent in externalVents) {
				sum += vent.GetOxygenLevel();
			}			
			return sum*100F/externalVents.Count;
		}
		
		private double getTankFill() {
			double sum = 0;		
			foreach (IMyGasTank tank in tanks) {
				sum += tank.FilledRatio;
			}			
			return sum/tanks.Count;
		}
		
		private static VRageMath.Color getColor(float f) {
			if (f <= RED_THRESHOLD)
				return RED_IF_PRESSURIZED ? new VRageMath.Color(0, 255, 0, 255) : new VRageMath.Color(255, 0, 0, 255);
			else if (f <= GREEN_THRESHOLD)
				return new VRageMath.Color(255, 255, 0, 255);
			else
				return RED_IF_PRESSURIZED ? new VRageMath.Color(255, 0, 0, 255) : new VRageMath.Color(0, 255, 0, 255);
		}
		
		internal class Hangar {
			
			private readonly MyGridProgram caller;
			internal readonly string hangarID;
			private readonly VRage.Game.ModAPI.Ingame.IMyCubeGrid shipGrid;
			
			private readonly List<IMyLightingBlock> auxLights = new List<IMyLightingBlock>();
			private readonly List<IMyLightingBlock> spotlights = new List<IMyLightingBlock>();
			private readonly List<IMyAirVent> vents = new List<IMyAirVent>();
			private readonly List<IMySensorBlock> sensors = new List<IMySensorBlock>();
			private readonly List<IMyDoor> doors = new List<IMyDoor>();
			private readonly List<IMyAirtightHangarDoor> entrance = new List<IMyAirtightHangarDoor>();
			private readonly List<IMyTextPanel> screens = new List<IMyTextPanel>();
		
			private Color currentColor;
			private Color lastColor;
			private int colorTick;
			
			private const float BLUE_THRESH = 0.95F;
			private const float GREEN_THRESH = 0.7F;
			private const float YELLOW_THRESH = 0.5F;
			private const float ORANGE_THRESH = 0.3F;
			private const float RED_THRESH = 0.1F;
			
			private readonly Color BLUE_COLOR = new Color(40, 120, 255, 255);
			private readonly Color GREEN_COLOR = new Color(40, 255, 40, 255);
			private readonly Color YELLOW_COLOR = new Color(255, 255, 40, 255);
			private readonly Color ORANGE_COLOR = new Color(255, 128, 40, 255);
			private readonly Color RED_COLOR = new Color(255, 40, 40, 255);
			private readonly Color DARKRED_COLOR = new Color(127, 40, 40, 255);
			
			private readonly Color BLACK = new Color(0, 0, 0, 255);		
			private readonly Color GRAY1 = new Color(60, 60, 60, 255);	
			private readonly Color GRAY2 = new Color(20, 20, 20, 255);		
			private readonly Color WHITE = new Color(255, 255, 255, 255);		
			private readonly Vector2 screenSize = new Vector2(512, 512);	
			private readonly Vector2 barSize1 = new Vector2(384, 512);		
			private readonly Vector2 barSize2 = new Vector2(384-16, 512);	
			
			internal Hangar(MyGridProgram p, string id) {
				caller = p;
				shipGrid = caller.Me.CubeGrid;
				hangarID = id;
				build();
			}
			
			internal void build() {
				IMyBlockGroup grp = caller.GridTerminalSystem.GetBlockGroupWithName(hangarID+" - "+AUX_GROUP_TAG);
				if (grp != null)
					grp.GetBlocksOfType<IMyLightingBlock>(auxLights, b => b.CubeGrid == shipGrid);
				
				caller.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(spotlights, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == shipGrid);
				grp = caller.GridTerminalSystem.GetBlockGroupWithName(hangarID+" "+AUX_DISPLAY_TAG);
				if (grp != null)
					grp.GetBlocksOfType<IMyLightingBlock>(spotlights, b => b.CubeGrid == shipGrid && !auxLights.Contains(b));
				
				caller.GridTerminalSystem.GetBlocksOfType<IMyAirVent>(vents, b => isConnectedToHangar(b.CustomName, hangarID) && b.CustomName.Contains(VENT_GROUP_TAG) && b.CubeGrid == shipGrid);
				caller.GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, b => b.CustomName.Contains(hangarID) && b.CustomName.Contains(SENSOR_TAG) && b.CubeGrid == shipGrid);
				caller.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens, b => b.CustomName.Contains(hangarID) && b.CustomName.Contains(SCREEN_TAG) && b.CubeGrid == shipGrid);
				
				caller.GridTerminalSystem.GetBlocksOfType<IMyAirtightHangarDoor>(entrance, b => b.CustomName.Contains(hangarID) && b.CustomName.Contains(hangarID) && b.CubeGrid == shipGrid);
				caller.GridTerminalSystem.GetBlocksOfType<IMyDoor>(doors, b => isConnectedToHangar(b.CustomName, hangarID) && b.CubeGrid == shipGrid && !(b is IMyAirtightHangarDoor));
				
				List<IMyAirtightHangarDoor> li = new List<IMyAirtightHangarDoor>();
				grp = caller.GridTerminalSystem.GetBlockGroupWithName(hangarID+" Doors");
				if (grp != null)
					grp.GetBlocksOfType<IMyAirtightHangarDoor>(li, b => b.CubeGrid == shipGrid);
				foreach (IMyAirtightHangarDoor h in li) {
					if (!entrance.Contains(h)) {
						entrance.Add(h);
					}
				}
			}
			
			internal void tick(bool fullUpdate, float atmo, double tankFill, HashSet<IMyDoor> closedDoors, HashSet<IMyAirVent> depressurizing) {
				float f = getAirLevel();
				if (fullUpdate) {
					bool open = isShipWaiting();
					caller.Echo("Hangar '"+hangarID+"' Open Queued: "+open);
					caller.Echo("Hangar '"+hangarID+"' Air Level: "+f+"%");
					setHangarStatus(open, f, atmo, tankFill, closedDoors, depressurizing);
				}
				Color c = getColor(f);		
				if (c != currentColor) {
					setColor(c);
				}
				Color c2 = colorTick < COLOR_FADE_DURATION ? Color.Lerp(lastColor, currentColor, colorTick/COLOR_FADE_DURATION) : currentColor;
				//Echo(lastColor+" > "+currentColor+" p="+colorTick/4F+" = "+c2);
				foreach (IMyLightingBlock light in spotlights) {
					light.Color = c2;
				}			
				foreach (IMyLightingBlock light in auxLights) {
					light.Enabled = f >= AMBIENT_LIGHT_THRESHOLD;
				}			
				float frac = f/100F;
				foreach (IMyTextPanel scr in screens) {
					setScreenContent(scr, frac);
				}
				colorTick++;
			}
		
			private float getAirLevel() {
				float sum = 0;			
				foreach (IMyAirVent vent in vents) {
					sum += vent.GetOxygenLevel();
				}			
				return sum*100F/vents.Count;
			}
			
			private void setScreenContent(IMyTextPanel scr, float f) {
				scr.ContentType = ContentType.SCRIPT;
       			MySpriteDrawFrame frame = scr.DrawFrame();
       			frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: screenSize, color: BLACK));
       			frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: barSize1, color: GRAY1));
       			frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: barSize2, color: GRAY2));
				
       			drawBox(frame, f, BLUE_COLOR, 1);
       			drawBox(frame, f, GREEN_COLOR, BLUE_THRESH);
       			drawBox(frame, f, YELLOW_COLOR, GREEN_THRESH);
       			drawBox(frame, f, ORANGE_COLOR, YELLOW_THRESH);
       			drawBox(frame, f, RED_COLOR, ORANGE_THRESH);
       			drawBox(frame, f, DARKRED_COLOR, RED_THRESH);
       			
       			frame.Add(MySprite.CreateText(String.Format("{0:0.000}", (f*100))+"%", "monospace", WHITE, 3));
       			
       			frame.Dispose();
			}
			
			private void drawBox(MySpriteDrawFrame frame, float f, Color c, float limit) {
				f = Math.Min(f, limit);
				float h = f*512;
				Vector2 box = new Vector2(barSize2.X, h);
    			Vector2 ctr = new Vector2(256, 512-h/2);
    			MySprite sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: box, color: c);
       			sprite.Position = ctr;
				frame.Add(sprite);
			}
		
			private void setHangarStatus(bool open, float air, float atmo, double tankFill, HashSet<IMyDoor> closedDoors, HashSet<IMyAirVent> depressurizing) {
				bool sectioned = true;
				foreach (IMyDoor door in doors) {
					if (door.OpenRatio > 0) {
						sectioned = false;
						break;
					}
				}
				bool locked = true;
				foreach (IMyAirtightHangarDoor door in entrance) {
					bool noWaste = air <= 0.01 || (allowOpenIfNoO2Space && tankFill >= 0.999);
					bool canOpen = (sectioned && open && noWaste) || atmo >= OPEN_ATMO_THRESHOLD;
					if (open && !noWaste && !canOpen) {
						//caller.Echo("Cannot open hangar '"+hangarID+"' due to leftover, unstorable air");
					}
					if (canOpen)
						door.OpenDoor();
					else
						door.CloseDoor();
					if (door.OpenRatio > 0) {
						locked = false;
						//caller.Echo("Hangar '"+hangarID+"' has open ("+door.OpenRatio*100+"%) door: "+door.CustomName);
					}
				}
				//caller.Echo("Hangar '"+hangarID+"' door state: "+locked);
				foreach (IMyAirVent vent in vents) {
					vent.Depressurize = open || !locked || depressurizing.Contains(vent);
					if (vent.Depressurize)
						depressurizing.Add(vent);
				}
				foreach (IMyDoor door in doors) {
					if ((!locked || open) && atmo < OPEN_ATMO_THRESHOLD) {
						door.CloseDoor();
						closedDoors.Add(door);
					}
					else if (!closedDoors.Contains(door)) {
						door.OpenDoor();
					}
				}
			}
		
			private void setColor(Color c) {
				caller.Echo("Setting color to "+c);
				lastColor = currentColor;
				currentColor = c;
				colorTick = 0;
			}
			
			private bool isHangarSealed() {
				foreach (IMyAirtightHangarDoor door in entrance) {
					if (door.OpenRatio > 0)
						return false;
				}
				return true;
			}
			
			private bool isShipWaiting() {
				List<MyDetectedEntityInfo> li = new List<MyDetectedEntityInfo>();
				foreach (IMySensorBlock sensor in sensors) {
					sensor.DetectEnemy = false;
					sensor.DetectFloatingObjects = false;
					sensor.DetectFriendly = true;
					sensor.DetectLargeShips = true;
					sensor.DetectSmallShips = true;
					sensor.DetectStations = false;
					sensor.DetectSubgrids = false;
					sensor.DetectPlayers = true;
					sensor.DetectNeutral = false;
					sensor.DetectedEntities(li);
				}
				return li.Count > 0;
			}
			
		}
		
		//====================================================
	}
}