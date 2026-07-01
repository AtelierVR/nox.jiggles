using GatorDragonGames.JigglePhysics;
using UnityEngine;
using Gizmos = Nox.CCK.Development.Gizmos;

namespace Nox.CCK.Jiggles {
    /// <summary>
    /// CCK component that registers a JigglePhysics collider (sphere, capsule or plane)
    /// into the global JigglePhysics system.
    /// This is a passive data component — the actual lifecycle is driven by <see cref="JiggleModule"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class Collider : MonoBehaviour {
        [SerializeField]
        private JiggleColliderSerializable jiggleCollider;

        [field: SerializeField]
        public Vector3 offset { get; private set; }

        [field: SerializeField]
        public Quaternion offsetRotation { get; private set; } = Quaternion.identity;

        private bool _registered;

        public JiggleColliderSerializable ColliderData
            => jiggleCollider;

        /// <summary>
        /// Registers this collider with the JigglePhysics system (idempotent).
        /// </summary>
        public void Register() {
            if (_registered)
                return;
            if (jiggleCollider.transform == null)
                jiggleCollider.transform = transform;
            jiggleCollider.transform.position += offset;
            jiggleCollider.transform.rotation *= offsetRotation;
            JigglePhysics.AddJiggleCollider(jiggleCollider);
            _registered = true;
        }

        /// <summary>
        /// Unregisters this collider from the JigglePhysics system.
        /// </summary>
        public void Unregister() {
            if (!_registered)
                return;
            JigglePhysics.RemoveJiggleCollider(jiggleCollider);
            _registered = false;
        }

        private void OnDrawGizmos() {
            var t = jiggleCollider.transform != null ? jiggleCollider.transform : transform;
            var c = jiggleCollider.collider;
            var pos = t.position + offset;
            var rot = t.rotation * offsetRotation;
            var averageScale = t.lossyScale.x;

            Gizmos.Color = new Color(0.854902f, 0.6470588f, 0.1254902f, 1f);
            var r = Mathf.Max(0f, c.radius) * averageScale;

            switch (c.type) {
                case JiggleCollider.JiggleColliderType.Sphere:
                    Gizmos.DrawWireSphere(pos, r);
                    break;
                case JiggleCollider.JiggleColliderType.Capsule: {
                    var axisDir = c.capsuleAxis switch {
                        JiggleCollider.CapsuleAxis.X => rot * Vector3.right,
                        JiggleCollider.CapsuleAxis.Y => rot * Vector3.up,
                        JiggleCollider.CapsuleAxis.Z => rot * Vector3.forward,
                        _ => rot * Vector3.up
                    };
                    var halfHeight = Mathf.Max(0f, c.height) * averageScale * 0.5f;
                    var top = pos + axisDir * halfHeight;
                    var bottom = pos - axisDir * halfHeight;
                    Gizmos.DrawWireCapsule(bottom, top, r);
                    break;
                }
                case JiggleCollider.JiggleColliderType.Plane: {
                    var up = rot * Vector3.up;
                    var right = rot * Vector3.right;
                    var forward = rot * Vector3.forward;
                    var size = 1f;
                    var p1 = pos + (right + forward) * size;
                    var p2 = pos + (right - forward) * size;
                    var p3 = pos + (-right - forward) * size;
                    var p4 = pos + (-right + forward) * size;
                    Gizmos.DrawLine(p1, p2);
                    Gizmos.DrawLine(p2, p3);
                    Gizmos.DrawLine(p3, p4);
                    Gizmos.DrawLine(p4, p1);
                    Gizmos.DrawLine(p1, p3);
                    Gizmos.DrawLine(p2, p4);
                    Gizmos.DrawLine(pos, pos + up * size * 0.5f);
                    break;
                }
            }
        }

        private void OnDestroy() {
            Unregister();
        }
    }
}
