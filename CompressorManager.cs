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

using IMyInventoryOwner = VRage.Game.ModAPI.Ingame.IMyInventoryOwner;
using IMyInventory = VRage.Game.ModAPI.Ingame.IMyInventory;
using IMyInventoryItem = VRage.Game.ModAPI.Ingame.IMyInventoryItem;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;
using IMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

namespace Ingame_Scripts.CompressorManager {
	
	public class Program : MyGridProgram {
		
		//ACTUAL PROGRAM YOU WOULD COPY BEGINS BELOW THIS LINE
		//====================================================

		//----------------------------------------------------------------------------------------------------------------
		//Change the values of any of these as you see fit to configure the script as per your ship configuration or needs
		//----------------------------------------------------------------------------------------------------------------
		//What fill % to stop emptying tanks into compressors at or start emptying compressors into tanks 
		private const float minimumO2TankFraction = 0.1F;
		private const float maximumO2TankFraction = 0.75F;
		private const float minimumH2TankFraction = 0.25F;
		private const float maximumH2TankFraction = 0.8F;
			
		//The thresholds (fractions out of 1.0) at which a fill bar changes color
		private const float GREEN_THRESH = 0.75F;
		private const float YELLOW_THRESH = 0.5F;
		private const float RED_THRESH = 0.25F;
		
		const string SCREEN_TAG = "CompressorScreen"; //Any LCD panel with this in its name will show the gas levels between tanks and compressors
		//----------------------------------------------------------------------------------------------------------------
		
		
		//----------------------------------------------------------------------------------------------------------------
		//Do not change anything below here, as this is the actual program.
		//----------------------------------------------------------------------------------------------------------------
				
		private readonly Dictionary<string, GasSystem> systems = new Dictionary<string, GasSystem>();
			
		private readonly Color GREEN_COLOR = new Color(40, 255, 40, 255);
		private readonly Color YELLOW_COLOR = new Color(255, 255, 40, 255);
		private readonly Color RED_COLOR = new Color(255, 40, 40, 255);
			
		private readonly Color BLACK = new Color(0, 0, 0, 255);		
		private readonly Color GRAY1 = new Color(60, 60, 60, 255);	
		private readonly Color GRAY2 = new Color(20, 20, 20, 255);		
		private readonly Color WHITE = new Color(255, 255, 255, 255);		
		private readonly Vector2 screenSize = new Vector2(512, 512);
		private readonly float groupGap = 48;
		private readonly float barGap = 24;
		private readonly float edgePadding = 16;
		
		private readonly Vector2 barSizeBorder;
		private readonly Vector2 barSizeBar;
		
		private readonly Vector2 flowBox = new Vector2(20, 20);
		
		private readonly List<IMyTextPanel> screens = new List<IMyTextPanel>();
		
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			systems.Add("H2", new GasSystem("H2", "Hydrogen", minimumH2TankFraction, maximumH2TankFraction, this));
			systems.Add("O2", new GasSystem("O2", "Oxygen", minimumO2TankFraction, maximumO2TankFraction, this));
				
			int bars = systems.Count;
			int ggaps = bars-1;
			float gapSpace = ggaps*groupGap+barGap*bars+edgePadding*2;
			float barWidth = (screenSize.X-gapSpace)/bars/2;
			
			barSizeBorder = new Vector2(barWidth, 512);
			barSizeBar = new Vector2(barWidth-8, 512);
			
			GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens, b => b.CustomName.Contains(SCREEN_TAG) && b.CubeGrid == Me.CubeGrid);
		}
		
		public void Main() { //called each cycle
			List<MySpriteDrawFrame> li = new List<MySpriteDrawFrame>();
			foreach (IMyTextPanel scr in screens) {
				li.Add(prepareScreen(scr));
			}
			
			float x = edgePadding;
			foreach (string gas in systems.Keys) {
				float tankFrac = 0;
				float compFrac = 0;
				int flowDir = 0;
				systems[gas].handleTanks(out tankFrac, out compFrac, out flowDir);
				
				foreach (MySpriteDrawFrame frame in li) {
					addBarToScreen(frame, tankFrac, x);
					addBarToScreen(frame, compFrac, x+barGap+barSizeBorder.X);
					Vector2 flowPos = new Vector2(x+barSizeBorder.X+barGap/2, 256);
					switch(flowDir) {
						case -1:
							frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", size: flowBox, color: WHITE, position: flowPos, rotation: -1.57F)); //radians
							break;
						case 1:
							frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", size: flowBox, color: WHITE, position: flowPos, rotation: 1.57F));
							break;
						case 0:
							frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: flowBox, color: WHITE, position: flowPos));
							break;
					}
					MySprite text = MySprite.CreateText(gas, "monospace", WHITE, 3);
					text.Position = new Vector2(flowPos.X, flowPos.Y+96);
					frame.Add(text);
				}
				x += groupGap+barGap+barSizeBorder.X*2;
			}
			
			foreach (MySpriteDrawFrame frame in li) {
				frame.Dispose();
			}
		}
		
		private MySpriteDrawFrame prepareScreen(IMyTextPanel scr) {
			scr.ContentType = ContentType.SCRIPT;
       		MySpriteDrawFrame frame = scr.DrawFrame();
       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: screenSize, color: BLACK));
       		
       		float x = edgePadding;
       		for (int i = 0; i < systems.Count; i++) {
				Vector2 pos = new Vector2(x+barSizeBorder.X/2, 256);
	       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: barSizeBorder, color: GRAY1, position: pos));
	       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: barSizeBar, color: GRAY2, position: pos));
	       		x += barGap+barSizeBorder.X;
	       		
	       		pos = new Vector2(x+barSizeBorder.X/2, 256);
	       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: barSizeBorder, color: GRAY1, position: pos));
	       		frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", size: barSizeBar, color: GRAY2, position: pos));
	       		x += groupGap+barSizeBorder.X;
       		}
       		return frame;
		}
			
		private void addBarToScreen(MySpriteDrawFrame frame, float f, float x) {			
       		drawBox(frame, f, x, GREEN_COLOR, 1);
       		drawBox(frame, f, x, YELLOW_COLOR, GREEN_THRESH);
       		drawBox(frame, f, x, RED_COLOR, YELLOW_THRESH); 
		}
		
		private void drawBox(MySpriteDrawFrame frame, float f, float x, Color c, float limit) {
			f = Math.Min(f, limit);
			float h = f*512;
			Vector2 box = new Vector2(barSizeBar.X, h);
    		Vector2 ctr = new Vector2(x+barSizeBorder.X/2, 512-h/2);
    		MySprite sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: box, color: c);
       		sprite.Position = ctr;
			frame.Add(sprite);
		}
		
		internal class GasSystem {
			
			private readonly string name;
			
			private readonly float minFrac;
			private readonly float maxFrac;
			
			private readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
			private readonly List<IMyGasTank> compressors = new List<IMyGasTank>();
		
			private bool loading;
			private bool unloading;
			
			internal GasSystem(string name, string blockName, float min, float max, MyGridProgram caller) {
				this.name = name;
				minFrac = min;
				maxFrac = max;
				caller.GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks, b => b.CubeGrid == caller.Me.CubeGrid && b.CustomName.Contains(blockName) && !b.CustomName.Contains("Compress"));
				caller.GridTerminalSystem.GetBlocksOfType<IMyGasTank>(compressors, b => b.CubeGrid == caller.Me.CubeGrid && b.CustomName.Contains(blockName) && b.CustomName.Contains("Compress"));
			}
		
			internal void handleTanks(out float tankFrac, out float compFrac, out int flowDir) {
				float f = getTankLevel();
				tankFrac = f;
				compFrac = getCompressorLevel();
				bool fill = f >= (loading ? minFrac*1.2F : maxFrac);
				bool empty = f < (unloading ? maxFrac/1.2F : minFrac);
				if (fill && compFrac >= 0.999)
					fill = false;
				if (empty && compFrac <= 0.001)
					empty = false;
				//Echo(name+" status: "+String.Format("{0:0.000}", (tankFrac*100))+"% tanks, "+String.Format("{0:0.000}", (compFrac*100))+" compressors = "+fill+"/"+empty);
				if (fill) {
					loading = true;
					unloading = false;
					setCompressorStatuses(true, true);
					setTankStatuses(false);
				}
				else if (empty) {
					loading = false;
					unloading = true;
					setCompressorStatuses(true, false);
					setTankStatuses(true);
				}
				else {
					loading = false;
					unloading = false;
					setCompressorStatuses(false, false);
					setTankStatuses(false);
				}
				flowDir = loading ? 1 : (unloading ? -1 : 0);
			}
			
			private float getTankLevel() {
				double amt = 0;
				double cap = 0;
				foreach (IMyGasTank tank in tanks) {
					amt += tank.FilledRatio*tank.Capacity;
					cap += tank.Capacity;
				}
				return (float)(amt/cap);
			}
			
			private float getCompressorLevel() {
				double amt = 0;
				double cap = 0;
				foreach (IMyGasTank tank in compressors) {
					amt += tank.FilledRatio*tank.Capacity;
					cap += tank.Capacity;
				}
				return cap == 0 ? 0 : (float)(amt/cap);
			}
			
			private void setCompressorStatuses(bool enable, bool stockpile) {
				foreach (IMyGasTank tank in compressors) {
					tank.Enabled = enable;
					tank.Stockpile = stockpile;
				}
			}
			
			private void setTankStatuses(bool stockpile) {
				foreach (IMyGasTank tank in tanks) {
					tank.Stockpile = stockpile;
				}
			}
			
		}
		
		//====================================================
	}
}