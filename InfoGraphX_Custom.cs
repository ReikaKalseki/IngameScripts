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

using IMyInventoryOwner = VRage.Game.ModAPI.Ingame.IMyInventoryOwner;
using IMyInventory = VRage.Game.ModAPI.Ingame.IMyInventory;
using IMyInventoryItem = VRage.Game.ModAPI.Ingame.IMyInventoryItem;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;
using IMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

using VRage.Game.GUI.TextPanel;


/*

	---------------------
	InfoGraphx
	by SwiftyTheFox
	v1.1.1
	---------------------
		
	! Editing and republishing is permitted as long as 
	! the original is mentioned in the source code and 
	! in the workshop description by a workshop link.	
	
	Lost?
	The script can be fully configured int he "program" and "main" function below.

	Here is the manual:
	https://docs.google.com/document/d/1CtRepiuXXFldJGH6Uze5O-HZUcpSVA7Q8fn9rBeHnQg/edit#

Credits go to:
- The Crowd on the Keen Discord	 for the support
- Georgik for rewriting the UpdateGraph function. This was a quite a game changer performance wise.
- Krypt for finding a Bug related to Text panels
- Korvatus Klock for the Idea of the Hex Graph

Thank you, without you the script would not have gotten that far.

*/
/*



    public enum enGraphType {GAUGE, BAR, HEXAGON, TEXT, ICON};
    public enum enGraphSubType {SIMPLE};
    public enum enGraphStyleType {FIXED, GRADIENT};
    public enum enGraphIcon {NONE, ENERGY, HYDROGEN, OXYGEN, STORAGE, TEMPERATURE, CIRCLE, SQUARE, TEXT};

    public enum enContentType {BATTERY, SOLAR, HYDROGEN, OXYGEN, STORAGE};
    public enum enSourceType {NAME, GROUP};

	
public class Graph
    {
   
    public List<float> Progress = new List<float>();
    private List<float> ThresholdValue = new List<float>();
    private List<Color> ThresholdColor = new List <Color>();
    
    private Color backgroundColor = new Color(0,0,0,255);
    private Color emptyBarColor   = new Color(70,70,70,255);
    private Color iconColor   = new Color(70,70,70,255);
    private Color defaultBarColor = new Color(255,35,35,255);
 
    private enGraphType GraphType = 0;
    private enGraphSubType GraphSubType = 0;
    private enGraphStyleType GraphStyleType = 0;
    private enGraphIcon GraphIconType = 0;
    private string IconText = "";
    private string GraphText = "";    

    private Vector2 GraphCenter = new Vector2(128f,128f);
    private Vector2 GraphSize = new Vector2(128f,128f);

    //
    // Constructor
    //

    public Graph(enGraphType type, enGraphSubType subtype, enGraphStyleType  styletype)
        {
        GraphType = type;
        GraphSubType = subtype;
        GraphStyleType = styletype;

        Progress.Add(0f);
        AddColorPoint(0f, defaultBarColor);
        }

    public Graph(enGraphType type, enGraphSubType subtype, enGraphStyleType  styletype, Color mydefaultcolor)
        {
        GraphType = type;
        GraphSubType = subtype;
        GraphStyleType = styletype;

        Progress.Add(0f);
        AddColorPoint(0f, mydefaultcolor);
        }

    public Graph(enGraphType type, enGraphSubType subtype, enGraphStyleType  styletype, Vector2 center, Vector2 size)
        {
        GraphType = type;
        GraphSubType = subtype;
        GraphStyleType = styletype;
 
        GraphCenter = center;
        GraphSize = size;

        Progress.Add(0f);
        AddColorPoint(0f, defaultBarColor);
        }

    //
    // Public Functions
    //

    public void AddColorPoint(float threshold, Color color)
        {
        ThresholdValue.Add(threshold);
        ThresholdColor.Add(color);
        }

    public void SetBackgroundColor(Color backgroundcolor)
        {
        backgroundColor = backgroundcolor;
        }
  
    public void SetGraphIconColor(Color iconcolor)
        {
        iconColor = iconcolor;
        }
  
    public void SetEmptyBarColor(Color emptybarcolor)
        {
        emptyBarColor= emptybarcolor;
        }
  
    public void SetColorPoint(int pos, float threshold, Color color)
        {
        ThresholdValue[pos] = threshold;
        ThresholdColor[pos] = color;
        }

    public void SetGeometry(Vector2 center, Vector2 size)
        {
        GraphCenter = center;
        GraphSize = size;
        }

    public void SetGraphIcon(enGraphIcon icontype)
        {
        GraphIconType = icontype;
        }

    public void SetGraphIconText(string icontext)
        {
        IconText = icontext;
        }

    public void SetGraphText(string graphtext)
        {
        GraphText = graphtext;
        }

    public Color GetColorForProgress(float progress)
        {
        Color returnColor = new Color(0,0,0,0);

        for (int i=0; i < ThresholdValue.Count();i++)
            {
            if (progress > ThresholdValue[i])
                {
                returnColor = ThresholdColor[i];
                }
            }
        if (ThresholdValue.Count() == 1 || progress == 0.0f) 
            {
            returnColor = ThresholdColor[0];
            }

        return returnColor;
        }

    public void Draw(MySpriteDrawFrame frame)
        {
        switch (GraphType)
            {
            case enGraphType.GAUGE:
                ConstructGauge(frame);
                break;
            case enGraphType.BAR:
                ConstructBar(frame);
                break;
            case enGraphType.HEXAGON:
                ConstructHexagon(frame);
                break;
            case enGraphType.TEXT:
                ConstructText(frame);
                break;
            case enGraphType.ICON:
                ConstructIcon(frame);
                break;
            }
        }

    //
    // Private Functions
    //

    private string GetIconString(enGraphIcon icon)
        {
        switch(icon)
            {
            case enGraphIcon.ENERGY:
                return "IconEnergy";
            case enGraphIcon.HYDROGEN:
                return "IconHydrogen";
            case enGraphIcon.OXYGEN:
                return "IconOxygen";
            case enGraphIcon.STORAGE:
                return "SquareHollow";
            case enGraphIcon.SQUARE:
                return "SquareSimple";
            case enGraphIcon.CIRCLE:
                return "Circle"; 
           case enGraphIcon.TEMPERATURE:
                return "IconTemperature";
            case enGraphIcon.TEXT:
                return IconText;           
            default:
                return "";
            }
        }

    private void DrawStorageIcon(MySpriteDrawFrame frame, Vector2 center, Vector2 size)
        {
        Vector2 offset = new Vector2(0f,0f);
        Vector2 linesize = size;
        linesize.X = size.X*0.05f;
        linesize.Y = size.Y*0.5f;

        MySprite sprite;
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: linesize,
            color: iconColor
            );        
        offset.Y = linesize.Y/2f;
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.X = offset.X + linesize.Y * (float) Math.Cos(Math.PI/6);
        offset.Y = offset.Y - linesize.Y * (float) Math.Sin(Math.PI/6);
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.X = offset.X - linesize.Y * 2f * (float) Math.Cos(Math.PI/6);
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.X = linesize.Y/2f * (float) Math.Cos(Math.PI/6);
        offset.Y = linesize.Y *3f/2f* (float) Math.Sin(Math.PI/6);
        sprite.RotationOrScale = (float)Math.PI/3f;
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.Y = offset.Y - linesize.Y * 2f * (float) Math.Sin(Math.PI/6);
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.X = -linesize.Y/2f * (float) Math.Cos(Math.PI/6);
        offset.Y = offset.Y - linesize.Y /2f;
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.X = -linesize.Y/2f * (float) Math.Cos(Math.PI/6);
        offset.Y = linesize.Y *3f/2f* (float) Math.Sin(Math.PI/6);
        sprite.RotationOrScale = -(float)Math.PI/3f;
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.Y = offset.Y - linesize.Y * 2f * (float) Math.Sin(Math.PI/6);
        sprite.Position = center + offset;
        frame.Add(sprite);

        offset.X = linesize.Y/2f * (float) Math.Cos(Math.PI/6);
        offset.Y = offset.Y - linesize.Y /2f;
        sprite.Position = center + offset;
        frame.Add(sprite);

        }

    private void ConstructBar(MySpriteDrawFrame frame)
        {
        MySprite sprite;
        float height_full = GraphSize.Y*0.8f;
        float height_bar = GraphSize.Y*0.8f - GraphSize.X*0.2f;


        Vector2 fullBarCenter = GraphCenter;
        fullBarCenter.Y = fullBarCenter.Y - GraphSize.Y*0.1f+GraphSize.X*0.1f;        

        // Background 
     
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: GraphSize,
            color: backgroundColor
            );
        sprite.Position = GraphCenter;
        frame.Add(sprite);

        // Empty Bar

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: new Vector2(GraphSize.X*0.8f, height_full),
            color: emptyBarColor
            );       
  
        sprite.Position = fullBarCenter;
        frame.Add(sprite);

        // Progress

        if (GraphSubType == (int) enGraphSubType.SIMPLE)
            {  

            sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
                size: new Vector2(GraphSize.X * 0.7f, height_bar*Progress[0])
                );
            sprite.Color = GetColorForProgress(Progress[0]);
            sprite.Position = fullBarCenter + new Vector2(0f,height_bar*(1f-Progress[0])/2);
            frame.Add(sprite);

            if (GraphStyleType == enGraphStyleType.GRADIENT)
                {
                for (int i=ThresholdValue.Count()-1; i >= 0;i--)
                    {
                    if (Progress[0] > ThresholdValue[i])
                        {
                        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
                            size: new Vector2(GraphSize.X*0.7f, height_bar*ThresholdValue[i])
                            );
                        sprite.Color = GetColorForProgress(ThresholdValue[i]);
                        sprite.Position = fullBarCenter + new Vector2(0f,height_bar*(1f-ThresholdValue[i])/2);
                        frame.Add(sprite);                   
                        }
                    }
                }
            }       

        // Icon 
        if (GraphIconType != enGraphIcon.NONE)
            {
            if (GraphIconType == enGraphIcon.STORAGE) 
                {
                DrawStorageIcon(frame, GraphCenter, GraphSize*0.2f);
                }
            else if (GraphIconType == enGraphIcon.TEXT) 
                {
                sprite = MySprite.CreateText(IconText, "debug", iconColor, GraphSize.Y/256f, TextAlignment.CENTER);
                sprite.Position = GraphCenter+new Vector2(0f,GraphSize.Y*0.4f-GraphSize.X*0.25f);
                frame.Add(sprite);    
                }
            else
                {
                sprite = new MySprite(SpriteType.TEXTURE, GetIconString(GraphIconType) , 
                size: new Vector2(GraphSize.X*0.5f, GraphSize.X*0.5f), 
                    color: iconColor
                    );
                sprite.Position = GraphCenter+new Vector2(0f,GraphSize.Y*0.4f);
                frame.Add(sprite);     
                }
            }

        }

    private void ConstructGauge(MySpriteDrawFrame frame)
        {
        MySprite sprite;
        
        float angle;
        Vector2 barSize = new Vector2(GraphSize.Y*0.18f,GraphSize.Y*0.02f);
        Vector2 offset = new Vector2(0,0);

        Color tempColor = new Color(255,255,255,255);
        
        // Background 
     
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: GraphSize,
            color: backgroundColor
            );
        sprite.Position = GraphCenter;
        frame.Add(sprite);

        // Disc
        sprite = new MySprite(SpriteType.TEXTURE, "Circle" , 
            size: GraphSize*0.8f,
            color: emptyBarColor
            );
            sprite.Position = GraphCenter;
            frame.Add(sprite);

        // Data / Progress

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: barSize
            );

        if (GraphSubType == (int) enGraphSubType.SIMPLE)
            {
            if (GraphStyleType == (int) enGraphStyleType.FIXED) sprite.Color = GetColorForProgress(Progress[0]);
            for (float i=0f; i< Progress[0]; i += 0.01f)
                {

                angle = (float)Math.PI*3/2*(float)i- (float)Math.PI / 4f;

                offset.X = -(float)Math.Cos(angle)*GraphSize.Y*0.3f;
                offset.Y = -(float)Math.Sin(angle)*GraphSize.Y*0.3f;

                sprite.Position =  GraphCenter+offset;
                sprite.RotationOrScale = angle;

                if (GraphStyleType == enGraphStyleType.GRADIENT) 
                    {sprite.Color= GetColorForProgress(i);}

                frame.Add(sprite);
                }
            }

        // Disc Cover
        sprite = new MySprite(SpriteType.TEXTURE, "Circle" , 
            size: GraphSize*0.4f,
            color: backgroundColor
            );
            sprite.Position = GraphCenter;
            frame.Add(sprite);


        // Cut outs    
            float HeightTriangle=(float)Math.Sqrt(3)/2f;
            sprite = new MySprite(SpriteType.TEXTURE, "Triangle" , 
                size: new Vector2(GraphSize.X*0.8f,GraphSize.Y / HeightTriangle * 0.4f),
                color: backgroundColor
                );
            sprite.Position = GraphCenter + new Vector2(0,GraphSize.Y*(0.2f+0.02f)-(GraphSize.Y/HeightTriangle-GraphSize.Y)*0.2f);
            frame.Add(sprite);
            

        // Icon 
        if (GraphIconType != enGraphIcon.NONE)
            {
            if (GraphIconType == enGraphIcon.STORAGE) 
                {
                DrawStorageIcon(frame, GraphCenter, GraphSize*0.2f);
                }
            else if (GraphIconType == enGraphIcon.TEXT) 
                {
                sprite = MySprite.CreateText(IconText, "debug", iconColor, GraphSize.Y/128f, TextAlignment.CENTER);
                sprite.Position = GraphCenter-new Vector2(0f,GraphSize.X*0.25f/2f);
                frame.Add(sprite);    
                }
            else
                {
                sprite = new MySprite(SpriteType.TEXTURE, GetIconString(GraphIconType) , 
                        size: GraphSize*0.25f, 
                        color: iconColor
                        );
                sprite.Position = GraphCenter;

                frame.Add(sprite);     
                }
            }

        // GraphText
        if (GraphText != "")    
            {
            sprite = MySprite.CreateText(GraphText, "debug", emptyBarColor, GraphSize.Y/256f, TextAlignment.CENTER);
            sprite.Position = GraphCenter+new Vector2(0f,GraphSize.Y*0.3f);
            frame.Add(sprite);
            }
        }

		private void ConstructHexagon(MySpriteDrawFrame frame)
        {
        MySprite sprite;

        // Background 
		// h = sin(60)*a // a = h * 2 / sqrt(3) = graphsize.Y / sqrt(3)

		float a = GraphSize.Y / (float) Math.Sqrt(3);     

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: new Vector2 (a, GraphSize.Y),
            color: backgroundColor
            );
        sprite.Position = GraphCenter;
        frame.Add(sprite);

        		sprite.RotationOrScale = (float)Math.PI / 3f;
        frame.Add(sprite);

        		sprite.RotationOrScale = (float)Math.PI * 2f / 3f;
        frame.Add(sprite);

        // Colored Hexagon

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple" , 
            size: new Vector2(a*0.8f, GraphSize.Y*0.8f),
            color: GetColorForProgress(Progress[0])
            );       
  
        sprite.Position = GraphCenter;
        frame.Add(sprite);

        		sprite.RotationOrScale = (float)Math.PI / 3f;
        frame.Add(sprite);

        		sprite.RotationOrScale = (float)Math.PI * 2f / 3f;
        frame.Add(sprite);

		// Icon		

        if (GraphIconType != enGraphIcon.NONE)
            {
            if (GraphIconType == enGraphIcon.STORAGE) 
                {
                DrawStorageIcon(frame, GraphCenter, GraphSize*0.5f);
                }
            else if (GraphIconType == enGraphIcon.TEXT) 
                {
                sprite = MySprite.CreateText(IconText, "debug", iconColor, GraphSize.X/64f, TextAlignment.CENTER);
                sprite.Position = GraphCenter-new Vector2(0f,GraphSize.X*0.5f/2f);
                frame.Add(sprite);    
                }
            else
                {
                sprite = new MySprite(SpriteType.TEXTURE, GetIconString(GraphIconType) , 
                        size: GraphSize*0.5f, 
                        color: iconColor
                        );
                sprite.Position = GraphCenter;

                frame.Add(sprite);     
                }
            }			
		}

		private void ConstructText(MySpriteDrawFrame frame)
        {
        MySprite sprite;
        sprite = MySprite.CreateText(GraphText, "debug", GetColorForProgress(Progress[0]), GraphSize.Y, TextAlignment.CENTER);
        sprite.Position = GraphCenter;
        frame.Add(sprite);		
        		}
		
		private void ConstructIcon(MySpriteDrawFrame frame)
        {
        MySprite sprite;
        if (GraphIconType != enGraphIcon.NONE)
            {
            if (GraphIconType == enGraphIcon.STORAGE) 
                {
                DrawStorageIcon(frame, GraphCenter, GraphSize);
                }
            else if (GraphIconType == enGraphIcon.TEXT) 
                {
                sprite = MySprite.CreateText(IconText, "debug", iconColor, GraphSize.Y, TextAlignment.CENTER);
                sprite.Position = GraphCenter-new Vector2(0f,GraphSize.Y/2f);
                frame.Add(sprite);    
                }
            else
                {
                sprite = new MySprite(SpriteType.TEXTURE, GetIconString(GraphIconType) , 
                        size: GraphSize, 
                        color: iconColor
                        );
                sprite.Position = GraphCenter;

                frame.Add(sprite);     
                }
            }			
		}


    }

public class GraphBroker
    {

    //
    // Member variables
    //

    public List<Graph> Graphs = new List<Graph>();
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
   
    	//private String Definition = "";
    	private Program _program;

    //
    // Constructor
    //

    public GraphBroker(Program program)
        {
        _program = program;
        }

    //
    // Public Functions
    //

    public void AddGraph(enGraphType type, enGraphSubType subtype, enGraphStyleType  styletype)
        {
        Graphs.Add(new Graph(type, subtype, styletype));
        }

    public void UpdateGraph(int number, string sourcename, enContentType contenttype)
        {
        UpdateGraph(number, sourcename, contenttype, enSourceType.NAME);
        }

    public void UpdateGraph(int number, string sourcename, enContentType contenttype, enSourceType sourcetype)
    {
        float maxvalue = 0;
        float value = 0;

        if (sourcetype == enSourceType.NAME)
        {
            _program.GridTerminalSystem.SearchBlocksOfName(sourcename, blocks);
        }
        if (sourcetype == enSourceType.GROUP)
        {
            IMyBlockGroup group = _program.GridTerminalSystem.GetBlockGroupWithName(sourcename);
            if (group == null)
                {
                return;
                }
            
            group.GetBlocks(blocks);
        }

        switch (contenttype)
        {
            case enContentType.BATTERY:
                Graphs[number].SetGraphIcon(enGraphIcon.ENERGY);
                foreach (IMyTerminalBlock block in blocks)
                {
                    IMyBatteryBlock battery = block as IMyBatteryBlock;
                    if (battery == null) continue;
                    value += battery.CurrentStoredPower;
                    maxvalue += battery.MaxStoredPower;
                }
                break;
            case enContentType.SOLAR:
                Graphs[number].SetGraphIcon(enGraphIcon.TEXT);
                Graphs[number].SetGraphIconText("S");
                foreach (IMyTerminalBlock block in blocks)
                {
                    IMySolarPanel solar = block as IMySolarPanel;
                    if (solar == null) continue;
                    value += solar.CurrentOutput;
                    maxvalue += solar.MaxOutput;
                }
                break;
            case enContentType.HYDROGEN:
                Graphs[number].SetGraphIcon(enGraphIcon.HYDROGEN);
                foreach (IMyTerminalBlock block in blocks)
                {
                    IMyGasTank gasTank = block as IMyGasTank;
                    if (gasTank == null) continue;
                    var hydrogenId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Hydrogen");
                    var sourceComp = gasTank.Components.Get<MyResourceSourceComponent>();
                    if (sourceComp == null || sourceComp.ResourceTypes.IndexOf(hydrogenId) == -1) continue;
                    value += (float)(gasTank.Capacity * gasTank.FilledRatio);
                    maxvalue += gasTank.Capacity;
                }
                break;
            case enContentType.OXYGEN:
                Graphs[number].SetGraphIcon(enGraphIcon.OXYGEN);
                foreach (IMyTerminalBlock block in blocks)
                {
                    IMyGasTank gasTank = block as IMyGasTank;
                    if (gasTank == null) continue;
                    var oxygenId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Oxygen");
                    var sourceComp = gasTank.Components.Get<MyResourceSourceComponent>();
                    if (sourceComp == null || sourceComp.ResourceTypes.IndexOf(oxygenId) == -1) continue;
                    value += (float)(gasTank.Capacity * gasTank.FilledRatio);
                    maxvalue += gasTank.Capacity;
                }
                break;
            case enContentType.STORAGE:
                Graphs[number].SetGraphIcon(enGraphIcon.STORAGE);
                foreach (IMyTerminalBlock block in blocks)
                {
                    for (int i = 0; i < block.InventoryCount; i++)
                    {
                        var inventory = block.GetInventory(i);
                        value += (float)inventory.CurrentVolume;
                        maxvalue += (float)inventory.MaxVolume;
                    }
                }
                break;
        }

        if (maxvalue > 0)
        {
            Graphs[number].Progress[0] = value / maxvalue;
        }
        else
        {
            Graphs[number].Progress[0] = 0;
        }
    }

    public void Draw(string blockname, int surfacenum)
        {
        IMyTextSurface targetsurface;
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        
        _program.GridTerminalSystem.SearchBlocksOfName(blockname, blocks);

        foreach (IMyTextSurfaceProvider block in blocks) 
            {
            //Thanks to Krypt for this bugfind
            if (block is IMyTextPanel && surfacenum == 0)
                {
                targetsurface = block as IMyTextSurface;
                }
            else
                {
                targetsurface = block.GetSurface(surfacenum);
                }   

            using(var frame = targetsurface.DrawFrame())
                {
                foreach (Graph g in Graphs)
                    {g.Draw(frame);}
                }
            }
        }

    // 
    // Private Functions
    // 

    private float UnitValue(string unit)
        {
        if (unit.Equals("kWh"))return 1000f;
        if (unit.Equals("MWh")) return 1000000f;

        if (unit.Equals("kW"))return 1000f;
        if (unit.Equals("MW")) return 1000000f;

        return 1;
        }

    }
    
    public class Program : MyGridProgram {

GraphBroker _GraphBroker;

public Program() 
{ 
    _GraphBroker = new GraphBroker(this);

    _GraphBroker.AddGraph(enGraphType.BAR, enGraphSubType.SIMPLE, enGraphStyleType.GRADIENT);
    _GraphBroker.Graphs[0].SetGeometry(new Vector2(32,256), new Vector2(64,256));
    _GraphBroker.Graphs[0].AddColorPoint(0.1f, new Color(255,255,0,255)); 
    _GraphBroker.Graphs[0].AddColorPoint(0.3f, new Color(0,255,0,255)); 

    _GraphBroker.AddGraph(enGraphType.BAR, enGraphSubType.SIMPLE, enGraphStyleType.GRADIENT);
    _GraphBroker.Graphs[1].SetGeometry(new Vector2(96,256), new Vector2(64,256));
    _GraphBroker.Graphs[1].AddColorPoint(0.1f, new Color(255,255,0,255)); 
    _GraphBroker.Graphs[1].AddColorPoint(0.3f, new Color(0,255,0,255)); 

    _GraphBroker.AddGraph(enGraphType.BAR, enGraphSubType.SIMPLE, enGraphStyleType.FIXED);
    _GraphBroker.Graphs[2].SetGeometry(new Vector2(160,256), new Vector2(64,256));
    _GraphBroker.Graphs[2].AddColorPoint(0.1f, new Color(255,255,0,255)); 
    _GraphBroker.Graphs[2].AddColorPoint(0.3f, new Color(0,255,0,255)); 

    _GraphBroker.AddGraph( enGraphType.BAR, enGraphSubType.SIMPLE, enGraphStyleType.FIXED);
    _GraphBroker.Graphs[3].SetGeometry(new Vector2(224,256), new Vector2(64,256));
    _GraphBroker.Graphs[3].AddColorPoint(0.1f, new Color(255,255,0,255)); 
    _GraphBroker.Graphs[3].AddColorPoint(0.3f, new Color(0,255,0,255)); 


    _GraphBroker.AddGraph(enGraphType.GAUGE, enGraphSubType.SIMPLE, enGraphStyleType.GRADIENT);
    _GraphBroker.Graphs[4].SetGeometry(new Vector2(384,256), new Vector2(256,256));
    _GraphBroker.Graphs[4].SetColorPoint(0,0.0f, new Color(0,255,0,255)); 
    _GraphBroker.Graphs[4].AddColorPoint(0.8f, new Color(255,255,0,255)); 
    _GraphBroker.Graphs[4].AddColorPoint(0.99f, new Color(255,0,0,255)); 
    _GraphBroker.Graphs[4].SetGraphText("Cargo");

    _GraphBroker.AddGraph(enGraphType.TEXT, enGraphSubType.SIMPLE, enGraphStyleType.FIXED);
    _GraphBroker.Graphs[5].SetGeometry(new Vector2(256,96), new Vector2(1,1));
    _GraphBroker.Graphs[5].SetColorPoint(0,0.0f, new Color(150,150,150,255)); 
        
    _GraphBroker.Graphs[5].SetGraphText("System Info");
    
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

}

public void Save() 
{ 
    // Called when the program needs to save its state. Use
    // this method to save your state to the Storage field
    // or some other means. 
    // 
    // This method is optional and can be removed if not
    // needed. 
} 

public void Main(string argument, UpdateType updateSource) 
{
    	// Updating the graphs with privided Function "UpdateGraph" of the class GraphBroker. 

    _GraphBroker.UpdateGraph(0, "Oxygen Tank", enContentType.OXYGEN);
    _GraphBroker.UpdateGraph(1, "Hydrogen Tank", enContentType.HYDROGEN);
    _GraphBroker.UpdateGraph(2, "Battery", enContentType.BATTERY);
    _GraphBroker.UpdateGraph(3, "Solar Panel", enContentType.SOLAR);
    _GraphBroker.UpdateGraph(4, "Cargo Container", enContentType.STORAGE);
    
    // Commented line below shows how to set the Process manually instead of using .UpdateGraph
    // _GraphBroker.Graphs[0].Progress[0] = 0.00f;

    	// Drawing of all Graphs of one GraphBroker on a given Surface of a block.
    _GraphBroker.Draw("Global Display", 0); 
} 

}*/