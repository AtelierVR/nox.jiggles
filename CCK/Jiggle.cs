using System.Collections.Generic;
using GatorDragonGames.JigglePhysics;
using UnityEngine;
using Gizmos = Nox.CCK.Development.Gizmos;

namespace Nox.CCK.Jiggles {
    /// <summary>
    /// CCK component that extends <see cref="JiggleRig"/> for use as an avatar jiggle rig.
    /// Inherits all JiggleRig configuration and behaviour.
    /// Lifecycle is driven by <see cref="JiggleModule"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class Jiggle : JiggleRig {
        [SerializeField]
        private List<Collider> jiggleColliders = new();

        private void Reset() {
            FallbackRootBone();
        }

        private void OnValidate() {
            FallbackRootBone();
        }

        private void FallbackRootBone() {
            var data = GetJiggleRigData();
            if (data.rootBone == null) {
                data.rootBone = transform;
            }
        }

        private void OnDrawGizmosSelect() { }

        private void OnDrawGizmos() {
            FallbackRootBone();
            DrawColliderGizmos();
            DrawBoneGizmos();
        }

        private void DrawColliderGizmos() {
            if (jiggleColliders == null) return;
            Gizmos.Color = new Color(0.854902f, 0.6470588f, 0.1254902f, 1f);
            foreach (var cc in jiggleColliders) {
                if (cc == null) continue;
                var c = cc.ColliderData;
                var t = c.transform != null ? c.transform : cc.transform;
                var col = c.collider;
                var pos = t.position + cc.offset;
                var rot = t.rotation * cc.offsetRotation;
                var avgScale = t.lossyScale.x;
                var r = Mathf.Max(0f, col.radius) * avgScale;

                switch (col.type) {
                    case JiggleCollider.JiggleColliderType.Sphere:
                        Gizmos.DrawWireSphere(pos, r);
                        break;
                    case JiggleCollider.JiggleColliderType.Capsule: {
                        var axisDir = col.capsuleAxis switch {
                            JiggleCollider.CapsuleAxis.X => rot * Vector3.right,
                            JiggleCollider.CapsuleAxis.Y => rot * Vector3.up,
                            JiggleCollider.CapsuleAxis.Z => rot * Vector3.forward,
                            _ => rot * Vector3.up
                        };
                        var halfH = Mathf.Max(0f, col.height) * avgScale * 0.5f;
                        Gizmos.DrawWireCapsule(pos - axisDir * halfH, pos + axisDir * halfH, r);
                        break;
                    }
                    case JiggleCollider.JiggleColliderType.Plane: {
                        var right = rot * Vector3.right;
                        var forward = rot * Vector3.forward;
                        var s = 1f;
                        var p1 = pos + (right + forward) * s;
                        var p2 = pos + (right - forward) * s;
                        var p3 = pos + (-right - forward) * s;
                        var p4 = pos + (-right + forward) * s;
                        Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3);
                        Gizmos.DrawLine(p3, p4); Gizmos.DrawLine(p4, p1);
                        Gizmos.DrawLine(p1, p3); Gizmos.DrawLine(p2, p4);
                        Gizmos.DrawLine(pos, pos + rot * Vector3.up * s * 0.5f);
                        break;
                    }
                }
            }
        }

        private void DrawBoneGizmos() {
            var data = GetJiggleRigData();
            if (data.rootBone == null) return;

            var jiggleTree = JigglePhysics.CreateJiggleTree(data, null);
            var points = jiggleTree.points;
            var parameters = jiggleTree.parameters;
            var bones = jiggleTree.bones;

            Gizmos.Color = new Color(0.9607844f, 0.9607844f, 0.9607844f, 1f);

            for (var i = 0; i < points.Length; i++) {
                var pt = points[i];
                if (pt.parentIndex == -1) continue;
                if (!points[pt.parentIndex].hasTransform) continue;

                var parent = points[pt.parentIndex];
                var parentPos = (Vector3)parent.position;
                var childPos = (Vector3)pt.position;
                var boneScale = (Vector3)bones[i].lossyScale;
                var avgBoneScale = (boneScale.x + boneScale.y + boneScale.z) / 3f;
                var parentRadius = parameters[pt.parentIndex].collisionRadius * avgBoneScale;
                var childRadius = parameters[i].collisionRadius * avgBoneScale;

                Gizmos.DrawWireCapsule(parentPos, childPos, parentRadius, childRadius);

                var boneDir = (childPos - parentPos).normalized;
                var angleLimit = parameters[pt.parentIndex].angleLimit;
                if (angleLimit > 0f) {
                    Gizmos.Color = new Color(0.3f, 0.85f, 0.3f, 0.3f);
                    Gizmos.DrawSolidCone(parentPos, boneDir, angleLimit, 0.05f);
                    Gizmos.Color = new Color(0.9607844f, 0.9607844f, 0.9607844f, 1f);
                }
            }
        }
    }
}
