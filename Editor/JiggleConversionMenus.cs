using GatorDragonGames.JigglePhysics;
using UnityEditor;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Jiggles.Editor {
    public static class JiggleConversionMenus {
        private const string ConvertMenuPath = "GameObject/Nox/Convert to CCK Jiggles/";

        public const string LogTag = nameof(JiggleConverter);

        // ── JiggleRig → Jiggle ────────────────────────────────────────────────

        [MenuItem(ConvertMenuPath + "JiggleRig → Jiggle", false, 0)]
        private static void ConvertJiggleRigToJiggle() {
            var count = 0;

            foreach (var go in Selection.gameObjects)
                count += ConvertJiggleRigsUnder(go);

            if (count == 0)
                Logger.LogWarning("Nothing to convert: no JiggleRig found under the selection.", tag: LogTag);
        }

        [MenuItem(ConvertMenuPath + "JiggleRig → Jiggle", true)]
        private static bool ConvertJiggleRigToJiggleValidate() {
            foreach (var go in Selection.gameObjects) {
                if (!go)
                    continue;

                foreach (var rig in go.GetComponentsInChildren<JiggleRig>(true)) {
                    if (!rig || rig is Jiggle)
                        continue;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts every rig of the hierarchy that is not a <see cref="Jiggle"/> yet: selecting a parent
        /// converts all of its jiggles at once.
        /// </summary>
        private static int ConvertJiggleRigsUnder(GameObject root) {
            if (!root)
                return 0;

            var count = 0;

            foreach (var rig in root.GetComponentsInChildren<JiggleRig>(true)) {
                if (!rig || rig is Jiggle)
                    continue;

                ConvertJiggleRig(rig);
                count++;
            }

            return count;
        }

        private static void ConvertJiggleRig(JiggleRig rig) {
            var go       = rig.gameObject;
            var data     = rig.GetJiggleRigData();
            var animated = rig.HasAnimatedParameters;

            // The old rig unregisters when it goes away: drop it before the new one registers on the same root
            // (JigglePhysics refuses two trees on one root transform).
            rig.enabled = false;
            Undo.DestroyObjectImmediate(rig);

            // In play mode the rig initializes in OnEnable: keep the GameObject inactive until its data is set,
            // otherwise the component registers (or throws) with an empty rig.
            var wasActive = go.activeSelf;
            if (Application.isPlaying && wasActive)
                go.SetActive(false);

            // Add Jiggle component and carry the rig data over
            var jiggle = Undo.AddComponent<Jiggle>(go);
            Undo.RecordObject(jiggle, "Convert JiggleRig → Jiggle");
            jiggle.Configure(data);
            jiggle.HasAnimatedParameters = animated;
            EditorUtility.SetDirty(jiggle);

            if (wasActive)
                go.SetActive(true);

            EditorUtility.SetDirty(go);
            Logger.Log($"Converted JiggleRig → Jiggle on '{go.name}'.", go, tag: LogTag);
        }

        // ── JiggleColliderExample → Collider ──────────────────────────────────

        [MenuItem(ConvertMenuPath + "ColliderExample → Collider", false, 1)]
        private static void ConvertColliderExampleToCollider() {
            var count = 0;

            foreach (var go in Selection.gameObjects)
                count += ConvertColliderExamplesUnder(go);

            if (count == 0)
                Logger.LogWarning("Nothing to convert: no JiggleColliderExample found under the selection.", tag: LogTag);
        }

        [MenuItem(ConvertMenuPath + "ColliderExample → Collider", true)]
        private static bool ConvertColliderExampleToColliderValidate() {
            foreach (var go in Selection.gameObjects) {
                if (!go)
                    continue;

                foreach (var example in go.GetComponentsInChildren<JiggleColliderExample>(true))
                    if (example && !example.GetComponent<Collider>())
                        return true;
            }

            return false;
        }

        /// <summary>
        /// Converts every collider of the hierarchy that has no CCK <see cref="Collider"/> yet: selecting a
        /// parent converts all of its colliders at once.
        /// </summary>
        private static int ConvertColliderExamplesUnder(GameObject root) {
            if (!root)
                return 0;

            var count = 0;

            foreach (var example in root.GetComponentsInChildren<JiggleColliderExample>(true)) {
                if (!example || example.GetComponent<Collider>())
                    continue;

                ConvertColliderExample(example);
                count++;
            }

            return count;
        }

        private static void ConvertColliderExample(JiggleColliderExample example) {
            var go = example.gameObject;

            // Read private jiggleCollider via SerializedObject
            var so = new SerializedObject(example);
            var colliderProp = so.FindProperty("jiggleCollider");

            // Add Collider component
            var collider = Undo.AddComponent<Collider>(go);

            // Copy serialized jiggleCollider data
            var colliderSo = new SerializedObject(collider);
            var targetProp = colliderSo.FindProperty("jiggleCollider");

            if (colliderProp != null && targetProp != null)
                CopyProperty(colliderProp, targetProp);

            colliderSo.ApplyModifiedPropertiesWithoutUndo();

            // Remove original JiggleColliderExample: it unregisters itself, the CCK collider is registered by
            // the avatar module when the avatar is enabled.
            Undo.DestroyObjectImmediate(example);

            EditorUtility.SetDirty(go);
            Logger.Log($"Converted JiggleColliderExample → Collider on '{go.name}'.", go, tag: LogTag);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Deep-copies a SerializedProperty from source to target by walking its children.
        /// Works on all Unity versions without relying on CopySerialized/CopySerializedIfDifferent.
        /// </summary>
        private static void CopyProperty(SerializedProperty src, SerializedProperty dst) {
            if (src == null || dst == null)
                return;

            switch (dst.propertyType) {
                case SerializedPropertyType.Generic:
                    // Recurse into children
                    var srcChild = src.Copy();
                    var dstChild = dst.Copy();
                    var srcEnd = src.GetEndProperty();
                    var dstEnd = dst.GetEndProperty();

                    if (!srcChild.NextVisible(true) || !dstChild.NextVisible(true))
                        return;

                    do {
                        // Find matching child by name
                        var srcIter = srcChild.Copy();
                        var found = false;
                        do {
                            if (srcIter.name == dstChild.name) {
                                CopyProperty(srcIter, dstChild);
                                found = true;
                                break;
                            }
                        } while (srcIter.NextVisible(false) && !SerializedProperty.EqualContents(srcIter, srcEnd));

                        if (!found) {
                            Logger.LogWarning($"Could not find matching property '{dstChild.name}' in source.", tag: LogTag);
                        }
                    } while (dstChild.NextVisible(false) && !SerializedProperty.EqualContents(dstChild, dstEnd));
                    break;

                case SerializedPropertyType.ObjectReference:
                    dst.objectReferenceValue = src.objectReferenceValue;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    dst.intValue = src.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    dst.boolValue = src.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    dst.floatValue = src.floatValue;
                    break;
                case SerializedPropertyType.String:
                    dst.stringValue = src.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    dst.colorValue = src.colorValue;
                    break;
                case SerializedPropertyType.Vector2:
                    dst.vector2Value = src.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    dst.vector3Value = src.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    dst.vector4Value = src.vector4Value;
                    break;
                case SerializedPropertyType.Quaternion:
                    dst.quaternionValue = src.quaternionValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    dst.animationCurveValue = src.animationCurveValue;
                    break;
                case SerializedPropertyType.Bounds:
                    dst.boundsValue = src.boundsValue;
                    break;
                case SerializedPropertyType.Rect:
                    dst.rectValue = src.rectValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    dst.vector2IntValue = src.vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    dst.vector3IntValue = src.vector3IntValue;
                    break;
                case SerializedPropertyType.RectInt:
                    dst.rectIntValue = src.rectIntValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    dst.boundsIntValue = src.boundsIntValue;
                    break;
                default:
                    // Fallback: use managedReferenceValue for class types
                    if (src.isArray) {
                        dst.arraySize = src.arraySize;
                        for (var i = 0; i < src.arraySize; i++)
                            CopyProperty(src.GetArrayElementAtIndex(i), dst.GetArrayElementAtIndex(i));
                    }
                    break;
            }
        }
    }
}
