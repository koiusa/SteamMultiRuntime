# Targeting Showcase

This prebuilt sample combines the current `TargetingController` with the Steam Multi Runtime
production input adapter, Cinemachine cameras, polished UI Toolkit indicators, and the generic
`TargetMarkerRandomSpawner`, which creates ten targets at random positions when Play Mode starts.
The spawned prefab and its default material are reusable Runtime assets from the base
`com.koiusa.targetingsystem` package; the Showcase does not keep duplicate copies.

Use `Tools > SteamMultiRuntime > Maintenance > Targeting > Rebuild Showcase Sample` to regenerate the serialized
scene and its target prefab after changing the sample composition.

Production controls:

- Move: WASD / left stick
- Camera: mouse / right stick
- Jump: Space / gamepad south button
- Lock On: toggle Single targeting
- Multi Lock: `3` / right-stick press selects up to eight visible targets; press again to clear
- Single to Multi: while Single Lock is active, hold L2 for Strafe and temporary Multi Lock; release L2 to return to the same primary Single target
- Previous / Next: change the primary target

Each spawned target uses `TargetMarkerRandomMover` to travel smoothly between random destinations
around its spawn position.
