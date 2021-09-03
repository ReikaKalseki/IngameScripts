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

namespace Ingame_Scripts.CargoEvac {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------		
		
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		//Change the body of any of these as you see fit to configure how certain functions are evaluated.
		//----------------------------------------------------------------------------------------------------------------
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
		private readonly List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
		private readonly List<IMyReactor> reactors = new List<IMyReactor>();
		private readonly List<IMyJumpDrive> jump = new List<IMyJumpDrive>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(turrets, b => b.CubeGrid.IsSameConstructAs(Me.CubeGrid));
			GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors, b => b.CubeGrid.IsSameConstructAs(Me.CubeGrid));
			GridTerminalSystem.GetBlocksOfType<IMyJumpDrive>(jump, b => b.CubeGrid.IsSameConstructAs(Me.CubeGrid));
		}
		
		public void Main() {	
			if (shouldEscape()) {
				jump[0].ApplyAction("Jump");
			}
		}
		
		private bool shouldEscape() {
			return false;
		}
		
		//====================================================
	}
}