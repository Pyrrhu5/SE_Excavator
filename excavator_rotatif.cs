/*
 * ==========================================================================
 *                             ROTATIF EXCAVATOR                             
 * ==========================================================================
 *
 * The autonomous rotatif excavator will drill in an half-moon shape as deep
 * as it can or until it detects no more resources are pulled.
 *
 * Componants required:
 * 		- A group of cargo containers: 						"[Excavator] Cargos"
 *  	- A drill: 											"[Excavator] Drill"
 *  	- A rotor: 											"[Excavator] Rotor"
 *  	- A group of vertical pistons: 						"[Excavator] Pistons vertical"
 *  	- A group of vertical pistons 
 *    		oriented in reverse compare to the previous one:"[Excavator] Pistons vertical r"
 *  	- A group of horizontal pistons: 					"[Excavator] Pistons horizontal"
 *  	- [Optional] A LCD display: 						"[Excavator] LCD"
 *  
 *  Drilling phases:
 *  	- startup_phase
 *  	- loop until piston h has reached max
 *  		- loop until piston v has reached max || not collecting
 *  			- loop until rotor.has_reached_max:
 * 					- lower piston vertical
 * 					- inverse rotor
 * 			- raise piston vertical
 * 			- increase piston horizontal
 * 		- stop_phase
 */

/* =============================================================================
 *                                 CONFIGURATION
 * =============================================================================
 * Edit these global variables to suit your setup
 */
// Beginning of the name for the componants and groups controlled by the script
const string elementsName = "[Excavator] - ";

/* These variables should be fine, but you can edit them if need be */
// The sum of the speed of all piston on one axis put together
const float baseVelocity = 0.4f;
const float rotorVelocity = 1.0f;
// Number of meter for the pistons to travel in one phase
const float pistonTravel = 1.0f;
// Number of strick ticks the cargo is being check
// before considering no more ores are being collected
const int nIterCargoCheck = 100;

/* =============================================================================
 *                               CONTROL VARIABLES
 * =============================================================================
 * Global variables, no touchy
 */

// Is it currently collecting resources
bool isCollecting = false;
// Cargo size of the last check
float previousCargo;
// For the pistons
float pistonsVelocityH;
float pistonsVelocityV;
float maxDistH;
float maxDistV;
float pistonsTravelPerPhaseV;
float pistonsTravelPerPhaseH;
float pistonsCurrentDistV;
float pistonsTravel = 2.0f;
// for the rotor
float minAngle;
float maxAngle;
float targetAngle;
// For the cargo check
int currentIter;
float maxCargo = 0.0f;
// Control flow
// The phase index in List<Action> phases
// it's currently executing
int? currentPhase = 0;
// List of drill phases functions
List<Func<Boolean>> phases = new List<Func<Boolean>>();
bool phaseHasStarted = false;

/* =============================================================================
 *                                  COMPONANTS
 * =============================================================================
 * Grab the excavator componants
 */

// Drill head
IMyShipDrill drill;
// Cargo
List<IMyTerminalBlock> cargos = new List<IMyTerminalBlock>();
// Pistons
List<IMyPistonBase> pistonsVertical 		= new List<IMyPistonBase>();
List<IMyPistonBase> pistonsVerticalReversed	= new List<IMyPistonBase>();
List<IMyPistonBase> pistonsHorizontal 		= new List<IMyPistonBase>();
// Rotor
IMyMotorStator rotor;
// LCD display
IMyTextPanel display;

/* =============================================================================
 *                                     INIT
 * =============================================================================
 * Grab the game elements and setup the global variables.
 */
public Program() {
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	// Pistons vertical
	IMyBlockGroup pistonsGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Pistons vertical");
	pistonsGroup.GetBlocks(temp);
	foreach ( IMyPistonBase piston in temp ) { 
		maxDistV += 10.0f;
		pistonsVertical.Add((IMyPistonBase) piston);
	}
	temp = new List<IMyTerminalBlock>();
	pistonsGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Pistons vertical r");
	pistonsGroup.GetBlocks(temp);
	foreach ( IMyPistonBase piston in temp ) {
	   maxDistV += 10.0f;
	   pistonsVerticalReversed.Add((IMyPistonBase) piston);
	}
	pistonsVelocityV = baseVelocity / (pistonsVertical.Count + pistonsVerticalReversed.Count);
	pistonsTravelPerPhaseV = pistonsTravel / (pistonsVertical.Count + pistonsVerticalReversed.Count);

	// Pistons horizontal
	temp = new List<IMyTerminalBlock>();
	pistonsGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Pistons horizontal");
	pistonsGroup.GetBlocks(temp);
	foreach ( IMyPistonBase piston in temp ) {
		maxDistH += 10.0f;
		pistonsHorizontal.Add((IMyPistonBase) piston);
	}
	pistonsVelocityH = baseVelocity / pistonsHorizontal.Count;
	pistonsTravelPerPhaseH = pistonsTravel / pistonsHorizontal.Count;

	// Drill
	drill = GridTerminalSystem.GetBlockWithName(elementsName + "Drill") as IMyShipDrill;

	// cargos
	IMyBlockGroup cargosGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Cargos");
	cargosGroup.GetBlocks(cargos);
	foreach ( IMyCargoContainer cargo in cargos ) { maxCargo += (float) cargo.GetInventory(0).MaxVolume * 1000f; }
	// Rotor
	rotor = GridTerminalSystem.GetBlockWithName(elementsName + "Rotor") as IMyMotorStator;
	minAngle = rotor.LowerLimitDeg;
	maxAngle = rotor.UpperLimitDeg;

	// LCD
	display = GridTerminalSystem.GetBlockWithName(elementsName + "LCD") as IMyTextPanel;
  
	string msg = "Initialized";
	msg += "\n" + "Drill: " + (drill != null);
	msg += "\n" + "Rotor: " + (rotor != null);
	msg += "\n" + pistonsVertical.Count.ToString() + " vertical pistons";
	msg += "\n" + pistonsVerticalReversed.Count.ToString() + " reversed vertical pistons";
	msg += "\n" + pistonsHorizontal.Count.ToString() + " horizontal pistons";
	msg += "\n" + cargos.Count.ToString() + " cargos";
	msg += "\n" + "Display: " + (display != null);
	Echo (msg);
	if ( display != null ) { display.WriteText(msg); }
	status("Ready"); 

	// Add the drilling phases to the drilling sequence
	phases.Add(startup_phase);
	phases.Add(stop_phase);
	phases.Add(rotation_phase);
	phases.Add(vertical_phase);
	phases.Add(horizontal_phase);
}
/* ========================================================================== */
/*                                   UTILS                                    */
/* ========================================================================== */
public void status( string phase ){
	string msg;
	float percentCargo = ( current_cargo() / maxCargo ) * 100;
	float percentCargoNs =  ( current_cargo_ns() / maxCargo ) * 100;
	msg = phase;
	msg += "\n" + "Cargo load:" + percentCargo.ToString("0.00") + "%" ;
	msg += "\n" + "Cargo load (without stones): " + percentCargoNs.ToString("0.00") + "%" ;
	msg += "\n" + "Distance vertical:   " + current_distance(pistonsVertical).ToString("0.00") + "m";
	msg += "\n" + "Distance vertical (reversed):   " + current_distance(pistonsVerticalReversed).ToString("0.00") + "m";
	msg += "\n" + "Distance horizontal:   " + current_distance(pistonsHorizontal).ToString("0.00") + "m";
	msg += "\n" + "Travel vertical:        " + ((current_distance(pistonsVertical)/maxDistV)*100).ToString("0.00") + "%";
	msg += "\n" + "Travel vertical (reversed):        " + ((current_distance(pistonsVerticalReversed)/maxDistV)*100).ToString("0.00") + "%";
	msg += "\n" + "Travel horizontal:        " + ((current_distance(pistonsHorizontal)/maxDistH)*100).ToString("0.00") + "%";
	if (  display != null ) { display.WriteText(msg); }
	Echo(msg);
}

public float current_distance(List<IMyPistonBase> pistons) {
	float cDist = 0.0f;
	foreach ( IMyPistonBase piston in pistons ) { cDist += piston.CurrentPosition; }
	return cDist;
}


public float current_cargo() {
	// Get the total volume of the cargos containers
	float cLoad = 0.0f;
	foreach ( IMyCargoContainer cargo in cargos ) { cLoad += (float) cargo.GetInventory(0).CurrentVolume * 1000f; }
	return cLoad;
}


public bool cargo_has_stones() {
	foreach ( IMyCargoContainer cargo in cargos ) {
		IMyInventory cargoInv = cargo.GetInventory(0);
		for ( int i = 0; i < cargoInv.ItemCount; i++ ) {
			MyInventoryItem item = (MyInventoryItem) cargoInv.GetItemAt(i);
			if ( item.Type.SubtypeId.Contains("Stone") ){
				return true;
			}
		}
	}
	return false;
}

public float current_cargo_ns() {
	// Get the total volume of the cargos containers excluding stones
	float cLoad = 0.0f;
	foreach ( IMyCargoContainer cargo in cargos ) {
		IMyInventory cargoInv = cargo.GetInventory(0);
		for ( int i = 0; i < cargoInv.ItemCount; i++ ) {
			MyInventoryItem item = (MyInventoryItem) cargoInv.GetItemAt(i);
			if ( !item.Type.SubtypeId.Contains("Stone") ){
				float amount = ( (float) item.Amount * 0.37f); // const is volume per kg of ores
				cLoad += (float) amount;
			}
		}
	}
	return cLoad;
}


public bool cargo_is_full() {
	float percent = (float) current_cargo() / (float) maxCargo;
	return ( percent >= .99 );
}


public bool cargo_is_full_ns() {
	float percent = (float) current_cargo_ns() / (float) maxCargo;
	return ( percent >= .99 );
}

/* =============================================================================
 *                                  COMPONANTS
 * =============================================================================
 */

public void move_pistons(List<IMyPistonBase> pistons, float target, char axis){
	float velocity = 0.0f;
	switch ( axis ){
		case 'h':
			velocity = pistonsVelocityH;
			break;
		case 'v':
			velocity = pistonsVelocityV;
			break;
	}
	if (target < pistons[0].CurrentPosition){
		velocity = -velocity;
	}
	foreach ( IMyPistonBase piston in pistons ){
		piston.MinLimit = target;
		piston.MaxLimit = target;
		piston.Velocity = velocity;
	}
}

/* Evaluate if a list of pistons have reached their destination
 * by evaluating the max/min limit and the current position
 * of the first piston in the list
 */
public bool pistons_at_max(List<IMyPistonBase> pistons, bool isReversed){
	float maxDistance;
	if ( isReversed ){
		maxDistance = pistons[0].MinLimit;
	} else {
		maxDistance = pistons[0].MaxLimit;
	}

	return ( pistons[0].CurrentPosition == maxDistance );
}

public bool is_rotor_at_max(){
	float currentAngle = (float) Math.Round(rotor.Angle / (float) Math.PI * 180f);
	if (rotor.TargetVelocityRPM < 0){
		return ( (currentAngle - 1) < rotor.LowerLimitDeg );
	} else {
		return ( (currentAngle + 1) > rotor.UpperLimitDeg );
	}
}

/* =============================================================================
 *                                    PHASES
 * =============================================================================
 */

public bool startup_phase(){
	status("Initialization");
	// Start of the phase
	if ( !phaseHasStarted ){
		Runtime.UpdateFrequency = UpdateFrequency.Update10;
		// initialize cargo variables
		previousCargo = current_cargo_ns();
		currentIter = 0;
		isCollecting = false;
		// Set the rotor at the start position
		rotor.TargetVelocityRPM = rotorVelocity;
		targetAngle = maxAngle;
		phaseHasStarted = true;
	}

	if ( !is_rotor_at_max() ) {
		return false;
	} else if ( pistonsVertical[0].CurrentPosition != 0.0f){
		move_pistons(pistonsVertical, 0.0f, 'v');
		move_pistons(pistonsVerticalReversed, 10.0f * pistonsVerticalReversed.Count, 'v');
		return false;
	}

	// Start the drills
	drill.Enabled = true;

	currentPhase = 2;
	phaseHasStarted = false;
	status("Initialized");
	return true;
}

public bool stop_phase(){
	if ( !phaseHasStarted ){
		rotor.TargetVelocityRPM = rotorVelocity;
		drill.Enabled = false;
		move_pistons(pistonsVertical, 0, 'v');
		move_pistons(pistonsVerticalReversed, 10, 'v');
		move_pistons(pistonsHorizontal, 0, 'h');
		phaseHasStarted = true;
	}

	if (
		pistons_at_max(pistonsVertical, true)
		&& pistons_at_max(pistonsVerticalReversed, false)
		&& pistons_at_max(pistonsHorizontal, true)
		&& is_rotor_at_max()
	){
		currentPhase = null;
		status("Stopped");
		return true;
	}

	status("Stopping");
	return false;
}

public bool rotation_phase(){
	status("Drilling - Rotation phase");
	// stop
	// Start of the phase, invert the rotation
	if ( !phaseHasStarted ){
		if ( targetAngle == minAngle ){
			rotor.TargetVelocityRPM = -rotorVelocity;
			targetAngle = maxAngle;
		} else {
			rotor.TargetVelocityRPM = rotorVelocity;
			targetAngle = minAngle;
		}
		phaseHasStarted = true;
	}

	// End of the phase when the rotor is at
	// max distance
	// Start vertical phase
	if ( is_rotor_at_max() ){
		// If the vertical is at max
		if ( pistonsVertical[0].CurrentPosition == maxDistV ){
			currentPhase = 4;
		} else  if (pistonsHorizontal[0].CurrentPosition == maxDistH ) {
			currentPhase = 1;
		} else {
			currentPhase = 3;
		}
		phaseHasStarted = false;
		return true;
	}
	return false;
}

public bool vertical_phase(){
	status("Drilling - Vertical phase");
	// Start of the phase, lower pistons
	if ( !phaseHasStarted ){
		move_pistons(
			pistonsVertical,
			pistonsVertical[0].MaxLimit + pistonsTravelPerPhaseV,
			'v'
		);
		move_pistons(
			pistonsVerticalReversed,
			pistonsVerticalReversed[0].MaxLimit - pistonsTravelPerPhaseV,
			'v'
		);
		phaseHasStarted = true;
	}

	// End of the phase when the pistons are
	// at max distance.
	// Start horizontal phase
	if ( pistons_at_max(pistonsVertical, false) ) {
		currentPhase = 2;
		phaseHasStarted = false;
		return true;
	}
	return false;
}

public bool horizontal_phase(){
	status("Drilling - Horizontal phase");
	// Raise the vertical pistons
	if ( !phaseHasStarted ){
		move_pistons(
			pistonsVertical,
			0,
			'v'
		);
		move_pistons(
			pistonsVerticalReversed,
			10 * pistonsVerticalReversed.Count,
			'v'
		);
	}

	// Wait for the vertical pistons to be raised
	if ( !pistons_at_max(pistonsVertical, true) ){
	   return false;
	}

	if ( !phaseHasStarted ){
		move_pistons(
			pistonsHorizontal,
			pistonsHorizontal[0].MinLimit + pistonsTravelPerPhaseH,
			'h'
		);
		phaseHasStarted = true;
	}

	// End of the phase when the horizontal piston
	// is lowered
	// Start a rotation phase
	if ( pistons_at_max(pistonsHorizontal, false) ){
		currentPhase = 2;
		phaseHasStarted = false;
		return true;
	}
	return false;
}

/* =============================================================================
 *                                     MAIN
 * =============================================================================
 */

public void Main(string argument, UpdateType updateSource) {
	// Auto execution
	if ( (updateSource & UpdateType.Update10) != 0 ) {
		if (currentPhase != null){
			phases[(int)currentPhase]();
		} else {
			Runtime.UpdateFrequency = UpdateFrequency.None;
		}
	}
	// Manual launch
	if ( (updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0 ){
		switch (argument){
			case "start":
				phaseHasStarted = false;
				currentPhase = 0;
				startup_phase();
				break;
			case "stop": 
				phaseHasStarted = false;
				currentPhase = 1;
				stop_phase();
				break;
			case "status":
				status("Status report");
				return;
			default: 
				status("ERROR - Wrong argument [start|stop|status]");
				break;
		}
	} 
}
