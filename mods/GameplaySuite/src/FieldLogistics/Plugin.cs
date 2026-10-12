using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using HarmonyLib;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

namespace FieldLogistics;

public sealed class Plugin : IModpackPlugin
{
    private const string ModName = "Field Logistics";
    private const string Version = "1.0.6";
    private const string VehicleSkillId = "active.fieldlogistics_vehicle_resupply";
    private const string ShareSkillId = "active.fieldlogistics_redistribute_ammo";
    private const string ModuleId = "accessory.fieldlogistics_vehicle_ammo_supply";
    private const string TestVehicleId = "vehicle.chassis_ifv";

    private static MelonLogger.Instance _log;
    private static Plugin _instance;
    private HarmonyLib.Harmony _harmony;
    private int _updateCounter;
    private int _pendingAmmoUpdateCounter;

    private static readonly Dictionary<string, AmmoSnapshot> _ammoSnapshots = new();
    private static readonly Dictionary<string, PendingAmmoCredit> _pendingAmmoCredits = new();
    private static readonly Dictionary<string, PendingRedistribution> _pendingRedistributions = new();
    private static readonly Dictionary<string, PendingModeSync> _pendingModeSyncs = new();
    private static readonly HashSet<IntPtr> _configuredVehicleResupplyActors = new();
    private static readonly HashSet<IntPtr> _configuredAmmoShareActors = new();
    private bool _testModuleGranted;
    private bool _testVehicleGranted;
    private bool _testLoadoutReadyLogged;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _instance = this;
        _harmony = harmony;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("Vehicle ammunition supply");
            settings.AddToggle("DefaultVehicleResupply", "All player vehicles get 1 resupply use", true);
            settings.AddNumber("VehicleRefillCharges", "Extra refill uses from Ammo Supply module", 1, 12, 4);
            settings.AddNumber("VehicleRefillAPCost", "Refill AP cost", 0, 100, 60);

            settings.AddHeader("Availability");
            settings.AddNumber("Rarity", "Rarity", 1, 100, 20);
            settings.AddNumber("BlackMarketMaxQuantity", "Black market max quantity", 0, 10, 1);
            settings.AddNumber("MinimumCampaignProgress", "Minimum campaign progress", 0, 100, 0);

            settings.AddHeader("Infantry ammo sharing - experimental");
            settings.AddToggle("EnableAmmoSharing", "Enable adjacent infantry ammo sharing", false);
            settings.AddNumber("AmmoSharingAPCost", "Redistribute ammo AP cost", 0, 100, 40);

            settings.AddHeader("Testing");
            settings.AddToggle("GrantTestLoadout", "Grant 1 Ammo Supply + 1 IFV for testing", false);
            settings.AddToggle("DebugLogging", "Debug logging", false);
        });

        ModSettings.OnSettingChanged += OnSettingChanged;
        TacticalEventHooks.OnSkillUsed += OnTacticalSkillUsed;
        TacticalEventHooks.OnEntitySpawned += OnTacticalEntitySpawned;
        DebugMsg($"{ModName} {Version}: initialized. Player vehicles get 1 resupply use by default; the Ammo Supply module adds extra uses. Infantry ammo sharing remains experimental/OFF by default.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        GameState.RunDelayed(30, () =>
        {
            ApplySettingsToTemplates();
            TryGrantTestLoadout();
        });
    }

    public void OnUpdate()
    {
        // Pending ammo credits are intentionally lightweight and mission-local.
        // MENACE can expose an inactive squad weapon as -1; retry a few times per
        // second and apply the queued delta as soon as the real skill becomes readable.
        _pendingAmmoUpdateCounter++;
        if (_pendingAmmoUpdateCounter >= 15)
        {
            _pendingAmmoUpdateCounter = 0;
            ProcessPendingAmmoWork();
        }

        _updateCounter++;
        if (_updateCounter >= 180)
        {
            _updateCounter = 0;
            TryGrantTestLoadout();
        }
    }

    public void OnGUI() { }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        TacticalEventHooks.OnSkillUsed -= OnTacticalSkillUsed;
        TacticalEventHooks.OnEntitySpawned -= OnTacticalEntitySpawned;
        _ammoSnapshots.Clear();
        _pendingAmmoCredits.Clear();
        _pendingRedistributions.Clear();
        _pendingModeSyncs.Clear();
        _configuredVehicleResupplyActors.Clear();
        _configuredAmmoShareActors.Clear();
        try { _harmony?.UnpatchSelf(); } catch { }
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, ModName, StringComparison.Ordinal)) return;

        if (string.Equals(key, "EnableAmmoSharing", StringComparison.Ordinal) && !SharingEnabled())
        {
            _ammoSnapshots.Clear();
            _pendingAmmoCredits.Clear();
            _pendingRedistributions.Clear();
            _pendingModeSyncs.Clear();
        }

        GameState.RunDelayed(1, () =>
        {
            ApplySettingsToTemplates();
            TryGrantTestLoadout();
        });
    }


    private static void OnTacticalEntitySpawned(IntPtr entityPtr)
    {
        if (entityPtr == IntPtr.Zero) return;
        ScheduleVehicleResupplyConfiguration(entityPtr, 0);
        ScheduleAmmoShareSkillConfiguration(entityPtr, 0);
    }

    private static void ScheduleVehicleResupplyConfiguration(IntPtr entityPtr, int attempt)
    {
        if (entityPtr == IntPtr.Zero || _configuredVehicleResupplyActors.Contains(entityPtr)) return;

        int delay = attempt switch
        {
            0 => 5,
            1 => 20,
            2 => 60,
            _ => 120
        };

        GameState.RunDelayed(delay, () =>
        {
            if (_configuredVehicleResupplyActors.Contains(entityPtr)) return;
            bool done = TryConfigureVehicleResupply(entityPtr);
            if (!done && attempt < 3)
                ScheduleVehicleResupplyConfiguration(entityPtr, attempt + 1);
        });
    }

    private static bool TryConfigureVehicleResupply(IntPtr entityPtr)
    {
        try
        {
            var actor = new GameObj(entityPtr).ToManaged();
            if (actor == null) return false;

            var template = Invoke(actor, "GetTemplate")
                        ?? GetMember(actor, "Template")
                        ?? GetMember(actor, "m_Template");
            string templateId = GetStringMember(template, "m_ID")
                             ?? GetStringMember(template, "name")
                             ?? GetStringMember(template, "Name")
                             ?? string.Empty;

            // The manifest only grants the skill to player vehicle templates. Keep
            // the runtime path equally strict so NPC/enemy vehicles are untouched.
            if (!templateId.StartsWith("player_vehicle.", StringComparison.OrdinalIgnoreCase))
                return true;

            var skillContainer = Invoke(actor, "GetSkills")
                              ?? Invoke(actor, "GetSkillContainer")
                              ?? GetMember(actor, "SkillContainer")
                              ?? GetMember(actor, "Skills");
            if (skillContainer == null) return false;

            bool hasModule = ActorHasItemTemplate(actor, ModuleId);
            int baseUses = GetBool("DefaultVehicleResupply", true) ? 1 : 0;
            int moduleUses = hasModule ? Math.Max(0, GetInt("VehicleRefillCharges", 4)) : 0;
            int desiredUses = baseUses + moduleUses;

            object refillSkill = FindSkillInContainer(skillContainer, VehicleSkillId);

            if (desiredUses <= 0)
            {
                // When the optional default use is disabled, vehicles without the
                // module should not retain a dead/grey resupply button.
                if (refillSkill != null)
                    Invoke(skillContainer, "RemoveByID", VehicleSkillId);
                _configuredVehicleResupplyActors.Add(entityPtr);
                if (DebugEnabled())
                    DebugMsg($"Vehicle resupply configured: {templateId}, module={hasModule}, uses=0 (skill absent/removed).");
                return true;
            }

            if (refillSkill == null)
            {
                // Do not rely exclusively on the modpack manifest having populated
                // EntityTemplate.Skills before this actor was materialized. Other mods
                // can legitimately touch vehicle templates earlier in the load sequence,
                // and loaded saves can also contain actors created before our patch pass.
                // First ask MENACE to rebuild inherent skills from the live template;
                // if that still does not expose our action, add an instance of the
                // already-registered SkillTemplate directly to the live SkillContainer.
                refillSkill = EnsureSkillInContainer(template, skillContainer, VehicleSkillId);
                if (refillSkill == null) return false;
            }

            var concreteSkill = TryAsConcreteSkill(refillSkill);
            if (concreteSkill == null)
            {
                if (DebugEnabled())
                    DebugWarning($"Vehicle resupply configuration retry: '{VehicleSkillId}' resolved only as {refillSkill?.GetType().FullName ?? "null"}, not as Skill.");
                return false;
            }

            // SkillContainer.GetAllSkills() is declared as List<BaseSkill>. Il2CppInterop
            // therefore hands us a BaseSkill wrapper even when the native object is a
            // concrete Menace.Tactical.Skills.Skill. Reflection on that base wrapper
            // cannot see Skill-only APIs such as SetUses/SetMaxUses/SynchronizeUses.
            // Re-wrap/cast the same native object as Skill and call the generated
            // interop methods directly. This keeps the native use/charge state intact
            // and avoids fragile reflection against the declared collection element type.
            concreteSkill.SetMaxUses(desiredUses);
            concreteSkill.SetUses(desiredUses);
            concreteSkill.SynchronizeUses();
            concreteSkill.SetEnabled(true);
            RequestSkillUiRefresh(actor, "vehicle-resupply");

            int actualMax = concreteSkill.GetMaxUses();
            int actualUses = concreteSkill.GetUses();
            int actualCharges = concreteSkill.GetCharges();

            _configuredVehicleResupplyActors.Add(entityPtr);
            if (DebugEnabled())
            {
                DebugMsg($"Vehicle resupply configured: {templateId}, module={hasModule}, base={baseUses}, extra={moduleUses}, desired={desiredUses}, max={actualMax}, uses={actualUses}, charges={actualCharges}");
            }
            return true;
        }
        catch (Exception ex)
        {
            if (DebugEnabled())
                DebugWarning($"Vehicle resupply configuration retry: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void ScheduleAmmoShareSkillConfiguration(IntPtr entityPtr, int attempt)
    {
        if (entityPtr == IntPtr.Zero || _configuredAmmoShareActors.Contains(entityPtr)) return;

        int delay = attempt switch
        {
            0 => 5,
            1 => 20,
            2 => 60,
            _ => 120
        };

        GameState.RunDelayed(delay, () =>
        {
            if (_configuredAmmoShareActors.Contains(entityPtr)) return;
            bool done = TryConfigureAmmoShareSkill(entityPtr);
            if (!done && attempt < 3)
                ScheduleAmmoShareSkillConfiguration(entityPtr, attempt + 1);
        });
    }

    private static bool TryConfigureAmmoShareSkill(IntPtr entityPtr)
    {
        try
        {
            var actor = new GameObj(entityPtr).ToManaged();
            if (actor == null) return false;

            var template = Invoke(actor, "GetTemplate")
                        ?? GetMember(actor, "Template")
                        ?? GetMember(actor, "m_Template");
            string templateId = GetStringMember(template, "m_ID")
                             ?? GetStringMember(template, "name")
                             ?? GetStringMember(template, "Name")
                             ?? string.Empty;

            // Match the manifest scope exactly. Enemy/allied NPC infantry should not
            // receive the player-facing ammo-sharing action.
            if (!templateId.StartsWith("player_squad.", StringComparison.OrdinalIgnoreCase))
                return true;

            var skillContainer = Invoke(actor, "GetSkills")
                              ?? Invoke(actor, "GetSkillContainer")
                              ?? GetMember(actor, "SkillContainer")
                              ?? GetMember(actor, "Skills");
            if (skillContainer == null) return false;

            object shareSkill = FindSkillInContainer(skillContainer, ShareSkillId);
            if (shareSkill == null)
            {
                shareSkill = EnsureSkillInContainer(template, skillContainer, ShareSkillId);
                if (shareSkill == null) return false;
            }

            _configuredAmmoShareActors.Add(entityPtr);
            return true;
        }
        catch (Exception ex)
        {
            if (DebugEnabled())
                DebugWarning($"Ammo-sharing skill configuration retry: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static object FindSkillInContainer(object skillContainer, string skillId)
    {
        if (skillContainer == null || string.IsNullOrEmpty(skillId)) return null;

        var allSkills = Invoke(skillContainer, "GetAllSkills")
                     ?? GetMember(skillContainer, "Skills")
                     ?? GetMember(skillContainer, "m_Skills");
        if (allSkills == null) return null;

        foreach (var skill in EnumerateObjects(allSkills))
        {
            if (skill == null) continue;
            string id = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill);
            if (string.Equals(id, skillId, StringComparison.OrdinalIgnoreCase))
                return TryAsConcreteSkill(skill) ?? skill;
        }

        return null;
    }

    private static object EnsureSkillInContainer(object entityTemplate, object skillContainer, string skillId)
    {
        object existing = FindSkillInContainer(skillContainer, skillId);
        if (existing != null) return existing;

        // Preferred path: let MENACE populate inherent skills from the actor's current
        // EntityTemplate. This also handles actors restored from saves before our
        // template patch pass without constructing synthetic skill state ourselves.
        if (entityTemplate != null)
        {
            Invoke(skillContainer, "AddInherentSkills", entityTemplate);
            Invoke(skillContainer, "ScheduleUpdate");
            existing = FindSkillInContainer(skillContainer, skillId);
            if (existing != null)
            {
                if (DebugEnabled())
                    DebugMsg($"Runtime synchronized '{skillId}' through AddInherentSkills.");
                return existing;
            }
        }

        // Fallback: the custom SkillTemplate can be registered correctly even when the
        // EntityTemplate.Skills merge was missed because of load ordering. Create only
        // this missing skill and append it to the live container; never replace the
        // container or another mod's skills.
        var templateObj = Templates.Find("SkillTemplate", skillId);
        if (templateObj.IsNull) return null;

        var skillTemplate = templateObj.ToManaged();
        if (skillTemplate == null) return null;

        object created = Invoke(skillTemplate, "CreateSkill", (object)null);
        if (created == null) return null;

        Invoke(skillContainer, "Add", created);
        existing = FindSkillInContainer(skillContainer, skillId);
        if (existing == null) return null;

        Invoke(existing, "SetEnabled", true);
        SetMember(skillContainer, "UiNeedsUpdate", true);
        Invoke(skillContainer, "ScheduleUpdate");

        if (DebugEnabled())
            DebugMsg($"Runtime synchronized missing skill '{skillId}' directly into SkillContainer.");
        return existing;
    }

    private static bool ActorHasItemTemplate(object actor, string templateId)
    {
        if (actor == null || string.IsNullOrWhiteSpace(templateId)) return false;

        var itemContainer = Invoke(actor, "GetItems")
                         ?? Invoke(Invoke(actor, "GetCurrentProperties"), "GetItems")
                         ?? GetMember(actor, "Items")
                         ?? GetMember(actor, "m_Items");
        if (itemContainer == null) return false;

        var allItems = Invoke(itemContainer, "GetAllItems")
                    ?? GetMember(itemContainer, "Items")
                    ?? GetMember(itemContainer, "m_Items");
        if (allItems == null) return false;

        foreach (var item in EnumerateObjects(allItems))
        {
            if (item == null) continue;
            var itemTemplate = Invoke(item, "GetTemplate")
                            ?? GetMember(item, "Template")
                            ?? GetMember(item, "m_Template");
            string id = GetStringMember(itemTemplate, "m_ID")
                     ?? GetStringMember(itemTemplate, "name")
                     ?? GetStringMember(itemTemplate, "Name");
            if (string.Equals(id, templateId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }


    private static void OnTacticalSkillUsed(IntPtr userPtr, IntPtr skillPtr, IntPtr targetTilePtr)
    {
        if (!SharingEnabled() || userPtr == IntPtr.Zero || skillPtr == IntPtr.Zero)
            return;

        try
        {
            var skillObj = new GameObj(skillPtr);
            var skill = skillObj.ToManaged();
            if (skill == null) return;

            // Keep the last trustworthy ammo state fresh. A squad that just fired is
            // necessarily the active/readable actor, so cache it after MENACE has
            // consumed the shot. This gives the ledger a reliable value if that same
            // squad later becomes an inactive target and reports -1.
            if (IsRegularLimitedSquadWeaponSkill(skill))
                GameState.RunDelayed(2, () => CacheActorAmmoByPtr(userPtr, "weapon-use"));

            string id = Invoke(skill, "GetID")?.ToString();
            if (string.IsNullOrEmpty(id))
                id = GetSkillTemplateId(skill);
            if (!string.Equals(id, ShareSkillId, StringComparison.OrdinalIgnoreCase))
                return;

            if (targetTilePtr == IntPtr.Zero)
            {
                DebugWarning("Redistribute Ammo: share skill had no target tile.");
                return;
            }

            // Resolve the target actor immediately while MENACE still guarantees that
            // targetTilePtr belongs to this skill use.  Alpha.18 kept the tile pointer
            // for two frames and only then asked it for its occupant, which could make
            // the exact same pair behave differently when the skill direction was
            // reversed.  Defer only the mutation, not target resolution.
            var targetTile = new GameObj(targetTilePtr).ToManaged();
            var targetActor = GetEntityFromTile(targetTile);
            var targetActorPtr = GetNativePointer(targetActor);
            if (targetActorPtr == IntPtr.Zero)
            {
                DebugWarning("Redistribute Ammo: target actor could not be resolved at skill-use time.");
                return;
            }

            if (targetActorPtr == userPtr) return;

            if (DebugEnabled())
                DebugMsg($"Redistribute Ammo scheduled: source=0x{userPtr.ToInt64():X}, target=0x{targetActorPtr.ToInt64():X}.");

            // Do not mutate weapon skill state while MENACE is still unwinding its
            // native skill-use callback.  The two stable actor pointers are safe to
            // resolve again a couple of frames later.
            GameState.RunDelayed(2, () => ExecuteDeferredRedistribution(userPtr, targetActorPtr));
        }
        catch (Exception ex)
        {
            DebugWarning($"Redistribute Ammo scheduling failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ExecuteDeferredRedistribution(IntPtr userPtr, IntPtr targetActorPtr)
    {
        try
        {
            if (!SharingEnabled() || userPtr == IntPtr.Zero || targetActorPtr == IntPtr.Zero) return;

            var user = new GameObj(userPtr).ToManaged();
            var target = new GameObj(targetActorPtr).ToManaged();

            if (user == null || target == null)
            {
                DebugWarning("Redistribute Ammo: could not resolve the captured source or target actor after the skill completed.");
                return;
            }

            if (!IsInfantryManaged(user) || !IsInfantryManaged(target))
            {
                if (DebugEnabled()) DebugMsg("Redistribute Ammo: source or target is not infantry; ignored.");
                return;
            }

            if (GetNativePointer(user) == GetNativePointer(target)) return;

            if (!TryRedistributeSquadWeaponAmmo(user, target, userPtr, targetActorPtr, out var summary, out var expectedA, out var expectedB))
            {
                DebugWarning($"Redistribute Ammo: no compatible same-category squad-weapon ammo pools could be read/written safely. " +
                              $"source=0x{userPtr.ToInt64():X} [{DescribeAmmoPools(user)}] | " +
                              $"target=0x{targetActorPtr.ToInt64():X} [{DescribeAmmoPools(target)}]");
                return;
            }

            // SetUses/GetCharges can update the runtime Skill immediately while the tactical HUD
            // still shows its cached charge count. MENACE exposes the intended native refresh path
            // on SkillContainer, so request it rather than calling Update() directly (which other
            // mods may patch). Then verify again a few frames later, after the scheduled refresh had
            // a chance to run.
            RequestSkillUiRefresh(user, "source");
            RequestSkillUiRefresh(target, "target");
            if (expectedA >= 0 || expectedB >= 0)
                GameState.RunDelayed(3, () => VerifyRedistributionAfterRefresh(userPtr, targetActorPtr, expectedA, expectedB));

            DebugMsg($"Redistribute Ammo: {summary}");
        }
        catch (Exception ex)
        {
            DebugWarning($"Redistribute Ammo failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RequestSkillUiRefresh(object actor, string label)
    {
        try
        {
            if (actor == null) return;
            var skills = Invoke(actor, "GetSkills")
                      ?? Invoke(actor, "GetSkillContainer")
                      ?? GetMember(actor, "SkillContainer")
                      ?? GetMember(actor, "Skills");
            if (skills == null)
            {
                if (DebugEnabled()) DebugMsg($"Redistribute Ammo UI refresh: {label} has no SkillContainer.");
                return;
            }

            bool flagSet = SetMember(skills, "UiNeedsUpdate", true)
                        || SetMember(skills, "_UiNeedsUpdate_k__BackingField", true);
            bool scheduled = TryInvokeNoArgVoid(skills, "ScheduleUpdate", out var error);

            if (DebugEnabled())
            {
                DebugMsg($"Redistribute Ammo UI refresh: {label} flag={flagSet}, scheduled={scheduled}" +
                          (scheduled ? string.Empty : $", error={error}"));
            }
        }
        catch (Exception ex)
        {
            DebugWarning($"Redistribute Ammo UI refresh failed safely for {label}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void VerifyRedistributionAfterRefresh(IntPtr userPtr, IntPtr targetActorPtr, int expectedA, int expectedB)
    {
        try
        {
            if (userPtr == IntPtr.Zero || targetActorPtr == IntPtr.Zero) return;
            var user = new GameObj(userPtr).ToManaged();
            var target = new GameObj(targetActorPtr).ToManaged();
            if (user == null || target == null) return;

            var poolsA = GetSquadWeaponPools(user);
            var poolsB = GetSquadWeaponPools(target);
            TrySelectCompatibleAmmoPools(poolsA, poolsB, true, out var a, out var actualA, out var b, out var actualB);

            string detailA = a == null ? "no pool" : DescribeAmmoPool(a);
            string detailB = b == null ? "no pool" : DescribeAmmoPool(b);

            if (a != null && actualA >= 0) CacheAmmoSnapshot(userPtr, a, actualA, "post-refresh");
            if (b != null && actualB >= 0) CacheAmmoSnapshot(targetActorPtr, b, actualB, "post-refresh");

            bool okA = expectedA < 0 || actualA == expectedA;
            bool okB = expectedB < 0 || actualB == expectedB;
            if (a != null && b != null && okA && okB)
            {
                DebugMsg($"Redistribute Ammo post-refresh verify: A={actualA} expected={(expectedA < 0 ? "pending" : expectedA.ToString())} | B={actualB} expected={(expectedB < 0 ? "pending" : expectedB.ToString())} - readable state persisted after SkillContainer refresh.");
                return;
            }

            DebugWarning($"Redistribute Ammo post-refresh verify FAILED: A={actualA} expected={(expectedA < 0 ? "pending" : expectedA.ToString())} [{detailA}] | B={actualB} expected={(expectedB < 0 ? "pending" : expectedB.ToString())} [{detailB}]. MENACE changed or exposed different ammo state after refresh.");
        }
        catch (Exception ex)
        {
            DebugWarning($"Redistribute Ammo post-refresh verification failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DescribeAmmoPool(AmmoPool pool)
    {
        if (pool == null) return "null";
        int current = pool.GetCurrentCharges(out var detail);
        return $"{pool.Key}[{pool.Category}]={current}/{pool.MaxCharges} {{{detail}}}";
    }

    private static void ApplySettingsToTemplates()
    {
        try
        {
            var module = Templates.Find("AccessoryTemplate", ModuleId);
            if (!module.IsNull)
            {
                Templates.WriteField(module, "Rarity", GetInt("Rarity", 20));
                Templates.WriteField(module, "BlackMarketMaxQuantity", GetInt("BlackMarketMaxQuantity", 1));
                Templates.WriteField(module, "MinCampaignProgress", GetInt("MinimumCampaignProgress", 0));
            }

            var vehicleSkill = Templates.Find("SkillTemplate", VehicleSkillId);
            if (!vehicleSkill.IsNull)
            {
                Templates.WriteField(vehicleSkill, "IsLimitedUses", true);
                Templates.WriteField(vehicleSkill, "Uses", 1);
                Templates.WriteField(vehicleSkill, "ActionPointCost", GetInt("VehicleRefillAPCost", 60));
            }

            var shareSkill = Templates.Find("SkillTemplate", ShareSkillId);
            if (!shareSkill.IsNull)
            {
                Templates.WriteField(shareSkill, "ActionPointCost", GetInt("AmmoSharingAPCost", 40));
                Templates.WriteField(shareSkill, "IsHidden", !SharingEnabled());
            }

            if (DebugEnabled())
            {
                DebugMsg($"Settings applied: default vehicle resupply={GetBool("DefaultVehicleResupply", true)}, module extra uses={GetInt("VehicleRefillCharges", 4)}, module cost=fixed 20, market max={GetInt("BlackMarketMaxQuantity", 1)}, rarity={GetInt("Rarity", 20)}, sharing={SharingEnabled()}.");
            }
        }
        catch (Exception ex)
        {
            DebugWarning($"Could not apply one or more settings to templates: {ex.Message}");
        }
    }

    private static void TryGrantTestLoadout()
    {
        if (!GetBool("GrantTestLoadout", false)) return;
        if (_instance == null) return;

        // Inventory.SpawnItem only succeeds while StrategyState/OwnedItems exists.
        // Keep the two grants tracked separately so a temporary failure cannot duplicate
        // whichever item already succeeded on the previous attempt.
        if (!_instance._testModuleGranted)
        {
            if (DebugEnabled())
            {
                var moduleTemplate = Templates.Find("AccessoryTemplate", ModuleId);
                var refillTemplate = Templates.Find("SkillTemplate", VehicleSkillId);
                if (moduleTemplate.IsNull || refillTemplate.IsNull)
                {
                    DebugMsg($"Testing loadout: waiting for templates (module={!moduleTemplate.IsNull}, refillSkill={!refillTemplate.IsNull}).");
                    return;
                }
            }

            string result = Inventory.SpawnItem(ModuleId);
            if (result.StartsWith("Spawned:", StringComparison.OrdinalIgnoreCase))
            {
                _instance._testModuleGranted = true;
                DebugMsg($"Testing loadout: {result}");
            }
            else if (DebugEnabled())
            {
                DebugMsg($"Testing loadout: module not granted yet: {result}");
            }
        }

        if (!_instance._testVehicleGranted)
        {
            string result = Inventory.SpawnItem(TestVehicleId);
            if (result.StartsWith("Spawned:", StringComparison.OrdinalIgnoreCase))
            {
                _instance._testVehicleGranted = true;
                DebugMsg($"Testing loadout: {result}");
            }
            else if (DebugEnabled())
            {
                DebugMsg($"Testing loadout: IFV not granted yet: {result}");
            }
        }

        if (_instance._testModuleGranted && _instance._testVehicleGranted && !_instance._testLoadoutReadyLogged)
        {
            _instance._testLoadoutReadyLogged = true;
            DebugMsg("Testing loadout ready: 1 Field Ammunition Supply and 1 IFV Chassis were added to Owned Items for this game session.");
        }
    }

    private static bool CanRedistributeSquadWeaponAmmo(object actorA, object actorB)
    {
        return TryCalculateRedistribution(actorA, actorB, out _, out _, out _, out _, out _, out _);
    }

    private static bool TryCalculateRedistribution(
        object actorA, object actorB,
        out AmmoPool a, out AmmoPool b,
        out int currentA, out int currentB,
        out int targetA, out int targetB)
    {
        a = null;
        b = null;
        currentA = currentB = targetA = targetB = 0;

        var poolsA = GetSquadWeaponPools(actorA);
        var poolsB = GetSquadWeaponPools(actorB);
        if (poolsA.Count == 0 || poolsB.Count == 0) return false;

        // Only weapons in the same weapon family/category may share ammunition.
        // A single weapon item can still expose several fire-mode skills; the pool
        // selection below chooses the best readable pool within a common category.
        if (!TrySelectCompatibleAmmoPools(poolsA, poolsB, false, out a, out currentA, out b, out currentB))
            return false;
        if (a == null || b == null || currentA < 0 || currentB < 0) return false;

        int total = currentA + currentB;
        targetA = Math.Min(a.MaxCharges, (total + 1) / 2);
        targetB = total - targetA;

        if (targetB > b.MaxCharges)
        {
            int overflow = targetB - b.MaxCharges;
            targetB = b.MaxCharges;
            targetA = Math.Min(a.MaxCharges, targetA + overflow);
        }
        if (targetA > a.MaxCharges)
        {
            int overflow = targetA - a.MaxCharges;
            targetA = a.MaxCharges;
            targetB = Math.Min(b.MaxCharges, targetB + overflow);
        }

        int storable = Math.Min(total, a.MaxCharges + b.MaxCharges);
        if (targetA + targetB != storable)
        {
            targetA = Math.Min(a.MaxCharges, (storable + 1) / 2);
            targetB = Math.Min(b.MaxCharges, storable - targetA);
        }

        return true;
    }

    private static AmmoPool SelectBestAmmoPool(List<AmmoPool> pools, out int current)
    {
        current = -1;
        AmmoPool best = null;
        int bestCurrent = -1;

        foreach (var pool in pools)
        {
            int value = pool.GetCurrentCharges(out _);
            if (value < 0) continue;

            // Prefer a pool that actually contains ammo. Then prefer the larger
            // magazine/capacity and finally the larger current value. This avoids
            // choosing a stale 0-charge alternate fire mode over the real weapon pool.
            bool better = best == null
                       || (value > 0 && bestCurrent == 0)
                       || ((value > 0) == (bestCurrent > 0) && pool.MaxCharges > best.MaxCharges)
                       || ((value > 0) == (bestCurrent > 0) && pool.MaxCharges == best.MaxCharges && value > bestCurrent);
            if (!better) continue;

            best = pool;
            bestCurrent = value;
        }

        current = bestCurrent;
        return best;
    }

    private static AmmoPool SelectBestAmmoPoolAllowUnreadable(List<AmmoPool> pools, out int current)
    {
        var readable = SelectBestAmmoPool(pools, out current);
        if (readable != null) return readable;

        current = -1;
        AmmoPool fallback = null;
        foreach (var pool in pools)
        {
            if (pool == null) continue;
            if (fallback == null || pool.MaxCharges > fallback.MaxCharges)
                fallback = pool;
        }
        return fallback;
    }

    private static bool TrySelectCompatibleAmmoPools(
        List<AmmoPool> poolsA, List<AmmoPool> poolsB, bool allowUnreadable,
        out AmmoPool a, out int currentA, out AmmoPool b, out int currentB)
    {
        a = null;
        b = null;
        currentA = -1;
        currentB = -1;
        if (poolsA == null || poolsB == null || poolsA.Count == 0 || poolsB.Count == 0) return false;

        int bestReadable = -1;
        int bestNonZero = -1;
        int bestCapacity = -1;
        int bestCurrentTotal = -1;

        var categories = poolsA
            .Where(p => p != null && !string.IsNullOrEmpty(p.Category))
            .Select(p => p.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            var candidatesA = poolsA.Where(p => p != null && string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
            var candidatesB = poolsB.Where(p => p != null && string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidatesB.Count == 0) continue;

            int valueA;
            int valueB;
            var candidateA = allowUnreadable
                ? SelectBestAmmoPoolAllowUnreadable(candidatesA, out valueA)
                : SelectBestAmmoPool(candidatesA, out valueA);
            var candidateB = allowUnreadable
                ? SelectBestAmmoPoolAllowUnreadable(candidatesB, out valueB)
                : SelectBestAmmoPool(candidatesB, out valueB);
            if (candidateA == null || candidateB == null) continue;

            int readable = (valueA >= 0 ? 1 : 0) + (valueB >= 0 ? 1 : 0);
            int nonZero = (valueA > 0 ? 1 : 0) + (valueB > 0 ? 1 : 0);
            int capacity = candidateA.MaxCharges + candidateB.MaxCharges;
            int currentTotal = Math.Max(0, valueA) + Math.Max(0, valueB);

            bool better = a == null
                       || readable > bestReadable
                       || (readable == bestReadable && nonZero > bestNonZero)
                       || (readable == bestReadable && nonZero == bestNonZero && capacity > bestCapacity)
                       || (readable == bestReadable && nonZero == bestNonZero && capacity == bestCapacity && currentTotal > bestCurrentTotal);
            if (!better) continue;

            a = candidateA;
            b = candidateB;
            currentA = valueA;
            currentB = valueB;
            bestReadable = readable;
            bestNonZero = nonZero;
            bestCapacity = capacity;
            bestCurrentTotal = currentTotal;
        }

        return a != null && b != null;
    }

    private static bool CollectionContainsSkillTemplate(object collection, string skillId)
    {
        foreach (var item in EnumerateObjects(collection))
        {
            if (item == null) continue;
            string id = GetStringMember(item, "m_ID")
                     ?? GetStringMember(item, "name")
                     ?? GetStringMember(item, "Name");
            if (string.Equals(id, skillId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool TryAddToCollection(object collection, object item, out string failure)
    {
        failure = string.Empty;
        if (collection == null || item == null)
        {
            failure = "collection or item was null";
            return false;
        }

        Exception last = null;
        var methods = collection.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Add" && m.GetParameters().Length == 1)
            .ToArray();

        foreach (var method in methods)
        {
            try
            {
                method.Invoke(collection, new[] { item });
                return true;
            }
            catch (Exception ex)
            {
                last = ex.InnerException ?? ex;
            }
        }

        failure = last == null
            ? $"no Add(T) method found on {collection.GetType().FullName}"
            : $"Add(T) threw {last.GetType().Name}: {last.Message}";
        return false;
    }

    private static string GetUnityObjectName(object obj)
    {
        if (obj == null) return null;
        return GetStringMember(obj, "name")
            ?? GetStringMember(obj, "Name")
            ?? GetStringMember(obj, "m_Name")
            ?? Invoke(obj, "GetName")?.ToString();
    }

    private static bool IsInfantryActor(GameObj actor)
    {
        if (actor.IsNull) return false;
        return IsInfantryManaged(actor.ToManaged());
    }

    private static bool IsInfantryManaged(object actor)
    {
        if (actor == null) return false;

        if (InvokeBool(actor, "IsInfantry")) return true;
        if (InvokeBool(actor, "IsVehicle")) return false;

        var template = Invoke(actor, "GetTemplate")
                    ?? GetMember(actor, "Template")
                    ?? GetMember(actor, "m_Template");
        if (template != null)
        {
            var actorTypeValue = GetMember(template, "ActorType") ?? GetMember(template, "m_ActorType");
            if (actorTypeValue != null)
            {
                try { return Convert.ToInt32(actorTypeValue) == 0; } catch { }
            }
        }

        return false;
    }

    private static bool TryRedistributeSquadWeaponAmmo(
        object actorA, object actorB, IntPtr actorPtrA, IntPtr actorPtrB,
        out string summary, out int expectedA, out int expectedB)
    {
        summary = string.Empty;
        expectedA = -1;
        expectedB = -1;

        var poolsA = GetSquadWeaponPools(actorA);
        var poolsB = GetSquadWeaponPools(actorB);
        if (poolsA.Count == 0 || poolsB.Count == 0) return false;

        if (!TrySelectCompatibleAmmoPools(poolsA, poolsB, true, out var a, out var liveA, out var b, out var liveB))
        {
            if (DebugEnabled())
            {
                string categoriesA = string.Join(",", poolsA.Select(p => p?.Category ?? "unknown").Distinct());
                string categoriesB = string.Join(",", poolsB.Select(p => p?.Category ?? "unknown").Distinct());
                DebugMsg($"Redistribute Ammo: no shared weapon category. source=[{categoriesA}] target=[{categoriesB}]");
            }
            return false;
        }

        bool readableA = liveA >= 0;
        bool readableB = liveB >= 0;

        if (readableA) CacheAmmoSnapshot(actorPtrA, a, liveA, "redistribute-read");
        if (readableB) CacheAmmoSnapshot(actorPtrB, b, liveB, "redistribute-read");

        int currentA = liveA;
        int currentB = liveB;
        bool cachedA = false;
        bool cachedB = false;

        if (currentA < 0 && TryGetAmmoSnapshot(actorPtrA, a.Key, a.MaxCharges, out var snapshotA))
        {
            currentA = snapshotA;
            cachedA = true;
        }
        if (currentB < 0 && TryGetAmmoSnapshot(actorPtrB, b.Key, b.MaxCharges, out var snapshotB))
        {
            currentB = snapshotB;
            cachedB = true;
        }

        // If one actor has never exposed a trustworthy ammo value, do not guess.
        // Remember the redistribution request and retry it after either squad becomes
        // readable. The readable side has already been cached above, so a later
        // selection of the other squad is enough to resolve the pair.
        if (currentA < 0 || currentB < 0)
        {
            QueuePendingRedistribution(actorPtrA, actorPtrB);
            summary = $"queued until both weapon states are known (A={(currentA < 0 ? "unknown" : currentA.ToString())}, B={(currentB < 0 ? "unknown" : currentB.ToString())})";
            return true;
        }

        CalculateBalancedTargets(a, b, currentA, currentB, out var targetA, out var targetB);

        a.GetCurrentCharges(out var detailA);
        b.GetCurrentCharges(out var detailB);
        DebugMsg($"Redistribute Ammo read: category={a.Category}; A={currentA}/{a.MaxCharges}{(cachedA ? " cached" : string.Empty)} [{detailA}] | B={currentB}/{b.MaxCharges}{(cachedB ? " cached" : string.Empty)} [{detailB}]");

        if (targetA == currentA && targetB == currentB)
        {
            summary = $"already balanced at {currentA}/{a.MaxCharges} + {currentB}/{b.MaxCharges}; no ammo moved";
            return true;
        }

        // Fast path: both native weapon states are readable. Keep the atomic writer
        // from alpha.18+ unchanged.
        if (readableA && readableB)
        {
            if (!a.SetCurrentCharges(targetA)) return false;
            if (!b.SetCurrentCharges(targetB))
            {
                if (!a.SetCurrentCharges(currentA))
                    DebugWarning($"Redistribute Ammo: second pool failed and rollback of first pool to {currentA} also failed.");
                else
                    DebugMsg($"Redistribute Ammo: second pool failed; first pool rolled back to {currentA}.");
                return false;
            }

            CacheAmmoSnapshot(actorPtrA, a, targetA, "redistribute-write");
            CacheAmmoSnapshot(actorPtrB, b, targetB, "redistribute-write");
            QueuePendingModeSyncIfNeeded(actorPtrA, a);
            QueuePendingModeSyncIfNeeded(actorPtrB, b);
            expectedA = targetA;
            expectedB = targetB;
            summary = $"{currentA}/{a.MaxCharges} + {currentB}/{b.MaxCharges} -> {targetA}/{a.MaxCharges} + {targetB}/{b.MaxCharges} (total {targetA + targetB})";
            return true;
        }

        int deltaA = targetA - currentA;
        int deltaB = targetB - currentB;

        // Safe deferred-transfer path. We only apply an immediate change when the
        // readable actor is the donor and the unreadable actor is the receiver.
        // Ammo is removed from the donor now and recorded as a positive credit "in
        // transit". This can never duplicate ammo. When the receiver stops reporting
        // -1, the credit is added to its then-current value (not an old absolute value).
        if (readableA && !readableB && deltaA < 0 && deltaB > 0)
        {
            if (!a.SetCurrentCharges(targetA)) return false;
            CacheAmmoSnapshot(actorPtrA, a, targetA, "pending-donor-write");
            QueuePendingModeSyncIfNeeded(actorPtrA, a);
            QueuePendingAmmoCredit(actorPtrB, b.Key, b.MaxCharges, deltaB);
            expectedA = targetA;
            summary = $"{currentA}/{a.MaxCharges} -> {targetA}/{a.MaxCharges}; queued +{deltaB} ammo for target while its weapon reports -1";
            return true;
        }

        if (!readableA && readableB && deltaB < 0 && deltaA > 0)
        {
            if (!b.SetCurrentCharges(targetB)) return false;
            CacheAmmoSnapshot(actorPtrB, b, targetB, "pending-donor-write");
            QueuePendingModeSyncIfNeeded(actorPtrB, b);
            QueuePendingAmmoCredit(actorPtrA, a.Key, a.MaxCharges, deltaA);
            expectedB = targetB;
            summary = $"{currentB}/{b.MaxCharges} -> {targetB}/{b.MaxCharges}; queued +{deltaA} ammo for source while its weapon reports -1";
            return true;
        }

        // The unreadable actor would have to donate (or both sides are unreadable).
        // Never credit ammo before the donor can be debited: keep the whole operation
        // pending instead, preserving total ammunition exactly.
        QueuePendingRedistribution(actorPtrA, actorPtrB);
        summary = $"queued redistribution because the unreadable actor would need to donate (A={currentA}, B={currentB})";
        return true;
    }

    private static void CalculateBalancedTargets(AmmoPool a, AmmoPool b, int currentA, int currentB, out int targetA, out int targetB)
    {
        int total = currentA + currentB;
        targetA = Math.Min(a.MaxCharges, (total + 1) / 2);
        targetB = total - targetA;

        if (targetB > b.MaxCharges)
        {
            int overflow = targetB - b.MaxCharges;
            targetB = b.MaxCharges;
            targetA = Math.Min(a.MaxCharges, targetA + overflow);
        }
        if (targetA > a.MaxCharges)
        {
            int overflow = targetA - a.MaxCharges;
            targetA = a.MaxCharges;
            targetB = Math.Min(b.MaxCharges, targetB + overflow);
        }

        int storable = Math.Min(total, a.MaxCharges + b.MaxCharges);
        if (targetA + targetB != storable)
        {
            targetA = Math.Min(a.MaxCharges, (storable + 1) / 2);
            targetB = Math.Min(b.MaxCharges, storable - targetA);
        }
    }

    private static List<AmmoPool> GetSquadWeaponPools(object actor)
    {
        var result = new List<AmmoPool>();
        var groups = new Dictionary<string, List<object>>();
        var groupMax = new Dictionary<string, int>();
        var groupItems = new Dictionary<string, object>();
        var groupCategories = new Dictionary<string, string>();

        // Alpha.21: start from the actor's real equipped ItemContainer, not from the
        // tactical SkillContainer. MENACE can expose a non-active target's skill
        // views as derived wrappers with GetCharges/GetUses == -1. The equipped
        // Item instances are the stable ownership root for weapon skills.
        var itemContainer = Invoke(actor, "GetItems")
                         ?? Invoke(Invoke(actor, "GetCurrentProperties"), "GetItems")
                         ?? GetMember(actor, "Items")
                         ?? GetMember(actor, "m_Items");

        if (itemContainer != null)
        {
            var allItems = Invoke(itemContainer, "GetAllItems");
            if (allItems != null)
            {
                foreach (var item in EnumerateObjects(allItems))
                {
                    if (item == null) continue;
                    var itemSkills = Invoke(item, "GetSkills")
                                  ?? GetMember(item, "Skills")
                                  ?? GetMember(item, "m_Skills");
                    if (itemSkills == null) continue;

                    foreach (var itemSkill in EnumerateObjects(itemSkills))
                        TryAddAmmoSkill(itemSkill, item, groups, groupMax, groupItems, groupCategories);
                }
            }
        }

        // Merge the actor SkillContainer too. For the active unit these instances
        // are often the most complete/writable views. For non-active units they can
        // be derived; AddUniqueSkill plus the Item grouping keeps both representations
        // available without making the pool depend on which one was enumerated first.
        var skillContainer = Invoke(actor, "GetSkills")
                          ?? Invoke(actor, "GetSkillContainer")
                          ?? GetMember(actor, "SkillContainer")
                          ?? GetMember(actor, "Skills");
        if (skillContainer != null)
        {
            var skillsObj = Invoke(skillContainer, "GetAllSkills")
                         ?? GetMember(skillContainer, "Skills")
                         ?? GetMember(skillContainer, "m_Skills");
            if (skillsObj != null)
            {
                foreach (var entry in EnumerateObjects(skillsObj))
                {
                    if (entry == null) continue;
                    TryAddAmmoSkill(entry, GetSkillItemObject(entry), groups, groupMax, groupItems, groupCategories);
                }
            }
        }

        // One final expansion from every concrete Item we discovered. This matters
        // when the SkillContainer contributed only a derived view but GetItem()
        // still led us to the equipped weapon object.
        foreach (var pair in groupItems.ToList())
        {
            if (!groups.TryGetValue(pair.Key, out var list) || pair.Value == null) continue;
            var itemSkills = Invoke(pair.Value, "GetSkills")
                          ?? GetMember(pair.Value, "Skills")
                          ?? GetMember(pair.Value, "m_Skills");
            if (itemSkills == null) continue;

            foreach (var itemSkill in EnumerateObjects(itemSkills))
            {
                if (itemSkill == null) continue;
                TryAddAmmoSkill(itemSkill, pair.Value, groups, groupMax, groupItems, groupCategories);
            }
        }

        foreach (var pair in groups)
        {
            int max = groupMax.TryGetValue(pair.Key, out var value) ? value : 0;
            string category = groupCategories.TryGetValue(pair.Key, out var family) ? family : "squad_weapon";
            if (max > 0) result.Add(new AmmoPool(pair.Key, pair.Value, max, category));
        }

        return result;
    }

    private static bool TryAddAmmoSkill(
        object skill,
        object explicitItem,
        Dictionary<string, List<object>> groups,
        Dictionary<string, int> groupMax,
        Dictionary<string, object> groupItems,
        Dictionary<string, string> groupCategories)
    {
        if (skill == null) return false;

        var template = Invoke(skill, "GetTemplate")
                    ?? GetMember(skill, "Template")
                    ?? GetMember(skill, "m_Template");
        if (template == null) return false;

        if (!TemplateHasTag(template, "squad_weapon")) return false;
        if (TemplateHasTag(template, "special_weapon")) return false;
        if (TemplateHasTag(template, "accessory")) return false;

        int uses = GetIntMember(template, "Uses", 0);
        bool limited = GetBoolMember(template, "IsLimitedUses", uses > 0)
                    || GetBoolMember(template, "HasLimitedUses", false);
        if (!limited || uses <= 0) return false;

        string skillId = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill) ?? "unknown";
        var item = explicitItem ?? GetSkillItemObject(skill);
        string itemKey = GetItemIdentity(item);
        string family = GetWeaponFamily(template);
        string key = !string.IsNullOrEmpty(itemKey)
            ? itemKey
            : $"family:{family}:uses:{uses}";

        if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<object>();
        AddUniqueSkill(list, skill);
        if (item != null && !groupItems.ContainsKey(key)) groupItems[key] = item;
        groupMax[key] = Math.Max(groupMax.TryGetValue(key, out var oldMax) ? oldMax : 0, uses);
        if (!groupCategories.TryGetValue(key, out var existingCategory)
            || string.Equals(existingCategory, "squad_weapon", StringComparison.OrdinalIgnoreCase))
        {
            groupCategories[key] = family;
        }

        return true;
    }

    private static void AddUniqueSkill(List<object> list, object skill)
    {
        if (list == null || skill == null) return;
        var ptr = GetNativePointer(skill);
        foreach (var existing in list)
        {
            if (ReferenceEquals(existing, skill)) return;
            if (ptr != IntPtr.Zero && GetNativePointer(existing) == ptr) return;
        }
        list.Add(skill);
    }

    private static string GetWeaponFamily(object template)
    {
        string[] families =
        {
            "assault_rifle", "battle_rifle", "smg", "shotgun", "pdw",
            "carbine", "machine_gun", "rifle", "anti_infantry"
        };
        foreach (var family in families)
            if (TemplateHasTag(template, family)) return family;

        // A small number of vanilla squad-weapon fire modes omit the family tag even
        // though the template ID still contains it (for example a PDW burst mode).
        // Use the ID only as a secondary classification source; never use caliber.
        string id = GetStringMember(template, "m_ID")
                 ?? GetStringMember(template, "ID")
                 ?? GetStringMember(template, "Name")
                 ?? string.Empty;
        foreach (var family in families)
            if (id.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0) return family;

        return "other_squad_weapon";
    }

    private static bool IsRegularLimitedSquadWeaponSkill(object skill)
    {
        if (skill == null) return false;
        var template = Invoke(skill, "GetTemplate")
                    ?? GetMember(skill, "Template")
                    ?? GetMember(skill, "m_Template");
        if (template == null) return false;
        if (!TemplateHasTag(template, "squad_weapon")) return false;
        if (TemplateHasTag(template, "special_weapon") || TemplateHasTag(template, "accessory")) return false;

        int uses = GetIntMember(template, "Uses", 0);
        bool limited = GetBoolMember(template, "IsLimitedUses", uses > 0)
                    || GetBoolMember(template, "HasLimitedUses", false);
        return limited && uses > 0;
    }

    private static string AmmoLedgerKey(IntPtr actorPtr, string poolKey)
    {
        return actorPtr.ToInt64().ToString("X") + "|" + (poolKey ?? "unknown");
    }

    private static string PendingPairKey(IntPtr sourcePtr, IntPtr targetPtr)
    {
        return sourcePtr.ToInt64().ToString("X") + ">" + targetPtr.ToInt64().ToString("X");
    }

    private static void CacheAmmoSnapshot(IntPtr actorPtr, AmmoPool pool, int current, string reason)
    {
        if (actorPtr == IntPtr.Zero || pool == null || current < 0 || current > pool.MaxCharges) return;
        string key = AmmoLedgerKey(actorPtr, pool.Key);
        _ammoSnapshots[key] = new AmmoSnapshot(actorPtr, pool.Key, pool.MaxCharges, current);
    }

    private static bool TryGetAmmoSnapshot(IntPtr actorPtr, string poolKey, int maxCharges, out int current)
    {
        current = -1;
        if (actorPtr == IntPtr.Zero || string.IsNullOrEmpty(poolKey)) return false;
        if (!_ammoSnapshots.TryGetValue(AmmoLedgerKey(actorPtr, poolKey), out var snapshot)) return false;
        if (snapshot == null || snapshot.MaxCharges != maxCharges || snapshot.Current < 0 || snapshot.Current > maxCharges) return false;
        current = snapshot.Current;
        return true;
    }

    private static void CacheActorAmmoByPtr(IntPtr actorPtr, string reason)
    {
        if (!SharingEnabled() || actorPtr == IntPtr.Zero) return;
        try
        {
            var actor = new GameObj(actorPtr).ToManaged();
            if (actor == null || !IsInfantryManaged(actor)) return;
            foreach (var pool in GetSquadWeaponPools(actor))
            {
                int current = pool.GetCurrentCharges(out _);
                if (current >= 0) CacheAmmoSnapshot(actorPtr, pool, current, reason);
            }
        }
        catch (Exception ex)
        {
            if (DebugEnabled())
                DebugMsg($"Redistribute Ammo cache skipped safely for actor=0x{actorPtr.ToInt64():X}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void QueuePendingAmmoCredit(IntPtr actorPtr, string poolKey, int maxCharges, int delta)
    {
        if (actorPtr == IntPtr.Zero || string.IsNullOrEmpty(poolKey) || delta <= 0) return;
        string key = AmmoLedgerKey(actorPtr, poolKey);
        if (_pendingAmmoCredits.TryGetValue(key, out var existing))
        {
            existing.Delta += delta;
            DebugMsg($"Redistribute Ammo pending credit increased: actor=0x{actorPtr.ToInt64():X}, pool={poolKey}, pending=+{existing.Delta}.");
            return;
        }

        _pendingAmmoCredits[key] = new PendingAmmoCredit(actorPtr, poolKey, maxCharges, delta);
        DebugMsg($"Redistribute Ammo pending credit queued: actor=0x{actorPtr.ToInt64():X}, pool={poolKey}, pending=+{delta}.");
    }

    private static void QueuePendingRedistribution(IntPtr sourcePtr, IntPtr targetPtr)
    {
        if (sourcePtr == IntPtr.Zero || targetPtr == IntPtr.Zero || sourcePtr == targetPtr) return;
        string key = PendingPairKey(sourcePtr, targetPtr);
        if (_pendingRedistributions.ContainsKey(key)) return;
        _pendingRedistributions[key] = new PendingRedistribution(sourcePtr, targetPtr);
        DebugMsg($"Redistribute Ammo pending operation queued: source=0x{sourcePtr.ToInt64():X}, target=0x{targetPtr.ToInt64():X}.");
    }

    private static AmmoPool FindPendingPool(object actor, string poolKey, int maxCharges)
    {
        var pools = GetSquadWeaponPools(actor);
        foreach (var pool in pools)
            if (string.Equals(pool.Key, poolKey, StringComparison.Ordinal)) return pool;

        var sameCapacity = pools.Where(p => p != null && p.MaxCharges == maxCharges).ToList();
        return sameCapacity.Count == 1 ? sameCapacity[0] : null;
    }

    private static void QueuePendingModeSyncIfNeeded(IntPtr actorPtr, AmmoPool pool)
    {
        if (actorPtr == IntPtr.Zero || pool == null || !pool.HasUnresolvedSiblingModes()) return;
        string key = AmmoLedgerKey(actorPtr, pool.Key);
        if (_pendingModeSyncs.ContainsKey(key)) return;
        _pendingModeSyncs[key] = new PendingModeSync(actorPtr, pool.Key, pool.MaxCharges);
        DebugMsg($"Redistribute Ammo mode-sync queued: actor=0x{actorPtr.ToInt64():X}, pool={pool.Key}; one or more sibling fire modes are currently unresolved.");
    }

    private static void ProcessPendingAmmoWork()
    {
        if (!SharingEnabled()) return;
        ProcessPendingAmmoCredits();
        ProcessPendingRedistributions();
        ProcessPendingModeSyncs();
    }

    private static void ProcessPendingAmmoCredits()
    {
        if (_pendingAmmoCredits.Count == 0) return;

        foreach (var pair in _pendingAmmoCredits.ToList())
        {
            var pending = pair.Value;
            if (pending == null || pending.ActorPtr == IntPtr.Zero || pending.Delta <= 0)
            {
                _pendingAmmoCredits.Remove(pair.Key);
                continue;
            }

            try
            {
                var actor = new GameObj(pending.ActorPtr).ToManaged();
                if (actor == null) continue;
                var pool = FindPendingPool(actor, pending.PoolKey, pending.MaxCharges);
                if (pool == null) continue;

                int current = pool.GetCurrentCharges(out _);
                if (current < 0) continue;

                CacheAmmoSnapshot(pending.ActorPtr, pool, current, "pending-credit-readable");
                int target = Math.Min(pool.MaxCharges, current + pending.Delta);
                int applied = target - current;
                if (applied <= 0) continue;

                if (!pool.SetCurrentCharges(target)) continue;

                CacheAmmoSnapshot(pending.ActorPtr, pool, target, "pending-credit-applied");
                QueuePendingModeSyncIfNeeded(pending.ActorPtr, pool);
                pending.Delta -= applied;
                RequestSkillUiRefresh(actor, "pending-credit");

                if (pending.Delta <= 0)
                {
                    _pendingAmmoCredits.Remove(pair.Key);
                    DebugMsg($"Redistribute Ammo pending credit applied: actor=0x{pending.ActorPtr.ToInt64():X}, pool={pool.Key}, {current}->{target}; credit complete.");
                }
                else
                {
                    DebugMsg($"Redistribute Ammo pending credit partially applied: actor=0x{pending.ActorPtr.ToInt64():X}, pool={pool.Key}, {current}->{target}; remaining=+{pending.Delta}.");
                }
            }
            catch (Exception ex)
            {
                if (DebugEnabled())
                    DebugMsg($"Redistribute Ammo pending credit retry skipped safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ProcessPendingRedistributions()
    {
        if (_pendingRedistributions.Count == 0) return;

        foreach (var pair in _pendingRedistributions.ToList())
        {
            var pending = pair.Value;
            if (pending == null || pending.SourcePtr == IntPtr.Zero || pending.TargetPtr == IntPtr.Zero)
            {
                _pendingRedistributions.Remove(pair.Key);
                continue;
            }

            try
            {
                var source = new GameObj(pending.SourcePtr).ToManaged();
                var target = new GameObj(pending.TargetPtr).ToManaged();
                if (source == null || target == null) continue;

                if (!TryRedistributeSquadWeaponAmmo(source, target, pending.SourcePtr, pending.TargetPtr,
                        out var summary, out var expectedA, out var expectedB))
                    continue;

                bool stillPending = summary.StartsWith("queued until", StringComparison.Ordinal)
                                 || summary.StartsWith("queued redistribution", StringComparison.Ordinal);
                if (stillPending) continue;

                _pendingRedistributions.Remove(pair.Key);
                RequestSkillUiRefresh(source, "pending-source");
                RequestSkillUiRefresh(target, "pending-target");
                if (expectedA >= 0 || expectedB >= 0)
                    GameState.RunDelayed(3, () => VerifyRedistributionAfterRefresh(pending.SourcePtr, pending.TargetPtr, expectedA, expectedB));
                DebugMsg($"Redistribute Ammo pending operation resolved: {summary}");
            }
            catch (Exception ex)
            {
                if (DebugEnabled())
                    DebugMsg($"Redistribute Ammo pending operation retry skipped safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ProcessPendingModeSyncs()
    {
        if (_pendingModeSyncs.Count == 0) return;

        foreach (var pair in _pendingModeSyncs.ToList())
        {
            var pending = pair.Value;
            if (pending == null || pending.ActorPtr == IntPtr.Zero)
            {
                _pendingModeSyncs.Remove(pair.Key);
                continue;
            }

            try
            {
                var actor = new GameObj(pending.ActorPtr).ToManaged();
                if (actor == null) continue;
                var pool = FindPendingPool(actor, pending.PoolKey, pending.MaxCharges);
                if (pool == null) continue;

                int current = pool.GetCurrentCharges(out _);
                if (current < 0) continue;

                bool complete = pool.TrySynchronizeSiblingModes(current, out var detail);
                RequestSkillUiRefresh(actor, "mode-sync");
                CacheAmmoSnapshot(pending.ActorPtr, pool, current, "mode-sync");

                if (complete)
                {
                    _pendingModeSyncs.Remove(pair.Key);
                    DebugMsg($"Redistribute Ammo mode-sync complete: actor=0x{pending.ActorPtr.ToInt64():X}, pool={pool.Key}, ammo={current}/{pool.MaxCharges}; {detail}");
                }
                else if (DebugEnabled())
                {
                    DebugMsg($"Redistribute Ammo mode-sync still pending: actor=0x{pending.ActorPtr.ToInt64():X}, pool={pool.Key}, authoritative={current}/{pool.MaxCharges}; {detail}");
                }
            }
            catch (Exception ex)
            {
                if (DebugEnabled())
                    DebugMsg($"Redistribute Ammo mode-sync retry skipped safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private sealed class AmmoSnapshot
    {
        public IntPtr ActorPtr { get; }
        public string PoolKey { get; }
        public int MaxCharges { get; }
        public int Current { get; }

        public AmmoSnapshot(IntPtr actorPtr, string poolKey, int maxCharges, int current)
        {
            ActorPtr = actorPtr;
            PoolKey = poolKey;
            MaxCharges = maxCharges;
            Current = current;
        }
    }

    private sealed class PendingAmmoCredit
    {
        public IntPtr ActorPtr { get; }
        public string PoolKey { get; }
        public int MaxCharges { get; }
        public int Delta { get; set; }

        public PendingAmmoCredit(IntPtr actorPtr, string poolKey, int maxCharges, int delta)
        {
            ActorPtr = actorPtr;
            PoolKey = poolKey;
            MaxCharges = maxCharges;
            Delta = delta;
        }
    }

    private sealed class PendingRedistribution
    {
        public IntPtr SourcePtr { get; }
        public IntPtr TargetPtr { get; }

        public PendingRedistribution(IntPtr sourcePtr, IntPtr targetPtr)
        {
            SourcePtr = sourcePtr;
            TargetPtr = targetPtr;
        }
    }

    private sealed class PendingModeSync
    {
        public IntPtr ActorPtr { get; }
        public string PoolKey { get; }
        public int MaxCharges { get; }

        public PendingModeSync(IntPtr actorPtr, string poolKey, int maxCharges)
        {
            ActorPtr = actorPtr;
            PoolKey = poolKey;
            MaxCharges = maxCharges;
        }
    }

    private static string DescribeAmmoPools(object actor)
    {
        try
        {
            var pools = GetSquadWeaponPools(actor);
            if (pools.Count == 0) return "pools=0";

            var parts = new List<string>();
            foreach (var pool in pools)
            {
                int current = pool.GetCurrentCharges(out var detail);
                parts.Add($"{pool.Key}={current}/{pool.MaxCharges} {{{detail}}}");
            }
            return $"pools={pools.Count}: " + string.Join("; ", parts);
        }
        catch (Exception ex)
        {
            return $"pool diagnostics failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private sealed class AmmoPool
    {
        private readonly List<object> _skills;
        public string Key { get; }
        public int MaxCharges { get; }
        public string Category { get; }

        public AmmoPool(string key, List<object> skills, int maxCharges, string category)
        {
            Key = key;
            _skills = skills;
            MaxCharges = maxCharges;
            Category = string.IsNullOrEmpty(category) ? "squad_weapon" : category;
        }

        public int GetCurrentCharges() => GetCurrentCharges(out _);

        public int GetCurrentCharges(out string detail)
        {
            var readings = new List<string>();
            int bestWritable = -1;
            int bestFallback = -1;

            foreach (var skill in _skills)
            {
                string id = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill) ?? "unknown";
                int charges = ReadSkillCharges(skill);
                int uses = ReadIntMethod(skill, "GetUses", -1);
                bool writable = HasIntMethod(skill, "SetUses");

                // For the currently active unit GetCharges() is authoritative. For a
                // non-active target MENACE 0.7.14 can expose GetCharges() == -1 even
                // though the weapon Item still has valid remaining uses. GetUses()
                // on the concrete Item-owned Skill is therefore the native fallback.
                int value = (charges >= 0 && charges <= MaxCharges)
                    ? charges
                    : ((uses >= 0 && uses <= MaxCharges) ? uses : -1);

                readings.Add($"{id}={value}[charges={charges},uses={uses}{(writable ? ",w" : ",derived")}]");

                if (value < 0 || value > MaxCharges) continue;
                if (writable) bestWritable = Math.Max(bestWritable, value);
                bestFallback = Math.Max(bestFallback, value);
            }

            detail = string.Join(", ", readings);
            return bestWritable >= 0 ? bestWritable : bestFallback;
        }

        private int ReadSkillCharges(object skill)
        {
            var value = Invoke(skill, "GetCharges")
                     ?? GetMember(skill, "Charges")
                     ?? GetMember(skill, "m_Charges");
            if (value != null)
            {
                try { return Convert.ToInt32(value); } catch { }
            }
            return -1;
        }

        public bool HasUnresolvedSiblingModes()
        {
            foreach (var skill in _skills)
            {
                int charges = ReadSkillCharges(skill);
                int uses = ReadIntMethod(skill, "GetUses", -1);
                int value = (charges >= 0 && charges <= MaxCharges)
                    ? charges
                    : ((uses >= 0 && uses <= MaxCharges) ? uses : -1);
                if (value < 0) return true;
            }
            return false;
        }

        public bool TrySynchronizeSiblingModes(int authoritative, out string detail)
        {
            authoritative = Math.Max(0, Math.Min(MaxCharges, authoritative));
            var diagnostics = new List<string>();
            var writable = new List<object>();

            foreach (var skill in _skills)
            {
                string id = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill) ?? "unknown";
                int charges = ReadSkillCharges(skill);
                int uses = ReadIntMethod(skill, "GetUses", -1);
                bool canWrite = HasIntMethod(skill, "SetUses");

                if (!canWrite)
                {
                    diagnostics.Add($"{id}: read-only charges={charges},uses={uses}");
                    continue;
                }

                writable.Add(skill);
                if (charges != authoritative || uses != authoritative)
                {
                    if (!TryInvokeIntVoid(skill, "SetUses", authoritative, out var error))
                    {
                        diagnostics.Add($"{id}: SetUses({authoritative}) failed: {error}");
                        detail = string.Join("; ", diagnostics);
                        return false;
                    }
                    diagnostics.Add($"{id}: forced to {authoritative}");
                }
                else
                {
                    diagnostics.Add($"{id}: already {authoritative}");
                }
            }

            foreach (var skill in writable)
                TryInvokeNoArgVoid(skill, "SynchronizeUses", out _);

            bool unresolved = false;
            bool mismatch = false;
            foreach (var skill in _skills)
            {
                string id = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill) ?? "unknown";
                int charges = ReadSkillCharges(skill);
                int uses = ReadIntMethod(skill, "GetUses", -1);
                int value = (charges >= 0 && charges <= MaxCharges)
                    ? charges
                    : ((uses >= 0 && uses <= MaxCharges) ? uses : -1);
                if (value < 0)
                {
                    unresolved = true;
                    diagnostics.Add($"{id}: still unresolved");
                }
                else if (value != authoritative)
                {
                    mismatch = true;
                    diagnostics.Add($"{id}: still {value}, expected {authoritative}");
                }
            }

            detail = string.Join("; ", diagnostics);
            return !unresolved && !mismatch;
        }

        public bool SetCurrentCharges(int value)
        {
            int before = GetCurrentCharges(out var beforeDetail);
            if (before < 0)
            {
                DebugWarning($"Redistribute Ammo: could not determine current charges before native SetUses. [{beforeDetail}]");
                return false;
            }

            value = Math.Max(0, Math.Min(MaxCharges, value));
            if (before == value)
            {
                DebugMsg($"Redistribute Ammo write: no change required ({before}/{MaxCharges}).");
                return true;
            }

            // Only genuine Skill fire modes expose SetUses(int). Some alternate
            // modes (observed with 7.62 sustained fire in 0.7.14) belong to the same
            // weapon item but are derived BaseSkill variants: GetCharges() can be -1
            // and SetUses(int) does not exist. They must not make the whole pool fail.
            var originalUses = new List<(object Skill, int Uses)>();
            var writtenSkills = new List<object>();
            var diagnostics = new List<string>();

            foreach (var skill in _skills)
            {
                string id = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill) ?? "unknown";
                int usesBefore = ReadIntMethod(skill, "GetUses", -1);
                int chargesBefore = ReadIntMethod(skill, "GetCharges", -1);

                if (!HasIntMethod(skill, "SetUses"))
                {
                    diagnostics.Add($"{id}: derived/read-only (uses={usesBefore}, charges={chargesBefore})");
                    continue;
                }

                if (!TryInvokeIntVoid(skill, "SetUses", value, out var setError))
                {
                    DebugWarning($"Redistribute Ammo: native SetUses({value}) threw on writable mode {id}: {setError}");
                    RestorePoolUses(originalUses);
                    return false;
                }

                originalUses.Add((skill, usesBefore));
                writtenSkills.Add(skill);

                int usesImmediate = ReadIntMethod(skill, "GetUses", -1);
                int chargesImmediate = ReadIntMethod(skill, "GetCharges", -1);
                diagnostics.Add($"{id}: uses {usesBefore}->{usesImmediate}, charges {chargesBefore}->{chargesImmediate}");
            }

            if (writtenSkills.Count == 0)
            {
                DebugWarning($"Redistribute Ammo: pool has no writable Skill.SetUses(int) fire mode. [{beforeDetail}]");
                return false;
            }

            // Synchronize from the real writable mode(s). Derived sibling modes may
            // not expose SynchronizeUses either; that is harmless and they are not
            // required for verification.
            foreach (var skill in writtenSkills)
            {
                if (!TryInvokeNoArgVoid(skill, "SynchronizeUses", out var syncError) && DebugEnabled())
                {
                    string id = Invoke(skill, "GetID")?.ToString() ?? GetSkillTemplateId(skill) ?? "unknown";
                    DebugMsg($"Redistribute Ammo: SynchronizeUses warning on {id}: {syncError}");
                }
            }

            int after = GetCurrentCharges(out var afterDetail);
            if (after == value)
            {
                DebugMsg($"Redistribute Ammo write: {before}->{after} via writable Skill.SetUses({value}) + SynchronizeUses; {string.Join("; ", diagnostics)}; before [{beforeDetail}] after [{afterDetail}]");
                return true;
            }

            DebugWarning($"Redistribute Ammo: SetUses expected {value} but authoritative GetCharges reads {after}; {string.Join("; ", diagnostics)}; before [{beforeDetail}] after [{afterDetail}]. Rolling back pool.");
            RestorePoolUses(originalUses);
            return false;
        }

        private static void RestorePoolUses(List<(object Skill, int Uses)> originals)
        {
            foreach (var entry in originals)
            {
                if (entry.Skill == null || entry.Uses < 0) continue;
                TryInvokeIntVoid(entry.Skill, "SetUses", entry.Uses, out _);
            }
            foreach (var entry in originals)
            {
                if (entry.Skill == null) continue;
                TryInvokeNoArgVoid(entry.Skill, "SynchronizeUses", out _);
            }
        }

    }


    private static object GetSkillItemObject(object skill)
    {
        if (skill == null) return null;
        return Invoke(skill, "GetItem")
            ?? GetMember(skill, "m_Item")
            ?? GetMember(skill, "Item");
    }

    private static string GetItemIdentity(object item)
    {
        var itemPtr = GetNativePointer(item);
        if (itemPtr != IntPtr.Zero) return "item:" + itemPtr.ToInt64().ToString("X");
        return null;
    }

    private static string GetSkillItemIdentity(object skill)
    {
        // Retained for diagnostics and callers that only need the grouping key.
        return GetItemIdentity(GetSkillItemObject(skill));
    }

    private static bool TemplateHasTag(object template, string tag)
    {
        var tags = GetMember(template, "Tags") ?? GetMember(template, "m_Tags");
        if (tags == null) return false;
        foreach (var value in EnumerateObjects(tags))
        {
            if (value == null) continue;
            string tagName = GetStringMember(value, "name")
                          ?? GetStringMember(value, "m_ID")
                          ?? GetStringMember(value, "Name")
                          ?? value.ToString();
            if (string.Equals(tagName, tag, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static IEnumerable<object> EnumerateObjects(object collection)
    {
        if (collection == null) yield break;

        if (collection is IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item;
            yield break;
        }

        object enumerator = null;
        try { enumerator = collection.GetType().GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance)?.Invoke(collection, null); }
        catch { }
        if (enumerator == null) yield break;

        var moveNext = enumerator.GetType().GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance);
        var current = enumerator.GetType().GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
        if (moveNext == null || current == null) yield break;

        while (true)
        {
            bool more;
            try { more = Convert.ToBoolean(moveNext.Invoke(enumerator, null)); } catch { yield break; }
            if (!more) yield break;
            object item = null;
            try { item = current.GetValue(enumerator); } catch { }
            yield return item;
        }
    }

    private static Il2CppMenace.Tactical.Skills.Skill TryAsConcreteSkill(object skill)
    {
        if (skill == null) return null;
        if (skill is Il2CppMenace.Tactical.Skills.Skill concrete) return concrete;

        if (skill is Il2CppObjectBase il2cppObj)
        {
            try { return il2cppObj.TryCast<Il2CppMenace.Tactical.Skills.Skill>(); }
            catch { }
        }

        return null;
    }

    private static IntPtr GetNativePointer(object obj)
    {
        if (obj == null) return IntPtr.Zero;
        if (obj is GameObj gameObj) return gameObj.Pointer;
        if (obj is Il2CppObjectBase il2cppObj) return il2cppObj.Pointer;
        var pointer = GetMember(obj, "Pointer");
        if (pointer is IntPtr ip) return ip;
        return IntPtr.Zero;
    }

    private static object GetEntityFromTile(object tile)
    {
        if (tile == null) return null;
        return Invoke(tile, "GetEntity")
            ?? Invoke(tile, "GetActor")
            ?? Invoke(tile, "GetOccupant")
            ?? GetMember(tile, "Entity")
            ?? GetMember(tile, "Actor")
            ?? GetMember(tile, "m_Entity")
            ?? GetMember(tile, "m_Actor");
    }

    private static string GetSkillTemplateId(object skill)
    {
        var template = GetMember(skill, "Template");
        if (template == null) return null;
        return GetStringMember(template, "name")
            ?? GetStringMember(template, "m_ID")
            ?? GetStringMember(template, "Name");
    }

    private static bool SharingEnabled() => GetBool("EnableAmmoSharing", false);
    private static bool DebugEnabled() => GetBool("DebugLogging", false);

    private static void DebugMsg(string message)
    {
        if (DebugEnabled()) _log?.Msg(message);
    }

    private static void DebugWarning(string message)
    {
        if (DebugEnabled()) _log?.Warning(message);
    }

    private static int GetInt(string key, int fallback)
    {
        try { return ModSettings.Get<int>(ModName, key); } catch { return fallback; }
    }

    private static bool GetBool(string key, bool fallback)
    {
        try { return ModSettings.Get<bool>(ModName, key); } catch { return fallback; }
    }

    private static object Invoke(object obj, string method, params object[] args)
    {
        if (obj == null) return null;
        try
        {
            var methods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == method && m.GetParameters().Length == args.Length);
            foreach (var m in methods)
            {
                try { return m.Invoke(obj, args); } catch { }
            }
        }
        catch { }
        return null;
    }

    private static bool InvokeBool(object obj, string method)
    {
        var value = Invoke(obj, method);
        if (value == null) return false;
        try { return Convert.ToBoolean(value); } catch { return false; }
    }

    private static object GetMember(object obj, string name)
    {
        if (obj == null) return null;
        try
        {
            var type = obj.GetType();
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(obj);
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(obj);
        }
        catch { }
        return null;
    }

    private static bool SetMember(object obj, string name, object value)
    {
        if (obj == null) return false;
        try
        {
            var type = obj.GetType();
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                return true;
            }
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(obj, Convert.ChangeType(value, field.FieldType));
                return true;
            }
        }
        catch { }
        return false;
    }

    private static int ReadIntMethod(object obj, string methodName, int fallback)
    {
        var value = Invoke(obj, methodName);
        if (value == null) return fallback;
        try { return Convert.ToInt32(value); } catch { return fallback; }
    }

    private static bool HasIntMethod(object obj, string methodName)
    {
        if (obj == null) return false;
        try
        {
            return obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(m => m.Name == methodName && m.GetParameters().Length == 1 &&
                          (m.GetParameters()[0].ParameterType == typeof(int) ||
                           m.GetParameters()[0].ParameterType == typeof(System.Int32)));
        }
        catch { return false; }
    }

    private static bool TryInvokeIntVoid(object obj, string methodName, int value, out string error)
    {
        error = string.Empty;
        if (obj == null)
        {
            error = "object is null";
            return false;
        }

        try
        {
            var methods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == methodName && m.GetParameters().Length == 1);
            Exception last = null;
            foreach (var method in methods)
            {
                try
                {
                    var parameterType = method.GetParameters()[0].ParameterType;
                    method.Invoke(obj, new[] { Convert.ChangeType(value, parameterType) });
                    return true;
                }
                catch (Exception ex)
                {
                    last = ex.InnerException ?? ex;
                }
            }

            error = last == null ? $"method {methodName}(int) not found" : $"{last.GetType().Name}: {last.Message}";
            return false;
        }
        catch (Exception ex)
        {
            var real = ex.InnerException ?? ex;
            error = $"{real.GetType().Name}: {real.Message}";
            return false;
        }
    }

    private static bool TryInvokeNoArgVoid(object obj, string methodName, out string error)
    {
        error = string.Empty;
        if (obj == null)
        {
            error = "object is null";
            return false;
        }

        try
        {
            var methods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == methodName && m.GetParameters().Length == 0);
            Exception last = null;
            foreach (var method in methods)
            {
                try
                {
                    method.Invoke(obj, null);
                    return true;
                }
                catch (Exception ex)
                {
                    last = ex.InnerException ?? ex;
                }
            }

            error = last == null ? $"method {methodName}() not found" : $"{last.GetType().Name}: {last.Message}";
            return false;
        }
        catch (Exception ex)
        {
            var real = ex.InnerException ?? ex;
            error = $"{real.GetType().Name}: {real.Message}";
            return false;
        }
    }

    private static bool TryInvokeIntSetter(object obj, string methodName, int value)
    {
        if (obj == null) return false;
        try
        {
            var methods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == methodName && m.GetParameters().Length == 1);
            foreach (var method in methods)
            {
                var p = method.GetParameters()[0].ParameterType;
                try
                {
                    method.Invoke(obj, new[] { Convert.ChangeType(value, p) });
                    return true;
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private static int GetIntMember(object obj, string name, int fallback)
    {
        var value = GetMember(obj, name);
        if (value == null) return fallback;
        try { return Convert.ToInt32(value); } catch { return fallback; }
    }

    private static bool GetBoolMember(object obj, string name, bool fallback)
    {
        var value = GetMember(obj, name);
        if (value == null) return fallback;
        try { return Convert.ToBoolean(value); } catch { return fallback; }
    }

    private static string GetStringMember(object obj, string name)
    {
        var value = GetMember(obj, name);
        return value?.ToString();
    }
}
