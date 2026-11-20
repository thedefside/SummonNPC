# SummonNPC

SummonNPC is a BepInEx plugin that changes the Dead Raiser staff's summoned `Skeleton_Friendly` into a player-styled summon. It clones the vanilla summon prefab, applies the player body model and configured appearance, and points the staff's existing `SpawnAbility` at the clone. The original summon AI, tame/follow behavior, health, damage, ownership, and spawn limit are retained.

## Version 0.4.0

- Added a configurable Male or Female player body model for the summon.
- Moved helmet, chest, legs, and cape settings into the `Equipment` config section.

The player body mesh is rebound to the cloned skeleton's animation bones by their relative paths, without changing the summon to a player `VisEquipment` model. The player's renderer bounds are copied as well, avoiding the skeleton's smaller bounds culling the larger player mesh. The plugin creates a helmet attachment on the cloned rig using the player's mount offset, keeping that alignment separate from the animated body bones. Equipment and the per-summon display name are configurable; these appearance changes do not alter summon combat stats. If the vanilla player and skeleton rigs no longer match, the plugin logs a warning and leaves the staff unchanged.

## Installation

Install the package with a Valheim mod manager, or copy `SummonNPC.dll` into `BepInEx\plugins\SummonNPC`. The Thunderstore package depends on BepInExPack for Valheim. Every multiplayer client must have the mod installed to resolve the custom summon prefab.

## Configuration

SummonNPC writes its BepInEx configuration to `BepInEx\config\com.summonnpc.valheim.cfg`.

- `Enable Mod`: defaults to `true`. Set to `false` and restart to leave the game's summon behavior unchanged.
- `NPC Names`: comma-separated names; one is randomly selected for each summon. Defaults to `NPC`.
- `Body Type`: `Male` (default) or `Female`, selecting the player body model used by the summon. Restart the game after changing this setting. Beard configuration remains available with either body type.
- `Hair` and `Beard`: drop-downs populated from the game's item database. `HairNone` and `BeardNone` remove those visuals and are the defaults; there is no separate `None` choice for these lists.
- `Helmet Prefab`, `Chest Prefab`, `Legs Prefab`, and `Cape Prefab` are in the `Equipment` section. Their drop-downs are populated from actual items in the matching wearable slot and each includes `None`. Defaults are `HelmetBronze`, `ArmorBronzeChest`, `ArmorBronzeLegs`, and `CapeTrollHide`.
- Prefab options beginning with `FW`, `SP`, `Goblin`, `Charred`, `Dverger`, `Jotun`, or `StoneGolem` are excluded.
- Hair and beard visibility follows the configured helmet's built-in hair settings.
- `Hair Color`: defaults to `#8C6345` (RGB 0.550, 0.390, 0.271). Set to `Default` to preserve the player prefab's default hair color; custom colors use hex values such as `#AABBCC`.
- `Skin Color`: defaults to `#A6A6A6` (RGB 0.650, 0.650, 0.650).

Prefab choices are loaded after the game's item database initializes. If the Configuration Manager was already open, close and reopen it after entering the world to refresh the choices. Invalid prefab names and colors are reported in the BepInEx log.

## Build

The project references local Valheim and Gale BepInEx assemblies. Defaults use Steam's standard Valheim install and Gale's Default profile. Override either path for a different installation:

```powershell
dotnet build .\SummonNPC.csproj -c Release
# Example: dotnet build .\SummonNPC.csproj -c Release -p:BepInExCoreDir="C:\path\to\BepInEx\core"
```

The Release build produces `bin\Release\netstandard2.1\SummonNPC.dll`.
