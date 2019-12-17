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

namespace Ingame_Scripts.O2Control {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "[O2Status]"; //Any LCD panel with this in its name is overridden to show the pressurization/breach status
		
		const string GROUP_ID = "O2 Section:"; //Groups starting with this in their name denote sections/rooms of the ship or station, and contain blocks in that section you wish the script to affect
		// (eg lights, alarms), as well as one or more vents for pressure testing; Names are derived from the remaining group name - For example, "GROUP_ID 3A" would tell the script there is a section "3A"
		const string EXTERNAL_BLOCK_ID = "External"; //Anything whose name contains this is assumed to be exposed to the ship/station exterior
		const string INTERFACE_DOOR_SECTION_PATTERN = @"\[(.*?)\]"; //hope you like Regex if you want to change this...
		const char INTERFACE_DOOR_SECTION_SPLIT = '|'; //The separator char between section 1 and section 2 in the door name; under default settings, an ID looks like [Sec1|Sec2] to indicate that the door
		// separates sections Sec1 and Sec2, and closing it (and any other doors with the same pair of tags) will isolate the two from each other, in case only one is breached
		
		const float MAX_ATMOSPHERE = 0.5F; //The value (out of 1) below which atmospheric pressure is low enough to be considerered space/unbreathable and in which lockdowns apply
		const bool CLOSE_ALL_DOORS = false; //Whether to close (and keep closed) all doors in the ship whenever a breach is detected
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		static bool isExternalDoor(string name) {
			//Either contains "External", or both "Outer" and either "Hangar" or "Airlock" (ie outer airlock doors)
			return name.Contains("External") || ((name.Contains("Hangar") || name.Contains("Airlock")) && name.Contains("Outer"));
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly ShipLayout layout;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			layout = new ShipLayout(this);
			layout.build();
		}
		
		public void Main() { //called each cycle
			layout.tick();
		}
		
		internal class ShipLayout {
			
			private readonly MyGridProgram caller;
			
			private readonly VRage.Game.ModAPI.Ingame.IMyCubeGrid shipGrid;
			
			private readonly Dictionary<string, SectionBlocks> sections = new Dictionary<string, SectionBlocks>();
			private readonly List<InterfaceDoor> interDoors = new List<InterfaceDoor>();
			
			private readonly List<IMyAirVent> externalVents = new List<IMyAirVent>();
			private readonly List<IMyDoor> externalDoors = new List<IMyDoor>();
			private readonly List<IMyGasTank> o2Tanks = new List<IMyGasTank>();
			
			private readonly List<IMyDoor> extraDoors = new List<IMyDoor>();
		
			private readonly List<IMyTextPanel> displays = new List<IMyTextPanel>();
			
			private HashSet<string> breachedSections = new HashSet<string>();			
			private bool atmoLastTick;
			
			internal ShipLayout(MyGridProgram p) {
				caller = p;
				shipGrid = caller.Me.CubeGrid;
			}
			
			internal void build() {
				List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
				caller.GridTerminalSystem.GetBlockGroups(groups, g => g.Name.StartsWith(GROUP_ID, StringComparison.InvariantCulture));
				foreach (IMyBlockGroup group in groups) {
					string name = group.Name.Substring(GROUP_ID.Length);
					while (name[0] == ' ') {
						name = name.Substring(1);
					}
					SectionBlocks sec = new SectionBlocks(caller.GridTerminalSystem, group);
					sections.Add(name, sec);
				}
				
				caller.GridTerminalSystem.GetBlocksOfType<IMyAirVent>(externalVents, b => b.CustomName.Contains(EXTERNAL_BLOCK_ID));
				caller.GridTerminalSystem.GetBlocksOfType<IMyGasTank>(o2Tanks, b => b.BlockDefinition.SubtypeName.ToLower().Contains("oxygen") || b.BlockDefinition.SubtypeName.ToLower().Contains("o2"));
				
				List<IMyDoor> li = new List<IMyDoor>();
				caller.GridTerminalSystem.GetBlocksOfType<IMyDoor>(li);
				foreach (IMyDoor door in li) {
					if (isExternalDoor(door.CustomName)) {
						externalDoors.Add(door);
					}
					else {
						System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(door.CustomName, INTERFACE_DOOR_SECTION_PATTERN);
						if (mc != null && mc.Count > 0 && mc[0].Groups.Count > 0) {
							string ids = mc[0].Groups[1].ToString();
							string[] parts = ids.Split(INTERFACE_DOOR_SECTION_SPLIT);
							interDoors.Add(new InterfaceDoor(door, parts[0], parts[1]));
						}
						else {
							extraDoors.Add(door);
						}
					}
				}
			
				caller.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(displays, b => b.CustomName.Contains(DISPLAY_TAG));
			}
			
			internal void tick() {
				bool atmo = noAtmo();
				
				foreach (IMyTextPanel scr in displays) {
					scr.WritePublicText(""); //clear
				}
				
				if (atmo) {					
					show("No external atmosphere present.");
					
					if (atmoLastTick != atmo) { //accidentally left outside doors open? But only want to fire this once, not continuously, or it keeps closing airlocks
						setExternalDoors(false);
					}
					
					breachedSections.Clear();
					
					HashSet<string> depressurizing = new HashSet<string>();
					
					foreach (var entry in sections) {
						string name = entry.Key;
						SectionBlocks sec = entry.Value;
						bool flag = false;
						
						if (sec.isDepressurized()) { //airlocks reguarly get opened to space; this is not a problem
							show("Section '"+name+"' is depressurized.");
							depressurizing.Add(name);
						}
						else if (sec.isBreached()) {
							show("Section '"+name+"' is breached!");
							flag = true;
						}
						else {
							show("Section '"+name+"' is pressurized at "+Math.Round(sec.getAirPressureFraction()*100, 1)+"% atmosphere.");
						}
						
						if (flag) {
							breachedSections.Add(name);
							sec.onBreach();
						}
						else {
							sec.reset();
						}
					}
					foreach (InterfaceDoor inter in interDoors) {
						//close any doors that interface with a breached section, but open any that are sealed on both sides, provided neither section is not actively attempting to depressurize
						inter.setState(!breachedSections.Contains(inter.sectionA) && !breachedSections.Contains(inter.sectionB) && !depressurizing.Contains(inter.sectionA) && !depressurizing.Contains(inter.sectionB));
					}
					
					if (breachedSections.Count > 0) {
						if (o2Tanks.Count > 0) {
							show("Oxygen Reserves at "+getOxygenReserves()*100+"%");
							if (CLOSE_ALL_DOORS) {
								setAllDoors(false);
							}
						}
					}
				}
				else {
					show("Usable external atmosphere present.");
					foreach (IMyAirVent vent in externalVents) { //fill O2 tanks, if present
						vent.Depressurize = true;
					}
					setAllDoors(true); //fresh air
					show("All doors open.");
				}
				atmoLastTick = atmo;
			}
		
			private void show(string text) {
				foreach (IMyTextPanel scr in displays) {
					scr.WritePublicText(text+"\n", true);
				}
				caller.Echo(text);
			}
			
			private double getOxygenReserves() {
				double p = 0;
				foreach (IMyGasTank tank in o2Tanks) {
					p += tank.FilledRatio;
		        }
				return p/externalVents.Count;
			}
			
			private bool noAtmo() {
				float p = 0;
				foreach (IMyAirVent vent in externalVents) {
					p += vent.GetOxygenLevel();
		        }
				//show("Pressure: "+100*p/externalVents.Count()+" %");
				return p/externalVents.Count < MAX_ATMOSPHERE;
			}
			
			private void setExternalDoors(bool open) {
				foreach (IMyDoor door in externalDoors) {
					if (open) {
						door.OpenDoor();
					}
					else {
						door.CloseDoor();
					}
		        }
			}
			
			private void setAllDoors(bool open) {
				setExternalDoors(open);
				/*
				foreach (var entry in sections) {
					SectionBlocks sec = entry.Value;
					sec.closeDoors();
				}*/
				foreach (InterfaceDoor door in interDoors) {
					door.setState(open);
				}
				foreach (IMyDoor door in extraDoors) {
					if (open) {
						door.OpenDoor();
					}
					else {
						door.CloseDoor();
					}
				}
			}
			
		}
		
		internal class InterfaceDoor {
		
			public readonly string sectionA;
			public readonly string sectionB;
			private readonly IMyDoor door;
			
			internal InterfaceDoor(IMyDoor d, string s1, string s2) {
				sectionA = s1;
				sectionB = s2;
				door = d;
			}
			
			public void setState(bool open) {
				if (open) {
					door.OpenDoor();
				}
				else {
					door.CloseDoor();
				}
			}
		
		}
		
		internal class SectionBlocks {
		
			//private readonly List<IMyDoor> doors = new List<IMyDoor>();
			private readonly List<IMyLightingBlock> o2Lights = new List<IMyLightingBlock>();
			private readonly List<IMySoundBlock> alarms = new List<IMySoundBlock>();
			private readonly List<IMyAirVent> vents = new List<IMyAirVent>();
			
			internal SectionBlocks(IMyGridTerminalSystem sys, IMyBlockGroup blocks) {
				blocks.GetBlocksOfType<IMyAirVent>(vents);
				blocks.GetBlocksOfType<IMySoundBlock>(alarms);
				blocks.GetBlocksOfType<IMyLightingBlock>(o2Lights);
			}
			
			/*
			internal void closeDoors() {
				foreach (IMyDoor door in doors) {
					door.ApplyAction("Open_Off"); 
				}
			}*/
			
			public bool isBreached() {
				foreach (IMyAirVent av in vents) {
					if (!av.CanPressurize) {
						return true;
					}
				}
				return false;
			}
			
			public bool isDepressurized() {
				foreach (IMyAirVent av in vents) {
					if (av.Depressurize) {
						return true;
					}
				}
				return false;
			}
			
			public float getAirPressureFraction() {
				float p = 0;
				foreach (IMyAirVent av in vents) {
					p += av.GetOxygenLevel();
				}
				return p/vents.Count;
			}
			
			internal void onBreach() {
				//foreach (IMyDoor door : doors) {
				//	
				//}
				setWarnings(true);
			}
			
			internal void reset() {
				//foreach (IMyDoor door : doors) {
				//	
				//}
				setWarnings(false);
			}
			
			private void setWarnings(bool on) {
				foreach (IMyLightingBlock light in o2Lights) {
					light.Enabled = on;
				}
				foreach (IMySoundBlock snd in alarms) {
					snd.Enabled = on;
				}
			}
		
		}
		
		//====================================================
	}
}