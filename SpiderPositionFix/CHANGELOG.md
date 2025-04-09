## 1.5.1

- Debug logs toggles Transpiler logs

### Bug fixes
- **Fixed NRE spam when debugVisuals toggle was off**
- Fixed spider going through doors

## 1.5.0

###  Reworked spider movement tweaks
- **Much better movement on clients**
	- Both position and movement should be relatively same on both ends
- More reliable and fleshed out movement
- Retains tilting and improved rotation

### Wall climbing improvements	
- Added transpiler for cases where raycast fails to get valid wall position multiple times

### Internal
- Added more visual debug tools for spider movement
- Added a method for instantiating debug balls
- Added and improved logs

### Known issues
- Spider sometimes set wall position on mid air objects
	- Spider will climb air as a result

- Updated CHANGELOG and README and manifest

## 1.4.0
- Improved movement and turning
- Logs spam less
- Avoidance priority decreases to lowest possible when spider is on wall.
 
#### Improved wall climbing system
- Added validation and recasting for floor position
	- Fixes spider failing to climb wall

#### Known issues
- ~~Spider does not move the same pace between the client and server~~
	- ~~Likely a vanilla LAN bug.~~
	- Confirmed to be caused by LAN testing

## 1.3.1
- Slightly changed how the fix is applied
- Improvements to overall movement

## 1.3.0
- Massively de-overengineered code
	- Instead of slowing down agent and interpolating position, mesh container is set to agent's position

## 1.2.1
- LogInfo made toggleable by debug toggle and set delay to reduce spam
- Added warning when spider is failing to climb wall

## 1.2.0
- Fixed spider never reaching the floor position
- Fixed spider not climbing wall despite being able to do so
- Agent gets disabled once spider reaches wall position
- Optimizations (Mostly swapping some variables and preventing execution on non-host clients)

## 1.1.2
- Fixed debug logger missing condition

## 1.1.1
- Rewamped how spider moves to wallposition. Spider agent now pathfinds below the wall position before meshcontainer and agent separates
- Changed formula of how much agent speed is reduced
- Spider now tilts when moving on slopes
- Tweaked thresholds

## 1.1.0
- Fixed idle animation having an offset. Spider is symmetrical.

## 1.0.8
- Fixed agent moving during stare.
- Tweaked thresholds
- Spider movement is less jittery when moving on slopes.

## 1.0.7
- Agent Jump areaMask is now toggled depending on whenever spider is inside or outside
	- Credit to AudioKnight for reporting the issue
- Spider now marks a position where it started climbing wall. Agent now uses this to warp to nearest navmesh instead of the spider
	- This fixes an issue agent being teleported to nearest NavMesh regardless if it was behind a wall or not
- Added Config:
	- Toggle to not modify agent areaMask
	- Toggle for debug logs

### 1.0.6
- Agent gets slowed down when a distance threshold between agent and container is crossed
- Rotation no longer snaps when crossing links
- Spider is slightly slower on links

## 1.0.5
- rotation patch is not applied when overrideSpiderLookrotation is true

## 1.0.4
- Instead of interpotating meshContainer position and rotation, the mod now interpolates Target position and rotation of the meshContainer. This results in much more natural movement.
- Spider looks at the end point of the offMeshLinks when traveling through them for the time being.

## 1.0.3
- Reverted back to Lerp interpolation
- Agent now teleports to the nearest navMesh point when the spider crawls off walls.
- Slightly loosened the tollerance
- removed static from variables. Spiders no longer affect eachother.
- Fixed a bug where the patch stopped applying fix after reaching wall possition

## 1.0.2
- Rewrote the code and tightened tolerance further to make movement look better on stairs and staircases <br>

## 1.0.1
- Made tolerance tighter, changed lerp to slerp<br>
 
## 1.0.0
- Initial release <br>
