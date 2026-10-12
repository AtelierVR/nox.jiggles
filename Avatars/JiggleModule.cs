using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GatorDragonGames.JigglePhysics;
using Nox.Avatars;
using Nox.Avatars.Scale;
using Nox.CCK.Jiggles;
using Nox.CCK.Utils;
using Nox.Jiggles.Avatars;
using UnityEngine;
using Collider = Nox.CCK.Jiggles.Collider;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Jiggles.Avatars {
    /// <summary>
    /// Avatar module that drives every <see cref="JiggleRig"/> and <see cref="Collider"/> of an avatar and
    /// exposes it as <see cref="IAvatarJiggles"/>. The CCK <see cref="Jiggle"/> is driven like any other
    /// <see cref="JiggleRig"/>, so an avatar does not have to be converted (see <see cref="JiggleRigExtensions"/>).
    /// <para>
    /// The avatar has a single jiggle group (see <see cref="Group"/>) and its rigs are flagged together: they
    /// are not updated while the avatar is placed, moved or rescaled, and the simulation is <b>re-seeded</b>
    /// once the avatar has settled after loading: a rig built while the avatar was still moving integrates
    /// from a stale pose, which is what stretches hair away from the head.
    /// </para>
    /// <para>
    /// The module listens on the avatar's module bus for <see cref="ScaleChangedEvent"/> (see
    /// <see cref="OnBusEvent"/>): the scale module tells the other modules the avatar changed size, and the
    /// jiggles are rebuilt from the new pose.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class JiggleModule : MonoBehaviour, IAvatarModule, IAvatarJiggles {
        /// <summary>
        /// Frames the simulation keeps running untouched after activation before it is re-seeded. The avatar
        /// loader parents, positions and scales the instance right around activation, so the trees must be
        /// rebuilt once that ministration is over.
        /// </summary>
        private const int SettleFrames = 2;

        private readonly List<JiggleRig> _rigs = new();

        // Group of this avatar's jiggles: the id of its descriptor (see IAvatarJiggles.Group), so a caller
        // can tell which avatar the flag applies to without holding this component.
        private string _group;

        // Whether the caller wants the jiggles updated (see SetEnabled).
        private bool _enabled = true;

        // False while the avatar is still being placed: the jiggles are flagged as "do not update" until it
        // has settled (see SettleFrames).
        private bool _settled;
        private int _settleFrames = -1;

        public int Priority
            => 40;

        /// <inheritdoc />
        public string Group
            => _group;

        /// <inheritdoc />
        public bool IsEnabled
            => _enabled;

        public async UniTask<bool> Setup(IRuntimeAvatar runtimeAvatar, AvatarModulePhase phase, CancellationToken token = default) {
            if (phase != AvatarModulePhase.Init)
                return true;

            // The group is the avatar's descriptor id: resolved here so OnEnable can flag it right away.
            _group = GetGroup(runtimeAvatar?.Descriptor);

            await UniTask.Yield(cancellationToken: token);
            // OnEnable owns the lifecycle; the settle pass re-seeds the simulations once the avatar is placed.
            return true;
        }

        private void OnEnable() {
            // Fallback when the module was not set up (added to a live avatar): the descriptor is the parent.
            _group ??= GetGroup(GetComponentInParent<IAvatarDescriptor>());

            RefreshRigs();

            // Register all colliders
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.Register();

            // The avatar is being placed (the loader positions and scales it around activation): flag the
            // jiggles as "do not update" and let them settle before they are simulated again.
            _settled      = false;
            _settleFrames = SettleFrames;
            Apply();
        }

        private void LateUpdate() {
            if (_settleFrames < 0)
                return;

            // A rig registers itself when its own component is enabled, which can land after this module:
            // keep re-applying the flag until the avatar has settled.
            Apply();

            if (_settleFrames-- > 0)
                return;

            // Everything is placed: clear the flag and rebuild the simulations from the settled pose.
            _settleFrames = -1;
            _settled      = true;
            Apply();
            Resync();
            Logger.LogDebug($"Jiggle simulation re-seeded after avatar load ({_rigs.Count} rig(s)).");
        }

        private void OnDisable() {
            // The avatar is going away: keep it flagged as "do not update".
            _settleFrames = -1;
            _settled      = false;

            // Unregister colliders
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.Unregister();

            // Unregister jiggle rigs
            var jiggles = GetComponentsInChildren<JiggleRig>(true);
            foreach (var jiggle in jiggles)
                jiggle.Suspend();
        }

        /// <summary>
        /// Module-bus handler: the avatar was rescaled (<see cref="ScaleChangedEvent.Name"/>), so the
        /// simulation is rebuilt from the new pose.
        /// </summary>
        public void OnBusEvent(string name, IBusContext context) {
            if (name != ScaleChangedEvent.Name)
                return;

            Resync();
        }

        /// <summary>Collects the avatar's rigs, the CCK <see cref="Jiggle"/> and the plain ones alike.</summary>
        private void RefreshRigs() {
            _rigs.Clear();

            foreach (var jiggle in GetComponentsInChildren<JiggleRig>(true)) {
                if (jiggle.IsDestroyed())
                    continue;

                _rigs.Add(jiggle);
            }
        }

        /// <summary>Group of an avatar's jiggles: the id of its descriptor, or <c>null</c> without one.</summary>
        private static string GetGroup(IAvatarDescriptor descriptor)
            => descriptor != null && descriptor.Anchor 
                ? descriptor.Anchor.GetId().ToString() 
                : null;

        /// <summary>
        /// Applies the flag to every rig: a rig is only simulated while the jiggles are enabled and the
        /// avatar has settled, so it is suspended the rest of the time (and re-seeded when it comes back).
        /// </summary>
        private void Apply() {
            foreach (var rig in _rigs) {
                if (rig.IsDestroyed())
                    continue;

                if (_enabled && _settled)
                    rig.Resume();
                else
                    rig.Suspend();
            }
        }

        #region IAvatarJiggles

        public void SetEnabled(bool enabled) {
            if (_enabled == enabled)
                return;

            _enabled = enabled;
            Apply();
        }

        public void Resync() {
            foreach (var rig in _rigs) {
                if (rig.IsDestroyed())
                    continue;

                // A suspended rig stays on the animation and is re-seeded when it resumes.
                rig.Resync();
            }
        }

        #endregion IAvatarJiggles

        /// <summary>
        /// Checks the avatar's jiggle setup before it is prepared: <b>several</b> modules are refused, every
        /// rig must have a root bone, and a prefab without the module gets one added on its anchor, so every
        /// avatar carries its own jiggles.
        /// </summary>
        public static bool Check(IAvatarDescriptor descriptor) {
            var modules = descriptor.GetModules<JiggleModule>();

            // The module drives every rig of the avatar: several of them is a prefab mistake, refuse it.
            if (modules.Length > 1) {
                Logger.LogError($"{nameof(JiggleModule)} must be present only once on the avatar (found {modules.Length}).", descriptor.Anchor);
                return false;
            }

            var module = modules.Length switch {
                1 => modules.FirstOrDefault(),
                _ => descriptor.Anchor.AddComponent<JiggleModule>()
            };

            if (!module) {
                Logger.LogError($"Verify that the Avatar prefab has a valid {nameof(JiggleModule)} component.", descriptor.Anchor);
                return false;
            }

            foreach (var jiggle in descriptor.Anchor.GetComponentsInChildren<JiggleRig>(true)) {
                if (jiggle.IsDestroyed())
                    continue;

                if (jiggle.GetJiggleRigData().GetHasRootTransformError()) {
                    Logger.LogError("Jiggle component has no root bone assigned.", jiggle);
                    return false;
                }
            }

            return true;
        }
    }
}
