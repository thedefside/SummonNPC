# SummonNPC

SummonNPC is a BepInEx plugin that customizes the Dead Raiser staff's `Skeleton_Friendly` summon. It clones the vanilla summon prefab, applies the configured body and appearance, and points the staff's existing `SpawnAbility` at the clone. The original summon AI, tame/follow behavior, health, damage, ownership, and spawn limit are retained.

## Screenshots

### Female

![Female summon](https://imgur.com/2ZdVnw6.jpeg)

### Male

![Male summon](https://i.imgur.com/hKlh3Y5.jpeg)

### Skeleton

![Skeleton summon](https://i.imgur.com/txy0LFx.jpeg)

## Version 0.7.0

- Added cosmetic melee weapon choices, including the `Tankard_dvergr`.
- Added the `Bow Prefab` option to change the summoned bow's appearance without changing its damage or spawn chance.
- Added separate cosmetic replacements for the two vanilla summon shields.
- Preserved vanilla equipment, damage, attacks, and random loadout chances.

## Version 0.6.0

- Added the `Skeleton` body type, retaining the skeleton's appearance and configured equipment.
- Adjusted helmet placement and added torso armor visuals for selected skeleton chest pieces.
- Added optional local debug logging and removed temporary visual diagnostics.

## Version 0.5.2

- Removed the one-handed weapon replacement option; summons now retain Valheim's random loadout to preserve AI attack behavior.

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

The player body mesh is rebound to the cloned skeleton's animation bones by their relative paths, without changing the summon to a player `VisEquipment` model. The player's renderer bounds are copied as well, avoiding the skeleton's smaller bounds culling the larger player mesh. The plugin creates a helmet attachment on the cloned rig using the player's mount offset, keeping that alignment separate from the animated body bones. Appearance and per-summon display names are configurable; the default weapon setting keeps Valheim's random loadout. If the vanilla player and skeleton rigs no longer match, the plugin logs a warning and leaves the staff unchanged.

## Gameplay video

[Watch the SummonNPC demo on YouTube](https://www.youtube.com/watch?v=atBMWniEYEE).

## Installation

Install the package with a Valheim mod manager, or copy `SummonNPC.dll` into `BepInEx\plugins\SummonNPC`. The Thunderstore package depends on BepInExPack for Valheim. For multiplayer, install SummonNPC on the server and every client. ServerSync v1.20 is bundled into `SummonNPC.dll`; no separate ServerSync installation is needed. The [ServerSync license](./lib/ServerSync.LICENSE.txt) is included in the repository.

## Configuration

SummonNPC writes its BepInEx configuration to `BepInEx\config\com.summonnpc.valheim.cfg`.

- `Enable Mod`: defaults to `true`. Set to `false` and restart to leave the game's summon behavior unchanged.
- `Debug Logging`: defaults to `false` and controls additional local troubleshooting messages. This setting is not server-synchronized; restart to capture startup details.
- `NPC Names`: comma-separated names; one is randomly selected for each summon. Defaults to `NPC`.
- `Body Type`: `Male` (default) or `Female`, selecting the player body model, or `Skeleton` to keep the vanilla skeleton body. Restart the game after changing this setting. Hair and beard appearance settings apply to player bodies; configured helmet, chest, legs, and cape equipment remains enabled with the skeleton body.
- Skeleton armor may leave bones visible. Selected chest pieces use an armored torso overlay; the Berserker and Berserker Undead chests retain their exposed-bone appearance. Some helmets may clip slightly with the skull.
- `Hair` and `Beard`: drop-downs populated from the game's item database. `HairNone` and `BeardNone` remove those visuals and are the defaults; there is no separate `None` choice for these lists.
- `Helmet Prefab`, `Chest Prefab`, `Legs Prefab`, and `Cape Prefab` are in the `Equipment` section. Their drop-downs are populated from actual items in the matching wearable slot and each includes `None`. Defaults are `HelmetBronze`, `ArmorBronzeChest`, `ArmorBronzeLegs`, and `CapeTrollHide`.
- `Melee Weapon`: `Default` preserves the random vanilla loadout. Select a craftable one-handed melee weapon or `Tankard_dvergr` to change the skeleton sword's appearance only; its original damage, attacks, and AI settings remain unchanged. Ranged loadouts are unchanged.
- `Bow Prefab` in `Equipment` changes the ranged summon's bow appearance. `Default` retains the vanilla look. The original bow item, damage, attacks, and chance of a bow summon remain unchanged.
- `ShieldWood Appearance` and `ShieldBronzeBuckler Appearance` are in the `Equipment` section. Choose `Default` to keep each original shield's look, or another shield as its cosmetic replacement. Shield items, combat behavior, and random loadouts are unchanged; replacements use the selected shield's default visual variant.
- The enemy prefab `Axe1h_JotunWarrior 1` is excluded from shield choices.
- The two shield settings from the previous test build are moved automatically. Other old entries under `Shield Replacements` may remain in the config file but are ignored.
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

The Release build produces `bin\Release\netstandard2.1\SummonNPC.dll`, with ServerSync merged into the plugin.
