using GatorDragonGames.JigglePhysics;
using UnityEditor;
using UnityEngine;

namespace Nox.CCK.Jiggles.Editor {
    public static class JiggleConversionMenus {
        private const string ConvertMenuPath = "GameObject/Nox/Convert to CCK Jiggles/";

        // ── JiggleRig → Jiggle ────────────────────────────────────────────────

        [MenuItem(ConvertMenuPath + "JiggleRig → Jiggle", false, 0)]
        private static void ConvertJiggleRigToJiggle() {
            foreach (var go in Selection.gameObjects) {
                ConvertJiggleRigOnGameObject(go);
            }
        }

        [MenuItem(ConvertMenuPath + "JiggleRig → Jiggle", true)]
        private static bool ConvertJiggleRigToJiggleValidate()
            => HasComponentOnSelection<JiggleRig>() && !HasComponentOnSelection<Jiggle>();

        private static void ConvertJiggleRigOnGameObject(GameObject go) {
            var rig = go.GetComponent<JiggleRig>();
            if (rig == null || go.GetComponent<Jiggle>() != null)
                return;

            // Read private jiggleRigData and animatedParameters via SerializedObject
            var so = new SerializedObject(rig);
            var dataProp = so.FindProperty("jiggleRigData");
            var animProp = so.FindProperty("animatedParameters");

            // Add Jiggle component
            var jiggle = Undo.AddComponent<Jiggle>(go);

            // Copy serialized data to the new Jiggle
            var jiggleSo = new SerializedObject(jiggle);
            var jiggleDataProp = jiggleSo.FindProperty("jiggleRigData");
            var jiggleAnimProp = jiggleSo.FindProperty("animatedParameters");

            if (dataProp != null && jiggleDataProp != null)
                CopyProperty(dataProp, jiggleDataProp);
            if (animProp != null && jiggleAnimProp != null)
                CopyProperty(animProp, jiggleAnimProp);

            jiggleSo.ApplyModifiedPropertiesWithoutUndo();

            // Remove original JiggleRig
            Undo.DestroyObjectImmediate(rig);

            EditorUtility.SetDirty(go);
            Debug.Log($"[Nox.CCK.Jiggles] Converted JiggleRig → Jiggle on '{go.name}'.", go);
        }

        // ── JiggleColliderExample → Collider ──────────────────────────────────

        [MenuItem(ConvertMenuPath + "ColliderExample → Collider", false, 1)]
        private static void ConvertColliderExampleToCollider() {
            foreach (var go in Selection.gameObjects) {
                ConvertColliderExampleOnGameObject(go);
            }
        }

        [MenuItem(ConvertMenuPath + "ColliderExample → Collider", true)]
        private static bool ConvertColliderExampleToColliderValidate()
            => HasComponentOnSelection<JiggleColliderExample>() && !HasComponentOnSelection<Collider>();

        private static void ConvertColliderExampleOnGameObject(GameObject go) {
            var example = go.GetComponent<JiggleColliderExample>();
            if (example == null || go.GetComponent<Collider>() != null)
                return;

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

            // Remove original JiggleColliderExample
            Undo.DestroyObjectImmediate(example);

            EditorUtility.SetDirty(go);
            Debug.Log($"[Nox.CCK.Jiggles] Converted JiggleColliderExample → Collider on '{go.name}'.", go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool HasComponentOnSelection<T>() where T : Component {
            foreach (var go in Selection.gameObjects) {
                if (go.GetComponent<T>() != null)
                    return true;
            }
            return false;
        }

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
                            Debug.LogWarning($"[JiggleConversion] Could not find matching property '{dstChild.name}' in source.");
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
                        for (int i = 0; i < src.arraySize; i++)
                            CopyProperty(src.GetArrayElementAtIndex(i), dst.GetArrayElementAtIndex(i));
                    }
                    break;
            }
        }
    }
}
