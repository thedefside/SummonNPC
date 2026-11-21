using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SummonNPC;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class SummonNpcPlugin : BaseUnityPlugin
{
    internal const string PluginGuid = "com.summonnpc.valheim";
    internal const string PluginName = "SummonNPC";
    internal const string PluginVersion = "0.5.1";
    internal static readonly string SummonPrefabName = "SummonNPC_Summon";
    private static readonly string[] ExcludedPrefabPrefixes =
        { "FW", "SP", "Goblin", "Charred", "Dverger", "Jotun", "StoneGolem" };
    internal static ManualLogSource LogSource { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableMod { get; private set; } = null!;
    internal static ConfigEntry<string> NpcNames { get; private set; } = null!;
    internal static ConfigEntry<string> BodyType { get; private set; } = null!;
    internal static ConfigEntry<string> HairPrefab { get; private set; } = null!;
    internal static ConfigEntry<string> BeardPrefab { get; private set; } = null!;
    internal static ConfigEntry<string> HairColor { get; private set; } = null!;
    internal static ConfigEntry<string> SkinColor { get; private set; } = null!;
    internal static ConfigEntry<string> HelmetPrefab { get; private set; } = null!;
    internal static ConfigEntry<string> ChestPrefab { get; private set; } = null!;
    internal static ConfigEntry<string> LegsPrefab { get; private set; } = null!;
    internal static ConfigEntry<string> CapePrefab { get; private set; } = null!;
    internal static ConfigEntry<string> OneHandedWeaponPrefab { get; private set; } = null!;

    private static ConfigFile PluginConfig { get; set; } = null!;
    internal static ConfigSync ServerConfigSync { get; private set; } = null!;
    private static bool _appearanceConfigBound;
    private Harmony? _harmony;

    private void Awake()
    {
        LogSource = base.Logger;
        PluginConfig = Config;
        EnableMod = Config.Bind("General", "Enable Mod", true,
            "Set to false to leave the game's summon behavior unchanged. Restart the game after changing this setting.");
        NpcNames = Config.Bind("Summon", "NPC Names", "NPC",
            "Comma-separated names; one name is randomly selected for each summon.");
        BodyType = Config.Bind("Appearance", "Body Type", "Male",
            new ConfigDescription(
                "Player body model used by the summoned NPC. Restart the game after changing this setting. " +
                "Beard selection remains available for either body type.",
                new AcceptableValueList<string>("Male", "Female")));

        ServerConfigSync = new ConfigSync(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            MinimumRequiredVersion = PluginVersion,
            ModRequired = true,
            IsLocked = true
        };
        AddSynchronizedConfigEntry(EnableMod);
        AddSynchronizedConfigEntry(NpcNames);
        AddSynchronizedConfigEntry(BodyType);

        LogSource.LogInfo("Loading SummonNPC.");
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(SummonNpcPlugin).Assembly);
    }

    private IEnumerator Start()
    {
        while (!IsObjectDatabaseReady(ObjectDB.instance))
        {
            yield return null;
        }

        BindAppearanceConfig(ObjectDB.instance);

        while (ZNet.instance == null ||
               ZNetScene.instance == null ||
               !ServerConfigSync.InitialSyncDone)
        {
            yield return null;
        }

        RegisterSummonNpcPatch.TryRegister(ZNetScene.instance);
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    internal static void TryBindAppearanceConfig()
    {
        ObjectDB? objectDatabase = ObjectDB.instance;
        if (IsObjectDatabaseReady(objectDatabase))
        {
            BindAppearanceConfig(objectDatabase);
        }
    }

    private static bool IsObjectDatabaseReady(ObjectDB? objectDatabase)
    {
        return objectDatabase != null &&
               objectDatabase.m_items.Count > 0 &&
               objectDatabase.m_recipes != null &&
               objectDatabase.m_recipes.Count > 0;
    }

    private static void BindAppearanceConfig(ObjectDB objectDatabase)
    {
        if (_appearanceConfigBound)
        {
            return;
        }

        string[] hairOptions = GetPrefabOptions(objectDatabase, PrefabChoice.Hair, "HairNone");
        string[] beardOptions = GetPrefabOptions(objectDatabase, PrefabChoice.Beard, "BeardNone");
        string[] helmetOptions = GetPrefabOptions(objectDatabase, PrefabChoice.Helmet, "HelmetBronze");
        string[] chestOptions = GetPrefabOptions(objectDatabase, PrefabChoice.Chest, "ArmorBronzeChest");
        string[] legsOptions = GetPrefabOptions(objectDatabase, PrefabChoice.Legs, "ArmorBronzeLegs");
        string[] capeOptions = GetPrefabOptions(objectDatabase, PrefabChoice.Cape, "CapeTrollHide");
        string[] oneHandedWeaponOptions = GetPrefabOptions(
            objectDatabase,
            PrefabChoice.OneHandedWeapon,
            "Default");

        HairPrefab = BindPrefabChoice("Hair", "HairNone", "Hair prefab name; use HairNone to remove hair.", hairOptions);
        BeardPrefab = BindPrefabChoice("Beard", "BeardNone", "Beard prefab name; use BeardNone to remove the beard.", beardOptions);
        HairColor = PluginConfig.Bind("Appearance", "Hair Color", "#8C6345",
            "Hex hair color, such as #8C6345. Use Default to preserve the game's default hair color.");
        SkinColor = PluginConfig.Bind("Appearance", "Skin Color", "#A6A6A6",
            "Hex skin color, such as #A6A6A6.");
        AddSynchronizedConfigEntry(HairColor);
        AddSynchronizedConfigEntry(SkinColor);
        HelmetPrefab = BindPrefabChoice("Helmet Prefab", "HelmetBronze",
            "Helmet prefab name, or None to remove the helmet.", helmetOptions, "Equipment");
        ChestPrefab = BindPrefabChoice("Chest Prefab", "ArmorBronzeChest",
            "Chest armor prefab name, or None to remove chest armor.", chestOptions, "Equipment");
        LegsPrefab = BindPrefabChoice("Legs Prefab", "ArmorBronzeLegs",
            "Leg armor prefab name, or None to remove leg armor.", legsOptions, "Equipment");
        CapePrefab = BindPrefabChoice("Cape Prefab", "CapeTrollHide",
            "Cape prefab name, or None to remove the cape.", capeOptions, "Equipment");
        OneHandedWeaponPrefab = BindPrefabChoice(
            "One-Handed Weapon Prefab",
            "Default",
            "One-handed weapon prefab name to replace the summoned NPC's default one-handed weapons. " +
            "Choose Default to preserve the game's random loadout; bows are not changed.",
            oneHandedWeaponOptions,
            "Equipment");

        _appearanceConfigBound = true;
        LogSource.LogInfo(
            $"Loaded appearance choices: hair={hairOptions.Length}, beard={beardOptions.Length}, " +
            $"helmets={helmetOptions.Length - 1}, chest={chestOptions.Length - 1}, " +
            $"legs={legsOptions.Length - 1}, capes={capeOptions.Length - 1}, " +
            $"one-handed weapons={oneHandedWeaponOptions.Length - 1}.");
    }

    private static void AddSynchronizedConfigEntry<T>(ConfigEntry<T> configEntry)
    {
        ServerConfigSync.AddConfigEntry(configEntry).SynchronizedConfig = true;
    }

    private static ConfigEntry<string> BindPrefabChoice(
        string key,
        string defaultValue,
        string description,
        string[] options,
        string section = "Appearance")
    {
        ConfigEntry<string> configEntry = PluginConfig.Bind(
            section,
            key,
            defaultValue,
            new ConfigDescription(description, new AcceptableValueList<string>(options)));
        AddSynchronizedConfigEntry(configEntry);
        return configEntry;
    }

    private static string[] GetPrefabOptions(ObjectDB objectDatabase, PrefabChoice choice, string defaultValue)
    {
        HashSet<string> prefabNames = new(StringComparer.Ordinal);
        bool isHairOrBeard = choice is PrefabChoice.Hair or PrefabChoice.Beard;
        bool isOneHandedWeapon = choice == PrefabChoice.OneHandedWeapon;
        if (isOneHandedWeapon)
        {
            AddCraftableWeaponOptions(objectDatabase, prefabNames);
        }
        else
        {
            AddPrefabOptions(objectDatabase.m_items, prefabNames, choice);
        }

        ItemDrop? defaultItem = objectDatabase.GetItemPrefab(defaultValue)?.GetComponent<ItemDrop>();
        if (!string.Equals(defaultValue, "None", StringComparison.OrdinalIgnoreCase) &&
            defaultItem != null &&
            (!isOneHandedWeapon || IsPlayerUsableOneHandedWeapon(defaultItem)))
        {
            prefabNames.Add(defaultValue);
        }

        List<string> options = new(prefabNames);
        if (isHairOrBeard)
        {
            options.Remove(defaultValue);
        }

        options.Sort(StringComparer.OrdinalIgnoreCase);
        if (isHairOrBeard)
        {
            if (prefabNames.Contains(defaultValue))
            {
                options.Insert(0, defaultValue);
            }
        }
        else
        {
            options.Insert(0, isOneHandedWeapon ? "Default" : "None");
        }

        if (options.Count == 0)
        {
            LogSource.LogWarning($"No {choice} prefabs were found in the loaded prefab registry.");
        }

        return options.ToArray();
    }

    private static void AddCraftableWeaponOptions(ObjectDB objectDatabase, HashSet<string> prefabNames)
    {
        foreach (Recipe? recipe in objectDatabase.m_recipes)
        {
            ItemDrop? itemDrop = recipe?.m_item;
            if (itemDrop == null ||
                !IsPlayerUsableOneHandedWeapon(itemDrop) ||
                IsExcludedPrefab(itemDrop.gameObject.name))
            {
                continue;
            }

            prefabNames.Add(itemDrop.gameObject.name);
        }
    }

    private static bool IsPlayerUsableOneHandedWeapon(ItemDrop? itemDrop)
    {
        ItemDrop.ItemData? itemData = itemDrop?.m_itemData;
        Attack? attack = itemData?.m_shared?.m_attack;
        return itemData?.m_shared?.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon &&
               attack != null &&
               !string.IsNullOrWhiteSpace(attack.m_attackAnimation) &&
               attack.m_attackProjectile == null;
    }

    private static void AddPrefabOptions(
        IEnumerable<GameObject> prefabs,
        HashSet<string> prefabNames,
        PrefabChoice choice)
    {
        foreach (GameObject? prefab in prefabs)
        {
            if (prefab == null || IsExcludedPrefab(prefab.name) ||
                string.Equals(prefab.name, "None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string itemType = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_itemType.ToString() ?? string.Empty;
            bool matches = choice switch
            {
                PrefabChoice.Hair => prefab.name.StartsWith("Hair", StringComparison.Ordinal),
                PrefabChoice.Beard => prefab.name.StartsWith("Beard", StringComparison.Ordinal),
                PrefabChoice.Helmet => itemType == "Helmet",
                PrefabChoice.Chest => itemType == "Chest",
                PrefabChoice.Legs => itemType == "Legs",
                PrefabChoice.Cape => itemType == "Shoulder",
                _ => false
            };
            if (matches)
            {
                prefabNames.Add(prefab.name);
            }
        }
    }

    private static bool IsExcludedPrefab(string prefabName)
    {
        foreach (string prefix in ExcludedPrefabPrefixes)
        {
            if (prefabName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private enum PrefabChoice
    {
        Hair,
        Beard,
        Helmet,
        Chest,
        Legs,
        Cape,
        OneHandedWeapon
    }
}

[HarmonyPatch(typeof(ZNetScene), "Awake")]
internal static class RegisterSummonNpcPatch
{
    private const string SourceSummonPrefabName = "Skeleton_Friendly";
    private const string StaffPrefabName = "StaffSkeleton";
    private const string PlayerPrefabName = "Player";
    private static GameObject? _cloneContainer;
    private static ZNetScene? _registeredScene;
    private static bool _registrationComplete;
    private static bool _waitingForServerConfigLogged;

    private static void Postfix(ZNetScene __instance)
    {
        SummonNpcPlugin.TryBindAppearanceConfig();

        if (!SummonNpcPlugin.ServerConfigSync.InitialSyncDone)
        {
            if (!_waitingForServerConfigLogged)
            {
                SummonNpcPlugin.LogSource.LogInfo(
                    "Waiting for the server configuration sync before registering the custom summon.");
                _waitingForServerConfigLogged = true;
            }

            return;
        }

        TryRegister(__instance);
    }

    internal static void TryRegister(ZNetScene scene)
    {
        if (ReferenceEquals(_registeredScene, scene) && _registrationComplete)
        {
            return;
        }

        _registrationComplete = false;
        if (!SummonNpcPlugin.EnableMod.Value)
        {
            SummonNpcPlugin.LogSource.LogInfo(
                "SummonNPC is disabled by the server configuration; vanilla summon behavior is unchanged.");
            _registeredScene = scene;
            _registrationComplete = true;
            return;
        }

        GameObject? sourceSummon = scene.GetPrefab(SourceSummonPrefabName);
        GameObject? playerPrefab = scene.GetPrefab(PlayerPrefabName);
        GameObject? staffPrefab = scene.GetPrefab(StaffPrefabName);

        if (sourceSummon == null || playerPrefab == null || staffPrefab == null)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "Could not find the vanilla Player, StaffSkeleton, or Skeleton_Friendly prefab; the Dead Raiser is unchanged.");
            return;
        }

        GameObject? clone = scene.GetPrefab(SummonNpcPlugin.SummonPrefabName);
        if (clone == null)
        {
            int bodyModelIndex = string.Equals(
                SummonNpcPlugin.BodyType.Value,
                "Female",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            clone = CreateSummonPrefab(sourceSummon, playerPrefab, bodyModelIndex);
            if (clone == null)
            {
                return;
            }

            if (!RegisterPrefab(scene, clone))
            {
                Object.Destroy(clone);
                return;
            }
        }

        ItemDrop? staffItem = staffPrefab.GetComponent<ItemDrop>();
        GameObject? attackProjectile = staffItem?.m_itemData?.m_shared?.m_attack?.m_attackProjectile;
        SpawnAbility? spawnAbility = attackProjectile != null
            ? attackProjectile.GetComponent<SpawnAbility>()
            : null;

        if (spawnAbility == null || spawnAbility.m_spawnPrefab == null)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "Could not find the Dead Raiser SpawnAbility; the Dead Raiser is unchanged.");
            return;
        }

        bool replacedPrefab = false;
        GameObject[] spawnPrefabs = (GameObject[])spawnAbility.m_spawnPrefab.Clone();
        for (int i = 0; i < spawnPrefabs.Length; i++)
        {
            if (spawnPrefabs[i] == null || spawnPrefabs[i].name != SourceSummonPrefabName)
            {
                continue;
            }

            spawnPrefabs[i] = clone;
            replacedPrefab = true;
        }

        if (!replacedPrefab)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "The Dead Raiser no longer references Skeleton_Friendly; leaving its spawn unchanged.");
            return;
        }

        spawnAbility.m_spawnPrefab = spawnPrefabs;
        SummonNpcPlugin.LogSource.LogInfo(
            "Dead Raiser now spawns the configured custom summon with vanilla friendly-skeleton behavior.");
        _registeredScene = scene;
        _registrationComplete = true;
    }

    private static GameObject? CreateSummonPrefab(
        GameObject sourceSummon,
        GameObject playerPrefab,
        int bodyModelIndex)
    {
        VisEquipment? summonVisuals = sourceSummon.GetComponent<VisEquipment>();
        VisEquipment? playerVisuals = playerPrefab.GetComponent<VisEquipment>();
        if (summonVisuals?.m_bodyModel == null || playerVisuals?.m_bodyModel == null ||
            playerVisuals.m_models == null || playerVisuals.m_models.Length <= bodyModelIndex ||
            playerVisuals.m_models[bodyModelIndex].m_mesh == null ||
            playerVisuals.m_bodyModel.rootBone == null ||
            summonVisuals.m_bodyModel.rootBone == null ||
            playerVisuals.m_bodyModel.sharedMaterials.Length == 0)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "Could not find the player and summon body models; the Dead Raiser is unchanged.");
            return null;
        }

        SkinnedMeshRenderer sourceBody = playerVisuals.m_bodyModel;
        VisEquipment.PlayerModel playerModel = playerVisuals.m_models[bodyModelIndex];
        Mesh playerBodyMesh = playerModel.m_mesh;
        Material[] playerBodyMaterials = GetBodyMaterials(
            sourceBody,
            playerModel,
            bodyModelIndex,
            playerBodyMesh.subMeshCount);
        if (sourceBody.bones.Length != playerBodyMesh.bindposes.Length ||
            playerBodyMaterials.Length != playerBodyMesh.subMeshCount)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                $"The {SummonNpcPlugin.BodyType.Value} body mesh is incompatible with the player rig " +
                $"(bones={sourceBody.bones.Length}, bind poses={playerBodyMesh.bindposes.Length}, " +
                $"materials={playerBodyMaterials.Length}, submeshes={playerBodyMesh.subMeshCount}); " +
                "the Dead Raiser is unchanged.");
            return null;
        }

        Transform[] playerBones = sourceBody.bones;
        if (playerBones.Length == 0)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "The player body model has no animation bones; the Dead Raiser is unchanged.");
            return null;
        }

        Transform container = GetCloneContainer();
        GameObject clone = Object.Instantiate(sourceSummon, container);
        clone.name = SummonNpcPlugin.SummonPrefabName;
        clone.AddComponent<SummonNpcMarker>();

        Character? character = clone.GetComponent<Character>();
        VisEquipment? cloneVisuals = clone.GetComponent<VisEquipment>();
        if (character == null || cloneVisuals?.m_bodyModel == null)
        {
            Object.Destroy(clone);
            SummonNpcPlugin.LogSource.LogWarning(
                "The friendly skeleton prefab is missing its Character or VisEquipment component; the Dead Raiser is unchanged.");
            return null;
        }

        SummonAppearance.CopyDefaultHairColor(playerVisuals, cloneVisuals);

        Transform? cloneRootBone = FindMatchingBone(
            sourceBody.rootBone,
            cloneVisuals.m_bodyModel.rootBone,
            sourceBody.rootBone);
        if (cloneRootBone == null)
        {
            Object.Destroy(clone);
            SummonNpcPlugin.LogSource.LogWarning(
                "Could not match the player and cloned-skeleton animation rigs; the Dead Raiser is unchanged.");
            return null;
        }

        Transform[] cloneBones = new Transform[playerBones.Length];
        for (int i = 0; i < playerBones.Length; i++)
        {
            Transform? cloneBone = FindMatchingBone(
                sourceBody.rootBone,
                cloneVisuals.m_bodyModel.rootBone,
                playerBones[i]);
            if (cloneBone == null)
            {
                Object.Destroy(clone);
                SummonNpcPlugin.LogSource.LogWarning(
                    $"Could not map player bone '{playerBones[i]?.name}' to the cloned skeleton; the Dead Raiser is unchanged.");
                return null;
            }

            cloneBones[i] = cloneBone;
        }

        character.m_name = "NPC";
        SkinnedMeshRenderer targetBody = cloneVisuals.m_bodyModel;
        targetBody.sharedMesh = playerBodyMesh;
        targetBody.sharedMaterials = playerBodyMaterials;
        targetBody.rootBone = cloneRootBone;
        targetBody.bones = cloneBones;
        targetBody.localBounds = sourceBody.localBounds;
        targetBody.updateWhenOffscreen = true;
        targetBody.enabled = true;
        if (playerVisuals.m_helmet != null)
        {
            Transform sourceHelmetMount = playerVisuals.m_helmet;
            Transform? cloneHelmetParent = sourceHelmetMount.parent != null
                ? FindMatchingBone(
                    sourceBody.rootBone,
                    cloneVisuals.m_bodyModel.rootBone,
                    sourceHelmetMount.parent)
                : null;
            if (cloneHelmetParent != null)
            {
                GameObject helmetMountObject = new("SummonNPC_SummonHelmetMount");
                Transform helmetMount = helmetMountObject.transform;
                helmetMount.SetParent(cloneHelmetParent, false);
                helmetMount.localPosition = sourceHelmetMount.localPosition;
                helmetMount.localRotation = sourceHelmetMount.localRotation;
                helmetMount.localScale = sourceHelmetMount.localScale;
                cloneVisuals.m_helmet = helmetMount;
                SummonNpcPlugin.LogSource.LogInfo(
                    $"Created the summon helmet attachment from the player mount offset at '{cloneHelmetParent.name}'.");
            }
            else
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "Could not map the player's helmet mount parent to the cloned skeleton rig; keeping the skeleton's helmet mount.");
            }
        }

        SummonNpcPlugin.LogSource.LogInfo(
            $"Configured {SummonNpcPlugin.BodyType.Value} summon body mesh '{targetBody.sharedMesh.name}' with {cloneBones.Length} cloned bones and bounds {targetBody.localBounds}.");

        return clone;
    }

    private static Material[] GetBodyMaterials(
        SkinnedMeshRenderer sourceBody,
        VisEquipment.PlayerModel playerModel,
        int bodyModelIndex,
        int subMeshCount)
    {
        Material[] sourceMaterials = sourceBody.sharedMaterials;
        if (bodyModelIndex == 0 && sourceMaterials.Length == subMeshCount)
        {
            return sourceMaterials;
        }

        Material? baseMaterial = playerModel.m_baseMaterial;
        if (baseMaterial == null && sourceMaterials.Length > 0)
        {
            baseMaterial = sourceMaterials[0];
        }

        if (baseMaterial == null)
        {
            return Array.Empty<Material>();
        }

        Material[] materials = new Material[subMeshCount];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = baseMaterial;
        }

        return materials;
    }

    private static Transform GetCloneContainer()
    {
        if (_cloneContainer != null)
        {
            return _cloneContainer.transform;
        }

        GameObject container = new GameObject("SummonNPC_ClonedPrefabs");
        container.SetActive(false);
        Object.DontDestroyOnLoad(container);
        _cloneContainer = container;
        return container.transform;
    }

    private static Transform? FindMatchingBone(Transform? sourceRoot, Transform? targetRoot, Transform? sourceBone)
    {
        if (sourceRoot == null || targetRoot == null || sourceBone == null)
        {
            return null;
        }

        if (sourceBone == sourceRoot)
        {
            return targetRoot;
        }

        List<string> path = new();
        Transform? current = sourceBone;
        while (current != null && current != sourceRoot)
        {
            path.Add(current.name);
            current = current.parent;
        }

        if (current != sourceRoot)
        {
            return null;
        }

        Transform? target = targetRoot;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            target = target?.Find(path[i]);
            if (target == null)
            {
                return null;
            }
        }

        return target;
    }

    private static bool RegisterPrefab(ZNetScene scene, GameObject prefab)
    {
        var namedPrefabsField = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
        if (namedPrefabsField?.GetValue(scene) is not IDictionary namedPrefabs)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "Could not access the prefab hash registry; the Dead Raiser is unchanged.");
            return false;
        }

        int prefabHash = scene.GetPrefabHash(prefab);
        if (prefabHash == 0)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                "Could not calculate the custom summon prefab hash; the Dead Raiser is unchanged.");
            return false;
        }

        if (!scene.m_prefabs.Contains(prefab))
        {
            scene.m_prefabs.Add(prefab);
        }

        namedPrefabs[prefabHash] = prefab;
        return true;
    }
}

internal sealed class SummonNpcMarker : MonoBehaviour
{
}

internal static class SummonAppearance
{
    private const string AssignedNpcNameZdoKey = "SummonNPC_NpcName";
    private static readonly Dictionary<string, int> PrefabHashes = new(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingPrefabWarnings = new(StringComparer.Ordinal);
    private static readonly FieldInfo? HairItemField = AccessTools.Field(typeof(VisEquipment), "m_hairItem");
    private static readonly FieldInfo? BeardItemField = AccessTools.Field(typeof(VisEquipment), "m_beardItem");
    private static readonly FieldInfo? HelmetHideHairField = AccessTools.Field(typeof(VisEquipment), "m_helmetHideHair");
    private static readonly FieldInfo? HelmetHideBeardField = AccessTools.Field(typeof(VisEquipment), "m_helmetHideBeard");
    private static readonly MethodInfo? HairEquippedMethod = AccessTools.Method(typeof(VisEquipment), "SetHairEquipped");
    private static readonly MethodInfo? BeardEquippedMethod = AccessTools.Method(typeof(VisEquipment), "SetBeardEquipped");
    private static readonly MethodInfo? HairItemSelectionMethod = AccessTools.Method(typeof(VisEquipment), "GetHairItem");
    private static readonly Type? AccessoryType = typeof(VisEquipment).Assembly.GetType(
        "ItemDrop+ItemData+AccessoryType");
    private static readonly object? HairAccessoryType = GetAccessoryType("Hair");
    private static readonly object? BeardAccessoryType = GetAccessoryType("Beard");
    private static bool _emptyNamesWarningLogged;
    private static bool _invalidHairColorWarningLogged;
    private static bool _invalidSkinColorWarningLogged;
    private static bool _missingHairItemFieldWarningLogged;
    private static bool _missingBeardItemFieldWarningLogged;
    private static bool _missingHairEquippedMethodWarningLogged;
    private static bool _missingBeardEquippedMethodWarningLogged;
    private static bool _missingHelmetHairSettingsWarningLogged;

    internal static bool IsSummon(GameObject gameObject)
    {
        return gameObject.GetComponent<SummonNpcMarker>() != null ||
               gameObject.name.StartsWith(SummonNpcPlugin.SummonPrefabName, StringComparison.Ordinal);
    }

    internal static void InitializeAssignedName(Tameable tameable)
    {
        TryGetAssignedName(tameable);
    }

    internal static string GetAssignedName(Tameable tameable, string fallback)
    {
        return TryGetAssignedName(tameable) ?? fallback;
    }

    private static string? TryGetAssignedName(Tameable tameable)
    {
        ZNetView? networkView = tameable.GetComponent<ZNetView>();
        if (networkView == null || !networkView.IsValid())
        {
            return null;
        }

        ZDO? zdo = networkView.GetZDO();
        if (zdo == null)
        {
            return null;
        }

        string name = zdo.GetString(AssignedNpcNameZdoKey, string.Empty);
        if (string.IsNullOrWhiteSpace(name) && networkView.IsOwner())
        {
            name = SelectNpcName();
            zdo.Set(AssignedNpcNameZdoKey, name);
        }

        if (!string.IsNullOrWhiteSpace(name) && networkView.IsOwner() &&
            !string.Equals(zdo.GetString(ZDOVars.s_tamedName, string.Empty), name, StringComparison.Ordinal))
        {
            zdo.Set(ZDOVars.s_tamedName, name);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Valheim groups summons by Character.m_name when enforcing the staff's summon limit.
        return name;
    }

    internal static void CopyDefaultHairColor(VisEquipment source, VisEquipment target)
    {
        var hairColorField = AccessTools.Field(typeof(VisEquipment), "m_hairColor");
        if (hairColorField?.GetValue(source) is Vector3 hairColor)
        {
            target.SetHairColor(hairColor);
            return;
        }

        SummonNpcPlugin.LogSource.LogWarning(
            "Could not read the vanilla default hair color; the summon will keep its current hair color.");
    }

    internal static bool TryGetConfiguredPrefabHash(string prefabName, out int hash)
    {
        if (IsNone(prefabName))
        {
            hash = 0;
            return true;
        }

        return TryGetPrefabHash(prefabName.Trim(), out hash);
    }

    internal static bool IsNone(string prefabName)
    {
        return string.IsNullOrWhiteSpace(prefabName) ||
            string.Equals(prefabName.Trim(), "None", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CanApplyConfiguredPrefab(string prefabName)
    {
        return !IsNone(prefabName) &&
            TryGetConfiguredPrefabHash(prefabName, out _);
    }

    internal static void PrepareConfiguredHairAndBeard(VisEquipment visuals)
    {
        if (TryGetConfiguredPrefabHash(SummonNpcPlugin.HairPrefab.Value, out int hairHash))
        {
            if (HairItemField != null)
            {
                HairItemField.SetValue(visuals, hairHash);
            }
            else if (!_missingHairItemFieldWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "Could not access VisEquipment.m_hairItem; configured hair may not be applied.");
                _missingHairItemFieldWarningLogged = true;
            }
        }

        if (TryGetConfiguredPrefabHash(SummonNpcPlugin.BeardPrefab.Value, out int beardHash))
        {
            if (BeardItemField != null)
            {
                BeardItemField.SetValue(visuals, beardHash);
            }
            else if (!_missingBeardItemFieldWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "Could not access VisEquipment.m_beardItem; configured beard may not be applied.");
                _missingBeardItemFieldWarningLogged = true;
            }
        }
    }

    internal static void ApplyConfiguredHairAndBeard(VisEquipment visuals)
    {
        PrepareConfiguredHairAndBeard(visuals);

        if (TryGetConfiguredPrefabHash(SummonNpcPlugin.HairPrefab.Value, out int hairHash))
        {
            if (HairEquippedMethod != null)
            {
                if (HairEquippedMethod.Invoke(visuals, new object[] { hairHash }) is true)
                {
                    SummonNpcPlugin.LogSource.LogInfo(
                        $"Applied hair prefab '{SummonNpcPlugin.HairPrefab.Value}'.");
                }
            }
            else if (!_missingHairEquippedMethodWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "Could not access VisEquipment.SetHairEquipped; configured hair may not be applied.");
                _missingHairEquippedMethodWarningLogged = true;
            }
        }

        if (TryGetConfiguredPrefabHash(SummonNpcPlugin.BeardPrefab.Value, out int beardHash))
        {
            if (BeardEquippedMethod != null)
            {
                if (BeardEquippedMethod.Invoke(visuals, new object[] { beardHash }) is true)
                {
                    SummonNpcPlugin.LogSource.LogInfo(
                        $"Applied beard prefab '{SummonNpcPlugin.BeardPrefab.Value}'.");
                }
            }
            else if (!_missingBeardEquippedMethodWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "Could not access VisEquipment.SetBeardEquipped; configured beard may not be applied.");
                _missingBeardEquippedMethodWarningLogged = true;
            }
        }
    }

    internal static int GetHelmetAdjustedAccessoryHash(VisEquipment visuals, int configuredHash, bool beard)
    {
        FieldInfo? hideTypeField = beard ? HelmetHideBeardField : HelmetHideHairField;
        object? accessoryType = beard ? BeardAccessoryType : HairAccessoryType;
        if (HairItemSelectionMethod == null || hideTypeField == null || accessoryType == null)
        {
            if (!_missingHelmetHairSettingsWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "Could not access Valheim's helmet hair settings; configured hair or beard may clip through helmets.");
                _missingHelmetHairSettingsWarningLogged = true;
            }

            return configuredHash;
        }

        object? hideType = hideTypeField.GetValue(visuals);
        object? selectedHash = HairItemSelectionMethod.Invoke(
            visuals,
            new[] { hideType!, (object)configuredHash, accessoryType });
        if (selectedHash is int hash)
        {
            return hash;
        }

        throw new InvalidOperationException("Valheim's GetHairItem method did not return an accessory prefab hash.");
    }

    internal static void ApplyConfiguredHairColor(VisEquipment visuals)
    {
        if (TryGetConfiguredHairColor(out Color hairColor))
        {
            visuals.SetHairColor(new Vector3(hairColor.r, hairColor.g, hairColor.b));
        }
    }

    internal static bool TryGetPrefabHash(string prefabName, out int hash)
    {
        if (PrefabHashes.TryGetValue(prefabName, out hash))
        {
            return true;
        }

        ZNetScene? scene = ZNetScene.instance;
        GameObject? prefab = ObjectDB.instance?.GetItemPrefab(prefabName) ?? scene?.GetPrefab(prefabName);
        if (scene == null || prefab == null)
        {
            WarnMissingPrefab(prefabName);
            hash = 0;
            return false;
        }

        hash = scene.GetPrefabHash(prefab);
        if (hash == 0)
        {
            WarnMissingPrefab(prefabName);
            return false;
        }

        PrefabHashes[prefabName] = hash;
        SummonNpcPlugin.LogSource.LogInfo($"Using '{prefabName}' for summon appearance.");
        return true;
    }

    private static string SelectNpcName()
    {
        List<string> names = new();
        foreach (string candidate in SummonNpcPlugin.NpcNames.Value.Split(','))
        {
            string name = candidate.Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        if (names.Count == 0)
        {
            if (!_emptyNamesWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    "NPC Names contains no usable names; using 'NPC' instead.");
                _emptyNamesWarningLogged = true;
            }

            return "NPC";
        }

        return names[UnityEngine.Random.Range(0, names.Count)];
    }

    private static bool TryGetConfiguredHairColor(out Color color)
    {
        string value = SummonNpcPlugin.HairColor.Value.Trim();
        if (string.Equals(value, "Default", StringComparison.OrdinalIgnoreCase))
        {
            color = default;
            return false;
        }

        if (TryParseHexColor(value, out color))
        {
            return true;
        }

        if (!_invalidHairColorWarningLogged)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                $"Invalid Hair Color '{value}'; use 'Default' or a hex value such as #AABBCC.");
            _invalidHairColorWarningLogged = true;
        }

        color = default;
        return false;
    }

    internal static Color GetConfiguredSkinColor()
    {
        string value = SummonNpcPlugin.SkinColor.Value.Trim();
        if (TryParseHexColor(value, out Color color))
        {
            return color;
        }

        if (!_invalidSkinColorWarningLogged)
        {
            SummonNpcPlugin.LogSource.LogWarning(
                $"Invalid Skin Color '{value}'; use a hex value such as #A6A6A6. Keeping the default skin tone.");
            _invalidSkinColorWarningLogged = true;
        }

        return new Color(0.65f, 0.65f, 0.65f, 1f);
    }

    private static bool TryParseHexColor(string value, out Color color)
    {
        if ((value.Length == 7 || value.Length == 9) &&
            value[0] == '#' &&
            ColorUtility.TryParseHtmlString(value, out color))
        {
            return true;
        }

        color = default;
        return false;
    }

    private static object? GetAccessoryType(string name)
    {
        return AccessoryType != null && Enum.IsDefined(AccessoryType, name)
            ? Enum.Parse(AccessoryType, name)
            : null;
    }

    private static void WarnMissingPrefab(string prefabName)
    {
        if (MissingPrefabWarnings.Add(prefabName))
        {
            SummonNpcPlugin.LogSource.LogWarning(
                $"Could not resolve appearance prefab '{prefabName}'; leaving that visual unchanged.");
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
internal static class ApplySummonChestArmorPatch
{
    private static void Prefix(VisEquipment __instance, ref int hash)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject) &&
            SummonAppearance.TryGetConfiguredPrefabHash(SummonNpcPlugin.ChestPrefab.Value, out int armorHash))
        {
            hash = armorHash;
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetHelmetEquipped")]
internal static class ApplySummonHelmetPatch
{
    private static void Prefix(VisEquipment __instance, ref int hash)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject) &&
            SummonAppearance.TryGetConfiguredPrefabHash(SummonNpcPlugin.HelmetPrefab.Value, out int helmetHash))
        {
            hash = helmetHash;
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
internal static class ApplySummonLegArmorPatch
{
    private static void Prefix(VisEquipment __instance, ref int hash)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject) &&
            SummonAppearance.TryGetConfiguredPrefabHash(SummonNpcPlugin.LegsPrefab.Value, out int armorHash))
        {
            hash = armorHash;
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
internal static class ApplySummonCapePatch
{
    private static void Prefix(VisEquipment __instance, ref int hash, ref int variant, ref int quality)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject) &&
            SummonAppearance.TryGetConfiguredPrefabHash(SummonNpcPlugin.CapePrefab.Value, out int capeHash))
        {
            hash = capeHash;
            variant = 0;
            quality = 0;
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetHairEquipped")]
internal static class ApplySummonHairPatch
{
    private static void Prefix(VisEquipment __instance, ref int hash)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject) &&
            SummonAppearance.TryGetConfiguredPrefabHash(SummonNpcPlugin.HairPrefab.Value, out int hairHash))
        {
            hash = SummonAppearance.GetHelmetAdjustedAccessoryHash(__instance, hairHash, beard: false);
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetBeardEquipped")]
internal static class ApplySummonBeardPatch
{
    private static void Prefix(VisEquipment __instance, ref int hash)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject) &&
            SummonAppearance.TryGetConfiguredPrefabHash(SummonNpcPlugin.BeardPrefab.Value, out int beardHash))
        {
            hash = SummonAppearance.GetHelmetAdjustedAccessoryHash(__instance, beardHash, beard: true);
        }
    }
}

[HarmonyPatch(typeof(Tameable), "Awake")]
internal static class InitializeSummonNamePatch
{
    private static void Postfix(Tameable __instance)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject))
        {
            SummonAppearance.InitializeAssignedName(__instance);
        }
    }
}

[HarmonyPatch(typeof(ZNetScene), "CreateObject")]
internal static class InitializeSummonNameAfterNetworkSpawnPatch
{
    private static void Postfix(GameObject __result)
    {
        if (__result != null && SummonAppearance.IsSummon(__result))
        {
            Tameable? tameable = __result.GetComponent<Tameable>();
            if (tameable != null)
            {
                SummonAppearance.InitializeAssignedName(tameable);
            }
        }
    }
}

[HarmonyPatch(typeof(Humanoid), "GiveDefaultItem")]
internal static class ReplaceSummonMeleeWeaponPatch
{
    private static bool _replacementLogged;
    private static bool _missingWeaponWarningLogged;

    private static void Prefix(Humanoid __instance, ref GameObject __0)
    {
        if (!SummonAppearance.IsSummon(__instance.gameObject) || __0 == null)
        {
            return;
        }

        ItemDrop.ItemData? itemData = __0.GetComponent<ItemDrop>()?.m_itemData;
        if (itemData?.IsWeapon() == true)
        {
            SummonNpcPlugin.LogSource.LogInfo(
                $"Summon default weapon candidate '{__0.name}' ({itemData.m_shared?.m_itemType.ToString() ?? "unknown"}).");
        }

        string selectedPrefabName = SummonNpcPlugin.OneHandedWeaponPrefab.Value;
        if (string.Equals(selectedPrefabName, "Default", StringComparison.OrdinalIgnoreCase) ||
            !IsOneHandedWeapon(__0))
        {
            return;
        }

        GameObject? replacement = ObjectDB.instance?.GetItemPrefab(selectedPrefabName);
        if (replacement == null || !IsOneHandedWeapon(replacement))
        {
            if (!_missingWeaponWarningLogged)
            {
                SummonNpcPlugin.LogSource.LogWarning(
                    $"Could not find configured one-handed weapon '{selectedPrefabName}'; summoned NPCs will keep their original loadout.");
                _missingWeaponWarningLogged = true;
            }

            return;
        }

        if (__0 == replacement)
        {
            return;
        }

        __0 = replacement;
        LogReplacement(selectedPrefabName);
    }

    private static void LogReplacement(string prefabName)
    {
        if (!_replacementLogged)
        {
            SummonNpcPlugin.LogSource.LogInfo(
                $"Summoned NPC one-handed weapons are replaced with '{prefabName}'.");
            _replacementLogged = true;
        }
    }

    private static bool IsOneHandedWeapon(GameObject prefab)
    {
        ItemDrop? itemDrop = prefab.GetComponent<ItemDrop>();
        return itemDrop?.m_itemData?.m_shared?.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon;
    }
}

[HarmonyPatch(typeof(Tameable), "GetName")]
internal static class SetSummonTamedNamePatch
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject))
        {
            __result = SummonAppearance.GetAssignedName(__instance, __result);
        }
    }
}

[HarmonyPatch(typeof(Tameable), "GetHoverName")]
internal static class SetSummonHoverNamePatch
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject))
        {
            __result = SummonAppearance.GetAssignedName(__instance, __result);
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "UpdateVisuals")]
internal static class ApplySummonSkinColorPatch
{
    private static readonly int SkinColorProperty = Shader.PropertyToID("_SkinColor");
    private static readonly MaterialPropertyBlock PropertyBlock = new();

    private static void Prefix(VisEquipment __instance)
    {
        if (SummonAppearance.IsSummon(__instance.gameObject))
        {
            SummonAppearance.PrepareConfiguredHairAndBeard(__instance);
        }
    }

    private static void Postfix(VisEquipment __instance)
    {
        if (!SummonAppearance.IsSummon(__instance.gameObject))
        {
            return;
        }

        SummonAppearance.ApplyConfiguredHairAndBeard(__instance);
        SummonAppearance.ApplyConfiguredHairColor(__instance);

        SkinnedMeshRenderer? body = __instance.m_bodyModel;
        if (body == null)
        {
            return;
        }

        Mesh? mesh = body.sharedMesh;
        if (mesh == null)
        {
            return;
        }

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            body.GetPropertyBlock(PropertyBlock, i);
            PropertyBlock.SetColor(SkinColorProperty, SummonAppearance.GetConfiguredSkinColor());
            body.SetPropertyBlock(PropertyBlock, i);
        }
    }

}

[HarmonyPatch(typeof(VisEquipment), "SetupNpcHair")]
internal static class SuppressSummonHairPatch
{
    private static bool Prefix(VisEquipment __instance)
    {
        if (!SummonAppearance.IsSummon(__instance.gameObject))
        {
            return true;
        }

        string hairPrefab = SummonNpcPlugin.HairPrefab.Value;
        if (!SummonAppearance.CanApplyConfiguredPrefab(hairPrefab))
        {
            return false;
        }

        __instance.m_npcHairChance = 1f;
        return true;
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetupNpcBeard")]
internal static class SuppressSummonBeardPatch
{
    private static bool Prefix(VisEquipment __instance)
    {
        if (!SummonAppearance.IsSummon(__instance.gameObject))
        {
            return true;
        }

        string beardPrefab = SummonNpcPlugin.BeardPrefab.Value;
        if (!SummonAppearance.CanApplyConfiguredPrefab(beardPrefab))
        {
            return false;
        }

        __instance.m_npcBeardChance = 1f;
        return true;
    }
}
