using System.Collections.Generic;
using Nox.CCK.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nox.CCK.Jiggles.Editor {
    [CustomEditor(typeof(Jiggle))]
    public class JiggleEditor : InspectorEditor<Jiggle> {
        private SerializedProperty _jiggleRigData;
        private SerializedProperty _animatedParameters;

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

        private Label _paramsSummary;

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
            BuildUI(Content);
            return root;
        }

        private void InitProperties() {
            _jiggleRigData = serializedObject.FindProperty("jiggleRigData");
            _animatedParameters = serializedObject.FindProperty("animatedParameters");

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
        }

        // ── Build UI ──────────────────────────────────────────────────────────

        private void BuildUI(VisualElement content) {
            // ── Transform foldout ────────────────────────────────────────
            var transformFoldout = new Foldout { text = "Transform", value = true };
            transformFoldout.Add(MakeProp("rootBone", "Root Transform"));
            transformFoldout.Add(MakeProp("excludeRoot", "Exclude Root"));
            transformFoldout.Add(MakeProp("excludedTransforms", "Excluded Transforms"));
            content.Add(transformFoldout);

            // ── Forces foldout ───────────────────────────────────────────
            var forcesFoldout = new Foldout { text = "Forces", value = true };
            _modeField = new EnumField("Mode", JigglePhysicsMode.Simplified);
            _modeField.RegisterValueChangedCallback(evt => {
                _advancedToggle.boolValue = ((JigglePhysicsMode)evt.newValue) == JigglePhysicsMode.Advanced;
                _advancedToggle.serializedObject.ApplyModifiedProperties();
                UpdateModeVisibility();
            });
            forcesFoldout.Add(_modeField);

            _groupSimplified = new VisualElement();
            foreach (var name in SimplifiedNames) _groupSimplified.Add(MakeCurvedFloatRow(name));
            forcesFoldout.Add(_groupSimplified);

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

            forcesFoldout.Add(_groupAdvanced);
            content.Add(forcesFoldout);

            // ── Colliders foldout ────────────────────────────────────────
            var collidersFoldout = new Foldout { text = "Colliders", value = true };
            var collidersProp = serializedObject.FindProperty("jiggleColliders");
            var collidersField = new PropertyField();
            collidersField.BindProperty(collidersProp);
            collidersField.label = "Jiggle Colliders";
            collidersFoldout.Add(collidersField);
            content.Add(collidersFoldout);

            // ── Parameters foldout ───────────────────────────────────────
            var paramsFoldout = new Foldout { text = "Parameters", value = true };
            _paramsSummary = new Label();
            UpdateSummary();
            paramsFoldout.Add(_paramsSummary);
            paramsFoldout.Add(MakeEnumToggle(_animatedParameters, "Animated Parameters"));
            content.Add(paramsFoldout);

            // Initial state
            UpdateModeVisibility();
            UpdateSubToggles();

            // Track summary updates
            content.TrackPropertyValue(_stiffness.FindPropertyRelative("value"), _ => UpdateSummary());
            content.TrackPropertyValue(_drag.FindPropertyRelative("value"), _ => UpdateSummary());
            content.TrackPropertyValue(_gravity.FindPropertyRelative("value"), _ => UpdateSummary());

            content.TrackPropertyValue(_advancedToggle, _ => UpdateModeVisibility());
            content.TrackPropertyValue(_angleLimitToggle, _ => UpdateSubToggles());
            content.TrackPropertyValue(_collisionToggle, _ => UpdateSubToggles());
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

            // Wrapper vertical
            var wrapper = new VisualElement();

            // ── Row: label | slider | btn ──
            var row = new VisualElement();
            row.AddToClassList("curved-float-row");

            var label = new Label(GetDisplayName(name));
            row.Add(label);

            // Slider
            var slider = new Slider();
            slider.AddToClassList("curved-slider");
            slider.label = null;
            slider.BindProperty(valueProp);
            // Set range based on property
            (slider.lowValue, slider.highValue) = name switch {
                "stiffness" => (0f, 100f),
                "drag" => (0f, 50f),
                "airDrag" => (0f, 2f),
                "gravity" => (-20f, 20f),
                "stretch" => (0f, 2f),
                "angleLimit" => (0f, 180f),
                "collisionRadius" => (0f, 0.5f),
                _ => (0f, 1f)
            };
            row.Add(slider);

            // Curve toggle button
            var btn = new Button { text = "~" };
            btn.AddToClassList("curve-toggle-btn");
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
            slider.TrackPropertyValue(curveEnabledProp, _ =>
                UpdateCurveVisibility(name, curveEnabledProp.boolValue));

            return wrapper;
        }

        private void UpdateCurveVisibility(string name, bool visible) {
            if (_curveContainers.TryGetValue(name, out var container))
            if (_curveButtons.TryGetValue(name, out var btn))
                btn.text = visible ? "×" : "~";
                container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

        // ── Summary ──────────────────────────────────────────────────────────

        private void UpdateSummary() {
            if (_paramsSummary == null) return;
            var stiff = _stiffness?.FindPropertyRelative("value")?.floatValue ?? 0f;
            var drg = _drag?.FindPropertyRelative("value")?.floatValue ?? 0f;
            var grav = _gravity?.FindPropertyRelative("value")?.floatValue ?? 0f;
            _paramsSummary.text = $"Stiffness: {stiff:F2}   Drag: {drg:F2}   Gravity: {grav:F2}";
        }
    }
}
