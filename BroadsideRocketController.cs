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

namespace Ingame_Scripts.BroadsideRocketController {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		const string EXCLUSION_TAG = "ManualRocket";
		const int MAX_FIRE_RATE = 1; //how many seconds per shot each rocket launcher should wait to fire. Cannot be less than maximal fire rate (1 s/shot).
		
		const string SENSOR_TAG = "RocketSensors";
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly Random rand = new Random();  
		
		private readonly List<Launcher> launchers = new List<Launcher>();
		private readonly List<Sensor> sensors = new List<Sensor>();
		private readonly int ticksPerLaunch;
		private readonly int delayPerLauncher;
		
		private int tick;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update1;
			
			List<IMySmallMissileLauncher> li = new List<IMySmallMissileLauncher>();
			shuffle(li);
			GridTerminalSystem.GetBlocksOfType<IMySmallMissileLauncher>(li, b => b.CubeGrid == Me.CubeGrid && !b.CustomName.Contains(EXCLUSION_TAG));
			ticksPerLaunch = MAX_FIRE_RATE*60;
			delayPerLauncher = ticksPerLaunch/li.Count;
			int d = 0;
			foreach (IMySmallMissileLauncher b in li) {
				launchers.Add(new Launcher(b, ticksPerLaunch, d));
				//Echo("Queued a launcher with a delay of "+d+" ticks");
				d += delayPerLauncher;
			}
			
			List<IMySensorBlock> li2 = new List<IMySensorBlock>();
			GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(li2, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(SENSOR_TAG));
			foreach (IMySensorBlock b in li2) {
				sensors.Add(new Sensor(b));
			}
		}

		private void shuffle<T>(IList<T> list)   {  
		    int n = list.Count;  
		    while (n > 1) {  
		        n--;  
		        int k = rand.Next(n + 1);  
		        T value = list[k];  
		        list[k] = list[n];  
		        list[n] = value;  
		    }  
		}
		
		public void Main() {
			if (sensors.Count > 0) {
				
			}
			foreach (Launcher l in launchers) {
				l.tick(tick);
			}
			tick++;
		}
		
		internal class Launcher {
			
			private readonly IMySmallMissileLauncher tube;
			private readonly int cooldown;
			private readonly int offset;
			private readonly Base6Directions.Direction direction;
			
			internal Launcher(IMySmallMissileLauncher b, int timer, int offset) {
				tube = b;
				cooldown = timer;
				this.offset = offset;
				direction = b.Orientation.Forward;
			}
			
			public void tick(int time) {
				if (tube.Enabled) {
					if (time%cooldown == offset) {
						tube.ApplyAction("ShootOnce");
					}
				}
			}
			
		}
		
		internal class Sensor {
			
			private readonly IMySensorBlock sensor;
			private readonly Base6Directions.Direction direction;
			
			internal Sensor(IMySensorBlock b) {
				sensor = b;
				direction = sensor.Orientation.Forward;
			}
			
		}
		
		//====================================================
	}
}