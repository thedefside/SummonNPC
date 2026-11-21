# SummonNPC

SummonNPC is a BepInEx plugin that changes the Dead Raiser staff's summoned `Skeleton_Friendly` into a player-styled summon. It clones the vanilla summon prefab, applies the player body model and configured appearance, and points the staff's existing `SpawnAbility` at the clone. The original summon AI, tame/follow behavior, health, damage, ownership, and spawn limit are retained.

## Version 0.5.1

- Changed the default one-handed weapon option to `Default`, preserving Valheim's random loadout.
- Fixed summon-limit counting when custom NPC names are configured; summons keep their vanilla character identity for limit enforcement.

## Version 0.5.0

- Added server-authoritative configuration synchronization through bundled ServerSync. Install the mod on the server and every client.
- Synchronized each summon’s selected name so all clients see the same name.
- Added a server-synced one-handed weapon selector under `Equipment`, with a `Default` option to preserve Valheim’s random loadout.

## Version 0.4.0

- Added a configurable Male or Female player body model for the summon.
- Moved helmet, chest, legs, and cape settings into the `Equipment` config section.

The player body mesh is rebound to the cloned skeleton's animation bones by their relative paths, without changing the summon to a player `VisEquipment` model. The player's renderer bounds are copied as well, avoiding the skeleton's smaller bounds culling the larger player mesh. The plugin creates a helmet attachment on the cloned rig using the player's mount offset, keeping that alignment separate from the animated body bones. Appearance, per-summon display names, and the summoned NPC's one-handed weapon are configurable. Bows remain unchanged. If the vanilla player and skeleton rigs no longer match, the plugin logs a warning and leaves the staff unchanged.

## Gameplay video

[Watch the SummonNPC demo on YouTube](https://www.youtube.com/watch?v=atBMWniEYEE).

## Installation

Install the package with a Valheim mod manager, or copy `SummonNPC.dll` into `BepInEx\plugins\SummonNPC`. The Thunderstore package depends on BepInExPack for Valheim. For multiplayer, install SummonNPC on the server and every client. ServerSync v1.20 is bundled into `SummonNPC.dll`; no separate ServerSync installation is needed. The bundled library's license is included as `ServerSync.LICENSE.txt`.

## Configuration

SummonNPC writes its BepInEx configuration to `BepInEx\config\com.summonnpc.valheim.cfg`.

- `Enable Mod`: defaults to `true`. Set to `false` and restart to leave the game's summon behavior unchanged.
- `NPC Names`: comma-separated names; one is randomly selected for each summon. Defaults to `NPC`.
- `Body Type`: `Male` (default) or `Female`, selecting the player body model used by the summon. Restart the game after changing this setting. Beard configuration remains available with either body type.
- `Hair` and `Beard`: drop-downs populated from the game's item database. `HairNone` and `BeardNone` remove those visuals and are the defaults; there is no separate `None` choice for these lists.
- `Helmet Prefab`, `Chest Prefab`, `Legs Prefab`, and `Cape Prefab` are in the `Equipment` section. Their drop-downs are populated from actual items in the matching wearable slot and each includes `None`. Defaults are `HelmetBronze`, `ArmorBronzeChest`, `ArmorBronzeLegs`, and `CapeTrollHide`.
- `One-Handed Weapon Prefab`: choose from the game's craftable one-handed weapons with a non-projectile primary attack; creature attacks, throwables, and items without an attack are excluded. Defaults to `Default`, which preserves Valheim's random loadout and may include a bow or sword.
- Prefab options beginning with `FW`, `SP`, `Goblin`, `Charred`, `Dverger`, `Jotun`, or `StoneGolem` are excluded.
- Hair and beard visibility follows the configured helmet's built-in hair settings.
- `Hair Color`: defaults to `#8C6345` (RGB 0.550, 0.390, 0.271). Set to `Default` to preserve the player prefab's default hair color; custom colors use hex values such as `#AABBCC`.
- `Skin Color`: defaults to `#A6A6A6` (RGB 0.650, 0.650, 0.650).

Prefab choices are loaded after the game's item database initializes. If the Configuration Manager was already open, close and reopen it after entering the world to refresh the choices. Invalid prefab names and colors are reported in the BepInEx log.

## Multiplayer configuration

The server's SummonNPC configuration is synchronized to connected clients. Client settings are locked while connected; server admins can edit the authoritative settings. Each summon stores its selected name in Valheim's networked tameable data, so every client sees the same name. Clients without SummonNPC, or with a version below the server's minimum, are rejected during connection. A server without SummonNPC cannot provide synced settings or register the custom summon.

The client waits for the initial server configuration before registering the custom summon, so local appearance values do not determine the connected world's prefab. `Body Type` and `Enable Mod` still require a restart after changing.

## Build

The project references local Valheim and Gale BepInEx assemblies. Defaults use Steam's standard Valheim install and Gale's Default profile. Override either path for a different installation:

```powershell
dotnet build .\SummonNPC.csproj -c Release
# Example: dotnet build .\SummonNPC.csproj -c Release -p:BepInExCoreDir="C:\path\to\BepInEx\core"
```

The Release build produces `bin\Release\netstandard2.1\SummonNPC.dll`.
