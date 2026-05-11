******************************************
*              Voxel Play                *
* Copyright (C) Kronnect Technologies SL * 
*             README FILE                *
******************************************


What's Voxel Play?
--------------------

Voxel Play is a voxelized environment for your game. It aims to provide a complete solution for terrain, sky, water, UI, inventory and character interaction.


How to use this asset
---------------------
Firstly, you should run the Demo scenes to get an idea of the overall functionality.
Then, please take a look at the online documentation to learn how to use all the features that Voxel Play can offer.

Documentation/API reference
---------------------------
The user manual is available online:
https://kronnect.com/guides

You can find internal development notes in the Documentation folder.


Support
-------
Please read the documentation and browse/play with the demo scene and sample source code included before contacting us for support :-)

Have any question or issue?
* Support-Web: https://kronnect.com/support
* Support-Discord: https://discord.gg/EH2GMaM
* Email: contact@kronnect.com
* Twitter: @Kronnect



Future updates
--------------

All our assets follow an incremental development process by which a few beta releases are published on our support forum (kronnect.com).
We encourage you to signup and engage our forum. The forum is the primary support and feature discussions medium.

Of course, all updates of Voxel Play will be eventually available on the Asset Store.


Version history
---------------

Version 40.0.3
- Items with VoxelDefinition.dropItem assigned now spawn the item's own prefab when the voxel is destroyed, instead of a generic recoverable voxel
- Added configurable pickup radius: global setting in Voxel Play Environment ("Default Pickup Radius") with per-item override via ItemDefinition.pickupRadius (0 = use global)
- Added "Destroy With Voxel Below" option for CutoutCross voxels (default true). Disable to keep cross voxels in place when the voxel below is destroyed (cobwebs, barbed wire, decals, etc.)
- [Fix] Origin shift: pivot is now quantized to integer multiples of the threshold, keeping connected textures aligned after a shift
- [Fix] World-space UV shaders compensate the accumulated origin shift so textures stay anchored after the world is moved

Version 40.0.2
- [Fix] SampleHeightMapFractal step now honors its noiseTexture again
- [Fix] Use FindObjectsByType overload without sort mode on Unity 6.4+

Version 40.0.1
- [Fix] Player look angles are now normalized on save/load, preventing WASD drift and slow camera tilt after saving the world in the editor

Version 40.0
- Added Terrain Graph Editor: visual node-based interface for designing terrain generation
- Internal improvements and fixes

Version 35.0
- Added configurable vertical chunk distance (default 8, up to 40) for elevated camera setups
- Added biome mixing option with configurable spread for terrain transitions at biome borders (near and far chunks)
- Added Voxel Material Profile: ScriptableObject for sharing visual properties across voxel definitions
- Added Tint Gradient: noise-based color variation using a gradient per voxel type (modes: Random, Vertical, Horizontal)
- Added Editor Distance Multiplier (default x3, range 1-5) to extend visible chunk distances when editing in Scene View. Helps with elevated camera setups in the World Editor.
- Unity Terrain Generator improvements
- Unity Terrain Generator: Refresh and Generate now force a terrain patch below the camera when it's too far above the surface
- World Definition: added Center property for non-infinite worlds. Refresh button in Unity Terrain Generator now auto-sets center and extents from terrain bounds.
- Inspector improvements
- API: ModelDefinition.Create overloads now accept an optional List<MicroVoxels> parameter to include microvoxel data
- API: added ChunkRedraw(Boundsd bounds, ...) overload. Creates and renders all chunks within a bounds region, even if they are outside the visible distance. Useful for forcing terrain generation when the camera is far from the terrain surface.
- [Fix] Start On Flat now correctly spawns within terrain bounds for non-infinite worlds with offset terrains
- [Fix] Clouds now remain visible when "Adjust Camera Far Clip" is enabled with a close far clip distance

Version 34.1.1
- [Fix] World Editor Box Selection: microvoxels now rotate correctly when rotating pasted clipboard contents
- [Fix] Save game: header file now accumulates region IDs across sessions preventing loss of previously saved regions
- [Fix] Chunk unload: modified chunks are no longer released in Destroy mode preventing data loss of unsaved changes

Version 34.1
- World Editor Elevate/Level Terrain: added Surface Voxel and Fill Voxel overrides to bypass biome rules (with draggable recent voxels grid)
- World Editor Paint Tool: added Paint Depth slider to paint N voxels below the surface (stops at empty, non-terrain, or bedrock)
- World Editor Box Selection: added Replace mode (G key) to replace one voxel type with another within a selection
- Load optimizations

Version 34.0
- Added support for Unity 6.4

Version 33.4
- Added MicroVoxelsDefinition ScriptableObject for storing and reusing microvoxels shapes
- Voxel Definition Inspector: added Save/Load buttons to export/import microvoxels from MicroVoxelsDefinition assets
- World Editor: new tool to capture microvoxels from existing voxels in the scene and create MicroVoxelsDefinition assets
- MicroVoxelsDefinition can be created from Project context menu: Create > Voxel Play > MicroVoxels Definition

Version 33.3
- GPU memory optimizations and fixes
- [Fix] Removed shader warnings in Unity 6.3
- [Fix] New input system fixes

Version 33.2
- World definition: added setting to enable/disable Sun light decrease as it crosses underground chunks (bool lightSunAttenUnderground)
- [Fix] Fixes to placement animation and ghost collider when voxels are placed quickly

Version 33.1
- World Editor: added "Show Modified Chunks" section under File tab
- World Editor Remove Tool: hold Alt to remove different voxel types except the selection
- World Editor Flat Floor Tool: added option to remove anything above the floor when placed
- [Fix] World Editor fixes
- [Fix] NavMesh data is now generated for microvoxels (if type navigatable property is enabled)

Version 33.0
- CHANGE: API: OnVoxelBeforeDestroyed event is now of type VoxelCancelableEvent
- CHANGE: API: SetChunkRawData / GetChunkRawData now include any voxel property or microvoxels by default (if they exists)
- [Fix] Fixed Voxel Play shaders not rendering in deferred (they still render in forward stage though)

Version 32.2
- Voxel definition inspector: improved preview of microvoxels
- Reduced draw calls derived from torches
- Minor improvements to damaged particle/voxel shaders
- Vegetation generated by terrain is now visible on top of half voxels
- [Fix] Breaking a voxel on top layer of chunk which has no chunk on top didn't propagate Sun light correctly
- [Fix] Fixed false occlusion of transparent microvoxels

Version 32.1
- Placement animation: added "Use Ghost Collider" option
- Placenemt animation: added "Fade" option
- Placement animation: added support for transparent voxels
- [Fix] Voxel placement animation fixes

Version 31.1
- Biome definition: added optional water definition
- Internal memory allocation improvements
- API: added CreateMicroVoxels utility method that accepts a bottomY and topY
- API: added GetVoxelIndicesOnPath(start, end)
- API: added VoxelPlace overload that accepts a list of VoxelIndex entries

Version 31.0
- New global microvoxels architecture aimed to avoid duplication and reduce memory usage
- New save file format v22
- Improved performance of terrain generation using half voxels
- World definition: added minParticleSize/maxParticleSize under FX section

Version 30.2
- World Editor: added Biome Painter tool (under Terrain tools)
- API: added OnCollectVoxelDefinitions event which can be used to add runtime created voxel definitions during VP startup
- Added bevel width parameter when bevel feature is enabled
- Terrain Generators: added "Enabled" property. Disable it to just load/render any saved chunk.
- [Fix] Fixed bevel and relief mapping coexistence bug
- [Fix] Connected textures fixes
- [Fix] Fixed regression with SRB batcher
- [Fix] Minor internal fixes

Version 30.1
- Ability to toggle slab mode in playmode (default hotkey: H)
- When in slab mode, highlight will be restricted to the top or bottom half of a voxel and placement/damage will be affected
- Change: "biome smart surface" option is now a global property, not per biome. Disable it in Rendering Options section in Voxel Play Environment.
- API: added IsTopHalfAtPosition, IsBottomHalfAtPosition, IsHalfVoxelAtPosition utility methods
- [Fix] Dual slab rendering fixes

Version 30.0
- Terrain Default Generator: added option to generate half step voxels using microvoxels
- Additional face occlusion optimizations for microvoxels
- MicroVoxels now allow a secondary voxel type for material rendering. New MicroVoxels Layout APIs.
- Two slabs of different voxel types can be placed in the same voxel (internally it changes microvoxels layout to Slab)
- New load/file format (v21) with dynamic load system (regions are loaded on the fly instead of full load from start)
- Data of VoxelPlaySaveThis components are now tied to regions, not globally loaded (they also load dynamically with regions)
- Foam Color can now be customized for non-realistic water including transparency
- Improved resources loading
- [Fix] Fixed ModelWorldCapture properties bug

Version 21.1
- API: VoxelPreview now supports vegetation
- API: VoxelPreview now takes into account Connected Voxels rules
- [Change] VoxelPlayPlayer is no longer autocreated if no character controller exists in the scene
- [Change] API: GetVoxelIndices will now ensure chunks are created if mustHaveContent = true
- [Fix] Water spread fixes
- [Fix] API: fixed VoxelDamage overload bug

Version 21.0
- Added reflection probes support option (under Rendering Options in Voxel Play Environment). Note: environment reflections requiere PBR mode or shaders that support reflection probes.
- [Fix] Water spread fixes

Version 20.3
- API: added VoxelPreview method (currently supports regular - non vegetation - and custom voxel types)
- [Fix] Fixed a highlight rendering issue when origin shift is enabled

Version 20.2
- Vegetation placed by the default terrain generator now supports light intensity
- Extended use of "virtual" in common methods and "isFallBack" in custom editors to make classes more customizable
- Reduced memory footprint when loading a savegame file
- [Fix] ModelWorldCapture was not storing voxel properties
- [Fix] Fixed inactive revoverable voxels

Version 20.1
- Render type Opaque No-AO now supports PBR maps
- PBR shader internal improvements
- Dynamic voxels are now automatically consolidated when saving a game to file
- [Fix] Fixed regression with exporting chunks from the World Editor
- [Fix] Fixed compatibility between fluid shaders and Radiant Global Illumination
- [Fix] Default terrain generator was not applying voxel definition's tint color
- [Fix] Fixed some shader features not being refreshed in playmode when toggled in the inspector

Version 20.0
- Added full PBR support including metallic/smoothness/occlusion maps to voxel definitions (for opaque/opaque-6tex/cutout and transp-6tex render types)
- Added +20 new PBR voxel definitions to HQ_Forest demo scene
- Emission Mask is no longer a mask, but a color map
- Water voxel definitions now accept emission
- New render type: Fluid. Similar to water but specific for lava, acids, etc.
- API CHANGE in Rayhit/RayCast methods. The ignoreWater optional parameter is now an enum which accepts 3 options: "Defined By Water Voxel Definition", "Include Water" or "Ignore Water"
- Added "Ignore Water" option to Voxel Play First/Third Person Controllers inspector (allows you to choose if water voxels can be highlighted or not)
- CHANGE: removed global specular and fresnel options (since there's now proper metallic/smoothness options)
- API: added voxel definition optional parameter to GetVoxelIndices methods to filter by a specific voxel definition
- Internal code base fixes

Version 18.0
- Support for Unity environment lighting (Lighting tab). Ambient Lighting parameter in VPE is now a multiplier.
- Added diffuse wrap parameter to Voxel Play Environment

Version 17.4
- World Editor: added new tools to add rooms and towers
- World Editor Selection Tool: start and end positions can be edited directly in the inspector
- World Editor Model Capture Tool: option to include voxel tint colors
- [Fix] CutOut shader fixes related to SSAO
- [Fix] Fixed a voxel occlusion issue when using connected voxel rule and microvoxels

Version 17.3
- World Editor Tools: new tool under Sculpt section for selection/cut/paste/fill/delete operations
- Model definition: added "treeRandomRotation" property (defaults to true)
- Terrain Default Generator: now uses chunk.SetVoxel method for surface voxels to support microvoxels instead of the simpler and faster voxel.Set
- [Fix] ModelWorldCapture now captures torches and store them in the model definition
- [Fix] Voxels placed with ModelPlace were not setting the emissive lighting
- [Fix] Fixed multi-file save game issue loading from resources
- [Fix] Prevents errores and emits an informative message when using the new input system and using the default input controller

Version 17.2
- API: added VoxelHighlight(Vector3d position...) new overload
- Improvements to connected voxel rule resolution
- Smart biome surface: added option to biome so when a voxel dirt becomes part of the surface, it will be rendered as a voxel top
- [Fix] Far chunks renderer is now compatible with origin shift
- [Fix] Fixed water voxel height when placed using VoxelPlace API
- [Fix] Fixed GetHeightMapInfoFast regression which caused some chunks use incorrect contents

Version 17.1
- Added "Restore Player Position" option under Load Save Game (defaults to true)
- API: CHANGE: OnVoxelBeforeDropItem(VoxelHitInfo hitInfo, bool create) signature changed, chunk parameer has been removed as it was already included in the hitInfo struct.
- API: added OnVoxelAfterDropItem(hitInfo, gameObject): triggered just after a voxel is destroyed and recoverable voxel has been created.
- Fixes and minor world editor improvements

Version 17.0
- Connected voxels: added "Additional voxel definitions" slot
- Water now supports texture variations
- Added "Water Animation Speed" which controls wave speed of non-realistic water
- Vegetation voxel definitions: added "Random Offset" toggle
- Vegetation voxel definitions: added "Height Variation" property
- Far Chunks Renderer is now compatible with Curvature option
- VoxelDefinitionEditor class supports inherited editors
- Model Definitions can now store custom properties for the model bits (using ModelBit.SetProperty)
- Door state (open/closed) is now stored as a voxel property and saved between sessions
- API: added VoxelQueryProperty(propertyName, List<VoxelPropertyMatch>...)
- API: added MicroVoxelPlaceSlab method
- API: added CreateMicroVoxels(bool[,,] array, MicroVoxels mv): creates/updates MicroVoxels structure which can be passed to VoxelPlace
- API: VoxelPlace methods now receive an optional MicroVoxels parameter

Version 16.4
- Connected voxels: added option to ignore vegetation
- Minor UI improvements to biome definition editor
- API: added GetTimeOfDay() - returns a value in the 0-24h range
- [Fix] GPU instancing rendering now supports connected voxels
- [Fix] In some rare cases, connected voxels use a wrong rotation

Version 16.2.2
- [Fix] Microvoxels lighting fixes when rotated
- [Fix] Assorted fixes related to character controllers
- [Fix] World Editor fixes

Version 16.2.1
- Removed "Render Low Priority" option as it affected world editor experience
- World Editor: add safety check when trying to stamp a model using voxel definitions that are not accesible in the world sub-folders

Version 16.2
- Voxel collapsing: added options to destroy voxels automatically when they collapse
- Added shadow support to dynamic voxels in URP
- [Fix] Far chunks renderer now takes into account voxel definition tint color

Version 16.1
- World Editor: new sculpt tool for creating slabs
- Voxel Play Environment: "Create" world definition button now generates a sample world definition plus flat terrain generator and basic biome
- API: added ClearHighlight method. Removes any highlight (outline) from voxels.
- API: added IsVegetationAatPosition method

Version 16.0
- World Editor: new shapes section: floor, wall tools
- World Editor: new recent voxel definitions panel
- Voxel definitions: added a warning if the material associated with the prefab does not have GPU Instancing enabled when using the GPU Instancing option
- Voxel definitions: tint color property for vegetation
- Multiple texture variations assets with different side rules are now supported for the same voxel definition
- API: added OnModelBuildEndWithIndices event (also received list of voxels placed)
- Removed caps on chunks, tree and vegetation generation per frame resulting in a significant performance improvement
- Maximum cap of Visible Chunk Distance increased to 32
- CHANGE: API: OnTreeBeforeCreate / OnTreeAfterCreate events now receive more data
- [Fix] Fixed farchunks lighting issue in URP at night
- [Fix] Water no longer spills when destroying nearby voxels if the Spreads option is disabled in voxel definition
- [Fix] World editor: fixed left vegetation when lowering terrain

Version 15.5
- World Editor: automatic backup is now off by default (can be enabled in the save section)
- World Editor: model placing tool improvements
- Non square textures are now scaled to fit the specified texture resolution

Version 15.4
- World Editor: saving the scene now automatically also saves the voxel world if world editor is enabled and active
- World Editor: selecting an unknown voxel definition will automatically add it to the world definition more voxels section
- World Editor: automatic backup option under File / Save section
- World Editor: minor UI changes
- [Fix] API: VoxelPlayConverter.GenerateGameObject() lighgting fix when passing textures/normals

Version 15.3
- World Editor: quality of life improvements
- Far Chunks Renderer: added "High Range" option to support heights greater than 255

Version 15.2
- Far chunks rendering: added "deep water" option and improved water specular rendering
- Microvoxels: fixes and improvements in world editor tools and rotation handling

Version 15.1
- Ability to add microvoxels to water positions
- World editor: added "Ignore Water" option to build tool
- SceneView editing: enbling gizmos is no longer required to use the sceneview tools

Version 15.0
- Revamped Voxel Play Environment inspector
- New world Editor tools that work in SceneView with undo support
- New model definition creation workflow through the world editor tools
- Microvoxels support with customizable size (2, 4, 8 or 16 subdivisions)
- New savegame format (v15) that includes microvoxels data and incremental region saving
- Biomes now support additional top and dirt voxels as well as lake bed voxels
- Ability to render far chunks using a raymarch method
- Far chunks water shader with shadow and reflections support
- Connected textures can now be used with texture variations at same time
- Added "waterSpreadsInBuildMode" option to World Definition. If enabled, water will spread in build mode.
- SRP batcher optimizations
- API: new event OnBeforeWorldLoad. Allows to change world settings before the world is initialized.
- URP mode is now automatically configured
- Middle mouse button now selects the voxel definition under the crosshair as well as the tint color
- [Fix] Connected textures: fixed an issue related to voxel definitions loading order

Version 14.4.1
- Connected voxel editor: configurations can now be collapsed/expanded
- [Fix] Fixed an issue with normal map encoding in linear color space
- [Fix] Fixed default character controller input conflict with inventory UI

Version 14.4
- Several internal minor optimizations
- Default camera fov in demo scenes and character controller is now 70 (from 60) which makes voxel look a bit smaller and improves immersion
- API: added ChunkHasCollider method
- [Fix] Fixed an issue which produced some invisible colliders when voxels collapse

Version 14.3
- API: added VoxelToCube()
- API: added OnChunkBeforeDetailGeneration event
- API: added UpdateVoxelDefinitionTextures(voxelDefinition)
- [Fix] Added depth texture compatibility to vegetation shaders

Version 14.2
- Added compatibility with screen space shadows in Unity 2022.3 URP (ie. Umbra Soft Shadows asset)
- API: added new GetVoxelIndices overloads to convert efficiently a list/array of Vector3d into a list/array of VoxelIndex structs
- Improved VR state detection

Version 14.1
- Added Global Specular option to Voxel Play Environment: improves lighting for regular opaque voxels
- Connected Voxels: expanded rules to support 26 neighbours
- Minor loop optimizations
- [Fix] Fixed speed of vegetation shadows animation
- [Fix] Fixed some tree leaves not receiving AO

Version 14.0
- Added support for URP Render Graph
- Prevents texture array packers to exceed system capacity when using texture variations or connected textures
- Ensures visible distance is always >= force chunk distance value

Version 13.8
- Multi-step Terrain Generator: added "Offset" parameter to texture sampler operators
- Voxel Play Environment inspector: added option to increase the number of different materials that can be used in a single chunk
- [Fix] Fixed import settings for tree textures of demo scene HQForest
- [Fix] Fixed regression bug that prevented rendering of normal/displacement maps on side faces

Version 13.7
- Texture alpha values are now ignored when loading a texture for opaque voxel definitions
- API: added ModelPlaceAlignment parameter to all ModelPlace methods which allows placing the model centered or not at the given position
- API: added additional GetVoxelColor / SetVoxelColor overloads
- [Fix] Fixed bug in GetVoxelNeighbourhood11 method which was not returning the correct voxel positions

Version 13.6
- Multi-Step Terrain Generator: added "Sample Heightmap From Unity Terrain" operator
- Unity Terrain Generator: Min Height field is now visible
- Terrain Generators: added "Add Water" option. Now you can ignore water in terrain generation using this checkbox.
- API: GetVoxelNeighbourhood now returns the world space position of the voxels
- API: Added SetVoxels(boxMin, boxMax, array) and SetVoxels(position, array)
- API: Added more ModelDefinition.Create overloads that take list of voxel definitions, voxels or colors
- API: ModelCreateGameObject now can use textures and normal maps
- [Fix] Fixed Randomization affecting app random state
- [Fix] Render queue of realistic water with no shadows shader has been changed to avoid sorting order issues with transparent voxels

Version 13.5
- Ability to use Voxel Play Environment without default UI or input controllers
- Added Demo Scene 6: creates a voxel gameobject from a model definition
- Added "NavMesh Resolution" option to Voxel Play Environment inspector
- API: Change: ModelCreateGameObject() is now a public static method
- [Fix] Some shaders won't render shadows correctly in builds in certain Unity version due to multi_compile changes in URP

Version 13.4
- Prefab Spawner: added "requireCollider" and "requireNavMesh" options
- Torch/smooth lighting is now compatible with custom voxels which use multiple mesh filters
- API: added OnWorldLoaded event
- Improved shadow rendering of cutout voxels
- [Fix] Fixes for server mode
- [Fix] API: fixes to VoxelOverlap method

Version 13.3
- Inspector can now select and edit multiple voxel definitions
- Unload Far Chunks: option to destroy far chunks but keep modified chunks
- [Fix] Fixed a rare chunk dictionary hash collision

Version 13.2
- Added "Voxel Padding" option under Voxel Environment inspector -> Voxel Generation section. Controls voxel mesh extra padding on/off which avoids gaps when using greedy meshing.
- Added custom post processing option to remove gap/white pixels between adjacen geometry when voxel padding is disabled
- Texture Variations: voxel rotation is now also used as seed for randomization

Version 13.1
- Added "Damage Particles" global option to Voxel Play Environment inspector (used to enable/disable particle effects globally)
- [Fix] Fixed particles not falling in Unity 2023 beta due to a Physics regression bug

Version 13.0
- Connected Voxels: option to apply rules during rendering (keeps voxel definition but renders a different voxel in its place).
- Texture Variations: workflow similar to Connected Textures to provide texture variations to any voxel definition. See: https://kronnect.com/docs/voxel-play/

Version 12.2.1
- Added support for Domain Reload options when Render In Editor mode is enabled

Version 12.2
- Fly mode implemented in third person controller (press F to enable Fly Mode, Q/E keys to move down/up)
- A warning is now shown in the inspector if Depth Priming Mode is enabled in URP
- API: added GetChunksBounds() - returns the bounds enclosing all generated chunks
- [Fix] Fixed a regression bug which resulted in textures not being saved when using the Export Chunks command

Version 12.1
- Added "Shadow Tint Color" option to World Definition (under Sky & Lighting section). Enable the feature in Voxel Play Environment ("Colored Shadows" checkbox)

Version 12.0.1
- [Fix] Vegetation now receives correct per-pixel lighting when URP native light option is enabled
- [Fix] Fixed an issue with initialization of virtual lights

Version 12.0
- Added Unity 2022/URP 14 Forward+ support, supporting +8 native lights: https://i.imgur.com/52Ovvygl.jpg
- GPU instancing now supports meshes with multiple submeshes and materials
- Added GPU instancing support to VPModelTextureAlpha & VPModelTextureAlphaDoubleSided shaders
- Added "Instancing Culling Mode" option to Voxel Play Environment. Aggresive is the default value: culls non visible voxels. Gentle allows some padding to keep shadows from invisible voxels. Disabled: renders all voxels, regardless of their positions vs camera.
- Added HDR support to color and emission color properties of VP materials
- Added "Fog Tint" option to Voxel Play Environment
- Added "VPModelTextureCuoutDoubleSided" shader
- Regrouped some properties in Voxel Play Environment inspector for better visibility
- [Fix] Fixed a realistic water shader glitch when viewed through tree leaves

Version 11.5.1
- API: added OnItemConsumed / OnItemsClear events to VoxelPlayPlayer API
- [Fix] Voxel Play Behaviour now applies lighting to objects that use multi-materials

Version 11.5
- Added 'Unload Far Chunks Mode' option (visibility or destroy)
- API: added "ignoreFrustum" parameter to ChunkRedraw methods. This ensures the chunk will be rendered regardless of frustum visibility (or visible distance).
- [Fix] Fixed chunks not being generated when setting distanceAnchor property to a non-camera gameobject

Version 11.4
- Added "Climb Max Step Height" parameter to first person character controller. Determines the maximum height of a step to allow climbing.
- Connected textures resolver now takes into account player orientation
- [Fix] Internal: texture packer now can differentiate textures that use same diffuse texture but different normal maps

Version 11.3
- Removed "hasContent" field from Voxel structure, replaced by boolean property. This saves 2 words of memory per voxel.
- API: added ChunkRedrawNeighbours method.

Version 11.2
- Vegetation voxels can now be collected when destroyed (see "Can Be Collected" option in the voxel definition)
- Added "Drop Probability" property to voxel definition
- Particles and damage cracks are now influenced by torch lights

Version 11.1
- Render In Editor: added new detail level "Standard with no detail generators"
- [Fix] Fixes for the Unity Terrain to Voxel terrain generator

Version 11.0
- Added virtual point lights. See: https://kronnect.com/docs/voxel-play/
- Tweaked lighting equation so normal map are visible under shadows
- API: added "IncludeVoxelProperties" option to GetChunkRawData/SetChunkRawData methods
- API: added "cancel" parameter to event OnModelBuildStart
- [Fix] Fixed potential issue when reloading textures by refreshing certain properties in the inspector
- [Fix] Qubicle import fixes

Version 10.9.3
- [Fix] Fixed Light Manager issue not detecting player camera displacement correctly in URP

Version 10.9.1
- Improved URP shader support with the inclusion of specific DepthOnly and DepthNormals passes
- [Fix] Fixed random generation issue that affected placement of vegetation on certain platforms
- [Fix] Fixed some custom voxel properties not being loaded correctly from a savegame

Version 10.9
- Minimum Unity version 2020.3.16
- Added "Delayed Initialization" option to Voxel Play Environment inspector. You can initialize the engine calling the Init() or InitAndLoadSaveGame() methods instead
- Biome Explorer: added position to tooltip
- Voxel Definitions: texture sample field is now exposed in the inspector for all voxel definitions
- [Fix] Fixed voxel signature calculation which resulted in some collision mesh issues

Version 10.8
- Added "AllowNestedExecutions" to detail generator class
- Voxel Definition: added "Greedy Meshing" option to override materials
- [Fix] Model fitToTerrain property was being ignored when placing models with the default character controllers

Version 10.7.3
- [Fix] Fixed issue in Unity Editor when no camera is present in the scene and VP is initialized

Version 10.7.2
- [Fix] Fixed issue with building steps voxels disappearing when stacking other voxels nearby
- [Fix] Fixed issue with rendering materials that use different relief/normal map settings
- [Fix] Fixed water with no shadows shader render queue which resulted in overdraw with other transparent objects

Version 10.7.1
- Improvements to the Unity terrain generator
- Added a warning when water level is higher than maximum terrain height
- Reduced usage of global keywords
- [Fix] Fixed savannah tree 1
- [Fix] Fixed issue with warning when connected texture is not valid
- [Fix] Fixed native URP issue in builds using Unity 2020.3 or later

Version 10.7
- Custom voxels: added new properties when GPU instancing is enabled: GenerateCollider & GenerateNavMesh. See: https://kronnect.com/docs/voxel-play/
- Custom voxels: added Occludes Forward/Back/Left/Right/Top/Bottom optimization options.
- Item.itemChunk & item.itemVoxelIndex are now generalized for all persistent items. Previously, only torches used those two fields of Item class

Version 10.6
- Connected textures: added slot for optional normal map
- API: added ModelWorldCapture(bounds). Captures a portion of the world into a Model Definition
- [Fix] Fixed potential memory leak with "Unload Far NavMesh" option
- [Fix] Fixed voxel highlight edges material leak when destroying a highlighted custom voxel

Version 10.5.3
- Added Bright Point Lights Max Distance option to Voxel Play Environment inspector
- Added more verbose messages during initialization
- [Fix] Fixed a missing foe prefab reference in demo scene 3

Version 10.5.2
- Added SendMessageOptions.DontRequireReceiver to SendMessage commands when loading/saving a scene to prevent console warnings
- Added "Can Climb" option to first person controller
- Added "Manage Voxel Rotation" to character controller
- [Fix] Save/load game fixes

Version 10.5.1
- API: added "fallbackVoxelDefinition" to load savegame methods (replaces a missing voxel definition from the savegame with an alternate voxel definition)
- Added an inspector error message if Enable URP Support is activated but Universal RP package is not present or configured
- Added support to origin shift to foes in demo scene flat terrain
- [Fix] Fixed character controller position not being applied correctly when loading a saved game
- [Fix] Fixed origin shift regression with first person character controller
- [Fix] Fixed dynamic voxel textures not reflecting all textures when rotating a 6-textured cube

Version 10.5
- Improvements to water placement/destruction in build mode
- Improvements to realistic water appearance on side faces
- [Fix] Fixed custom voxels visibility not being preserved when updating a chunk

Version 10.4
- API: added VoxelGetRotation methods
- Constructor: added tiny delay when returning to focus to prevent accidental clicks
- Constructor: improvements to "Save As New..." option
- [Fix] Constructor: voxel rotations are lost when using the Displace command
- [Fix] Constructor: voxels at z position=0 were not saved correctly
- [Fix] Fixed footfall sounds update failing when character is not grounded

Version 10.3.1
- API: added ChunkReset() method
- [Fix] Fixed water blocks rendering in black in URP when camera background is set to solid color

Version 10.3
- Custom voxels: added "Compute Lighting" option (experimental). This option bakes surrounding lighting and AO into the mesh vertex colors at runtime.
- Internal improvements related to multiple player instances
- DefaultCaveGenerator: added minLength / maxLength properties (length random range for tunnels)
- Improvements to terrain generator and caves
- Improved torch lighting falloff in linear color space
- [Fix] OnGameLoaded event not fired when calling LoadGameFromByteArray
- [Fix] Fixed transparent blocks rendering in black in URP when camera background is set to solid color
- [Fix] Fixed /teleport console command bug
- [Fix] Fixed an error when visible lights exceed 32
- [Fix] Fixed chunk rendering issue when pool is exhausted
- [Fix] Fixed texture bleeding for opaque side textures with solid colors

Version 10.2
- Added debug info when loading connected textures
- Added buoyance effect to particles when underwater (in practice, they fall slower underwater)
- Improved Connected Texture editor visuals
- Added helps section to Voxel Play Environment inspector
- Added menu links to online documentation, youtube tutorials and support forum
- API: improved transition between dynamic voxel to regular voxel using VoxelCancelDynamic method
- API: virtualized methods of character controllers for easier customization
- [Fix] Damage particles now use the textureSample field in voxel definition if present
- [Fix] Voxels were highlighted when highlighting is disabled when using the third person controller

Version 10.1
- Change: chunk.isAboveSurface now defaults to true
- Optimization of the voxel thumbnail generation. New "Drop Voxel Texture Resolution". See: https://kronnect.com/docs/voxel-play/
- UI: removed console message when crouching
- [Fix] First person character controller fixes
- [Fix] Fixes related to the water level transition
- [Fix] Fixed model colors imported with Qubicle when rendering in linear color space

Version 10.0 4/Aug/2021
- Support for URP native lights including point and spot lights with shadows
- Improved underwater effect (fog, caustics) and air to water transition
- Added /fps command to console to toggle fps display on/off
- [Fix] Fixed rogue white pixels on the edges of some voxels visible underground in very dark areas
- [Fix] Fixed an issue with collider rebuild which could led to player falling down
