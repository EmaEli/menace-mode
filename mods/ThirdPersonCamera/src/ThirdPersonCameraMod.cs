using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.States;
using Il2CppMenace.UI;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(MenaceThirdPersonCamera.ThirdPersonCameraMod),
    "Cinematic Action Camera", "1.0.0", "MENACE Community")]

namespace MenaceThirdPersonCamera
{
    // Camera-only. No changes to units, AP, AI, weapons, pathfinding or combat outcomes.
    public sealed class ThirdPersonCameraMod : MelonMod
    {
        private const string SettingsCategory = "MenaceThirdPersonCamera";
        private const string MenuName = "Cinematic Action Camera";
        private const float TransitionSpeed = 8f;

        private enum EventKind { Movement = 0, Firing = 1 }
        // An IL2CPP callback can fail on a transient/destroyed entity. One bad
        // event must not permanently switch off all cinematic cameras.
        private enum AutoHook { Movement, MovementFinished, AttackStarted }

        private static ThirdPersonCameraMod s_instance;
        // MEN-006 / 0.3.4 compatibility contract, retained from PR #16.
        public static bool IsCinematicViewActive => s_instance != null && s_instance._active;

        // Read-only migration bridge. The historical MelonPreferences category and all
        // advanced entries remain unchanged; only the seven menu values are seeded once.
        public static Dictionary<string, object> GetLegacySettingsValues()
        {
            var c = s_instance;
            if (c == null || c._orbitSensitivity == null) return null;
            return new Dictionary<string, object>
            {
                ["AutomaticMode"] = c._automaticMode.Value,
                ["MovementChance"] = c._movementChance.Value,
                ["FiringChance"] = c._firingChance.Value,
                ["FollowVisibleEnemyMovement"] = c._enemyMovements.Value,
                ["VehicleDistance"] = c._vehicleDistance.Value,
                ["VehicleHeight"] = c._vehicleHeight.Value,
                ["OrbitSensitivity"] = c._orbitSensitivity.Value,
            };
        }
        private HarmonyLib.Harmony _harmony;
        private bool _movementHookInstalled;
        private bool _shotHookInstalled;
        private bool _reportedCallbackDetails;
        private readonly HashSet<AutoHook> _disabledAutoHooks = new HashSet<AutoHook>();
        private bool _autoFailed;
        private bool _reportedError;
        private bool _active;
        private bool _manual;
        private bool _movementFinished;
        private EventKind _autoKind;
        private float _autoUntil;
        private float _muteAutomaticUntil;
        private float _nextAutoSwitchAllowed;
        private readonly Dictionary<long, float> _eventDecisions = new Dictionary<long, float>();
        // Independent RNG: camera choices must not advance the game's random state.
        private readonly System.Random _cameraRandom = new System.Random();
        private int _seenActorId;
        private bool _seenMovement;

        private MelonPreferences_Entry<string> _toggleKey;
        private MelonPreferences_Entry<string> _autoToggleKey;
        private MelonPreferences_Entry<bool> _automaticMode;
        private MelonPreferences_Entry<int> _movementChance;
        private MelonPreferences_Entry<int> _firingChance;
        private MelonPreferences_Entry<bool> _enemyMovements;
        private MelonPreferences_Entry<float> _vehicleDistance;
        private MelonPreferences_Entry<float> _vehicleHeight;
        private MelonPreferences_Entry<float> _distance;
        private MelonPreferences_Entry<float> _height;
        private MelonPreferences_Entry<float> _shoulder;
        private MelonPreferences_Entry<float> _fieldOfView;
        private MelonPreferences_Entry<float> _orbitSensitivity;
        private MelonPreferences_Entry<string> _orbitButton;
        private MelonPreferences_Entry<bool> _debug;
        private bool? _runtimeAutoOverride;

        // The optional companion modpack registers options with ModpackLoader's
        // strongly typed API. This standalone MelonMod only reads those values;
        // without the companion it uses MelonPreferences transparently.
        private bool _menuRegistered;
        private MethodInfo _menuGet;
        private float _menuRetryAt;
        private int _menuAttempts;
        private float _nextSettingsRefresh;
        private bool _cachedAutomaticMode;
        private float _cachedMovementChance = 80f;
        private float _cachedFiringChance = 100f;
        private bool _cachedEnemyMovements = true;
        private float _cachedVehicleDistance = 8.5f;
        private float _cachedVehicleHeight = 3.8f;
        private float _cachedOrbitSensitivity = 0.35f;

        private TacticalCamera _tacticalCamera;
        private Camera _unityCamera;
        private Actor _trackedActor;
        private Element _trackedElement;
        private int _actorId;

        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _savedFov;
        private float _savedNear;
        private float _savedFar;
        private bool _savedSkipAutoFocus;
        private bool _uiHidden;
        private bool _reportedUiError;
        private float _nextUiRestoreAttempt;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private Vector3 _heading;
        private Vector3 _lastElementPosition;
        private float _orbitYaw;
        private float _orbitPitch;

        public override void OnInitializeMelon()
        {
            s_instance = this;
            var category = MelonPreferences.CreateCategory(SettingsCategory,
                "Cinematic Action Camera");
            _toggleKey = category.CreateEntry("ToggleKey", "F6");
            _autoToggleKey = category.CreateEntry("AutoToggleKey", "F7");
            _automaticMode = category.CreateEntry("AutomaticMode", false);
            _movementChance = category.CreateEntry("MovementChance", 80);
            _firingChance = category.CreateEntry("FiringChance", 100);
            _enemyMovements = category.CreateEntry("FollowVisibleEnemyMovement", true);
            _vehicleDistance = category.CreateEntry("VehicleDistance", 8.5f);
            _vehicleHeight = category.CreateEntry("VehicleHeight", 3.8f);
            _distance = category.CreateEntry("Distance", 3.2f);
            _height = category.CreateEntry("Height", 1.75f);
            _shoulder = category.CreateEntry("ShoulderOffset", 0.55f);
            _fieldOfView = category.CreateEntry("FieldOfView", 66f);
            _orbitSensitivity = category.CreateEntry("OrbitSensitivity", 0.35f);
            _orbitButton = category.CreateEntry("OrbitMouseButton", "Middle");
            _debug = category.CreateEntry("Debug", false);

            InstallEventHooks();
        }

        private void InstallEventHooks()
        {
            try
            {
                _harmony = new HarmonyLib.Harmony("menace.thirdpersoncamera.events");
                _movementHookInstalled = PatchEvent("InvokeOnMovement", 5, nameof(MovementPostfix));
                PatchEvent("InvokeOnMovementFinished", 2, nameof(MovementFinishedPostfix));
                _shotHookInstalled = PatchEvent("InvokeOnAttackTileStart", 4, nameof(AttackStartedPostfix));
                // Never switch to a friendly victim on hit, miss, suppression,
                // or attack targeting. The camera is an observer, not a hit reaction.
                if (!_movementHookInstalled || !_shotHookInstalled)
                    MelonLogger.Warning("Some cinematic event hooks were unavailable; check Debug for details.");
            }
            catch (Exception ex)
            {
                // Manual F6 camera must remain usable even if event hooking fails.
                MelonLogger.Warning("Automatic camera event hooks unavailable: " + ex.GetType().Name);
            }
        }

        private bool PatchEvent(string methodName, int arity, string postfixName)
        {
            try
            {
                MethodInfo target = null;
                foreach (MethodInfo candidate in typeof(TacticalManager).GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (candidate.Name == methodName && candidate.GetParameters().Length == arity)
                    {
                        target = candidate;
                        break;
                    }
                }
                MethodInfo handler = typeof(ThirdPersonCameraMod).GetMethod(postfixName,
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (target == null || handler == null)
                    return false;
                _harmony.Patch(target, postfix: new HarmonyMethod(handler));
                DebugLog("Hook active: " + methodName);
                return true;
            }
            catch (Exception ex)
            {
                if (_debug != null && _debug.Value)
                    MelonLogger.Warning("Hook failed: " + methodName + " (" + ex.GetType().Name + ")");
                return false;
            }
        }

        // Native IL2CPP methods are patched at their event-invocation sites.
        // The callbacks never change any original parameters or results.
        private static void MovementPostfix(Actor _actor)
        {
            if (s_instance != null)
                s_instance.SafeEvent(AutoHook.Movement, () => s_instance.HandleMovement(_actor));
        }

        private static void MovementFinishedPostfix(Actor _actor)
        {
            if (s_instance != null)
                s_instance.SafeEvent(AutoHook.MovementFinished, () => s_instance.HandleMovementFinished(_actor));
        }

        private static void AttackStartedPostfix(Actor _actor, Skill _skill, Tile _targetTile,
            float _attackDurationInSec)
        {
            if (s_instance != null)
                s_instance.SafeEvent(AutoHook.AttackStarted,
                    () => s_instance.HandleAttack(_actor, _skill, _targetTile, _attackDurationInSec));
        }

        private void SafeEvent(AutoHook hook, Action action)
        {
            if (_autoFailed || !AutomaticEnabled() || _disabledAutoHooks.Contains(hook)) return;
            try { action(); }
            catch (Exception ex)
            {
                // Disable only the failing native callback. Other event kinds
                // remain operational, including the manual F6 camera.
                _disabledAutoHooks.Add(hook);
                if (hook == AutoHook.Movement)
                {
                    // Restore the pre-existing lightweight movement-poll fallback.
                    _movementHookInstalled = false;
                    _seenActorId = 0;
                    _seenMovement = false;
                }
                if (hook == AutoHook.AttackStarted) _shotHookInstalled = false;

                // An exception inside camera activation may leave an automatic
                // shot partially active. Restore normal camera conservatively.
                try { if (_active && !_manual) ExitThirdPerson(false); }
                catch { /* Never propagate from an IL2CPP native callback. */ }

                // One complete exception per session for root-cause analysis,
                // then one short warning per distinct failed hook at most.
                if (!_reportedCallbackDetails)
                {
                    _reportedCallbackDetails = true;
                    MelonLogger.Error("Automatic third-person event " + hook +
                        " failed; only this event hook is disabled. Full exception (once): " + ex);
                }
                else
                {
                    MelonLogger.Warning("Automatic third-person event " + hook +
                        " disabled after " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private void HandleMovement(Actor actor)
        {
            if (!IsEligibleAutoActor(actor, EventKind.Movement)) return;
            ConsiderAutoCamera(actor, EventKind.Movement, 1.8f);
        }

        private void HandleMovementFinished(Actor actor)
        {
            if (!_active || _manual || actor == null || actor.ID != _actorId || _autoKind != EventKind.Movement)
                return;
            _movementFinished = true;
            _autoUntil = Mathf.Max(_autoUntil, Time.realtimeSinceStartup + 0.85f);
        }

        private void HandleAttack(Actor attacker, Skill skill, Tile tile, float duration)
        {
            // Follow friendly fire only; NEVER focus the friendly unit receiving
            // incoming shots. The unused target tile is kept for Harmony signature.
            if (!IsPlayerUnit(attacker) || skill == null || !skill.IsAttack()) return;
            float clip = Mathf.Clamp(duration + 1.0f, 2.2f, 10f);
            ConsiderAutoCamera(attacker, EventKind.Firing, clip);
        }

        private void ConsiderAutoCamera(Actor actor, EventKind kind, float clipDuration)
        {
            if (_autoFailed || !AutomaticEnabled() || !IsEligibleAutoActor(actor, kind)) return;
            TacticalManager manager = TacticalManager.Get();
            if (manager == null || !TacticalManager.IsMissionRunning() || manager.IsPaused()) return;
            if (Time.realtimeSinceStartup < _muteAutomaticUntil) return;
            // Friendly units are followed on our turn; visible enemies can be
            // followed during the AI turn when their own movement starts.
            if (kind == EventKind.Movement && actor.IsPlayer() && !manager.IsPlayerTurn()) return;

            float now = Time.realtimeSinceStartup;
            if (_active && _manual) return;
            if (_active && !_manual && _actorId == actor.ID && kind == _autoKind)
            {
                // The original accepted action is already on screen. Do not re-roll.
                _autoUntil = Mathf.Max(_autoUntil, now + clipDuration);
                if (kind == EventKind.Movement) _movementFinished = false;
                return;
            }
            if (_active && !_manual)
            {
                if (_actorId == actor.ID && kind < _autoKind) return;
                // Avoid rapidly swapping between soldiers during a volley.
                if (_actorId != actor.ID &&
                    (now < _nextAutoSwitchAllowed || kind <= _autoKind)) return;
            }

            // One chance roll per distinct action/volley, even when another
            // type of cinematic view is already active for the same soldier.
            long decisionKey = ((long)(uint)actor.ID << 2) | (long)kind;
            float previous;
            if (_eventDecisions.TryGetValue(decisionKey, out previous))
            {
                float debounce = kind == EventKind.Movement ? 0.8f : 1.5f;
                if (now - previous < debounce) return;
            }
            _eventDecisions[decisionKey] = now;
            if (!RollChance(kind)) return;

            if (_active && _actorId == actor.ID)
            {
                _autoKind = kind;
                _autoUntil = Mathf.Max(_autoUntil, now + clipDuration);
                _movementFinished = kind != EventKind.Movement;
                return;
            }
            if (_active) ExitThirdPerson(false);
            if (!TryEnterThirdPerson(actor, false, kind)) return;
            _autoKind = kind;
            _autoUntil = now + clipDuration;
            _movementFinished = false;
            _nextAutoSwitchAllowed = now + 1.25f;
            DebugLog("Auto camera " + kind + " actor=" + actor.ID);
        }

        private bool RollChance(EventKind kind)
        {
            // Same movement percentage for player and visible enemy units;
            // same firing percentage for player infantry and player vehicles.
            float chance = kind == EventKind.Movement ? _cachedMovementChance : _cachedFiringChance;
            chance = Mathf.Clamp(chance, 0f, 100f);
            if (chance <= 0f) return false;
            if (chance >= 100f) return true;
            return _cameraRandom.NextDouble() * 100.0 < chance;
        }

        private static bool IsSupportedUnit(Actor actor)
        {
            return actor != null && actor.IsAlive() && (actor.IsInfantry() || actor.IsVehicle());
        }

        private static bool IsPlayerUnit(Actor actor)
        {
            return IsSupportedUnit(actor) && actor.IsPlayer();
        }

        private static bool IsCurrentlyVisibleEnemy(Actor actor)
        {
            if (!IsSupportedUnit(actor)) return false;
            try
            {
                // A previously discovered/contacted enemy is not enough. Check
                // current tactical concealment AND actual rendered 3D visibility.
                return !actor.IsPlayerOrPlayerAI() && actor.IsDiscoveredByPlayer() &&
                    !actor.IsHiddenToPlayer() && actor.Is3DModelVisibleToPlayer();
            }
            catch
            {
                // IL2CPP entities can disappear during turn/scene transitions.
                // Do not leak fog-of-war information or disable unrelated hooks.
                return false;
            }
        }

        private bool IsEligibleAutoActor(Actor actor, EventKind kind)
        {
            if (IsPlayerUnit(actor)) return true;
            return kind == EventKind.Movement && _cachedEnemyMovements &&
                IsCurrentlyVisibleEnemy(actor);
        }

        private static bool TacticalUiIsFrontmost()
        {
            UIManager uiManager = UIManager.Get();
            if (uiManager == null) return false;
            UIScreen current = uiManager.GetActiveScreen();
            return current != null && string.Equals(current.GetPrefabName(),
                "TacticalUI", StringComparison.Ordinal);
        }

        public override void OnUpdate()
        {
            try
            {
                // Recovery if a native UI visibility change failed while
                // exiting, e.g. during a tactical screen transition.
                if (!_active && _uiHidden && Time.realtimeSinceStartup >= _nextUiRestoreAttempt)
                {
                    _nextUiRestoreAttempt = Time.realtimeSinceStartup + 0.75f;
                    if (TacticalUiIsFrontmost()) RestoreTacticalUi();
                }
                TryAttachInGameSettings();
                RefreshSettings();

                if (WasKeyPressed(_autoToggleKey.Value, Key.F7))
                {
                    // A separate camera-update failure is still a global safety
                    // stop, but an explicit user F7 press can re-arm it once.
                    bool enable = _autoFailed || !AutomaticEnabled();
                    if (_autoFailed)
                    {
                        _autoFailed = false;
                        _reportedError = false;
                    }
                    _runtimeAutoOverride = enable;
                    MelonLogger.Msg("Automatic third-person camera " + (AutomaticEnabled() ? "ON" : "OFF") +
                        " (F7, current session).");
                    if (!AutomaticEnabled() && _active && !_manual) ExitThirdPerson();
                }
                if (WasKeyPressed(_toggleKey.Value, Key.F6))
                {
                    if (_active)
                    {
                        ExitThirdPerson();
                        _muteAutomaticUntil = Time.realtimeSinceStartup + 2.0f;
                    }
                    else
                    {
                        TacticalManager manager = TacticalManager.Get();
                        if (manager != null && TacticalManager.IsMissionRunning() &&
                            manager.IsPlayerTurn() && !manager.IsPaused())
                            TryEnterThirdPerson(manager.GetActiveActor(), true);
                    }
                }

                // Fallback if this specific build of MENACE cannot patch movement events.
                if (!_movementHookInstalled && !_autoFailed && AutomaticEnabled())
                    PollMovementStart();
            }
            catch (Exception ex) { FailOnce(ex); }
        }

        private void PollMovementStart()
        {
            TacticalManager manager = TacticalManager.Get();
            if (manager == null || !TacticalManager.IsMissionRunning() || !manager.IsPlayerTurn())
            {
                _seenActorId = 0;
                _seenMovement = false;
                return;
            }
            Actor actor = manager.GetActiveActor();
            if (!IsPlayerUnit(actor))
            {
                _seenActorId = 0;
                _seenMovement = false;
                return;
            }
            bool moving = actor.IsMoving();
            if (_seenActorId != actor.ID)
            {
                _seenActorId = actor.ID;
                _seenMovement = false;
            }
            if (moving && !_seenMovement)
                ConsiderAutoCamera(actor, EventKind.Movement, 2.0f);
            _seenMovement = moving;
        }

        public override void OnLateUpdate()
        {
            if (!_active) return;
            try
            {
                TacticalManager manager = TacticalManager.Get();
                if (manager == null || !TacticalManager.IsMissionRunning() ||
                    manager.GetCamera() == null || manager.GetCamera().GetCamera() != _unityCamera ||
                    _trackedActor == null || !_trackedActor.IsAlive() ||
                    !TacticalUiIsFrontmost() || (_manual && !manager.IsPlayerTurn()) ||
                    (!_manual && !_trackedActor.IsPlayer() && !IsCurrentlyVisibleEnemy(_trackedActor)))
                {
                    ExitThirdPerson();
                    return;
                }

                if (_manual)
                {
                    Actor selected = manager.GetActiveActor();
                    if (selected != null && selected.ID != _actorId)
                    {
                        ExitThirdPerson();
                        return;
                    }
                }
                else if (!AutomaticEnabled())
                {
                    ExitThirdPerson();
                    return;
                }
                else
                {
                    float now = Time.realtimeSinceStartup;
                    if (_autoKind == EventKind.Movement && !_movementFinished && _trackedActor.IsMoving())
                        _autoUntil = Mathf.Max(_autoUntil, now + 0.4f);
                    if (now >= _autoUntil)
                    {
                        ExitThirdPerson();
                        return;
                    }
                }

                if (_trackedElement == null || !_trackedElement.IsAlive())
                {
                    _trackedElement = FindTrackingElement(_trackedActor);
                    if (_trackedElement == null || !_trackedElement.IsAlive())
                    {
                        ExitThirdPerson();
                        return;
                    }
                    _lastElementPosition = _trackedElement.transform.position;
                }
                UpdateOrbitInput();
                UpdateThirdPersonCamera();
            }
            catch (Exception ex) { FailOnce(ex); }
        }

        private void UpdateOrbitInput()
        {
            if (WasKeyPressed("R", Key.R))
            {
                _orbitYaw = 0f;
                _orbitPitch = 0f;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            bool right = string.Equals(_orbitButton.Value, "Right", StringComparison.OrdinalIgnoreCase);
            bool dragging = right ? mouse.rightButton.isPressed : mouse.middleButton.isPressed;
            if (!dragging) return;
            Vector2 delta = mouse.delta.ReadValue();
            float sensitivity = Mathf.Clamp(_cachedOrbitSensitivity, 0.05f, 2f);
            _orbitYaw += delta.x * sensitivity;
            _orbitPitch = Mathf.Clamp(_orbitPitch - delta.y * sensitivity, -30f, 65f);
            if (Mathf.Abs(_orbitYaw) > 360f) _orbitYaw = Mathf.Repeat(_orbitYaw, 360f);
        }

        private static bool WasKeyPressed(string configured, Key fallback)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            // DevHotkeys uses Ctrl+F6 and Ctrl+F7. Do not consume those
            // combinations as camera shortcuts (plain F6/F7 still work).
            if ((keyboard.leftCtrlKey != null && keyboard.leftCtrlKey.isPressed) ||
                (keyboard.rightCtrlKey != null && keyboard.rightCtrlKey.isPressed)) return false;
            Key key;
            if (!Enum.TryParse(configured, true, out key) || key == Key.None) key = fallback;
            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static Element FindTrackingElement(Actor actor)
        {
            if (actor == null) return null;
            Element first = actor.GetFirstElementOrNull();
            if (first != null && first.IsAlive()) return first;
            int count = Mathf.Clamp(actor.GetOriginalElementCount(), 0, 32);
            for (int i = 0; i < count; i++)
            {
                Element element = actor.GetElementSafe(i);
                if (element != null && element.IsAlive()) return element;
            }
            return null;
        }

        private bool TryEnterThirdPerson(Actor actor, bool manual, EventKind kind = EventKind.Movement)
        {
            if (!IsSupportedUnit(actor)) return false;
            if (manual ? !IsPlayerUnit(actor) : !IsEligibleAutoActor(actor, kind)) return false;
            TacticalManager manager = TacticalManager.Get();
            if (manager == null || !TacticalManager.IsMissionRunning() || !TacticalUiIsFrontmost()) return false;
            Element element = FindTrackingElement(actor);
            if (element == null) return false;
            TacticalCamera tacticalCamera = manager.GetCamera();
            Camera camera = tacticalCamera == null ? null : tacticalCamera.GetCamera();
            if (camera == null) return false;

            _tacticalCamera = tacticalCamera;
            _unityCamera = camera;
            _trackedActor = actor;
            _trackedElement = element;
            _actorId = actor.ID;
            _manual = manual;
            _savedPosition = camera.transform.position;
            _savedRotation = camera.transform.rotation;
            _savedFov = camera.fieldOfView;
            _savedNear = camera.nearClipPlane;
            _savedFar = camera.farClipPlane;
            _savedSkipAutoFocus = TacticalCameraController.SkipAutoFocus;
            _smoothedPosition = _savedPosition;
            _smoothedRotation = _savedRotation;
            _lastElementPosition = element.transform.position;
            _heading = FlatDirection(actor.GetForward(), element.transform.forward);
            _orbitYaw = 0f;
            _orbitPitch = 0f;

            tacticalCamera.CancelAutoCamera();
            TacticalCameraController.SkipAutoFocus = true;
            _active = true;
            HideTacticalUi();
            DebugLog((manual ? "Manual" : "Automatic") + " camera ON for actor=" + _actorId);
            return true;
        }

        private void UpdateThirdPersonCamera()
        {
            Vector3 position = _trackedElement.transform.position;
            Vector3 movement = position - _lastElementPosition;
            movement.y = 0f;
            _lastElementPosition = position;
            Vector3 nextHeading = FlatDirection(_trackedActor.GetForward(), _heading);
            if (_trackedActor.IsMoving() && movement.sqrMagnitude > 0.000025f)
                nextHeading = movement.normalized;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.1f);
            _heading = Vector3.Slerp(_heading, nextHeading, Blend(9f, dt)).normalized;

            Vector3 orbitForward = Quaternion.AngleAxis(_orbitYaw, Vector3.up) * _heading;
            Vector3 right = Vector3.Cross(Vector3.up, orbitForward).normalized;
            bool vehicle = _trackedActor.IsVehicle();
            float distance = vehicle ? Mathf.Clamp(_cachedVehicleDistance, 4f, 25f)
                : Mathf.Clamp(_distance.Value, 1.25f, 12f);
            float height = vehicle ? Mathf.Clamp(_cachedVehicleHeight, 1.5f, 12f)
                : Mathf.Clamp(_height.Value, 0.6f, 6f);
            float shoulder = vehicle ? 0f : Mathf.Clamp(_shoulder.Value, -1.5f, 1.5f);
            float pitch = _orbitPitch * Mathf.Deg2Rad;
            Vector3 desiredPosition = position + Vector3.up * (height + Mathf.Sin(pitch) * distance)
                - orbitForward * (distance * Mathf.Cos(pitch)) + right * shoulder;
            // Smoothly move the focus from the path ahead to the soldier as the
            // user orbits. No binary camera target jump at the first mouse pixel.
            float facingAway = Mathf.Abs(Mathf.DeltaAngle(0f, _orbitYaw));
            float lead = Mathf.Clamp01(1f - facingAway / 45f - Mathf.Abs(_orbitPitch) / 40f);
            float lookHeight = vehicle ? Mathf.Max(1.75f, height * 0.55f) : 1.38f;
            float lookLead = vehicle ? 2.5f : 1.25f;
            Vector3 lookPoint = position + Vector3.up * lookHeight + _heading * (lookLead * lead);
            Vector3 lookDirection = lookPoint - desiredPosition;
            if (lookDirection.sqrMagnitude < 0.01f) return;
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, desiredPosition, Blend(TransitionSpeed, dt));
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, desiredRotation, Blend(10f, dt));

            // TacticalCamera.Update owns the gameplay camera; change only final render pose.
            _unityCamera.transform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
            _unityCamera.fieldOfView = Mathf.Clamp(_fieldOfView.Value, 40f, 95f);
            _unityCamera.nearClipPlane = 0.05f;
            _unityCamera.farClipPlane = Mathf.Max(_savedFar, 350f);
        }

        private static float Blend(float speed, float deltaTime) { return 1f - Mathf.Exp(-speed * deltaTime); }

        private static Vector3 FlatDirection(Vector3 preferred, Vector3 fallback)
        {
            preferred.y = 0f;
            if (preferred.sqrMagnitude > 0.0001f) return preferred.normalized;
            fallback.y = 0f;
            if (fallback.sqrMagnitude > 0.0001f) return fallback.normalized;
            return Vector3.forward;
        }

        private void HideTacticalUi()
        {
            if (_uiHidden) return;
            try
            {
                // Native MENACE helper hides tactical panels, floating unit HUDs,
                // aiming/path overlays and other tactical indicators together.
                _uiHidden = true; // Also recover if native call fails halfway.
                UITactical.SetUIVisibility(false);
            }
            catch (Exception ex)
            {
                if (!_reportedUiError)
                {
                    _reportedUiError = true;
                    MelonLogger.Warning("Could not hide tactical UI: " + ex.GetType().Name);
                }
            }
        }

        private void RestoreTacticalUi()
        {
            if (!_uiHidden) return;
            try
            {
                UITactical.SetUIVisibility(true);
                _uiHidden = false;
            }
            catch (Exception ex)
            {
                if (!_reportedUiError)
                {
                    _reportedUiError = true;
                    MelonLogger.Warning("Could not restore tactical UI: " + ex.GetType().Name);
                }
            }
        }

        private void ExitThirdPerson(bool debug = true)
        {
            if (!_active && !_uiHidden) return;
            bool wasActive = _active;
            _active = false;
            try
            {
                if (wasActive)
                {
                    TacticalCameraController.SkipAutoFocus = _savedSkipAutoFocus;
                    if (_unityCamera != null)
                    {
                        _unityCamera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
                        _unityCamera.fieldOfView = _savedFov;
                        _unityCamera.nearClipPlane = _savedNear;
                        _unityCamera.farClipPlane = _savedFar;
                    }
                }
            }
            finally
            {
                // Never leave the mission unplayable with an invisible HUD.
                RestoreTacticalUi();
                _tacticalCamera = null;
                _unityCamera = null;
                _trackedActor = null;
                _trackedElement = null;
                _manual = false;
                _eventDecisions.Clear();
            }
            if (debug) DebugLog("Third-person camera OFF");
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (_active || _uiHidden)
            {
                try { ExitThirdPerson(false); }
                catch { RestoreTacticalUi(); }
            }
            _eventDecisions.Clear();
            _seenActorId = 0;
            _seenMovement = false;
        }

        public override void OnApplicationQuit()
        {
            if (_active || _uiHidden)
            {
                try { ExitThirdPerson(false); }
                catch { RestoreTacticalUi(); }
            }
        }

        private bool AutomaticEnabled()
        {
            return !_autoFailed && (_runtimeAutoOverride ?? _cachedAutomaticMode);
        }

        private void RefreshSettings()
        {
            // Avoid reflective ModpackLoader calls in per-frame camera and hook code.
            float now = Time.realtimeSinceStartup;
            if (now < _nextSettingsRefresh) return;
            _nextSettingsRefresh = now + 0.5f;
            bool setting = MenuBoolean("AutomaticMode", _automaticMode.Value);
            // If the user changed the actual options screen, it takes priority over F7.
            if (_runtimeAutoOverride.HasValue && _cachedAutomaticMode != setting)
                _runtimeAutoOverride = null;
            _cachedAutomaticMode = setting;
            _cachedMovementChance = MenuNumber("MovementChance", _movementChance.Value);
            _cachedFiringChance = MenuNumber("FiringChance", _firingChance.Value);
            _cachedEnemyMovements = MenuBoolean("FollowVisibleEnemyMovement", _enemyMovements.Value);
            _cachedVehicleDistance = MenuNumber("VehicleDistance", _vehicleDistance.Value);
            _cachedVehicleHeight = MenuNumber("VehicleHeight", _vehicleHeight.Value);
            _cachedOrbitSensitivity = MenuNumber("OrbitSensitivity", _orbitSensitivity.Value);
        }

        // The 0.3 reflection-based UI registration was unreliable and silently
        // failed for some ModpackLoader versions. The companion modpack now owns
        // registration via ModSettings.Register, exactly like Larger Maps.
        // This reader has no direct SDK dependency, so F6/F7 still work without
        // the loader or its companion settings modpack.
        private void TryAttachInGameSettings()
        {
            if ((_menuRegistered && _menuGet?.DeclaringType?.FullName == "MenaceModSettings.ModSettings")
                || _menuAttempts >= 12 || Time.realtimeSinceStartup < _menuRetryAt) return;
            _menuAttempts++;
            _menuRetryAt = Time.realtimeSinceStartup + 3f;
            try
            {
                Type api = null;
                // Prefer Jiangyu even when both loaders coexist, independent of assembly order.
                foreach (string typeName in new[] { "MenaceModSettings.ModSettings", "Menace.SDK.ModSettings", "Menace.ModpackLoader.ModSettings" })
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        api = assembly.GetType(typeName, false);
                        if (api != null) break;
                    }
                    if (api != null) break;
                }
                if (api == null) return;
                foreach (MethodInfo candidate in api.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (!candidate.IsGenericMethod && candidate.Name == "Get" &&
                        parameters.Length >= 2 &&
                        parameters[0].ParameterType == typeof(string) &&
                        parameters[1].ParameterType == typeof(string) &&
                        HasOptionalSuffix(parameters, 2))
                    {
                        _menuGet = candidate;
                        _menuRegistered = true;
                        _nextSettingsRefresh = 0f;
                        DebugLog("Camera settings reader attached");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_debug != null && _debug.Value)
                    MelonLogger.Warning("ModpackLoader settings reader unavailable: " + ex.GetType().Name);
            }
        }

        private static bool HasOptionalSuffix(ParameterInfo[] parameters, int supplied)
        {
            if (parameters.Length < supplied) return false;
            for (int i = supplied; i < parameters.Length; i++)
                if (!parameters[i].IsOptional) return false;
            return true;
        }

        private static object[] CompleteArguments(ParameterInfo[] parameters, params object[] supplied)
        {
            object[] result = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                result[i] = i < supplied.Length ? supplied[i] : Type.Missing;
            return result;
        }

        private object ReadMenuValue(string key)
        {
            if (!_menuRegistered || _menuGet == null) return null;
            foreach (string group in new[] { MenuName, "Third Person Camera" })
            {
                try { return _menuGet.Invoke(null, CompleteArguments(_menuGet.GetParameters(), group, key)); }
                catch { }
            }
            return null;
        }

        private bool MenuBoolean(string key, bool fallback)
        {
            object value = ReadMenuValue(key);
            if (value == null) return fallback;
            try { return Convert.ToBoolean(value); }
            catch { return fallback; }
        }

        private float MenuNumber(string key, float fallback)
        {
            object value = ReadMenuValue(key);
            if (value == null) return fallback;
            try { return Convert.ToSingle(value); }
            catch { return fallback; }
        }

        private void DebugLog(string message)
        {
            if (_debug != null && _debug.Value) MelonLogger.Msg(message);
        }

        private void FailOnce(Exception ex)
        {
            try { ExitThirdPerson(); }
            catch { _active = false; TacticalCameraController.SkipAutoFocus = _savedSkipAutoFocus; }
            _autoFailed = true; // Avoid repeated failing transitions from game events.
            if (_reportedError) return;
            _reportedError = true;
            MelonLogger.Error("Third-person camera disabled after an error: " + ex);
        }
    }
}
