namespace Nox.Jiggles {
    /// <summary>
    /// A jiggle rig controllable at runtime. Implemented by the CCK <c>Jiggle</c> component so the avatar
    /// module can drive it without depending on the concrete type.
    /// </summary>
    public interface IJiggle {
        /// <summary>True while the rig is unregistered from the simulation (see <see cref="Suspend"/>).</summary>
        bool IsSuspended { get; }

        /// <summary>Stops simulating the rig: its bones keep following the animation. Idempotent.</summary>
        void Suspend();

        /// <summary>Resumes the rig and rebuilds its simulation from the current pose. Idempotent.</summary>
        void Resume();

        /// <summary>Rebuilds the simulation from the current pose, dropping any accumulated offset.</summary>
        void Resync();
    }
}
