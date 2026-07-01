using GatorDragonGames.JigglePhysics;
using Nox.CCK.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nox.CCK.Jiggles.Editor {
    [CustomEditor(typeof(Collider))]
    public class ColliderEditor : InspectorEditor<Collider> {
        private SerializedProperty _jiggleCollider;
        private SerializedProperty _transform;
        private SerializedProperty _offset;
        private SerializedProperty _offsetRotation;
        private SerializedProperty _collider;
        private SerializedProperty _type;
        private SerializedProperty _radius;

        private VisualElement _groupCapsule;
        private HelpBox _previewBox;

        public override VisualElement CreateInspectorGUI() {
            var root = base.CreateInspectorGUI();
            if (Content == null) return root;

            var uxml = Resources.Load<VisualTreeAsset>("ColliderEditor");
            if (uxml != null) {
                var contentRoot = uxml.CloneTree();
                Content.Add(contentRoot);

                var uss = Resources.Load<StyleSheet>("ColliderEditor");
                if (uss != null) root.styleSheets.Add(uss);

                BindSerializedProperties(contentRoot);
                BindShapeToggle(contentRoot);
                BindPreview(contentRoot);
            } else {
                Content.Add(new Label("ColliderEditor.uxml not found in Resources."));
            }

            return root;
        }

        private void BindSerializedProperties(VisualElement root) {
            _jiggleCollider = serializedObject.FindProperty("jiggleCollider");
            _offset = serializedObject.FindProperty("offset");
            _offsetRotation = serializedObject.FindProperty("offsetRotation");
            _transform = _jiggleCollider.FindPropertyRelative("transform");
            _collider = _jiggleCollider.FindPropertyRelative("collider");
            _type = _collider.FindPropertyRelative("type");
            _radius = _collider.FindPropertyRelative("radius");

            BindProp(root, "prop-transform", _jiggleCollider.FindPropertyRelative("transform"));
            BindProp(root, "prop-offset", _offset);
            BindProp(root, "prop-offset-rotation", _offsetRotation);
            BindProp(root, "prop-type", _collider.FindPropertyRelative("type"));
            BindProp(root, "prop-radius", _collider.FindPropertyRelative("radius"));
            BindProp(root, "prop-height", _collider.FindPropertyRelative("height"));

            _groupCapsule = root.Q<VisualElement>("group-capsule");
            _previewBox = root.Q<HelpBox>("helpbox-preview");

            // Do NOT call root.Bind(serializedObject) — we bind everything manually
        }

        private void BindProp(VisualElement root, string elementName, SerializedProperty prop) {
            var el = root.Q<PropertyField>(elementName);
            if (el != null && prop != null) el.BindProperty(prop);
            else if (el != null) Debug.LogWarning($"[ColliderEditor] Property '{elementName}' not found.");
        }

        private void BindShapeToggle(VisualElement root) {
            UpdateCapsuleVisibility();
            if (_type != null)
                root.TrackPropertyValue(_type, _ => UpdateCapsuleVisibility());
        }

        private void UpdateCapsuleVisibility() {
            if (_groupCapsule == null || _type == null) return;
            var isCapsule = (JiggleCollider.JiggleColliderType)_type.enumValueIndex == JiggleCollider.JiggleColliderType.Capsule;
            _groupCapsule.style.display = isCapsule ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BindPreview(VisualElement root) {
            UpdatePreview();
            if (_transform != null)
                root.TrackPropertyValue(_transform, _ => UpdatePreview());
            if (_offset != null)
                root.TrackPropertyValue(_offset, _ => UpdatePreview());
            if (_offsetRotation != null)
                root.TrackPropertyValue(_offsetRotation, _ => UpdatePreview());
            if (_type != null)
                root.TrackPropertyValue(_type, _ => UpdatePreview());
            if (_radius != null)
                root.TrackPropertyValue(_radius, _ => UpdatePreview());
        }

        private void UpdatePreview() {
            if (_previewBox == null) return;

            var t = _transform?.objectReferenceValue as Transform;
            var o = _offset?.vector3Value ?? Vector3.zero;
            var or = _offsetRotation?.quaternionValue ?? Quaternion.identity;
            var collType = _type != null ? (JiggleCollider.JiggleColliderType)_type.enumValueIndex : JiggleCollider.JiggleColliderType.Sphere;
            var r = _radius?.floatValue ?? 0f;

            if (t != null) {
                var pos = t.position + o;
                _previewBox.text = $"World Position: {pos}\nOffset: {o}\nRotation: {or.eulerAngles}\nType: {collType}\nRadius: {r:F3}";
                _previewBox.messageType = HelpBoxMessageType.Info;
            } else {
                _previewBox.text = "Assign a Target Transform. If left empty, this GameObject's transform will be used at runtime.";
                _previewBox.messageType = HelpBoxMessageType.Warning;
            }
        }
    }
}
