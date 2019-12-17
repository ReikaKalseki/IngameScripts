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

namespace Ingame_Scripts.SolarTracking {
	
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


// Generated from ZerothAngel's SEScripts version 1fee37d00657
// Modules: solaralignment, solargyrocontroller, printutils, eventdriver, shipcontrol, thrustcontrol, gyrocontrol, shiporientation, commons 

// MIT licensed. See https://github.com/ZerothAngel/SEScripts for raw code. 

// !!! Leading whitespace stripped to save bytes !!! 
/*
// Solar Alignment 
const bool SOLAR_ALIGNMENT_DEFAULT_ACTIVE = true;

// SolarGyroController 
const float SOLAR_GYRO_VELOCITY = 0.05 f; // In radians per second 
const double SOLAR_GYRO_AXIS_TIMEOUT = 15.0; // In seconds 
const float SOLAR_GYRO_MIN_ERROR = 0.005 f; // As a fraction of theoretical max 
// If you're using modded solar panels, set these values (both in MW) 
const float SOLAR_PANEL_MAX_POWER_LARGE = 0.120f; // Max power of large panels 
const float SOLAR_PANEL_MAX_POWER_SMALL = 0.030f; // Max power of small panels 

// !!! CODE BEGINS HERE, EDIT AT YOUR OWN RISK !!! 

private readonly EventDriver eventDriver = new EventDriver();
private readonly SolarGyroController solarGyroController =
 new SolarGyroController(
  GyroControl.Pitch,
  GyroControl.Roll,
  GyroControl.Yaw
 );
private readonly ZAStorage myStorage = new ZAStorage();
private readonly ShipOrientation shipOrientation = new ShipOrientation();
private bool FirstRun = true;
Program() {
 Runtime.UpdateFrequency |= UpdateFrequency.Once;
}
void Main(string argument, UpdateType updateType) {
 var commons = new ShipControlCommons(this, updateType, shipOrientation,
  storage: myStorage);
 if (FirstRun) {
  FirstRun = false;
  myStorage.Decode(Storage);
  shipOrientation.SetShipReference(commons, "SolarGyroReference");
  solarGyroController.ConditionalInit(commons, eventDriver, SOLAR_ALIGNMENT_DEFAULT_ACTIVE);
 }
 eventDriver.Tick(commons, argAction: () => {
   solarGyroController.HandleCommand(commons, eventDriver, argument);
  },
  postAction: () => {
   solarGyroController.Display(commons);
  });
 if (commons.IsDirty) Storage = myStorage.Encode();
}

public class SolarGyroController {
 private
 const double RunDelay = 1.0;
 private
 const string ActiveKey = "SolarGyroController_Active";
 public struct SolarPanelDetails {
  public float MaxPowerOutput;
  public float DefinedPowerOutput;
  public SolarPanelDetails(IEnumerable < IMyTerminalBlock > blocks) {
   MaxPowerOutput = 0.0f;
   DefinedPowerOutput = 0.0f;
   foreach(var panel in ZACommons.GetBlocksOfType < IMySolarPanel > (blocks)) {
    if (panel.IsFunctional && panel.IsWorking) {
     MaxPowerOutput += panel.MaxOutput;
     DefinedPowerOutput += panel.CubeGrid.GridSize == 2.5 f ? SOLAR_PANEL_MAX_POWER_LARGE : SOLAR_PANEL_MAX_POWER_SMALL;
    }
   }
  }
 }
 private readonly int[] AllowedAxes;
 private readonly float[] LastVelocities;
 private readonly TimeSpan AxisTimeout = TimeSpan.FromSeconds(SOLAR_GYRO_AXIS_TIMEOUT);
 private float ? MaxPower = null;
 private int AxisIndex = 0;
 private bool Active = false;
 private TimeSpan TimeOnAxis;
 private float CurrentMaxPower;
 public SolarGyroController(params int[] allowedAxes) {
  AllowedAxes = (int[]) allowedAxes.Clone();
  LastVelocities = new float[AllowedAxes.Length];
  for (int i = 0; i < LastVelocities.Length; i++) {
   LastVelocities[i] = SOLAR_GYRO_VELOCITY;
  }
 }
 public void Init(ZACommons commons, EventDriver eventDriver) {
  var shipControl = (ShipControlCommons) commons;
  var gyroControl = shipControl.GyroControl;
  gyroControl.Reset();
  gyroControl.EnableOverride(true);
  Active = true;
  SaveActive(commons);
  MaxPower = null; // Use first-run initialization 
  CurrentMaxPower = 0.0f;
  eventDriver.Schedule(0.0, Run);
 }
 public void ConditionalInit(ZACommons commons, EventDriver eventDriver,
  bool defaultActive = false) {
  var activeValue = commons.GetValue(ActiveKey);
  if (activeValue != null) {
   bool active;
   if (Boolean.TryParse(activeValue, out active)) {
    if (active) Init(commons, eventDriver);
    return;
   }
  }
  if (defaultActive) Init(commons, eventDriver);
 }
 public void Run(ZACommons commons, EventDriver eventDriver) {
  if (!Active) return;
  var shipControl = (ShipControlCommons) commons;
  var gyroControl = shipControl.GyroControl;
  var currentAxis = AllowedAxes[AxisIndex];
  if (MaxPower == null) {
   MaxPower = -100.0f; // Start with something absurdly low to kick things off 
   gyroControl.Reset();
   gyroControl.EnableOverride(true);
   gyroControl.SetAxisVelocity(currentAxis, LastVelocities[AxisIndex]);
   TimeOnAxis = eventDriver.TimeSinceStart + AxisTimeout;
  }
  var solarPanelDetails = new SolarPanelDetails(commons.Blocks);
  CurrentMaxPower = solarPanelDetails.MaxPowerOutput;
  var minError = solarPanelDetails.DefinedPowerOutput * SOLAR_GYRO_MIN_ERROR;
  var delta = CurrentMaxPower - MaxPower;
  MaxPower = CurrentMaxPower;
  if (delta > minError) {
   gyroControl.EnableOverride(true);
  } else if (delta < -minError) {
   gyroControl.EnableOverride(true);
   LastVelocities[AxisIndex] = -LastVelocities[AxisIndex];
   gyroControl.SetAxisVelocity(currentAxis, LastVelocities[AxisIndex]);
  } else {
   gyroControl.EnableOverride(false);
  }
  if (TimeOnAxis <= eventDriver.TimeSinceStart && MaxPower < solarPanelDetails.DefinedPowerOutput * (1.0f - SOLAR_GYRO_MIN_ERROR)) {
   AxisIndex++;
   AxisIndex %= AllowedAxes.Length;
   gyroControl.Reset();
   gyroControl.EnableOverride(true);
   gyroControl.SetAxisVelocity(AllowedAxes[AxisIndex], LastVelocities[AxisIndex]);
   TimeOnAxis = eventDriver.TimeSinceStart + AxisTimeout;
  }
  eventDriver.Schedule(RunDelay, Run);
 }
 public void HandleCommand(ZACommons commons, EventDriver eventDriver,
  string argument) {
  argument = argument.Trim().ToLower();
  if (argument == "pause") {
   Active = false;
   SaveActive(commons);
   var shipControl = (ShipControlCommons) commons;
   var gyroControl = shipControl.GyroControl;
   gyroControl.Reset();
   gyroControl.EnableOverride(false);
  } else if (argument == "resume") {
   if (!Active) Init(commons, eventDriver);
  }
 }
 public void Display(ZACommons commons) {
  if (!Active) {
   commons.Echo("Solar Max Power: Paused");
  } else {
   commons.Echo(string.Format("Solar Max Power: {0}", PrintUtils.FormatPower(CurrentMaxPower)));
  }
 }
 private void SaveActive(ZACommons commons) {
  commons.SetValue(ActiveKey, Active.ToString());
 }
}

public static class PrintUtils {
 public static string FormatPower(float value) {
  if (value >= 1.0f) {
   return string.Format("{0:F2} MW", value);
  } else if (value >= 0.001) {
   return string.Format("{0:F2} kW", value * 1000f);
  } else {
   return string.Format("{0:F2} W", value * 1000000f);
  }
 }
}

public class EventDriver {
 public struct FutureTickAction: IComparable < FutureTickAction > {
  public ulong When;
  public Action < ZACommons,
  EventDriver > Action;
  public FutureTickAction(ulong when, Action < ZACommons, EventDriver > action = null) {
   When = when;
   Action = action;
  }
  public int CompareTo(FutureTickAction other) {
   return When.CompareTo(other.When);
  }
 }
 public struct FutureTimeAction: IComparable < FutureTimeAction > {
  public TimeSpan When;
  public Action < ZACommons,
  EventDriver > Action;
  public FutureTimeAction(TimeSpan when, Action < ZACommons, EventDriver > action = null) {
   When = when;
   Action = action;
  }
  public int CompareTo(FutureTimeAction other) {
   return When.CompareTo(other.When);
  }
 }
 private
 const float TicksPerSecond = 60.0f;
 private readonly LinkedList < FutureTickAction > TickQueue = new LinkedList < FutureTickAction > ();
 private readonly LinkedList < FutureTimeAction > TimeQueue = new LinkedList < FutureTimeAction > ();
 private ulong Ticks; // Not a reliable measure of time because of variable update frequency. 
 public TimeSpan TimeSinceStart {
  get;
  private set;
 }
 public EventDriver() {
  TimeSinceStart = TimeSpan.FromSeconds(0);
 }
 private void KickTimer(ZACommons commons) {
  if (TickQueue.First != null) {
   commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
  } else if (TimeQueue.First != null) {
   var next = (float)(TimeQueue.First.Value.When.TotalSeconds - TimeSinceStart.TotalSeconds);
   if (next < (10.0f / TicksPerSecond)) {
    commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
   } else if (next < (100.0f / TicksPerSecond)) {
    commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update10;
   } else {
    commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
   }
  } else {
   commons.Program.Runtime.UpdateFrequency = UpdateFrequency.None;
  }
 }
 public void Tick(ZACommons commons, Action mainAction = null,
  Action preAction = null,
  Action argAction = null,
  Action postAction = null) {
  Ticks++;
  TimeSinceStart += commons.Program.Runtime.TimeSinceLastRun;
  bool runMain = false;
  if (preAction != null) preAction();
  if (argAction != null && (commons.UpdateType & ~(UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once)) != 0) argAction();
  while (TickQueue.First != null &&
   TickQueue.First.Value.When <= Ticks) {
   var action = TickQueue.First.Value.Action;
   TickQueue.RemoveFirst();
   if (action != null) {
    action(commons, this);
   } else {
    runMain = true;
   }
  }
  while (TimeQueue.First != null &&
   TimeQueue.First.Value.When <= TimeSinceStart) {
   var action = TimeQueue.First.Value.Action;
   TimeQueue.RemoveFirst();
   if (action != null) {
    action(commons, this);
   } else {
    runMain = true;
   }
  }
  if (runMain && mainAction != null) mainAction();
  if (postAction != null) postAction();
  KickTimer(commons);
 }
 public void Schedule(ulong delay, Action < ZACommons, EventDriver > action = null) {
  var future = new FutureTickAction(Ticks + delay, action);
  for (var current = TickQueue.First; current != null; current = current.Next) {
   if (future.CompareTo(current.Value) < 0) {
    TickQueue.AddBefore(current, future);
    return;
   }
  }
  TickQueue.AddLast(future);
 }
 public void Schedule(double seconds, Action < ZACommons, EventDriver > action = null) {
  var delay = Math.Max(seconds, 0.0);
  var future = new FutureTimeAction(TimeSinceStart + TimeSpan.FromSeconds(delay), action);
  for (var current = TimeQueue.First; current != null; current = current.Next) {
   if (future.CompareTo(current.Value) < 0) {
    TimeQueue.AddBefore(current, future);
    return;
   }
  }
  TimeQueue.AddLast(future);
 }
}

public class ShipControlCommons: ZACommons {
 private readonly ShipOrientation shipOrientation;
 public Base6Directions.Direction ShipUp {
  get {
   return shipOrientation.ShipUp;
  }
 }
 public Base6Directions.Direction ShipForward {
  get {
   return shipOrientation.ShipForward;
  }
 }
 public MyBlockOrientation ShipBlockOrientation {
  get {
   return shipOrientation.BlockOrientation;
  }
 }
 public ShipControlCommons(MyGridProgram program, UpdateType updateType,
  ShipOrientation shipOrientation,
  string shipGroup = null,
  ZAStorage storage = null): base(program, updateType, shipGroup: shipGroup, storage: storage) {
  this.shipOrientation = shipOrientation;
 }
 public GyroControl GyroControl {
  get {
   if (m_gyroControl == null) {
    m_gyroControl = new GyroControl();
    m_gyroControl.Init(Blocks,
     shipUp: shipOrientation.ShipUp,
     shipForward: shipOrientation.ShipForward);
   }
   return m_gyroControl;
  }
 }
 private GyroControl m_gyroControl = null;
 public ThrustControl ThrustControl {
  get {
   if (m_thrustControl == null) {
    m_thrustControl = new ThrustControl();
    m_thrustControl.Init(Blocks,
     shipUp: shipOrientation.ShipUp,
     shipForward: shipOrientation.ShipForward);
   }
   return m_thrustControl;
  }
 }
 private ThrustControl m_thrustControl = null;
 public void Reset(bool gyroOverride = false,
  bool ? thrusterEnable = true,
  Func < IMyThrust, bool > thrusterCondition = null) {
  GyroControl.Reset();
  GyroControl.EnableOverride(gyroOverride);
  ThrustControl.Reset(thrusterCondition);
  if (thrusterEnable != null) ThrustControl.Enable((bool) thrusterEnable, thrusterCondition);
 }
 public Vector3D ReferencePoint {
  get {
   if (m_referencePoint == null) {
    m_referencePoint = ShipController != null ? ShipController.CenterOfMass : Me.GetPosition();
   }
   return (Vector3D) m_referencePoint;
  }
 }
 private Vector3D ? m_referencePoint = null;
 public Vector3D ReferenceUp {
  get {
   if (m_referenceUp == null) {
    m_referenceUp = GetReferenceVector(shipOrientation.ShipUp);
   }
   return (Vector3D) m_referenceUp;
  }
 }
 private Vector3D ? m_referenceUp = null;
 public Vector3D ReferenceForward {
  get {
   if (m_referenceForward == null) {
    m_referenceForward = GetReferenceVector(shipOrientation.ShipForward);
   }
   return (Vector3D) m_referenceForward;
  }
 }
 private Vector3D ? m_referenceForward = null;
 public Vector3D ReferenceLeft {
  get {
   if (m_referenceLeft == null) {
    m_referenceLeft = GetReferenceVector(Base6Directions.GetLeft(shipOrientation.ShipUp, shipOrientation.ShipForward));
   }
   return (Vector3D) m_referenceLeft;
  }
 }
 private Vector3D ? m_referenceLeft = null;
 private Vector3D GetReferenceVector(Base6Directions.Direction direction) {
  var offset = Me.Position + Base6Directions.GetIntVector(direction);
  return Vector3D.Normalize(Me.CubeGrid.GridIntegerToWorld(offset) - Me.GetPosition());
 }
 public IMyShipController ShipController {
  get {
   if (m_shipController == null) {
    foreach(var block in Blocks) {
     var controller = block as IMyShipController;
     if (controller != null && controller.IsFunctional) {
      m_shipController = controller;
      break;
     }
    }
   }
   return m_shipController;
  }
 }
 private IMyShipController m_shipController = null;
 public Vector3D ? LinearVelocity {
  get {
   return ShipController != null ?
    ShipController.GetShipVelocities().LinearVelocity : (Vector3D ? ) null;
  }
 }
 public Vector3D ? AngularVelocity {
  get {
   return ShipController != null ?
    ShipController.GetShipVelocities().AngularVelocity : (Vector3D ? ) null;
  }
 }
}

public class ThrustControl {
 private readonly Dictionary < Base6Directions.Direction, List < IMyThrust >> thrusters = new Dictionary < Base6Directions.Direction, List < IMyThrust >> ();
 private void AddThruster(Base6Directions.Direction direction, IMyThrust thruster) {
  var thrusterList = GetThrusters(direction); // collect must be null to modify original list 
  thrusterList.Add(thruster);
 }
 public void Init(IEnumerable < IMyTerminalBlock > blocks,
  Func < IMyThrust, bool > collect = null,
  Base6Directions.Direction shipUp = Base6Directions.Direction.Up,
  Base6Directions.Direction shipForward = Base6Directions.Direction.Forward) {
  MyBlockOrientation shipOrientation = new MyBlockOrientation(shipForward, shipUp);
  thrusters.Clear();
  foreach(var block in blocks) {
   var thruster = block as IMyThrust;
   if (thruster != null && thruster.IsFunctional &&
    (collect == null || collect(thruster))) {
    var facing = thruster.Orientation.TransformDirection(Base6Directions.Direction.Forward); // Exhaust goes this way 
    var thrustDirection = Base6Directions.GetFlippedDirection(facing);
    var shipDirection = shipOrientation.TransformDirectionInverse(thrustDirection);
    AddThruster(shipDirection, thruster);
   }
  }
 }
 public List < IMyThrust > GetThrusters(Base6Directions.Direction direction,
  Func < IMyThrust, bool > collect = null,
  bool disable = false) {
  List < IMyThrust > thrusterList;
  if (!thrusters.TryGetValue(direction, out thrusterList)) {
   thrusterList = new List < IMyThrust > ();
   thrusters.Add(direction, thrusterList);
  }
  if (collect == null) {
   return thrusterList;
  } else {
   var result = new List < IMyThrust > ();
   foreach(var thruster in thrusterList) {
    if (collect(thruster)) {
     result.Add(thruster);
    } else if (disable) {
     thruster.Enabled = false;
    }
   }
   return result;
  }
 }
 public void SetOverride(Base6Directions.Direction direction, bool enable = true,
  Func < IMyThrust, bool > collect = null) {
  var thrusterList = GetThrusters(direction, collect, true);
  thrusterList.ForEach(thruster =>
   thruster.SetValue < float > ("Override", enable ?
    thruster.GetMaximum < float > ("Override") :
    0.0f));
 }
 public void SetOverride(Base6Directions.Direction direction, double percent,
  Func < IMyThrust, bool > collect = null) {
  percent = Math.Max(percent, 0.0);
  percent = Math.Min(percent, 1.0);
  var thrusterList = GetThrusters(direction, collect, true);
  thrusterList.ForEach(thruster =>
   thruster.SetValue < float > ("Override",
    (float)(thruster.GetMaximum < float > ("Override") * percent)));
 }
 public void Enable(Base6Directions.Direction direction, bool enable,
  Func < IMyThrust, bool > collect = null) {
  var thrusterList = GetThrusters(direction, collect, true);
  thrusterList.ForEach(thruster => thruster.Enabled = enable);
 }
 public void Enable(bool enable,
  Func < IMyThrust, bool > collect = null) {
  foreach(var thrusterList in thrusters.Values) {
   thrusterList.ForEach(thruster => {
    if (collect == null || collect(thruster)) thruster.Enabled = enable;
   });
  }
 }
 public void Reset(Func < IMyThrust, bool > collect = null) {
  foreach(var thrusterList in thrusters.Values) {
   thrusterList.ForEach(thruster => {
    if (collect == null || collect(thruster)) thruster.SetValue < float > ("Override", 0.0f);
   });
  }
 }
}

public class GyroControl {
 public
 const int Yaw = 0;
 public
 const int Pitch = 1;
 public
 const int Roll = 2;
 public readonly string[] AxisNames = new string[] {
  "Yaw",
  "Pitch",
  "Roll"
 };
 public struct GyroAxisDetails {
  public int LocalAxis;
  public int Sign;
  public GyroAxisDetails(int localAxis, int sign) {
   LocalAxis = localAxis;
   Sign = sign;
  }
 }
 public struct GyroDetails {
  public IMyGyro Gyro;
  public GyroAxisDetails[] AxisDetails;
  public GyroDetails(IMyGyro gyro, Base6Directions.Direction shipUp,
   Base6Directions.Direction shipForward) {
   Gyro = gyro;
   AxisDetails = new GyroAxisDetails[3];
   var shipLeft = Base6Directions.GetLeft(shipUp, shipForward);
   SetAxisDetails(gyro, Yaw, shipUp);
   SetAxisDetails(gyro, Pitch, shipLeft);
   SetAxisDetails(gyro, Roll, shipForward);
  }
  private void SetAxisDetails(IMyGyro gyro, int axis,
   Base6Directions.Direction axisDirection) {
   switch (gyro.Orientation.TransformDirectionInverse(axisDirection)) {
    case Base6Directions.Direction.Up:
     AxisDetails[axis] = new GyroAxisDetails(Yaw, -1);
     break;
    case Base6Directions.Direction.Down:
     AxisDetails[axis] = new GyroAxisDetails(Yaw, 1);
     break;
    case Base6Directions.Direction.Left:
     AxisDetails[axis] = new GyroAxisDetails(Pitch, -1);
     break;
    case Base6Directions.Direction.Right:
     AxisDetails[axis] = new GyroAxisDetails(Pitch, 1);
     break;
    case Base6Directions.Direction.Forward:
     AxisDetails[axis] = new GyroAxisDetails(Roll, 1);
     break;
    case Base6Directions.Direction.Backward:
     AxisDetails[axis] = new GyroAxisDetails(Roll, -1);
     break;
   }
  }
 }
 private readonly List < GyroDetails > gyros = new List < GyroDetails > ();
 public void Init(IEnumerable < IMyTerminalBlock > blocks,
  Func < IMyGyro, bool > collect = null,
  Base6Directions.Direction shipUp = Base6Directions.Direction.Up,
  Base6Directions.Direction shipForward = Base6Directions.Direction.Forward) {
  gyros.Clear();
  foreach(var block in blocks) {
   var gyro = block as IMyGyro;
   if (gyro != null &&
    gyro.IsFunctional && gyro.IsWorking && gyro.Enabled &&
    (collect == null || collect(gyro))) {
    var details = new GyroDetails(gyro, shipUp, shipForward);
    gyros.Add(details);
   }
  }
 }
 public void EnableOverride(bool enable) {
  gyros.ForEach(gyro => gyro.Gyro.GyroOverride = enable);
 }
 public void SetAxisVelocity(int axis, float velocity) {
  SetAxisVelocityRPM(axis, velocity * MathHelper.RadiansPerSecondToRPM);
 }
 public void SetAxisVelocityRPM(int axis, float rpmVelocity) {
  gyros.ForEach(gyro => gyro.Gyro.SetValue < float > (AxisNames[gyro.AxisDetails[axis].LocalAxis], gyro.AxisDetails[axis].Sign * rpmVelocity));
 }
 public void SetAxisVelocityFraction(int axis, float fraction) {
  SetAxisVelocityRPM(axis, 30.0f * fraction);
 }
 public void Reset() {
  gyros.ForEach(gyro => {
   gyro.Gyro.SetValue < float > ("Yaw", 0.0f);
   gyro.Gyro.SetValue < float > ("Pitch", 0.0f);
   gyro.Gyro.SetValue < float > ("Roll", 0.0f);
  });
 }
}

public class ShipOrientation {
 public Base6Directions.Direction ShipUp {
  get;
  private set;
 }
 public Base6Directions.Direction ShipForward {
  get;
  private set;
 }
 public MyBlockOrientation BlockOrientation {
  get {
   return new MyBlockOrientation(ShipForward, ShipUp);
  }
 }
 public ShipOrientation() {
  ShipUp = Base6Directions.Direction.Up;
  ShipForward = Base6Directions.Direction.Forward;
 }
 public void SetShipReference(IMyCubeBlock reference) {
  ShipUp = reference.Orientation.TransformDirection(Base6Directions.Direction.Up);
  ShipForward = reference.Orientation.TransformDirection(Base6Directions.Direction.Forward);
 }
 public void SetShipReference(ZACommons commons, string groupName,
  Func < IMyTerminalBlock, bool > condition = null) {
  var group = commons.GetBlockGroupWithName(groupName);
  if (group != null) {
   foreach(var block in group.Blocks) {
    if (block.CubeGrid == commons.Me.CubeGrid &&
     (condition == null || condition(block))) {
     SetShipReference(block);
     return;
    }
   }
  }
  ShipUp = Base6Directions.Direction.Up;
  ShipForward = Base6Directions.Direction.Forward;
 }
 public void SetShipReference < T > (IEnumerable < IMyTerminalBlock > blocks,
  Func < T, bool > condition = null)
 where T: IMyCubeBlock {
  var references = ZACommons.GetBlocksOfType < T > (blocks, condition);
  if (references.Count > 0) {
   SetShipReference(references[0]);
  } else {
   ShipUp = Base6Directions.Direction.Up;
   ShipForward = Base6Directions.Direction.Forward;
  }
 }
}

public class ZACommons {
 public
 const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
 public readonly MyGridProgram Program;
 public readonly UpdateType UpdateType;
 private readonly string ShipGroupName;
 private readonly ZAStorage Storage;
 public bool IsDirty {
  get;
  private set;
 }
 public List < IMyTerminalBlock > AllBlocks {
  get {
   if (m_allBlocks == null) {
    m_allBlocks = new List < IMyTerminalBlock > ();
    Program.GridTerminalSystem.GetBlocks(m_allBlocks);
   }
   return m_allBlocks;
  }
 }
 private List < IMyTerminalBlock > m_allBlocks = null;
 public List < IMyTerminalBlock > Blocks {
  get {
   if (m_blocks == null) {
    if (ShipGroupName != null) {
     var group = GetBlockGroupWithName(ShipGroupName);
     if (group != null) m_blocks = group.Blocks;
    }
    if (m_blocks == null) {
     m_blocks = new List < IMyTerminalBlock > ();
     foreach(var block in AllBlocks) {
      if (block.CubeGrid == Program.Me.CubeGrid) m_blocks.Add(block);
     }
    }
   }
   return m_blocks;
  }
 }
 private List < IMyTerminalBlock > m_blocks = null;
 public class BlockGroup {
  private readonly IMyBlockGroup MyBlockGroup;
  public BlockGroup(IMyBlockGroup myBlockGroup) {
   MyBlockGroup = myBlockGroup;
  }
  public String Name {
   get {
    return MyBlockGroup.Name;
   }
  }
  public List < IMyTerminalBlock > Blocks {
   get {
    if (m_blocks == null) {
     m_blocks = new List < IMyTerminalBlock > ();
     MyBlockGroup.GetBlocks(m_blocks);
    }
    return m_blocks;
   }
  }
  private List < IMyTerminalBlock > m_blocks = null;
 }
 public List < BlockGroup > Groups {
  get {
   if (m_groups == null) {
    var groups = new List < IMyBlockGroup > ();
    Program.GridTerminalSystem.GetBlockGroups(groups);
    m_groups = new List < BlockGroup > ();
    groups.ForEach(group => m_groups.Add(new BlockGroup(group)));
   }
   return m_groups;
  }
 }
 private List < BlockGroup > m_groups = null;
 public Dictionary < string, BlockGroup > GroupsByName {
  get {
   if (m_groupsByName == null) {
    m_groupsByName = new Dictionary < string, BlockGroup > ();
    foreach(var group in Groups) {
     m_groupsByName.Add(group.Name.ToLower(), group);
    }
   }
   return m_groupsByName;
  }
 }
 private Dictionary < string, BlockGroup > m_groupsByName = null;
 public ZACommons(MyGridProgram program, UpdateType updateType,
  string shipGroup = null, ZAStorage storage = null) {
  Program = program;
  UpdateType = updateType;
  ShipGroupName = shipGroup;
  Storage = storage;
  IsDirty = false;
 }
 public BlockGroup GetBlockGroupWithName(string name) {
  BlockGroup group;
  if (GroupsByName.TryGetValue(name.ToLower(), out group)) {
   return group;
  }
  return null;
 }
 public List < BlockGroup > GetBlockGroupsWithPrefix(string prefix) {
  var result = new List < BlockGroup > ();
  foreach(var group in Groups) {
   if (group.Name.StartsWith(prefix, IGNORE_CASE)) result.Add(group);
  }
  return result;
 }
 public static List < T > GetBlocksOfType < T > (IEnumerable < IMyTerminalBlock > blocks,
  Func < T, bool > collect = null) {
  var list = new List < T > ();
  foreach(var block in blocks) {
   if (block is T && (collect == null || collect((T) block))) list.Add((T) block);
  }
  return list;
 }
 public static T GetBlockWithName < T > (IEnumerable < IMyTerminalBlock > blocks, string name)
 where T: IMyTerminalBlock {
  foreach(var block in blocks) {
   if (block is T && block.CustomName.Equals(name, IGNORE_CASE)) return (T) block;
  }
  return default (T);
 }
 public static List < IMyTerminalBlock > SearchBlocksOfName(IEnumerable < IMyTerminalBlock > blocks, string name, Func < IMyTerminalBlock, bool > collect = null) {
  var result = new List < IMyTerminalBlock > ();
  foreach(var block in blocks) {
   if (block.CustomName.IndexOf(name, IGNORE_CASE) >= 0 &&
    (collect == null || collect(block))) {
    result.Add(block);
   }
  }
  return result;
 }
 public static void ForEachBlockOfType < T > (IEnumerable < IMyTerminalBlock > blocks, Action < T > action) {
  foreach(var block in blocks) {
   if (block is T) {
    action((T) block);
   }
  }
 }
 public static void EnableBlocks(IEnumerable < IMyTerminalBlock > blocks, bool enabled) {
  foreach(var block in blocks) {
   block.SetValue < bool > ("OnOff", enabled);
  }
 }
 public IMyProgrammableBlock Me {
  get {
   return Program.Me;
  }
 }
 public Action < string > Echo {
  get {
   return Program.Echo;
  }
 }
 public void SetValue(string key, string value) {
  if (Storage != null) {
   if (!string.IsNullOrWhiteSpace(value)) {
    Storage.Data[key] = value;
   } else {
    Storage.Data.Remove(key);
   }
   IsDirty = true;
  }
 }
 public string GetValue(string key) {
  string value;
  if (Storage != null && Storage.Data.TryGetValue(key, out value)) {
   return value;
  }
  return null;
 }
}
public class ZAStorage {
 private
 const char KEY_DELIM = '\\';
 private
 const char PAIR_DELIM = '$';
 private readonly string PAIR_DELIM_STR = new string(PAIR_DELIM, 1);
 public readonly Dictionary < string, string > Data = new Dictionary < string, string > ();
 public string Encode() {
  var encoded = new List < string > ();
  foreach(var kv in Data) {
   ValidityCheck(kv.Key);
   ValidityCheck(kv.Value);
   var pair = new StringBuilder();
   pair.Append(kv.Key);
   pair.Append(KEY_DELIM);
   pair.Append(kv.Value);
   encoded.Add(pair.ToString());
  }
  return string.Join(PAIR_DELIM_STR, encoded);
 }
 public void Decode(string data) {
  Data.Clear();
  var pairs = data.Split(PAIR_DELIM);
  for (int i = 0; i < pairs.Length; i++) {
   var parts = pairs[i].Split(new char[] {
    KEY_DELIM
   }, 2);
   if (parts.Length == 2) {
    Data[parts[0]] = parts[1];
   }
  }
 }
 private void ValidityCheck(string value) {
  if (value.IndexOf(KEY_DELIM) >= 0 ||
   value.IndexOf(PAIR_DELIM) >= 0) {
   throw new Exception(string.Format("String '{0}' cannot be used by ZAStorage!", value));
  }
 }
}
// Generated from ZerothAngel's SEScripts version 1fee37d00657
// Modules: solaralignment, solargyrocontroller, printutils, eventdriver, shipcontrol, thrustcontrol, gyrocontrol, shiporientation, commons 

// MIT licensed. See https://github.com/ZerothAngel/SEScripts for raw code. 

// !!! Leading whitespace stripped to save bytes !!! 

// Solar Alignment 
const bool SOLAR_ALIGNMENT_DEFAULT_ACTIVE = true;

// SolarGyroController 
const float SOLAR_GYRO_VELOCITY = 0.05 f; // In radians per second 
const double SOLAR_GYRO_AXIS_TIMEOUT = 15.0; // In seconds 
const float SOLAR_GYRO_MIN_ERROR = 0.005 f; // As a fraction of theoretical max 
// If you're using modded solar panels, set these values (both in MW) 
const float SOLAR_PANEL_MAX_POWER_LARGE = 0.120f; // Max power of large panels 
const float SOLAR_PANEL_MAX_POWER_SMALL = 0.030f; // Max power of small panels 

// !!! CODE BEGINS HERE, EDIT AT YOUR OWN RISK !!! 

private readonly EventDriver eventDriver = new EventDriver();
private readonly SolarGyroController solarGyroController =
 new SolarGyroController(
  GyroControl.Pitch,
  GyroControl.Roll,
  GyroControl.Yaw
 );
private readonly ZAStorage myStorage = new ZAStorage();
private readonly ShipOrientation shipOrientation = new ShipOrientation();
private bool FirstRun = true;
Program() {
 Runtime.UpdateFrequency |= UpdateFrequency.Once;
}
void Main(string argument, UpdateType updateType) {
 var commons = new ShipControlCommons(this, updateType, shipOrientation,
  storage: myStorage);
 if (FirstRun) {
  FirstRun = false;
  myStorage.Decode(Storage);
  shipOrientation.SetShipReference(commons, "SolarGyroReference");
  solarGyroController.ConditionalInit(commons, eventDriver, SOLAR_ALIGNMENT_DEFAULT_ACTIVE);
 }
 eventDriver.Tick(commons, argAction: () => {
   solarGyroController.HandleCommand(commons, eventDriver, argument);
  },
  postAction: () => {
   solarGyroController.Display(commons);
  });
 if (commons.IsDirty) Storage = myStorage.Encode();
}

public class SolarGyroController {
 private
 const double RunDelay = 1.0;
 private
 const string ActiveKey = "SolarGyroController_Active";
 public struct SolarPanelDetails {
  public float MaxPowerOutput;
  public float DefinedPowerOutput;
  public SolarPanelDetails(IEnumerable < IMyTerminalBlock > blocks) {
   MaxPowerOutput = 0.0f;
   DefinedPowerOutput = 0.0f;
   foreach(var panel in ZACommons.GetBlocksOfType < IMySolarPanel > (blocks)) {
    if (panel.IsFunctional && panel.IsWorking) {
     MaxPowerOutput += panel.MaxOutput;
     DefinedPowerOutput += panel.CubeGrid.GridSize == 2.5 f ? SOLAR_PANEL_MAX_POWER_LARGE : SOLAR_PANEL_MAX_POWER_SMALL;
    }
   }
  }
 }
 private readonly int[] AllowedAxes;
 private readonly float[] LastVelocities;
 private readonly TimeSpan AxisTimeout = TimeSpan.FromSeconds(SOLAR_GYRO_AXIS_TIMEOUT);
 private float ? MaxPower = null;
 private int AxisIndex = 0;
 private bool Active = false;
 private TimeSpan TimeOnAxis;
 private float CurrentMaxPower;
 public SolarGyroController(params int[] allowedAxes) {
  AllowedAxes = (int[]) allowedAxes.Clone();
  LastVelocities = new float[AllowedAxes.Length];
  for (int i = 0; i < LastVelocities.Length; i++) {
   LastVelocities[i] = SOLAR_GYRO_VELOCITY;
  }
 }
 public void Init(ZACommons commons, EventDriver eventDriver) {
  var shipControl = (ShipControlCommons) commons;
  var gyroControl = shipControl.GyroControl;
  gyroControl.Reset();
  gyroControl.EnableOverride(true);
  Active = true;
  SaveActive(commons);
  MaxPower = null; // Use first-run initialization 
  CurrentMaxPower = 0.0f;
  eventDriver.Schedule(0.0, Run);
 }
 public void ConditionalInit(ZACommons commons, EventDriver eventDriver,
  bool defaultActive = false) {
  var activeValue = commons.GetValue(ActiveKey);
  if (activeValue != null) {
   bool active;
   if (Boolean.TryParse(activeValue, out active)) {
    if (active) Init(commons, eventDriver);
    return;
   }
  }
  if (defaultActive) Init(commons, eventDriver);
 }
 public void Run(ZACommons commons, EventDriver eventDriver) {
  if (!Active) return;
  var shipControl = (ShipControlCommons) commons;
  var gyroControl = shipControl.GyroControl;
  var currentAxis = AllowedAxes[AxisIndex];
  if (MaxPower == null) {
   MaxPower = -100.0f; // Start with something absurdly low to kick things off 
   gyroControl.Reset();
   gyroControl.EnableOverride(true);
   gyroControl.SetAxisVelocity(currentAxis, LastVelocities[AxisIndex]);
   TimeOnAxis = eventDriver.TimeSinceStart + AxisTimeout;
  }
  var solarPanelDetails = new SolarPanelDetails(commons.Blocks);
  CurrentMaxPower = solarPanelDetails.MaxPowerOutput;
  var minError = solarPanelDetails.DefinedPowerOutput * SOLAR_GYRO_MIN_ERROR;
  var delta = CurrentMaxPower - MaxPower;
  MaxPower = CurrentMaxPower;
  if (delta > minError) {
   gyroControl.EnableOverride(true);
  } else if (delta < -minError) {
   gyroControl.EnableOverride(true);
   LastVelocities[AxisIndex] = -LastVelocities[AxisIndex];
   gyroControl.SetAxisVelocity(currentAxis, LastVelocities[AxisIndex]);
  } else {
   gyroControl.EnableOverride(false);
  }
  if (TimeOnAxis <= eventDriver.TimeSinceStart && MaxPower < solarPanelDetails.DefinedPowerOutput * (1.0f - SOLAR_GYRO_MIN_ERROR)) {
   AxisIndex++;
   AxisIndex %= AllowedAxes.Length;
   gyroControl.Reset();
   gyroControl.EnableOverride(true);
   gyroControl.SetAxisVelocity(AllowedAxes[AxisIndex], LastVelocities[AxisIndex]);
   TimeOnAxis = eventDriver.TimeSinceStart + AxisTimeout;
  }
  eventDriver.Schedule(RunDelay, Run);
 }
 public void HandleCommand(ZACommons commons, EventDriver eventDriver,
  string argument) {
  argument = argument.Trim().ToLower();
  if (argument == "pause") {
   Active = false;
   SaveActive(commons);
   var shipControl = (ShipControlCommons) commons;
   var gyroControl = shipControl.GyroControl;
   gyroControl.Reset();
   gyroControl.EnableOverride(false);
  } else if (argument == "resume") {
   if (!Active) Init(commons, eventDriver);
  }
 }
 public void Display(ZACommons commons) {
  if (!Active) {
   commons.Echo("Solar Max Power: Paused");
  } else {
   commons.Echo(string.Format("Solar Max Power: {0}", PrintUtils.FormatPower(CurrentMaxPower)));
  }
 }
 private void SaveActive(ZACommons commons) {
  commons.SetValue(ActiveKey, Active.ToString());
 }
}

public static class PrintUtils {
 public static string FormatPower(float value) {
  if (value >= 1.0f) {
   return string.Format("{0:F2} MW", value);
  } else if (value >= 0.001) {
   return string.Format("{0:F2} kW", value * 1000f);
  } else {
   return string.Format("{0:F2} W", value * 1000000f);
  }
 }
}

public class EventDriver {
 public struct FutureTickAction: IComparable < FutureTickAction > {
  public ulong When;
  public Action < ZACommons,
  EventDriver > Action;
  public FutureTickAction(ulong when, Action < ZACommons, EventDriver > action = null) {
   When = when;
   Action = action;
  }
  public int CompareTo(FutureTickAction other) {
   return When.CompareTo(other.When);
  }
 }
 public struct FutureTimeAction: IComparable < FutureTimeAction > {
  public TimeSpan When;
  public Action < ZACommons,
  EventDriver > Action;
  public FutureTimeAction(TimeSpan when, Action < ZACommons, EventDriver > action = null) {
   When = when;
   Action = action;
  }
  public int CompareTo(FutureTimeAction other) {
   return When.CompareTo(other.When);
  }
 }
 private
 const float TicksPerSecond = 60.0f;
 private readonly LinkedList < FutureTickAction > TickQueue = new LinkedList < FutureTickAction > ();
 private readonly LinkedList < FutureTimeAction > TimeQueue = new LinkedList < FutureTimeAction > ();
 private ulong Ticks; // Not a reliable measure of time because of variable update frequency. 
 public TimeSpan TimeSinceStart {
  get;
  private set;
 }
 public EventDriver() {
  TimeSinceStart = TimeSpan.FromSeconds(0);
 }
 private void KickTimer(ZACommons commons) {
  if (TickQueue.First != null) {
   commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
  } else if (TimeQueue.First != null) {
   var next = (float)(TimeQueue.First.Value.When.TotalSeconds - TimeSinceStart.TotalSeconds);
   if (next < (10.0f / TicksPerSecond)) {
    commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
   } else if (next < (100.0f / TicksPerSecond)) {
    commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update10;
   } else {
    commons.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
   }
  } else {
   commons.Program.Runtime.UpdateFrequency = UpdateFrequency.None;
  }
 }
 public void Tick(ZACommons commons, Action mainAction = null,
  Action preAction = null,
  Action argAction = null,
  Action postAction = null) {
  Ticks++;
  TimeSinceStart += commons.Program.Runtime.TimeSinceLastRun;
  bool runMain = false;
  if (preAction != null) preAction();
  if (argAction != null && (commons.UpdateType & ~(UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once)) != 0) argAction();
  while (TickQueue.First != null &&
   TickQueue.First.Value.When <= Ticks) {
   var action = TickQueue.First.Value.Action;
   TickQueue.RemoveFirst();
   if (action != null) {
    action(commons, this);
   } else {
    runMain = true;
   }
  }
  while (TimeQueue.First != null &&
   TimeQueue.First.Value.When <= TimeSinceStart) {
   var action = TimeQueue.First.Value.Action;
   TimeQueue.RemoveFirst();
   if (action != null) {
    action(commons, this);
   } else {
    runMain = true;
   }
  }
  if (runMain && mainAction != null) mainAction();
  if (postAction != null) postAction();
  KickTimer(commons);
 }
 public void Schedule(ulong delay, Action < ZACommons, EventDriver > action = null) {
  var future = new FutureTickAction(Ticks + delay, action);
  for (var current = TickQueue.First; current != null; current = current.Next) {
   if (future.CompareTo(current.Value) < 0) {
    TickQueue.AddBefore(current, future);
    return;
   }
  }
  TickQueue.AddLast(future);
 }
 public void Schedule(double seconds, Action < ZACommons, EventDriver > action = null) {
  var delay = Math.Max(seconds, 0.0);
  var future = new FutureTimeAction(TimeSinceStart + TimeSpan.FromSeconds(delay), action);
  for (var current = TimeQueue.First; current != null; current = current.Next) {
   if (future.CompareTo(current.Value) < 0) {
    TimeQueue.AddBefore(current, future);
    return;
   }
  }
  TimeQueue.AddLast(future);
 }
}

public class ShipControlCommons: ZACommons {
 private readonly ShipOrientation shipOrientation;
 public Base6Directions.Direction ShipUp {
  get {
   return shipOrientation.ShipUp;
  }
 }
 public Base6Directions.Direction ShipForward {
  get {
   return shipOrientation.ShipForward;
  }
 }
 public MyBlockOrientation ShipBlockOrientation {
  get {
   return shipOrientation.BlockOrientation;
  }
 }
 public ShipControlCommons(MyGridProgram program, UpdateType updateType,
  ShipOrientation shipOrientation,
  string shipGroup = null,
  ZAStorage storage = null): base(program, updateType, shipGroup: shipGroup, storage: storage) {
  this.shipOrientation = shipOrientation;
 }
 public GyroControl GyroControl {
  get {
   if (m_gyroControl == null) {
    m_gyroControl = new GyroControl();
    m_gyroControl.Init(Blocks,
     shipUp: shipOrientation.ShipUp,
     shipForward: shipOrientation.ShipForward);
   }
   return m_gyroControl;
  }
 }
 private GyroControl m_gyroControl = null;
 public ThrustControl ThrustControl {
  get {
   if (m_thrustControl == null) {
    m_thrustControl = new ThrustControl();
    m_thrustControl.Init(Blocks,
     shipUp: shipOrientation.ShipUp,
     shipForward: shipOrientation.ShipForward);
   }
   return m_thrustControl;
  }
 }
 private ThrustControl m_thrustControl = null;
 public void Reset(bool gyroOverride = false,
  bool ? thrusterEnable = true,
  Func < IMyThrust, bool > thrusterCondition = null) {
  GyroControl.Reset();
  GyroControl.EnableOverride(gyroOverride);
  ThrustControl.Reset(thrusterCondition);
  if (thrusterEnable != null) ThrustControl.Enable((bool) thrusterEnable, thrusterCondition);
 }
 public Vector3D ReferencePoint {
  get {
   if (m_referencePoint == null) {
    m_referencePoint = ShipController != null ? ShipController.CenterOfMass : Me.GetPosition();
   }
   return (Vector3D) m_referencePoint;
  }
 }
 private Vector3D ? m_referencePoint = null;
 public Vector3D ReferenceUp {
  get {
   if (m_referenceUp == null) {
    m_referenceUp = GetReferenceVector(shipOrientation.ShipUp);
   }
   return (Vector3D) m_referenceUp;
  }
 }
 private Vector3D ? m_referenceUp = null;
 public Vector3D ReferenceForward {
  get {
   if (m_referenceForward == null) {
    m_referenceForward = GetReferenceVector(shipOrientation.ShipForward);
   }
   return (Vector3D) m_referenceForward;
  }
 }
 private Vector3D ? m_referenceForward = null;
 public Vector3D ReferenceLeft {
  get {
   if (m_referenceLeft == null) {
    m_referenceLeft = GetReferenceVector(Base6Directions.GetLeft(shipOrientation.ShipUp, shipOrientation.ShipForward));
   }
   return (Vector3D) m_referenceLeft;
  }
 }
 private Vector3D ? m_referenceLeft = null;
 private Vector3D GetReferenceVector(Base6Directions.Direction direction) {
  var offset = Me.Position + Base6Directions.GetIntVector(direction);
  return Vector3D.Normalize(Me.CubeGrid.GridIntegerToWorld(offset) - Me.GetPosition());
 }
 public IMyShipController ShipController {
  get {
   if (m_shipController == null) {
    foreach(var block in Blocks) {
     var controller = block as IMyShipController;
     if (controller != null && controller.IsFunctional) {
      m_shipController = controller;
      break;
     }
    }
   }
   return m_shipController;
  }
 }
 private IMyShipController m_shipController = null;
 public Vector3D ? LinearVelocity {
  get {
   return ShipController != null ?
    ShipController.GetShipVelocities().LinearVelocity : (Vector3D ? ) null;
  }
 }
 public Vector3D ? AngularVelocity {
  get {
   return ShipController != null ?
    ShipController.GetShipVelocities().AngularVelocity : (Vector3D ? ) null;
  }
 }
}

public class ThrustControl {
 private readonly Dictionary < Base6Directions.Direction, List < IMyThrust >> thrusters = new Dictionary < Base6Directions.Direction, List < IMyThrust >> ();
 private void AddThruster(Base6Directions.Direction direction, IMyThrust thruster) {
  var thrusterList = GetThrusters(direction); // collect must be null to modify original list 
  thrusterList.Add(thruster);
 }
 public void Init(IEnumerable < IMyTerminalBlock > blocks,
  Func < IMyThrust, bool > collect = null,
  Base6Directions.Direction shipUp = Base6Directions.Direction.Up,
  Base6Directions.Direction shipForward = Base6Directions.Direction.Forward) {
  MyBlockOrientation shipOrientation = new MyBlockOrientation(shipForward, shipUp);
  thrusters.Clear();
  foreach(var block in blocks) {
   var thruster = block as IMyThrust;
   if (thruster != null && thruster.IsFunctional &&
    (collect == null || collect(thruster))) {
    var facing = thruster.Orientation.TransformDirection(Base6Directions.Direction.Forward); // Exhaust goes this way 
    var thrustDirection = Base6Directions.GetFlippedDirection(facing);
    var shipDirection = shipOrientation.TransformDirectionInverse(thrustDirection);
    AddThruster(shipDirection, thruster);
   }
  }
 }
 public List < IMyThrust > GetThrusters(Base6Directions.Direction direction,
  Func < IMyThrust, bool > collect = null,
  bool disable = false) {
  List < IMyThrust > thrusterList;
  if (!thrusters.TryGetValue(direction, out thrusterList)) {
   thrusterList = new List < IMyThrust > ();
   thrusters.Add(direction, thrusterList);
  }
  if (collect == null) {
   return thrusterList;
  } else {
   var result = new List < IMyThrust > ();
   foreach(var thruster in thrusterList) {
    if (collect(thruster)) {
     result.Add(thruster);
    } else if (disable) {
     thruster.Enabled = false;
    }
   }
   return result;
  }
 }
 public void SetOverride(Base6Directions.Direction direction, bool enable = true,
  Func < IMyThrust, bool > collect = null) {
  var thrusterList = GetThrusters(direction, collect, true);
  thrusterList.ForEach(thruster =>
   thruster.SetValue < float > ("Override", enable ?
    thruster.GetMaximum < float > ("Override") :
    0.0f));
 }
 public void SetOverride(Base6Directions.Direction direction, double percent,
  Func < IMyThrust, bool > collect = null) {
  percent = Math.Max(percent, 0.0);
  percent = Math.Min(percent, 1.0);
  var thrusterList = GetThrusters(direction, collect, true);
  thrusterList.ForEach(thruster =>
   thruster.SetValue < float > ("Override",
    (float)(thruster.GetMaximum < float > ("Override") * percent)));
 }
 public void Enable(Base6Directions.Direction direction, bool enable,
  Func < IMyThrust, bool > collect = null) {
  var thrusterList = GetThrusters(direction, collect, true);
  thrusterList.ForEach(thruster => thruster.Enabled = enable);
 }
 public void Enable(bool enable,
  Func < IMyThrust, bool > collect = null) {
  foreach(var thrusterList in thrusters.Values) {
   thrusterList.ForEach(thruster => {
    if (collect == null || collect(thruster)) thruster.Enabled = enable;
   });
  }
 }
 public void Reset(Func < IMyThrust, bool > collect = null) {
  foreach(var thrusterList in thrusters.Values) {
   thrusterList.ForEach(thruster => {
    if (collect == null || collect(thruster)) thruster.SetValue < float > ("Override", 0.0f);
   });
  }
 }
}

public class GyroControl {
 public
 const int Yaw = 0;
 public
 const int Pitch = 1;
 public
 const int Roll = 2;
 public readonly string[] AxisNames = new string[] {
  "Yaw",
  "Pitch",
  "Roll"
 };
 public struct GyroAxisDetails {
  public int LocalAxis;
  public int Sign;
  public GyroAxisDetails(int localAxis, int sign) {
   LocalAxis = localAxis;
   Sign = sign;
  }
 }
 public struct GyroDetails {
  public IMyGyro Gyro;
  public GyroAxisDetails[] AxisDetails;
  public GyroDetails(IMyGyro gyro, Base6Directions.Direction shipUp,
   Base6Directions.Direction shipForward) {
   Gyro = gyro;
   AxisDetails = new GyroAxisDetails[3];
   var shipLeft = Base6Directions.GetLeft(shipUp, shipForward);
   SetAxisDetails(gyro, Yaw, shipUp);
   SetAxisDetails(gyro, Pitch, shipLeft);
   SetAxisDetails(gyro, Roll, shipForward);
  }
  private void SetAxisDetails(IMyGyro gyro, int axis,
   Base6Directions.Direction axisDirection) {
   switch (gyro.Orientation.TransformDirectionInverse(axisDirection)) {
    case Base6Directions.Direction.Up:
     AxisDetails[axis] = new GyroAxisDetails(Yaw, -1);
     break;
    case Base6Directions.Direction.Down:
     AxisDetails[axis] = new GyroAxisDetails(Yaw, 1);
     break;
    case Base6Directions.Direction.Left:
     AxisDetails[axis] = new GyroAxisDetails(Pitch, -1);
     break;
    case Base6Directions.Direction.Right:
     AxisDetails[axis] = new GyroAxisDetails(Pitch, 1);
     break;
    case Base6Directions.Direction.Forward:
     AxisDetails[axis] = new GyroAxisDetails(Roll, 1);
     break;
    case Base6Directions.Direction.Backward:
     AxisDetails[axis] = new GyroAxisDetails(Roll, -1);
     break;
   }
  }
 }
 private readonly List < GyroDetails > gyros = new List < GyroDetails > ();
 public void Init(IEnumerable < IMyTerminalBlock > blocks,
  Func < IMyGyro, bool > collect = null,
  Base6Directions.Direction shipUp = Base6Directions.Direction.Up,
  Base6Directions.Direction shipForward = Base6Directions.Direction.Forward) {
  gyros.Clear();
  foreach(var block in blocks) {
   var gyro = block as IMyGyro;
   if (gyro != null &&
    gyro.IsFunctional && gyro.IsWorking && gyro.Enabled &&
    (collect == null || collect(gyro))) {
    var details = new GyroDetails(gyro, shipUp, shipForward);
    gyros.Add(details);
   }
  }
 }
 public void EnableOverride(bool enable) {
  gyros.ForEach(gyro => gyro.Gyro.GyroOverride = enable);
 }
 public void SetAxisVelocity(int axis, float velocity) {
  SetAxisVelocityRPM(axis, velocity * MathHelper.RadiansPerSecondToRPM);
 }
 public void SetAxisVelocityRPM(int axis, float rpmVelocity) {
  gyros.ForEach(gyro => gyro.Gyro.SetValue < float > (AxisNames[gyro.AxisDetails[axis].LocalAxis], gyro.AxisDetails[axis].Sign * rpmVelocity));
 }
 public void SetAxisVelocityFraction(int axis, float fraction) {
  SetAxisVelocityRPM(axis, 30.0f * fraction);
 }
 public void Reset() {
  gyros.ForEach(gyro => {
   gyro.Gyro.SetValue < float > ("Yaw", 0.0f);
   gyro.Gyro.SetValue < float > ("Pitch", 0.0f);
   gyro.Gyro.SetValue < float > ("Roll", 0.0f);
  });
 }
}

public class ShipOrientation {
 public Base6Directions.Direction ShipUp {
  get;
  private set;
 }
 public Base6Directions.Direction ShipForward {
  get;
  private set;
 }
 public MyBlockOrientation BlockOrientation {
  get {
   return new MyBlockOrientation(ShipForward, ShipUp);
  }
 }
 public ShipOrientation() {
  ShipUp = Base6Directions.Direction.Up;
  ShipForward = Base6Directions.Direction.Forward;
 }
 public void SetShipReference(IMyCubeBlock reference) {
  ShipUp = reference.Orientation.TransformDirection(Base6Directions.Direction.Up);
  ShipForward = reference.Orientation.TransformDirection(Base6Directions.Direction.Forward);
 }
 public void SetShipReference(ZACommons commons, string groupName,
  Func < IMyTerminalBlock, bool > condition = null) {
  var group = commons.GetBlockGroupWithName(groupName);
  if (group != null) {
   foreach(var block in group.Blocks) {
    if (block.CubeGrid == commons.Me.CubeGrid &&
     (condition == null || condition(block))) {
     SetShipReference(block);
     return;
    }
   }
  }
  ShipUp = Base6Directions.Direction.Up;
  ShipForward = Base6Directions.Direction.Forward;
 }
 public void SetShipReference < T > (IEnumerable < IMyTerminalBlock > blocks,
  Func < T, bool > condition = null)
 where T: IMyCubeBlock {
  var references = ZACommons.GetBlocksOfType < T > (blocks, condition);
  if (references.Count > 0) {
   SetShipReference(references[0]);
  } else {
   ShipUp = Base6Directions.Direction.Up;
   ShipForward = Base6Directions.Direction.Forward;
  }
 }
}

public class ZACommons {
 public
 const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
 public readonly MyGridProgram Program;
 public readonly UpdateType UpdateType;
 private readonly string ShipGroupName;
 private readonly ZAStorage Storage;
 public bool IsDirty {
  get;
  private set;
 }
 public List < IMyTerminalBlock > AllBlocks {
  get {
   if (m_allBlocks == null) {
    m_allBlocks = new List < IMyTerminalBlock > ();
    Program.GridTerminalSystem.GetBlocks(m_allBlocks);
   }
   return m_allBlocks;
  }
 }
 private List < IMyTerminalBlock > m_allBlocks = null;
 public List < IMyTerminalBlock > Blocks {
  get {
   if (m_blocks == null) {
    if (ShipGroupName != null) {
     var group = GetBlockGroupWithName(ShipGroupName);
     if (group != null) m_blocks = group.Blocks;
    }
    if (m_blocks == null) {
     m_blocks = new List < IMyTerminalBlock > ();
     foreach(var block in AllBlocks) {
      if (block.CubeGrid == Program.Me.CubeGrid) m_blocks.Add(block);
     }
    }
   }
   return m_blocks;
  }
 }
 private List < IMyTerminalBlock > m_blocks = null;
 public class BlockGroup {
  private readonly IMyBlockGroup MyBlockGroup;
  public BlockGroup(IMyBlockGroup myBlockGroup) {
   MyBlockGroup = myBlockGroup;
  }
  public String Name {
   get {
    return MyBlockGroup.Name;
   }
  }
  public List < IMyTerminalBlock > Blocks {
   get {
    if (m_blocks == null) {
     m_blocks = new List < IMyTerminalBlock > ();
     MyBlockGroup.GetBlocks(m_blocks);
    }
    return m_blocks;
   }
  }
  private List < IMyTerminalBlock > m_blocks = null;
 }
 public List < BlockGroup > Groups {
  get {
   if (m_groups == null) {
    var groups = new List < IMyBlockGroup > ();
    Program.GridTerminalSystem.GetBlockGroups(groups);
    m_groups = new List < BlockGroup > ();
    groups.ForEach(group => m_groups.Add(new BlockGroup(group)));
   }
   return m_groups;
  }
 }
 private List < BlockGroup > m_groups = null;
 public Dictionary < string, BlockGroup > GroupsByName {
  get {
   if (m_groupsByName == null) {
    m_groupsByName = new Dictionary < string, BlockGroup > ();
    foreach(var group in Groups) {
     m_groupsByName.Add(group.Name.ToLower(), group);
    }
   }
   return m_groupsByName;
  }
 }
 private Dictionary < string, BlockGroup > m_groupsByName = null;
 public ZACommons(MyGridProgram program, UpdateType updateType,
  string shipGroup = null, ZAStorage storage = null) {
  Program = program;
  UpdateType = updateType;
  ShipGroupName = shipGroup;
  Storage = storage;
  IsDirty = false;
 }
 public BlockGroup GetBlockGroupWithName(string name) {
  BlockGroup group;
  if (GroupsByName.TryGetValue(name.ToLower(), out group)) {
   return group;
  }
  return null;
 }
 public List < BlockGroup > GetBlockGroupsWithPrefix(string prefix) {
  var result = new List < BlockGroup > ();
  foreach(var group in Groups) {
   if (group.Name.StartsWith(prefix, IGNORE_CASE)) result.Add(group);
  }
  return result;
 }
 public static List < T > GetBlocksOfType < T > (IEnumerable < IMyTerminalBlock > blocks,
  Func < T, bool > collect = null) {
  var list = new List < T > ();
  foreach(var block in blocks) {
   if (block is T && (collect == null || collect((T) block))) list.Add((T) block);
  }
  return list;
 }
 public static T GetBlockWithName < T > (IEnumerable < IMyTerminalBlock > blocks, string name)
 where T: IMyTerminalBlock {
  foreach(var block in blocks) {
   if (block is T && block.CustomName.Equals(name, IGNORE_CASE)) return (T) block;
  }
  return default (T);
 }
 public static List < IMyTerminalBlock > SearchBlocksOfName(IEnumerable < IMyTerminalBlock > blocks, string name, Func < IMyTerminalBlock, bool > collect = null) {
  var result = new List < IMyTerminalBlock > ();
  foreach(var block in blocks) {
   if (block.CustomName.IndexOf(name, IGNORE_CASE) >= 0 &&
    (collect == null || collect(block))) {
    result.Add(block);
   }
  }
  return result;
 }
 public static void ForEachBlockOfType < T > (IEnumerable < IMyTerminalBlock > blocks, Action < T > action) {
  foreach(var block in blocks) {
   if (block is T) {
    action((T) block);
   }
  }
 }
 public static void EnableBlocks(IEnumerable < IMyTerminalBlock > blocks, bool enabled) {
  foreach(var block in blocks) {
   block.SetValue < bool > ("OnOff", enabled);
  }
 }
 public IMyProgrammableBlock Me {
  get {
   return Program.Me;
  }
 }
 public Action < string > Echo {
  get {
   return Program.Echo;
  }
 }
 public void SetValue(string key, string value) {
  if (Storage != null) {
   if (!string.IsNullOrWhiteSpace(value)) {
    Storage.Data[key] = value;
   } else {
    Storage.Data.Remove(key);
   }
   IsDirty = true;
  }
 }
 public string GetValue(string key) {
  string value;
  if (Storage != null && Storage.Data.TryGetValue(key, out value)) {
   return value;
  }
  return null;
 }
}
public class ZAStorage {
 private
 const char KEY_DELIM = '\\';
 private
 const char PAIR_DELIM = '$';
 private readonly string PAIR_DELIM_STR = new string(PAIR_DELIM, 1);
 public readonly Dictionary < string, string > Data = new Dictionary < string, string > ();
 public string Encode() {
  var encoded = new List < string > ();
  foreach(var kv in Data) {
   ValidityCheck(kv.Key);
   ValidityCheck(kv.Value);
   var pair = new StringBuilder();
   pair.Append(kv.Key);
   pair.Append(KEY_DELIM);
   pair.Append(kv.Value);
   encoded.Add(pair.ToString());
  }
  return string.Join(PAIR_DELIM_STR, encoded);
 }
 public void Decode(string data) {
  Data.Clear();
  var pairs = data.Split(PAIR_DELIM);
  for (int i = 0; i < pairs.Length; i++) {
   var parts = pairs[i].Split(new char[] {
    KEY_DELIM
   }, 2);
   if (parts.Length == 2) {
    Data[parts[0]] = parts[1];
   }
  }
 }
 private void ValidityCheck(string value) {
  if (value.IndexOf(KEY_DELIM) >= 0 ||
   value.IndexOf(PAIR_DELIM) >= 0) {
   throw new Exception(string.Format("String '{0}' cannot be used by ZAStorage!", value));
  }
 }
}
*/
		
		//====================================================
	}
	
}