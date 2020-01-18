/*
 * ==============================================================================
 *                                EXCAVATOR SCRIPT
 * ==============================================================================
 *
 * Manages the drilling sequences of a land vehicule equipped with a retratable
 * arm of pistons and drills.
 * For large and small grids vehicules and/or arm
 * without limit in the depth or circumference to drill
 *
 * Usage (arguments):
 * 	- start	: lock the vehicule, deploy the arm and start drilling
 *	- stop	: retract the drill, retract the arm and unlock the vehicule
 *	- status: print in the console and display the cargo status
 *	- park	: deploy the parking landing gear, no drilling phase
 *	- unpark: retract the parking landing gear
 *
 * Handles:
 *	- Locking/unlocking a safety lock for the vehicule
 * 	- Deploying/retracting the drilling arm
 *	- Stopping when the cargos are full of stones
 *	- Restarting when the cargos get empties of stones
 *	- Resetting to a drivable position when:
 *		+ the cargo is full of ores
 *		+ the pistons reached their limit
 *		+ the end of the ores' vein has been reached
 *		+ the engineer got tired and wants to go home
 *	- Unlimited number (within reason) of drills and pistons
 *	- Setting up the sorters for stone ejection
 *
 * Requirements (mandatory):
 * 	- A drill group: 			elementsName + "Drills"
 * 	- A piston group: 			elementsName + "Pistons arm"
 * 	- A rotor: 					elementsName + "Rotor arm"
 * 	- A piston group: 			elementsName + "Pistons lock"
 * 	- A landing gear group: 	elementsName + "Lg lock"
 * 	- A cargo group: 			elementsName + "Cargos"
 * 	- A sorter group: 			elementsName + "Sorters"
 *
 * Optionals:
 * 	- A display: 				elementsName + "Display"
 *
 * elementsName is by default "[Excavator] "
 * For example, the drill group would be named "[Excavator] Drills"
 */


// Config
float baseVelocity 			= 0.4f;				// the speed of the drilling pistons
string elementsName 		= "[Excavator] ";	// start of the name of the game elements (pistons, drills...)
float rotorArmDrillingDeg 	= 0.0f;				// rotor degrees for the arm in its drilling position
float rotorArmRestingDeg 	= -83.0f;			// rotor degrees for the arm in its resting position
float rotorArmVelocity 		= 0.5f;				// velocity of the rotor to deploy the arm in drilling position
int nIterCargoCheck 		= 100;				// Number of script ticks the cargo is being check
												// before considering no more ores are collected

// NO TOUCHY FROM HERE

// Global variables
float pistonsVelocity;
string phase;
float maxDistance = 0.0f;
float maxCargo = 0.0f;
float previousCargo;
int currentIter;
bool hasStartedCollecting;

// Games elements
// Arm
List<IMyTerminalBlock> pistonsArm = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> drills = new List<IMyTerminalBlock>();
IMyMotorStator rotorArm;
// Lock
List<IMyTerminalBlock> pistonsLock = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> lgLock = new List<IMyTerminalBlock>();
// Others
List<IMyTerminalBlock> cargos = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> sorters = new List<IMyTerminalBlock>();
IMyTextPanel display;


/* ========================================================================== */
/*                                CONSTRUCTOR                                 */
/* ========================================================================== */
public Program() 
{
	// Arm
	// pistons
   IMyBlockGroup pistonsGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Pistons arm");
   pistonsGroup.GetBlocks(pistonsArm);
   foreach ( IMyPistonBase piston in pistonsArm ) { maxDistance += (float) piston.MaxLimit; }
   pistonsVelocity = baseVelocity / pistonsArm.Count;
   // drills
   IMyBlockGroup drillsGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Drills");
   drillsGroup.GetBlocks(drills);
	// Rotor
	rotorArm = GridTerminalSystem.GetBlockWithName(elementsName + "Rotor arm") as IMyMotorStator;

	// lock
	// pistons
   IMyBlockGroup pistonsLkGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Pistons lock");
   pistonsLkGroup.GetBlocks(pistonsLock);
	foreach ( IMyPistonBase piston in pistonsLock ){
		piston.MaxLimit = 0.0f;
		piston.Velocity = -0.5f;	
	}
	// LGs
   IMyBlockGroup lgLockGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Lg lock");
   lgLockGroup.GetBlocks(lgLock);

	// Others
   // cargos
   IMyBlockGroup cargosGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Cargos");
   cargosGroup.GetBlocks(cargos);
   foreach ( IMyCargoContainer cargo in cargos ) { maxCargo += (float) cargo.GetInventory(0).MaxVolume * 1000f; }
   // sorters
   IMyBlockGroup sortersGroup = GridTerminalSystem.GetBlockGroupWithName(elementsName + "Sorters");
   sortersGroup.GetBlocks(sorters);
   // Set the sorters
   foreach ( IMyConveyorSorter sorter in sorters ) {
       List<MyInventoryItemFilter> filtered = new List <MyInventoryItemFilter>();
       filtered.Add(MyDefinitionId.Parse("MyObjectBuilder_Ore/Stone"));
       sorter.SetFilter(MyConveyorSorterMode.Whitelist, filtered);
       sorter.DrainAll = true;
	}  
   // LCD
   display = GridTerminalSystem.GetBlockWithName(elementsName + "Display") as IMyTextPanel;
  
   string msg = "Initialized";
   msg += "\n" + drills.Count.ToString() + " drills";
   msg += "\n" + pistonsArm.Count.ToString() + " pistons drilling arm";
	msg += "\n" + "Rotor arm: " + (rotorArm != null);
	msg += "\n" + pistonsLock.Count.ToString() + " pistons lock";
	msg += "\n" + lgLock.Count.ToString() + " landing gears lock";
   msg += "\n" + cargos.Count.ToString() + " cargos";
   msg += "\n" + sorters.Count.ToString() + " sorters";
   msg += "\n" + "Display: " + (display != null);
   Echo (msg);
   if ( display != null ) { display.WriteText(msg); }
   status("Ready"); 
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
	msg += "\n" + "Distance:   " + current_distance().ToString("0.00") + "m";
	msg += "\n" + "Travel:        " + ((current_distance()/maxDistance)*100).ToString("0.00") + "%";
	if (  display != null ) { display.WriteText(msg); }
	Echo(msg);
}


public float current_distance() {
   float cDist = 0.0f;
   foreach ( IMyPistonBase piston in pistonsArm ) { cDist += piston.CurrentPosition; }
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


public bool pistons_at_max(){
	float currentDistance = 0.0f;
	foreach ( IMyPistonBase piston in pistonsArm ) { currentDistance += (float) piston.CurrentPosition; }
	
	return ( currentDistance == maxDistance );
}

/* ========================================================================== */
/*                                   PHASES                                   */
/* ========================================================================== */
// All phases return a boolean for isFinished (phase is done when at true)

public bool initialization(){
	// Deploy the lock, then the arm

	// initialize cargo variable
	previousCargo = current_cargo_ns();
	currentIter = 0;
	hasStartedCollecting = false;
	
	// Deploy the lock
	if ( !park() ){ return false; }
	
	// Deploy the arm
	float currentAngle = (float) Math.Round(rotorArm.Angle / (float) Math.PI * 180f);
	if ( currentAngle != rotorArmDrillingDeg ){
		rotorArm.TargetVelocityRPM = rotorArmVelocity;
		status("Initialization - deploying the arm");
		return false;
	}

	status("Initialization - finalizing");
	// Start the drills
	foreach (IMyShipDrill drill in drills){drill.Enabled=true;}

	// Start the pistons
	foreach (IMyPistonBase piston in pistonsArm){piston.Velocity = pistonsVelocity;}
	// Launch drilling phase
	phase = "drilling";
	return true;
}


public bool standby() {

	if ( cargo_has_stones() ){

		foreach (IMyPistonBase piston in pistonsArm){ piston.Enabled = false; }
		foreach (IMyShipDrill drill in drills ){ drill.Enabled = false; }
		status("Standby - waiting to empty stones");
		return false;

	} else {

		foreach (IMyPistonBase piston in pistonsArm){ piston.Enabled = true; }
		foreach (IMyShipDrill drill in drills ){ drill.Enabled = true; }
		phase = "drilling";
		return true;

	}

}


public bool drilling(){
	
	// Check if ores collection has started
	if ( !hasStartedCollecting && (current_cargo_ns() != previousCargo) ){
		hasStartedCollecting = true;
	} else if ( hasStartedCollecting && (current_cargo_ns() == previousCargo) ){
		if (currentIter == nIterCargoCheck ) {
			phase = "reset";
			return true;
		} else {
			currentIter += 1;
		}
	}
	// Check cargo status and piston status
	if ( cargo_is_full() && !cargo_is_full_ns() ){
		phase = "standby";
		return false;
	} else if ( (cargo_is_full() && cargo_is_full_ns()) || pistons_at_max() ){
		phase = "reset";
		return true;
	} else {
		previousCargo = current_cargo_ns();
		string title = "Drilling - " + ( hasStartedCollecting ? "Collecting ores": "Not collecting ores");
		status(title);
		return false;
	}
}

public bool reset(){

	// pistons arm	
	bool isPistonRetracted = true;
	foreach (IMyPistonBase piston in pistonsArm){
		if ( piston.CurrentPosition != 0.0f )  {
			isPistonRetracted = false;
			break;
		}
	}
	
	if ( !isPistonRetracted ){
		foreach ( IMyPistonBase piston in pistonsArm ){
			piston.Velocity = - pistonsVelocity;
		}
		status("Resetting - retracting pistons");
		return false;
	} else {
		foreach (IMyShipDrill drill in drills){
			drill.Enabled = false;
		}
	}

	// rotor arm
	float currentAngle = (float) Math.Round(rotorArm.Angle / (float) Math.PI * 180f);
	if ( currentAngle != rotorArmRestingDeg ){
		rotorArm.TargetVelocityRPM = - rotorArmVelocity;
		status("Resetting - retracting the arm");
		return false;
	}

	// Unparking
	if ( !unpark() ){ return false; } 
	
	status("Ready");
	Runtime.UpdateFrequency = UpdateFrequency.None;
	return true;
}

public bool park(){
	bool canLock = false;
	bool isLocked = false;
	foreach (IMyLandingGear lg in lgLock ){
		if (lg.LockMode == LandingGearMode.ReadyToLock) {
			canLock = true;
			break;
		}
		if (lg.LockMode == LandingGearMode.Locked) {
			isLocked = true;
			break;
		}
	}
	// Lock the lock
	if ( canLock && !isLocked ){
		foreach (IMyLandingGear lg in lgLock ){
			lg.Lock();
		}
		
		status("Parking");
		return false;
	}
	// Deploy the lock piston
	if ( !canLock && !isLocked ){
		foreach (IMyPistonBase piston in pistonsLock ){
			piston.MaxLimit += 0.02f;
			piston.Velocity = 0.2f;
		}
		status("Parking");
		return false;
	}
	return true;
}

public bool unpark() {
	bool isLocked = false;
	foreach (IMyLandingGear lg in lgLock ){
		if (lg.LockMode == LandingGearMode.Locked) {
			isLocked = true;
			break;
		}
	}
	// Unlock the gear
	if ( isLocked ){
		foreach (IMyLandingGear lg in lgLock ){
			lg.Unlock();
		}
		
		status("Unparking");
		return false;
	}
	// Retract the lock piston
	if ( !isLocked ){
		foreach (IMyPistonBase piston in pistonsLock ){
			if (piston.CurrentPosition != piston.MinLimit ){
				piston.MaxLimit = 0.0f;
				piston.Velocity = -0.5f;
				status("Unparking");
				return false;
			}	
		}
	}
	return true;
}
/* ========================================================================== */
/*                                    MAIN                                    */
/* ========================================================================== */
public void Main(string argument, UpdateType updateSource) {
	// Auto execution
   if ( (updateSource & UpdateType.Update10) != 0 ) {
	   switch ( phase ){
		case "init":
			initialization();
			break;
		case "standby":
			standby();
			break;
		case "drilling":
			drilling();
			break;
		case "reset":
			reset();
			break;
		case "park":
			if ( park() ){ 
				Runtime.UpdateFrequency = UpdateFrequency.None;
				status("Parked");
			}
			break;
		case "unpark":
			if ( unpark() ){
				Runtime.UpdateFrequency = UpdateFrequency.None;
				status("Unparked");
			}
			break;
		default: break;
	   }
   }
   // Manual launch
   if ( (updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0 ){
		switch (argument){
		   case "start":
				phase = "init";
				break;
		   case "stop": 
				phase = "reset";
				break;
			case "status":
				status("Status report");
				return;
			case "park":
				phase = "park";
				break;
			case "unpark":
				phase = "unpark";
				break;
		   default: 
				status("ERROR - Wrong argument [start|stop]");
				break;
	   }
		Runtime.UpdateFrequency = UpdateFrequency.Update10;
	} 
}