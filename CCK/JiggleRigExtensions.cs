using System.Runtime.CompilerServices;
using GatorDragonGames.JigglePhysics;

namespace Nox.CCK.Jiggles {
    /// <summary>
    /// Runtime control of a jiggle rig: suspends it while its avatar is placed, moved or rescaled, and rebuilds
    /// its simulation from the current pose once it comes back.
    /// <para>
    /// Works on any <see cref="JiggleRig"/> — the CCK <see cref="Jiggle"/> included — so an avatar does not
    /// have to be converted to be driven by <c>JiggleModule</c>. The suspension state lives here (the package
    /// has none) and dies with the rig.
    /// </para>
    /// </summary>
    public static class JiggleRigExtensions {
        private static readonly ConditionalWeakTable<JiggleRig, object> Suspended = new();
        private static readonly object Marker = new();

        /// <summary>True while the rig is unregistered from the simulation (see <see cref="Suspend"/>).</summary>
        public static bool IsSuspended(this JiggleRig rig)
            => rig && Suspended.TryGetValue(rig, out _);

        /// <summary>
        /// Unregisters the rig from the simulation: its bones stop being simulated and keep whatever the
        /// animation writes. Idempotent.
        /// </summary>
        public static void Suspend(this JiggleRig rig) {
            if (!rig || IsSuspended(rig))
                return;

            rig.OnRemove();
            Suspended.Add(rig, Marker);
        }

        /// <summary>
        /// Registers the rig again and rebuilds its simulation from the current pose, so a rig suspended while
        /// the avatar moved does not resume with a stale offset. Idempotent.
        /// </summary>
        public static void Resume(this JiggleRig rig) {
            if (!rig || !IsSuspended(rig))
                return;

            Suspended.Remove(rig);
            Resync(rig);
        }

        /// <summary>
        /// Rebuilds the simulation from the current pose: the rig is unregistered then registered again, which
        /// dirties its tree, so the jobs re-seed the simulated points at the animated positions and drop any
        /// offset accumulated while the avatar moved (teleport, rescale, load, …).
        /// A no-op while suspended (the rebuild happens on <see cref="Resume"/>) or without a root bone.
        /// </summary>
        public static void Resync(this JiggleRig rig) {
            if (!rig || IsSuspended(rig) || rig.GetJiggleRigData().rootBone == null)
                return;

            rig.OnRemove();
            rig.OnInitialize();
        }
    }
}
