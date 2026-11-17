# In-game verification checklist

Use a disposable city (do not save over a real one). Game 1.6.x. Record result
(works / broken / not tested), what you saw, and relevant log lines (redact paths).
Results go in docs/qa.md; do not mark anything verified that was not run.

## 1. Discord RPC 0.9.3-beta.3 (Discord desktop running)
- [ ] Panel opens; connection state shows Connected
- [ ] Click each of the 3 privacy presets; the activity preview changes as labelled
- [ ] Turn enhanced presence off: vanilla presence returns in Discord
- [ ] Turn it back on; quit/restart Discord and confirm reconnect
- [ ] Toolbar and panel icon (official Discord symbol) render correctly
- [ ] DiscordRPC log: no failed callbacks or repeated errors

## 2. SaveGuard 1.2.4-beta.1
- [ ] Create a restore point; it appears in the list
- [ ] Load the restored copy; the city loads, original save untouched
- [ ] Note any error in the SaveGuard log

## 3. RoadRules 1.4.5-beta.1
- [ ] Apply a lane access rule on a road carrying traffic
- [ ] Do vehicles that already had a path keep using the lane afterwards? (known issue)
- [ ] Class/overlay matrix: test at least one rule per vehicle class
- [ ] Rule is saved and restored after save/reload

## 4. Junction Studio 1.1.4-beta.1 (enable Town Road Lane first)
- [ ] Mod loads without the missing-dependency error
- [ ] Apply a junction preset; redraw and undo behave
- [ ] Copy/paste of a junction preset

## 5. FastTrack 1.3.5-beta.2
- [ ] Adaptive LOD OFF by default
- [ ] Enable it: zoom out and in; detail drops only at aerial views and returns up close
- [ ] Disable/pause restores full detail; no visual glitches
- [ ] Compare FPS with and without it in the same scene

## Newer source also unreleased (smoke test if time): Traffic Stress Lab 1.5.7-beta.1
- [ ] Multiplier changes request volume; reverting restores normal behaviour
