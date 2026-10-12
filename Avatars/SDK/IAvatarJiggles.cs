using Nox.Avatars;

namespace Nox.Jiggles.Avatars {
    /// <summary>
    /// Controls the jiggle (secondary motion) of one avatar. Implemented by the avatar jiggle module and
    /// resolved through <see cref="IAvatarDescriptor.GetModules{T}"/>.
    /// <para>
    /// An avatar has a <b>single</b> jiggle group — <see cref="Group"/>, the id of the avatar's descriptor —
    /// so every rig of the avatar is flagged together. A flagged group is not updated: its rigs are kept out
    /// of the simulation while the avatar is placed, moved or rescaled, and are rebuilt from the current pose
    /// once it is enabled again, so they never resume with an offset between the simulated element and where
    /// it is drawn.
    /// </para>
    /// </summary>
    public interface IAvatarJiggles : IAvatarModule {
        /// <summary>
        /// Group of this avatar's jiggles: the id of its <see cref="IAvatarDescriptor"/> (see
        /// <c>Nox.CCK.Utils.ComponentExtension.GetId</c>), so two avatars never share a group.
        /// </summary>
        string Group { get; }

        /// <summary>Whether the avatar's jiggles are currently updated (see <see cref="SetEnabled"/>).</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Flags the avatar's jiggles as updated (<c>true</c>) or as "do not update" (<c>false</c>). Disabling
        /// stops the simulation without touching the animation; enabling rebuilds the simulation from the
        /// current pose. The module disables them while the avatar is being placed and enables them once it
        /// has settled.
        /// </summary>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Rebuilds every rig from the current pose: the simulated points are re-seeded at the animated bone
        /// positions, dropping any offset accumulated while the avatar moved (teleport, rescale, load, …).
        /// A no-op while the jiggles are disabled.
        /// </summary>
        void Resync();
    }
}
