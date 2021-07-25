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

namespace Ingame_Scripts.AnacondaProjector {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------	
		
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
				
		private IMyProjector projector;
		private bool done;
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
			
			List<IMyProjector> li = new List<IMyProjector>();
			GridTerminalSystem.GetBlocksOfType<IMyProjector>(li, b => b.CubeGrid == Me.CubeGrid);
			this.projector = li[0];
		}
		
		public void Main() { //called each cycle
			if (done)
				return;
			projector.ProjectionOffset = new Vector3I(4, 7, 16); //xyz = horiz/vertical/forwards
			projector.ProjectionRotation = new Vector3I(-1, 0, 0);
			projector.UpdateVisual();
			projector.UpdateOffsetAndRotation();
			done = true;
		}
		
		//====================================================
	}
}