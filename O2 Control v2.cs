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

using VRage.Game.GUI.TextPanel;

using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyFunctionalBlock = Sandbox.ModAPI.Ingame.IMyFunctionalBlock;
using IMyShipConnector = Sandbox.ModAPI.Ingame.IMyShipConnector;
using IMyLandingGear = SpaceEngineers.Game.ModAPI.Ingame.IMyLandingGear;
using IMyAirVent = SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent;
using IMySoundBlock = SpaceEngineers.Game.ModAPI.Ingame.IMySoundBlock;

namespace Ingame_Scripts.O2Controlv2 {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string DISPLAY_TAG = "O2Status"; //Any LCD panel with this in its name is overridden to show the pressurization/breach status
		
		 //Any vents/lights/etc containing this in their name are assumed to be involved in monitoring O2 status or part of an alarm system, and are activated accordingly. Section names are parsed by
		 //removing this string from their name (and trimming any leading or trailing " ", ":" or "-" characters, and any trailing numbers);
		 // whatever remains is considered the section the block is in/acts for. So "Cockpit Air Vent", "O2 Light - Reactor 7" and "O2 Alarm: Hangar A" become "Cockpit", "Reactor", and "Hangar A" respectively.
		const string VENT_ID = "Air Vent";
		const string LIGHT_ID = "O2 Light";
		const string SOUND_ID = "O2 Alarm";
		
		const string EXTERNAL_BLOCK_ID = "External"; //Anything whose name contains this is assumed to be exposed to the ship/station exterior
		const string INTERFACE_DOOR_SECTION_PATTERN = @"\[(.*?)\]"; //hope you like Regex if you want to change this...
		const char INTERFACE_DOOR_SECTION_SPLIT = '|'; //The separator char between section 1 and section 2 in the door name; under default settings, an ID looks like [Sec1|Sec2] to indicate that the door
		// separates sections Sec1 and Sec2, and closing it (and any other doors with the same pair of tags) will isolate the two from each other, in case only one is breached
		
		const float MAX_ATMOSPHERE = 0.5F; //The value (out of 1) below which atmospheric pressure is low enough to be considerered space/unbreathable and in which lockdowns apply
		const bool CLOSE_ALL_DOORS = false; //Whether to close (and keep closed) all doors in the ship whenever a breach is detected
		const bool SEPARATE_BREACHES = true; //Whether to close doors between two breached sections; not recommended to disable this
		
		const int SCREEN_UPDATE_RATE = 5; //How fast to update displays; larger is less often. More rapid updates mean more lag, especially if you have lots (15+) of screens. Default is about 1.5 times per second.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		static bool isExternalDoor(string name) {
			//Either contains "External", or both "Outer" and either "Hangar" or "Airlock" (ie outer airlock doors)
			return name.Contains(EXTERNAL_BLOCK_ID) || ((name.Contains("Hangar") || name.Contains("Airlock")) && name.Contains("Outer"));
		}
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly ShipLayout layout;
		
		const string whiteArrow = "";
		const string redArrow = "";
		const string greenArrow = "";
		const string blueArrow = "";
		const string yellowArrow = "";
		const string magentaArrow = "";
		const string cyanArrow = "";
		const string dot = "‧";
		
		private int tick = 0;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			layout = new ShipLayout(this);
			layout.build();
		}
		
		public void Main() { //called each cycle
			layout.tick(tick);
			tick++;
		}
		
		internal class ShipLayout {
			
			private readonly MyGridProgram caller;
			
			private readonly VRage.Game.ModAPI.Ingame.IMyCubeGrid shipGrid;
			
			private readonly Dictionary<string, SectionBlocks> sections = new Dictionary<string, SectionBlocks>();
			private readonly List<string> sectionSorting = new List<string>();
			private readonly List<InterfaceDoor> interDoors = new List<InterfaceDoor>();
			
			private readonly List<IMyAirVent> externalVents = new List<IMyAirVent>();
			private readonly List<IMyDoor> externalDoors = new List<IMyDoor>();
			private readonly List<IMyGasTank> o2Tanks = new List<IMyGasTank>();
			
			private readonly List<IMyDoor> extraDoors = new List<IMyDoor>();
		
			private readonly List<Display> displays = new List<Display>();
			
			private HashSet<string> breachedSections = new HashSet<string>();	
			private HashSet<string> breachedSectionsLastTick = new HashSet<string>();			
			private bool atmoLastTick;
			
			internal ShipLayout(MyGridProgram p) {
				caller = p;
				shipGrid = caller.Me.CubeGrid;
			}
			
			internal void build() {
				List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
				
				Dictionary<string, List<IMyTerminalBlock>> blocks = new Dictionary<string, List<IMyTerminalBlock>>();
				caller.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(new List<IMyTerminalBlock>(), b => cacheBlock(b, blocks));
				foreach (var entry in blocks) {
					SectionBlocks sec = new SectionBlocks(shipGrid, entry.Key, entry.Value);
					sections.Add(sec.sectionName, sec);
					sectionSorting.Add(sec.sectionName);
				}
				
				sectionSorting.Sort(new SectionComparator(sections));
				
				caller.GridTerminalSystem.GetBlocksOfType<IMyAirVent>(externalVents, b => b.CustomName.Contains(EXTERNAL_BLOCK_ID) && b.CubeGrid == caller.Me.CubeGrid);
				caller.GridTerminalSystem.GetBlocksOfType<IMyGasTank>(o2Tanks, b => b.CubeGrid == caller.Me.CubeGrid && (b.BlockDefinition.SubtypeName.ToLower().Contains("oxygen") || b.BlockDefinition.SubtypeName.ToLower().Contains("o2")));
				
				List<IMyDoor> li = new List<IMyDoor>();
				caller.GridTerminalSystem.GetBlocksOfType<IMyDoor>(li, b => b.CubeGrid == caller.Me.CubeGrid);
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
			
				List<IMyTextPanel> li2 = new List<IMyTextPanel>();
				caller.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(li2, b => b.CustomName.Contains(DISPLAY_TAG) && b.CubeGrid == caller.Me.CubeGrid);
				foreach (IMyTextPanel scr in li2) {
					displays.Add(new Display(scr));
				}
			}
			
			private bool cacheBlock(IMyTerminalBlock block, Dictionary<string, List<IMyTerminalBlock>> blocks) {
				string sec = getSection(block);
				if (sec != null) {
					List<IMyTerminalBlock> li = null;
					blocks.TryGetValue(sec, out li);
					if (li == null) {
						li = new List<IMyTerminalBlock>();
						blocks.Add(sec, li);
					}
					li.Add(block);
				}
				return false; //never actually collect into discarded list
			}
			
			private string getSection(IMyTerminalBlock block) {
				if (block.CubeGrid != caller.Me.CubeGrid)
					return null;
				
				string id = null;
				if (block is IMyAirVent) {
					id = VENT_ID;
				}
				else if (block is IMyLightingBlock) {
					id = LIGHT_ID;
				}
				else if (block is IMySoundBlock) {
					id = SOUND_ID;
				}
				
				if (id == null || !block.CustomName.Contains(id) || block.CustomName.Contains(EXTERNAL_BLOCK_ID))
					return null;
				
				string repl = block.CustomName.Replace(id, "");
				while (repl[0] == ' ' || repl[0] == ':' || repl[0] == '-') {
					repl = repl.Substring(1);
				}
				while (repl[repl.Length-1] == ' ' || repl[repl.Length-1] == ':' || repl[repl.Length-1] == '-' || char.IsNumber(repl[repl.Length-1])) {
					repl = repl.Substring(0, repl.Length-1);
				}
				return repl;
			}
			
			internal void tick(int tick) {
				bool atmo = noAtmo();
				
				foreach (Display scr in getUpdatingDisplays(tick)) {
					scr.display.WriteText(""); //clear
				}
				
				bool logic = tick%5 == 0;
				
				if (atmo) {					
					showText("No external atmosphere present.", tick);
					
					if (atmoLastTick != atmo) { //accidentally left outside doors open? But only want to fire this once, not continuously, or it keeps closing airlocks
						setExternalDoors(false);
					}
					
					if (logic) {
						breachedSections.Clear();
						
						HashSet<string> noAir = new HashSet<string>();
						HashSet<string> depressure = new HashSet<string>();
						
						foreach (string name in sectionSorting) {
							SectionBlocks sec = null;
							sections.TryGetValue(name, out sec);
							bool flag = false;
							
							if (sec.isDepressurized()) { //airlocks reguarly get opened to space; this is not a problem
								showStatus(sec, true, false, tick);
								if (sec.getAirPressureFraction() <= 0.01)
								noAir.Add(name);
								depressure.Add(name);
							}
							else if (sec.isBreached()) {
								showStatus(sec, false, true, tick);
								flag = true;
							}
							else {
								showStatus(sec, false, false, tick);
							}
							
							if (flag) {
								breachedSections.Add(name);
								noAir.Add(name);
								sec.onBreach();
							}
							else {
								sec.reset();
							}
						}
						foreach (InterfaceDoor inter in interDoors) {
							//close any doors that interface with a breached section, but open any that are sealed on both sides, provided neither section is not actively attempting to depressurize
							//but close all doors the first cycle, to try to determine which sections are actually breached (not just exposed via door access)
							inter.setState((SEPARATE_BREACHES ? (!noAir.Contains(inter.sectionA) && !noAir.Contains(inter.sectionB)) : noAir.Contains(inter.sectionA) == noAir.Contains(inter.sectionB)) && depressure.Contains(inter.sectionA) == depressure.Contains(inter.sectionB) && breachedSectionsLastTick.SetEquals(breachedSections));
						}
						
						if (breachedSections.Count > 0) {
							if (o2Tanks.Count > 0) {
								showText("Oxygen Reserves at "+getOxygenReserves()*100+"%", tick);
								if (CLOSE_ALL_DOORS) {
									setAllDoors(false);
								}
							}
						}
					}
					else {
						foreach (string name in sectionSorting) {
							SectionBlocks sec = null;
							sections.TryGetValue(name, out sec);
							showStatus(sec, sec.isDepressurized(), sec.isBreached(), tick);
						}
					}
				}
				else {
					showText("Usable external atmosphere present.", tick);
					if (logic) {
						foreach (IMyAirVent vent in externalVents) { //fill O2 tanks, if present
							vent.Depressurize = true;
						}
						setAllDoors(true); //fresh air
					}
					showText("All doors open.", tick);
				}
				
				atmoLastTick = atmo;
				breachedSectionsLastTick = new HashSet<string>(breachedSections);
			}
		
			private void showText(string text, int tick) {
				foreach (Display scr in getUpdatingDisplays(tick)) {
					scr.display.WriteText(text+"\n", true);
					//scr.ShowPublicTextOnScreen();
				}
				caller.Echo(text);
			}
		
			private void showStatus(SectionBlocks sec, bool depressure, bool breach, int tick) {
				int lines = sections.Count;
				float size = 1;
				if (lines > 18) {
					size -= (lines-18)*0.04F;
				}
				size = Math.Min(size, 0.67F);
				int maxSections = 44; //assuming zero padding and zero name length
				float ds = size-0.5F;
				maxSections = (int)(maxSections-16F*ds);
				int maxw = (maxSections/2);
				int pad = maxw-sec.sectionName.Length;
				int barSize = maxw;
				float f = depressure || breach ? 0 : sec.getAirPressureFraction();
				int fill = (int)(f*barSize+0.5);
				int red = barSize/4;
				int yellow = barSize/2;
				foreach (Display scr in getUpdatingDisplays(tick)) {
					scr.display.FontSize = size;
					scr.display.Font = "Monospace";
					String line = sec.sectionName+":";
					for (int i = 0; i < pad; i++) {
						int p = i+sec.sectionName.Length;
						line = line+(i == 0 || i == pad-1 || p%2 == 0 ? " " : dot);
					}
					for (int i = 0; i < barSize; i++) {
						bool has = i < fill;
						string color = has ? (i < red ? redArrow : (i < yellow ? yellowArrow : greenArrow)) : whiteArrow;
						if (depressure)
							color = cyanArrow;
						else if (breach)
							color = scr.update%2 == 0 ? magentaArrow : whiteArrow;
						line = line+color;
					}
					scr.display.WriteText(line+"\n", true);
				}
				caller.Echo(sec.sectionName+" Status: "+(depressure ? "Depressurized" : (breach ? "Breached!" : "Pressurized @ "+sec.getAirPressureFraction()*100+"%")));
			}
			
			private double getOxygenReserves() {
				double p = 0;
				foreach (IMyGasTank tank in o2Tanks) {
					p += tank.FilledRatio;
		        }
				return p/o2Tanks.Count;
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
			
			private List<Display> getUpdatingDisplays(int tick) {
				int mod = tick%SCREEN_UPDATE_RATE;
				List<Display> ret = new List<Display>();
				foreach (Display d in displays) {
					if (d.tickOffset == mod) {
						ret.Add(d);
						d.update++;
					}
				}
				return ret;
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
		
			private readonly VRage.Game.ModAPI.Ingame.IMyCubeGrid grid;
			public readonly string sectionName;
			
			private readonly Vector3D centerPosition = new Vector3D(0, 0, 0);
			
			//private readonly List<IMyDoor> doors = new List<IMyDoor>();
			private readonly List<IMyAirVent> vents = new List<IMyAirVent>();
			private readonly List<IMyLightingBlock> o2Lights = new List<IMyLightingBlock>();
			private readonly List<IMySoundBlock> alarms = new List<IMySoundBlock>();
			
			internal SectionBlocks(VRage.Game.ModAPI.Ingame.IMyCubeGrid grid, string name, List<IMyTerminalBlock> blocks) {
				this.grid = grid;
				sectionName = name;
				
				int blockCount = 0;
				foreach (IMyTerminalBlock block in blocks) {
					bool flag = false;
					if (block is IMyAirVent) {
						vents.Add(block as IMyAirVent);
						flag = true;
					}
					else if (block is IMyLightingBlock) {
						o2Lights.Add(block as IMyLightingBlock);
						flag = true;
					}
					else if (block is IMySoundBlock) {
						alarms.Add(block as IMySoundBlock);
						flag = true;
					}
					if (flag) {
						centerPosition += block.GetPosition();
						blockCount++;
					}
				}
				if (blockCount > 0) {
					centerPosition.X /= blockCount;
					centerPosition.Y /= blockCount;
					centerPosition.Z /= blockCount;
				}
				
				if (vents.Count == 0) {
					throw new Exception("Section "+sectionName+" has no corresponding air vents!");
				}
			}
			
			public Vector3D getRelativeCenter() {
				return this.centerPosition-grid.GetPosition();
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
		
		internal class Display {
			
			private static readonly Random rand = new Random();
			
			internal readonly IMyTextPanel display;
			internal readonly int tickOffset;
			
			internal int update = 0;
			
			internal Display(IMyTextPanel txt) {
				display = txt;
				tickOffset = rand.Next(SCREEN_UPDATE_RATE);
				txt.ContentType = ContentType.TEXT_AND_IMAGE;
			}
			
		}
		
		internal class SectionComparator : IComparer<string> {
			
			private readonly Dictionary<string, SectionBlocks> data = new Dictionary<string, SectionBlocks>();
			
			internal SectionComparator(Dictionary<string, SectionBlocks> map) {
				data = map;
			}
			
			public int Compare(string s1, string s2) {
				SectionBlocks sec1 = null;
				SectionBlocks sec2 = null;
				data.TryGetValue(s1, out sec1);
				data.TryGetValue(s2, out sec2);
				if (sec1 == null || sec2 == null) {
					return 0;
				}
				return sec1.getRelativeCenter().Length().CompareTo(sec2.getRelativeCenter().Length());
			}
			
		}
		
		//====================================================
	}
}