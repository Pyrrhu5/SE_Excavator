# SE_Excavator

**Version**: 1

This is an in-game script for Space Engineers

Manages the drilling sequences of a land vehicle equipped with a retractable arm of pistons and drills.

For large and small grids vehicles and/or arm without limit in the depth or circumference to drill
 
 **Usage** (arguments):

- start	: lock the vehicle, deploy the arm and start drilling
- stop	: retract the drill, retract the arm and unlock the vehicle
- status: print in the console and display the cargo status
- park	: deploy the parking landing gear, no drilling phase
- unpark: retract the parking landing gear
 
 **Handles**:

- Locking/unlocking a safety lock for the vehicle
- Deploying/retracting the drilling arm
- Stopping when the cargo are full of stones
- Restarting when the cargo get empties of stones
- Resetting to a drivable position when:
	+ the cargo is full of ores
 	+ the pistons reached their limit
 	+ the end of the ores' vein has been reached
 	+ the engineer got tired and wants to go home
- Unlimited number (within reason) of drills and pistons
- Setting up the sorters for stone ejection
 
 **Requirements** (mandatory):

- A drill group: 			elementsName + "Drills"
- A piston group: 			elementsName + "Pistons arm"
- A rotor: 				elementsName + "Rotor arm"
- A piston group: 			elementsName + "Pistons lock"
- A landing gear group: 	elementsName + "Lg lock"
- A cargo group: 			elementsName + "Cargos"
- A sorter group: 			elementsName + "Sorters"
 
 **Optionals**:

- A display: 				elementsName + "Display"
 
 elementsName is by default "[Excavator] "

 For example, the drill group would be named "[Excavator] Drills"

## License
Unmodified [MIT license](https://opensource.org/licenses/MIT)

See `License.md`

## Contributing

I welcome any suggestion, corrections or improvements via push requests or email.

Please do report any bugs with:

- the error message,
- the log (see the programmable block),
- the steps to reproduce it

