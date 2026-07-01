using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.Avatars;
using Nox.CCK.Jiggles;
using Nox.CCK.Utils;
using UnityEngine;
using Collider = Nox.CCK.Jiggles.Collider;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Jiggles {
    /// <summary>
    /// Single avatar module that drives all JigglePhysics components on an avatar.
    /// Finds <see cref="Nox.CCK.Jiggles.Jiggle"/> and <see cref="Nox.CCK.Jiggles.Collider"/>
    /// components on the descriptor and manages their lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public class JiggleModule : MonoBehaviour, IAvatarModule {
        public int Priority
            => 40;

        public async UniTask<bool> Setup(IRuntimeAvatar runtimeAvatar, AvatarModulePhase phase, CancellationToken token = default) {
            if (phase != AvatarModulePhase.Init)
                return true;

            await UniTask.Yield(cancellationToken: token);
            // OnEnable handles initialization — nothing to do here
            return true;
        }

        private void OnEnable() {
            // Initialize all Jiggle rigs
            var jiggles = GetComponentsInChildren<Jiggle>(true);
            foreach (var j in jiggles)
                j.OnInitialize();

            // Register all colliders
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.Register();
        }

        private void OnDisable() {
            // Unregister colliders
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.Unregister();

            // Remove jiggle rigs
            var jiggles = GetComponentsInChildren<Jiggle>(true);
            foreach (var j in jiggles)
                j.OnRemove();
        }

        /// <summary>
        /// Checks that the avatar descriptor's JigglePhysics setup is valid.
        /// </summary>
        public static bool Check(IAvatarDescriptor descriptor) {
            var jiggles = descriptor.Anchor.GetComponentsInChildren<Jiggle>(true);

            if (jiggles.Length == 0)
                return true; // No jiggles configured — allowed

            foreach (var j in jiggles) {
                if (j == null) continue;
                var data = j.GetJiggleRigData();
                if (data.GetHasRootTransformError()) {
                    Logger.LogError("Jiggle component has no root bone assigned.", j);
                    return false;
                }
            }

            return true;
        }
    }
}
