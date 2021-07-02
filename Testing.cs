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

namespace Ingame_Scripts.Testing {
	
	public class Program : MyGridProgram {
		
			private readonly List<IMyTextPanel> screens = new List<IMyTextPanel>();
			
			private readonly float BLUE_THRESH = 0.95F;
			private readonly float GREEN_THRESH = 0.7F;
			private readonly float YELLOW_THRESH = 0.5F;
			private readonly float ORANGE_THRESH = 0.3F;
			private readonly float RED_THRESH = 0.1F;
			
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
			
			public Program() {
				GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens);
				Runtime.UpdateFrequency = UpdateFrequency.Update1;
			}
			
			public void Main() { //called each cycle
				float f = 0.4F;
				foreach (IMyTextPanel scr in screens) {
					setScreenContent(scr, f);
				}
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
	}
}