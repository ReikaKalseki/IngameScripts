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

namespace Ingame_Scripts.JumpGateControl {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string EXCLUSION_TAG = "NonDriveGravGen"; //Gravity generators with this in their name will be ignored for the jump gate control
		
		//Whether to automatically determine the direction of the gate by counting grav gens.
		//If false, assumes directionality inline wiht the programmable block running this script.
		const bool FIND_DIRECTION_AUTOMATICALLY = false;
		const float RECHARGE_THRESHOLD = 0.8F; //If the battery fill fraction drops below this, recharging mode will be enabled.
		const float WAVE_SPEED = 1F; //How fast the light wave should move down the gate
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<GravitySource> gravGens = new List<GravitySource>();
		private readonly List<IMySensorBlock> sensors = new List<IMySensorBlock>();
		private readonly List<IMyTerminalBlock> powerGen = new List<IMyTerminalBlock>();
		private readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
		private readonly List<Light> lights = new List<Light>();
		
		private Vector3D jumpGateCenter = new Vector3D(0, 0, 0);
		internal Base6Directions.Direction prevailingGateDirection;
		
		private bool driveActive;
		private bool recharging;
		
		private int tick;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update1;
			
			//foreach (IMyCubeBlock block in Me.CubeGrid.
			prevailingGateDirection = Me.Orientation.Up;
			
			List<IMyGravityGenerator> li = new List<IMyGravityGenerator>();
			GridTerminalSystem.GetBlocksOfType<IMyGravityGenerator>(li, b => b.CubeGrid == Me.CubeGrid && !b.CustomName.Contains(EXCLUSION_TAG));
			if (FIND_DIRECTION_AUTOMATICALLY) {
				Dictionary<Base6Directions.Direction, int> counts = new Dictionary<Base6Directions.Direction, int>();
				foreach (IMyGravityGenerator gen in li) {
					Base6Directions.Direction dir = gen.Orientation.Up;
					int has = 0;
					counts.TryGetValue(dir, out has);
					has++;
					counts[dir] = has;
				}
				int max = 0;
				Base6Directions.Direction found = prevailingGateDirection;
				foreach (var entry in counts) {
					if (entry.Value > max) {
						max = entry.Value;
						found = entry.Key;
					}
				}
				prevailingGateDirection = found;
			}
			foreach (IMyGravityGenerator gen in li) {
				if (gen.Orientation.Up == prevailingGateDirection || gen.Orientation.Up == Base6Directions.GetFlippedDirection(prevailingGateDirection))
					gravGens.Add(new GravitySource(gen, prevailingGateDirection));
			}
			
			GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, b => b.CubeGrid == Me.CubeGrid);
			
			List<IMyReactor> li3 = new List<IMyReactor>();
			GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, b => b.CubeGrid == Me.CubeGrid);
			GridTerminalSystem.GetBlocksOfType<IMyReactor>(li3, b => b.CubeGrid == Me.CubeGrid);
			powerGen.AddArray<IMyTerminalBlock>(batteries.ToArray());
			powerGen.AddArray<IMyTerminalBlock>(li3.ToArray());
			
			foreach (GravitySource gen in gravGens) {
				gen.prepare();
			}
			
			List<IMyLightingBlock> li2 = new List<IMyLightingBlock>();
			GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(li2, b => b.CubeGrid == Me.CubeGrid);
			foreach (IMyLightingBlock b in li2) {
				lights.Add(new Light(b, prevailingGateDirection));
			}
		}
		
		public void Main() {
			if (tick%120 == 0) { //every 2s
				bool wait = isShipWaiting();
				Echo("Ship Waiting: "+wait);
				bool player = false;//arePlayersInside();
				//Echo("Players in Station: "+player);
				recharging = needsRecharging();
				Echo("Currently Recharging: "+recharging);
				setRecharge(recharging);
				driveActive = wait && !player && !recharging;
				Echo("Drive Enabled: "+driveActive);
				setState(driveActive);
			}
			Color c = getBaseColor();
			foreach (Light l in lights) {
				l.light.Color = l.isExitLight ? Color.Red : c;
				if (!l.isSpotlight) {
					//l.light.Radius = l.calcIntensity(tick);
					l.light.Color = Color.Lerp(Color.Black, c, l.calcIntensityFactor(tick));
				}
			}
			tick++;
		}
		
		private VRageMath.Color getBaseColor() {
			return recharging ? Color.DeepSkyBlue : (driveActive ? Color.Yellow : Color.Lime);
		}
		
		private bool needsRecharging() {
			float has = 0;
			float max = 0;
			foreach (IMyBatteryBlock b in batteries) {
				has += b.CurrentStoredPower;
				max += b.MaxStoredPower;
			}
			return has/max <= RECHARGE_THRESHOLD;
		}
		
		private void setRecharge(bool recharge) {
			foreach (GravitySource g in gravGens) {
				g.setEnabled(!recharge);
			}
			foreach (IMyBatteryBlock b in batteries) {
				b.OnlyRecharge = true;
			}
		}
			
		private bool arePlayersInside() {
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
				sensor.DetectedEntities(li);
				if (li.Count > 0)
					return true;
			}
			return false;
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
				sensor.DetectPlayers = false;
				sensor.DetectNeutral = false;
				sensor.DetectedEntities(li);
				if (li.Count > 0)
					return true;
			}
			return false;
		}
		
		private void setState(bool on) {
			foreach (IMyTerminalBlock b in powerGen) {
				if (on) {
					if (b is IMyBatteryBlock) {
						((IMyBatteryBlock)b).ChargeMode = ChargeMode.Auto;
					}
					else {
						b.ApplyAction("OnOff_On");
					}
				}
				else {
					if (b is IMyBatteryBlock) {
						((IMyBatteryBlock)b).ChargeMode = ChargeMode.Recharge;
					}
					else {
						b.ApplyAction("OnOff_Off");
					}
				}
			}
		}
		
		internal class GravitySource {
			
			private readonly IMyGravityGenerator generator;
			private readonly Vector3I position;
			private readonly bool isInverted;
			
			internal GravitySource(IMyGravityGenerator gen, Base6Directions.Direction up) {
				generator = gen;
				position = gen.Position;
				isInverted = gen.Orientation.Up != up;
			}
			
			internal void prepare() {
				generator.Enabled = true;
				generator.GravityAcceleration = isInverted ? -9.81F : 9.81F;
			}
			
			internal void setEnabled(bool on) {
				generator.Enabled = on;
			}
			
		}
		
		internal class Light {
			
			internal readonly IMyLightingBlock light;
			internal readonly bool isSpotlight;
			internal readonly bool isExitLight;
			
			private readonly float maxIntensity;
			private readonly float maxRadius;
			
			private readonly int positionValue;
			
			internal Light(IMyLightingBlock b, Base6Directions.Direction dir) {
				light = b;
				
				isSpotlight = b.BlockDefinition.SubtypeName.ToLowerInvariant().Contains("frontlight");
				isExitLight = isSpotlight && b.Orientation.Forward == Base6Directions.GetFlippedDirection(dir);
				
				b.Intensity = 20;
				b.Radius = 200;
				b.Falloff = 4;
				
				maxIntensity = b.Intensity;
				maxRadius = b.Radius;
				
				switch(dir) {
					case Base6Directions.Direction.Backward: //+Z
						positionValue = b.Position.Z;
						break;
					case Base6Directions.Direction.Forward:
						positionValue = -b.Position.Z;
						break;
					case Base6Directions.Direction.Right: //+X
						positionValue = b.Position.X;
						break;
					case Base6Directions.Direction.Left:
						positionValue = -b.Position.X;
						break;
					case Base6Directions.Direction.Down:
						positionValue = -b.Position.Y;
						break;
					case Base6Directions.Direction.Up: //+Y
						positionValue = b.Position.Y;
						break;
				}
			}
			
			internal float calcIntensityFactor(int tick) {
				double ang = (tick*WAVE_SPEED+positionValue*10)%360D;
				return (float)Math.Pow(0.67+0.33*Math.Sin(Math.PI*ang/180D), 2);
			}
		}
		
		//====================================================
	}
}