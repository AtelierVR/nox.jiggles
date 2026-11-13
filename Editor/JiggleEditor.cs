using System.Collections.Generic;
using System.Linq;
using Nox.Avatars.Parameters;
using Nox.CCK.Avatars.Parameters;
using Nox.CCK.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nox.CCK.Jiggles.Editor {
    public class JiggleEditor : InspectorEditor<Jiggle> {
        /// <summary>
        /// Names the rig publishes for <paramref name="prefix"/>, with the type each one needs in the Animator.
        /// Taken from the component itself, so the editor cannot drift from what the rig actually writes.
        /// </summary>
        private static (string Name, AnimatorControllerParameterType Type)[] GetParameterNames(string prefix) {
            var settings = new JiggleParameterSettings { prefix = prefix };

            return new (string, AnimatorControllerParameterType)[] {
                (settings.IsGrabbedParameter, AnimatorControllerParameterType.Bool),
                (settings.IsPosedParameter,   AnimatorControllerParameterType.Bool),
                (settings.AngleParameter,     AnimatorControllerParameterType.Float),
                (settings.StretchParameter,   AnimatorControllerParameterType.Float),
                (settings.SquishParameter,    AnimatorControllerParameterType.Float)
            };
        }

        private SerializedProperty _jiggleRigData;
        private SerializedProperty _animatedParameters;
        private SerializedProperty _rootBone;
        private SerializedProperty _collidersProp;

        private SerializedProperty _stiffness;
        private SerializedProperty _drag;
        private SerializedProperty _airDrag;
        private SerializedProperty _gravity;
        private SerializedProperty _stretch;
        private SerializedProperty _angleLimit;
        private SerializedProperty _collisionRadius;
        private SerializedProperty _advancedToggle;
        private SerializedProperty _angleLimitToggle;
        private SerializedProperty _collisionToggle;

        private VisualElement _groupSimplified;
        private VisualElement _groupAdvanced;
        private VisualElement _groupAngleLimit;
        private VisualElement _groupCollision;
        private EnumField _modeField;

        private readonly Dictionary<string, CurveField> _curveFields = new();
        private readonly Dictionary<string, VisualElement> _curveContainers = new();
        private readonly Dictionary<string, Button> _curveButtons = new();

        private VisualElement _notifications;
        private NotificationElement _rootNotification;
        private NotificationElement _parametersNotification;

        private Foldout _forcesFoldout;
        private Foldout _collidersFoldout;
        private Foldout _interactionFoldout;

        private NotificationElement _parameterNames;
        private SerializedProperty _parameterPrefix;
        private SerializedProperty _parameterMaxStretch;
        private SerializedProperty _parameterMaxSquish;

        private enum TrueFalse { False, True }

        private static readonly string[] SimplifiedNames = { "stiffness", "drag" };
        private static readonly string[] AdvancedNames = { "stiffness", "drag", "airDrag", "gravity", "stretch" };
        private static readonly string[] AngleLimitNames = { "angleLimit" };
        private static readonly string[] CollisionNames = { "collisionRadius" };

        public override VisualElement CreateInspectorGUI() {
            var root = base.CreateInspectorGUI();
            if (Content == null) return root;

            var uss = Resources.Load<StyleSheet>("JiggleEditor");
            if (uss != null) root.styleSheets.Add(uss);

            serializedObject.Update();
            InitProperties();

            // The base editor caches its root: a rebuild would stack a second panel on top of the first one.
            Content.Clear();
            BuildUI(Content);
            return root;
        }

        // The animator of the avatar can change without the component changing (another object selected and
        // edited): the parameter check is refreshed when the hierarchy moves.
        protected override void OnAttachToPanel(AttachToPanelEvent evt)
            => EditorApplication.hierarchyChanged += UpdateNotifications;

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
            => EditorApplication.hierarchyChanged -= UpdateNotifications;

        private void InitProperties() {
            _jiggleRigData = serializedObject.FindProperty("jiggleRigData");
            _animatedParameters = serializedObject.FindProperty("animatedParameters");
            _rootBone = _jiggleRigData.FindPropertyRelative("rootBone");
            _collidersProp = serializedObject.FindProperty("jiggleColliders");

            var tp = _jiggleRigData.FindPropertyRelative("jiggleTreeInputParameters");
            _advancedToggle = tp.FindPropertyRelative("advancedToggle");
            _collisionToggle = tp.FindPropertyRelative("collisionToggle");
            _angleLimitToggle = tp.FindPropertyRelative("angleLimitToggle");
            _stiffness = tp.FindPropertyRelative("stiffness");
            _drag = tp.FindPropertyRelative("drag");
            _airDrag = tp.FindPropertyRelative("airDrag");
            _gravity = tp.FindPropertyRelative("gravity");
            _stretch = tp.FindPropertyRelative("stretch");
            _angleLimit = tp.FindPropertyRelative("angleLimit");
            _collisionRadius = tp.FindPropertyRelative("collisionRadius");

            // The interaction settings are shown field by field, without the foldout Unity would draw for the
            // serialized struct (see JiggleParameterSettings).
            var parameters = serializedObject.FindProperty("parameters");
            _parameterPrefix = parameters?.FindPropertyRelative("prefix");
            _parameterMaxStretch = parameters?.FindPropertyRelative("maxStretch");
            _parameterMaxSquish = parameters?.FindPropertyRelative("maxSquish");
        }

        // ── Build UI ──────────────────────────────────────────────────────────

        private void BuildUI(VisualElement content) {
            BuildNotifications(content);

            // ── Transform foldout ────────────────────────────────────────
            var transformFoldout = new Foldout { text = "Transform", value = true };
            transformFoldout.Add(MakeProp("rootBone", "Root Transform"));
            transformFoldout.Add(MakeProp("excludeRoot", "Exclude Root"));
            transformFoldout.Add(MakeProp("excludedTransforms", "Excluded Transforms"));
            content.Add(transformFoldout);

            // ── Forces foldout ───────────────────────────────────────────
            _forcesFoldout = new Foldout { text = "Forces", value = true };
            _modeField = new EnumField("Mode", JigglePhysicsMode.Simplified);
            _modeField.tooltip = "Simplified: stiffness and drag only. Advanced: every force, per bone curve.";
            _modeField.RegisterValueChangedCallback(evt => {
                _advancedToggle.boolValue = ((JigglePhysicsMode)evt.newValue) == JigglePhysicsMode.Advanced;
                _advancedToggle.serializedObject.ApplyModifiedProperties();
                UpdateModeVisibility();
            });
            _forcesFoldout.Add(_modeField);

            _groupSimplified = new VisualElement();
            foreach (var name in SimplifiedNames) _groupSimplified.Add(MakeCurvedFloatRow(name));
            _forcesFoldout.Add(_groupSimplified);

            _groupAdvanced = new VisualElement { style = { display = DisplayStyle.None } };
            foreach (var name in AdvancedNames) _groupAdvanced.Add(MakeCurvedFloatRow(name));
            _groupAdvanced.Add(MakeProp("rootStretch", "Root Stretch", "jiggleTreeInputParameters"));
            _groupAdvanced.Add(MakeProp("soften", "Soften", "jiggleTreeInputParameters"));
            _groupAdvanced.Add(MakeProp("ignoreRootMotion", "Ignore Root Motion", "jiggleTreeInputParameters"));
            _groupAdvanced.Add(MakeProp("blend", "Blend", "jiggleTreeInputParameters"));

            // Angle limit sub-section
            var angleToggle = MakeEnumToggle("angleLimitToggle", "Angle Limit", "jiggleTreeInputParameters");
            _groupAdvanced.Add(angleToggle);
            _groupAngleLimit = new VisualElement();
            foreach (var name in AngleLimitNames) _groupAngleLimit.Add(MakeCurvedFloatRow(name));
            _groupAngleLimit.Add(MakeProp("angleLimitSoften", "Angle Limit Soften", "jiggleTreeInputParameters"));
            _groupAdvanced.Add(_groupAngleLimit);

            // Collision sub-section
            var colToggle = MakeEnumToggle("collisionToggle", "Collision", "jiggleTreeInputParameters");
            _groupAdvanced.Add(colToggle);
            _groupCollision = new VisualElement();
            foreach (var name in CollisionNames) _groupCollision.Add(MakeCurvedFloatRow(name));
            _groupAdvanced.Add(_groupCollision);

            _forcesFoldout.Add(_groupAdvanced);

            // Animated Parameters: whether the forces are re-read every frame (see JiggleRig.HasAnimatedParameters).
            var animatedToggle = MakeEnumToggle(_animatedParameters, "Animated Parameters");
            animatedToggle.tooltip = "Re-reads the forces every frame, for a rig whose values are animated.";
            _forcesFoldout.Add(animatedToggle);

            content.Add(_forcesFoldout);

            // ── Colliders foldout ────────────────────────────────────────
            _collidersFoldout = new Foldout { text = "Colliders", value = true };
            var collidersField = new PropertyField();
            collidersField.BindProperty(_collidersProp);
            collidersField.label = "Jiggle Colliders";
            _collidersFoldout.Add(collidersField);
            content.Add(_collidersFoldout);

            // ── Interaction parameters foldout ───────────────────────────
            _interactionFoldout = new Foldout {
                text = "Animator Parameters",
                // Opened when the rig publishes something: the section is what makes the values usable.
                value = !string.IsNullOrEmpty(_parameterPrefix?.stringValue)
            };

            // Field by field: the serialized settings struct would be drawn as a nested "Parameters" foldout.
            _interactionFoldout.Add(MakeField(_parameterPrefix, "Prefix",
                "Prefix of the published parameters: 'Hair' exposes Hair/IsGrabbed, Hair/IsPosed, Hair/Angle, " +
                "Hair/Stretch and Hair/Squish. Empty disables them."));
            _interactionFoldout.Add(MakeField(_parameterMaxStretch, "Max Stretch",
                "Multiple of the rest length at which {prefix}/Stretch reaches 1. 1 or less disables the range."));
            _interactionFoldout.Add(MakeField(_parameterMaxSquish, "Max Squish",
                "Multiple of the rest length at which {prefix}/Squish reaches 1. 1 disables the range."));

            _parameterNames = new NotificationElement {
                name = "parameter-names",
                Kind = NotificationKind.Info
            };
            _parameterNames.AddToClassList("mt-4");
            _interactionFoldout.Add(_parameterNames);

            content.Add(_interactionFoldout);

            // Initial state
            UpdateModeVisibility();
            UpdateSubToggles();
            UpdateColliderCount();
            UpdateParameterNames();
            UpdateNotifications();

            content.TrackPropertyValue(_advancedToggle, _ => UpdateModeVisibility());
            content.TrackPropertyValue(_angleLimitToggle, _ => UpdateSubToggles());
            content.TrackPropertyValue(_collisionToggle, _ => UpdateSubToggles());
            content.TrackPropertyValue(_rootBone, _ => UpdateNotifications());
            content.TrackPropertyValue(_collidersProp, _ => UpdateColliderCount());

            if (_parameterPrefix != null)
                content.TrackPropertyValue(_parameterPrefix, _ => {
                    UpdateParameterNames();
                    UpdateNotifications();
                });
        }

        // ── Notifications ─────────────────────────────────────────────────────

        /// <summary>
        /// Boxes shown above the foldouts when the rig cannot work (no root bone) or when its interaction
        /// parameters are not usable (missing from the Animator). They stay hidden otherwise, so a healthy rig
        /// keeps a quiet inspector.
        /// </summary>
        private void BuildNotifications(VisualElement content) {
            _notifications = new VisualElement { name = "notifications" };
            _notifications.AddToClassList("flex-col");
            _notifications.AddToClassList("gap-8");
            // Space between the boxes and the fields below (mx keeps them aligned with the foldouts).
            _notifications.AddToClassList("mx-4");
            _notifications.AddToClassList("mb-8");
            content.Add(_notifications);

            _rootNotification = new NotificationElement {
                name = "root-notification",
                Kind = NotificationKind.Error
            };
            _notifications.Add(_rootNotification);

            _parametersNotification = new NotificationElement {
                name = "parameters-notification",
                Kind = NotificationKind.Warning
            };
            _notifications.Add(_parametersNotification);

            ShowNotification(_rootNotification, false);
            ShowNotification(_parametersNotification, false);
        }

        private void UpdateNotifications() {
            // Called by EditorApplication.hierarchyChanged too: the panel may not be built yet.
            if (_notifications == null || serializedObject?.targetObject == null)
                return;

            UpdateRootNotification();
            UpdateParametersNotification();
        }

        private void UpdateRootNotification() {
            var missing = _rootBone == null || _rootBone.objectReferenceValue == null;

            if (missing)
                _rootNotification.Message = "No root bone assigned: the rig is not simulated. Assign the first bone of the chain.";

            ShowNotification(_rootNotification, missing);
        }

        private void UpdateParametersNotification() {
            var prefix = _parameterPrefix?.stringValue;

            if (string.IsNullOrEmpty(prefix)) {
                ShowNotification(_parametersNotification, false);
                return;
            }

            var animator = GetAnimator();

            // No Animator at all (a rig in a world, a prop): nothing to declare, the values are simply unused.
            if (!animator || !animator.runtimeAnimatorController) {
                _parametersNotification.Kind = NotificationKind.Info;
                _parametersNotification.Message =
                    $"No Animator above this rig: '{prefix}/...' has no effect until an Animator declares these parameters.";
                ShowNotification(_parametersNotification, true);
                return;
            }

            var missing = GetMissingParameters(prefix);
            if (missing is not { Count: > 0 }) {
                ShowNotification(_parametersNotification, false);
                return;
            }

            _parametersNotification.Kind = NotificationKind.Warning;
            _parametersNotification.ClearActions();
            _parametersNotification.Message =
                $"Missing parameters: {string.Join(", ", missing)}. Add them to the avatar parameters " +
                "(the Animator has to declare them too for the values to be published).";

            if (GetParameterAsset() != null)
                _parametersNotification.AddAction("jiggles.inspector.add_parameters", AddMissingParameters);

            _parametersNotification.AddAction("jiggles.inspector.copy_parameter_names", CopyMissingParameterNames);
            ShowNotification(_parametersNotification, true);
        }

        private static void ShowNotification(NotificationElement notification, bool visible)
            => notification.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        // ── Animator parameters ───────────────────────────────────────────────

        /// <summary>Animator of the avatar the rig belongs to (its own GameObject included), or <c>null</c>.</summary>
        private Animator GetAnimator()
            => Target ? Target.GetComponentInParent<Animator>(true) : null;

        /// <summary>
        /// Base controller of the Animator, for <b>reading</b> its parameters: an override only remaps clips, its
        /// parameters come from the controller it overrides.
        /// </summary>
        private static AnimatorController GetBaseController(Animator animator) {
            if (!animator || !animator.runtimeAnimatorController)
                return null;

            return animator.runtimeAnimatorController switch {
                AnimatorController controller => controller,
                AnimatorOverrideController { runtimeAnimatorController: AnimatorController baseController } => baseController,
                _ => null
            };
        }

        /// <summary>
        /// Parameters of <paramref name="prefix"/> that neither the Animator nor the avatar's parameter asset declare,
        /// or <c>null</c> when there is no Animator to read (the caller then cannot tell).
        /// </summary>
        private List<string> GetMissingParameters(string prefix) {
            if (string.IsNullOrEmpty(prefix))
                return new List<string>();

            var animator = GetAnimator();
            if (!animator || !animator.runtimeAnimatorController)
                return null;

            var declared = new HashSet<string>(animator.parameters.Select(parameter => parameter.name));

            // An animator bound to an override only reports the override's parameters: read the base controller.
            if (GetBaseController(animator) is { } controller) {
                foreach (var parameter in controller.parameters)
                    declared.Add(parameter.name);
            }

            // A parameter added to the avatar's parameter asset counts as declared: it is not reported anymore.
            var asset = GetParameterAsset();
            if (asset && asset.parameters != null) {
                foreach (var entry in asset.parameters) {
                    if (entry != null && !string.IsNullOrEmpty(entry.name))
                        declared.Add(entry.name);
                }
            }

            return GetParameterNames(prefix)
                .Select(parameter => parameter.Name)
                .Where(name => !declared.Contains(name))
                .ToList();
        }

        /// <summary>
        /// Parameters asset of the avatar the rig belongs to (the <c>AvatarParameters</c> asset the parameter
        /// module lists its parameters in), or <c>null</c> when the avatar has no module/asset.
        /// </summary>
        private AvatarParameters GetParameterAsset() {
            var module = Target ? Target.GetComponentInParent<AvatarParameterModule>(true) : null;
            return module ? module.parameters : null;
        }

        /// <summary>
        /// Adds the missing parameters to the avatar's parameter asset, so the avatar lists them even before the
        /// Animator does. The Animator controllers are <b>not</b> modified.
        /// </summary>
        private void AddMissingParameters() {
            var prefix = _parameterPrefix?.stringValue;
            var asset = GetParameterAsset();

            if (string.IsNullOrEmpty(prefix) || !asset)
                return;

            var missing = GetMissingParameters(prefix);
            if (missing is not { Count: > 0 })
                return;

            var entries = asset.parameters != null
                ? new List<ParameterEntry>(asset.parameters)
                : new List<ParameterEntry>();

            Undo.RecordObject(asset, "Add Jiggle Parameters");

            foreach (var (name, type) in GetParameterNames(prefix)) {
                if (!missing.Contains(name) || entries.Any(entry => entry != null && entry.name == name))
                    continue;

                entries.Add(new ParameterEntry {
                    name  = name,
                    type  = type == AnimatorControllerParameterType.Bool
						? ParameterType.Bool
						: ParameterType.Float,
                    // Published by the rig itself on every client: nothing to edit, save or synchronize.
                    flags = ParameterFlags.None,
                    value = string.Empty
                });
            }

            asset.parameters = entries.ToArray();
            EditorUtility.SetDirty(asset);
            UpdateParametersNotification();
        }

        private void CopyMissingParameterNames() {
            var missing = GetMissingParameters(_parameterPrefix?.stringValue);
            if (missing is not { Count: > 0 })
                return;

            EditorGUIUtility.systemCopyBuffer = string.Join(", ", missing);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private PropertyField MakeProp(string relPath, string label, string parentPath = null) {
            var pf = new PropertyField();
            var prop = parentPath != null
                ? _jiggleRigData.FindPropertyRelative(parentPath).FindPropertyRelative(relPath)
                : _jiggleRigData.FindPropertyRelative(relPath);
            if (prop != null) pf.BindProperty(prop);
            pf.label = label;
            return pf;
        }

        private static PropertyField MakeField(SerializedProperty prop, string label, string tooltip = null) {
            var pf = new PropertyField();
            if (prop != null) pf.BindProperty(prop);
            pf.label = label;
            if (tooltip != null) pf.tooltip = tooltip;
            return pf;
        }

        private VisualElement MakeEnumToggle(SerializedProperty prop, string label, System.Action onChanged = null) {
            if (prop == null) return new VisualElement();

            var enumField = new EnumField(label, prop.boolValue ? TrueFalse.True : TrueFalse.False);
            enumField.RegisterValueChangedCallback(evt => {
                prop.boolValue = (TrueFalse)evt.newValue == TrueFalse.True;
                prop.serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
            enumField.TrackPropertyValue(prop, _ => {
                enumField.SetValueWithoutNotify(prop.boolValue ? TrueFalse.True : TrueFalse.False);
                onChanged?.Invoke();
            });

            return enumField;
        }

        private VisualElement MakeEnumToggle(string relPath, string label, string parentPath = null) {
            var prop = parentPath != null
                ? _jiggleRigData.FindPropertyRelative(parentPath).FindPropertyRelative(relPath)
                : _jiggleRigData.FindPropertyRelative(relPath);
            return MakeEnumToggle(prop, label, UpdateSubToggles);
        }

        private VisualElement MakeCurvedFloatRow(string name) {
            var prop = GetCurvedFloatProp(name);
            if (prop == null) return new VisualElement();

            var valueProp = prop.FindPropertyRelative("value");
            var curveEnabledProp = prop.FindPropertyRelative("curveEnabled");
            var curveProp = prop.FindPropertyRelative("curve");
            if (valueProp == null || curveEnabledProp == null || curveProp == null)
                return new VisualElement();

            // Wrapper vertical
            var wrapper = new VisualElement();

            // ── Row: label | slider (+ value) | btn ──
            var row = new VisualElement();
            row.AddToClassList("curved-float-row");

            var label = new Label(GetDisplayName(name));
            label.tooltip = GetHint(name);
            row.Add(label);

            // Slider, in the domain the package documents (see JiggleTreeInputParameters), with its value field
            // so an exact number can be typed next to the handle.
            var (min, max) = GetRange(name);
            var slider = new Slider(min, max) {
                label = null,
                showInputField = true,
                tooltip = GetHint(name)
            };
            slider.AddToClassList("curved-slider");
            slider.BindProperty(valueProp);
            row.Add(slider);

            // Curve toggle button
            var btn = new Button { text = "~" };
            btn.AddToClassList("curve-toggle-btn");
            btn.tooltip = "Curve of this value along the chain (0 = root, 1 = tip).";
            btn.clicked += () => {
                curveEnabledProp.boolValue = !curveEnabledProp.boolValue;
                curveEnabledProp.serializedObject.ApplyModifiedProperties();
                UpdateCurveVisibility(name, curveEnabledProp.boolValue);
            };
            row.Add(btn);
            _curveButtons[name] = btn;

            wrapper.Add(row);

            // ── Curve (below row, aligned with slider+button) ──
            var curveField = new CurveField();
            curveField.AddToClassList("curved-field");
            curveField.style.display = DisplayStyle.None;
            curveField.BindProperty(curveProp);
            wrapper.Add(curveField);
            _curveContainers[name] = curveField;
            _curveFields[name] = curveField;

            // Initial curve visibility
            UpdateCurveVisibility(name, curveEnabledProp.boolValue);

            // Track changes
            slider.TrackPropertyValue(curveEnabledProp, _ => UpdateCurveVisibility(name, curveEnabledProp.boolValue));

            return wrapper;
        }

        private void UpdateCurveVisibility(string name, bool visible) {
            if (_curveContainers.TryGetValue(name, out var container))
                container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_curveButtons.TryGetValue(name, out var btn))
                btn.text = visible ? "×" : "~";
        }

        private static string GetDisplayName(string name) => name switch {
            "stiffness" => "Stiffness",
            "drag" => "Drag",
            "airDrag" => "Air Drag",
            "gravity" => "Gravity",
            "stretch" => "Stretch",
            "angleLimit" => "Angle Limit",
            "collisionRadius" => "Collision Radius",
            _ => name
        };

        /// <summary>Domain of each force, in the unit the package documents (see JiggleTreeInputParameters).</summary>
        private static (float Min, float Max) GetRange(string name) => name switch {
            "gravity" => (-2f, 2f),
            "collisionRadius" => (0f, 0.5f),
            _ => (0f, 1f)
        };

        private static string GetHint(string name) => name switch {
            "stiffness" => "How fast the chain goes back to its animated shape (0 = no shape memory, 1 = follows it).",
            "drag" => "Damping of the local motion (0 = free, 1 = never settles).",
            "airDrag" => "Damping of the motion inherited from the parents (1 = the chain ignores the body moving).",
            "gravity" => "World gravity multiplier, negative values pull the chain up.",
            "stretch" => "Length elasticity (0 = constant length, 1 = fully elastic).",
            "angleLimit" => "Maximum bend, as a fraction of a quarter turn (1 = 90°).",
            "collisionRadius" => "Radius of the collision shape around every bone, in metres.",
            _ => null
        };

        private SerializedProperty GetCurvedFloatProp(string name) => name switch {
            "stiffness" => _stiffness,
            "drag" => _drag,
            "airDrag" => _airDrag,
            "gravity" => _gravity,
            "stretch" => _stretch,
            "angleLimit" => _angleLimit,
            "collisionRadius" => _collisionRadius,
            _ => null
        };

        // ── Mode toggle ──────────────────────────────────────────────────────

        private enum JigglePhysicsMode { Simplified, Advanced }

        private void UpdateModeVisibility() {
            var isAdvanced = _advancedToggle?.boolValue ?? false;
            if (_groupSimplified != null)
                _groupSimplified.style.display = isAdvanced ? DisplayStyle.None : DisplayStyle.Flex;
            if (_groupAdvanced != null)
                _groupAdvanced.style.display = isAdvanced ? DisplayStyle.Flex : DisplayStyle.None;
            if (_modeField != null)
                _modeField.value = isAdvanced ? JigglePhysicsMode.Advanced : JigglePhysicsMode.Simplified;
            UpdateSubToggles();
        }

        private void UpdateSubToggles() {
            if (_groupAngleLimit != null)
                _groupAngleLimit.style.display = (_angleLimitToggle?.boolValue ?? false) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_groupCollision != null)
                _groupCollision.style.display = (_collisionToggle?.boolValue ?? false) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Foldout titles and summary ───────────────────────────────────────

        /// <summary>Count in the collider foldout title, so the list does not have to be opened to know it.</summary>
        private void UpdateColliderCount() {
            if (_collidersFoldout == null)
                return;

            var count = _collidersProp?.arraySize ?? 0;
            _collidersFoldout.text = count > 0 ? $"Colliders ({count})" : "Colliders";
        }

        /// <summary>
        /// Lists the animator parameters the rig publishes, so they can be added to the avatar controllers:
        /// the parameter module only registers what the controllers declare.
        /// </summary>
        private void UpdateParameterNames() {
            if (_parameterNames == null) return;

            var prefix = _parameterPrefix?.stringValue;
            _parameterNames.Message = string.IsNullOrEmpty(prefix)
                ? "Set a prefix to expose {prefix}/IsGrabbed, /IsPosed, /Angle, /Stretch and /Squish to the Animator."
                : $"Parameters: {prefix}/IsGrabbed, {prefix}/IsPosed, {prefix}/Angle, {prefix}/Stretch, {prefix}/Squish";
        }
    }
}
