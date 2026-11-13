using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GatorDragonGames.JigglePhysics;
using Nox.Avatars;
using Nox.Avatars.Parameters;
using Nox.Avatars.Scale;
using Nox.CCK.Utils;
using Nox.Jiggles.Avatars;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Jiggles.Avatars {
	/// <summary>
	/// Avatar module that drives every <see cref="JiggleRig"/> and <see cref="Collider"/> of an avatar and
	/// exposes it as <see cref="IAvatarJiggles"/>. The CCK <see cref="Jiggle"/> is driven like any other
	/// <see cref="JiggleRig"/>, so an avatar does not have to be converted (see <see cref="JiggleRigExtensions"/>).
	/// <para>
	/// The avatar has a single jiggle group (see <see cref="Group"/>) and its rigs are suspended until the
	/// Animator has posed the avatar: the prefab is instantiated and prepared while inactive, so the trees
	/// must be built from the first real pose and not from the spawn one (a rig seeded too early keeps its
	/// offsets from the wrong pose, which is what stretches hair away from the head).
	/// </para>
	/// <para>
	/// The module listens on the avatar's module bus for <see cref="ScaleChangedEvent"/> (see
	/// <see cref="OnBusEvent"/>): the scale module tells the other modules the avatar changed size, and the
	/// jiggles are rebuilt from the new pose.
	/// </para>
	/// <para>
	/// It also exposes the interaction parameters of its <see cref="Jiggle"/> rigs to the avatar's parameter
	/// module (see <see cref="JiggleParameterPublisher"/>): the values are measured on the simulated
	/// transforms, hence the late execution order (the jiggle updater poses the bones in its own
	/// <c>LateUpdate</c>).
	/// </para>
	/// </summary>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(10000)]
	public class JiggleModule : MonoBehaviour, IAvatarModule, IAvatarJiggles {
		public const string LogTag = nameof(JiggleModule);

		private readonly List<JiggleRig> _rigs = new();
		private readonly List<JiggleParameterPublisher> _publishers = new();

		/// <summary>Avatar parameter module the interaction parameters are written to (resolved lazily).</summary>
		private IParameterModule _parameterModule;

		/// <summary>Descriptor of the avatar this module drives: owns the Animator the simulation waits for.</summary>
		private IAvatarDescriptor _descriptor;

		// Group of this avatar's jiggles: the id of its descriptor (see IAvatarJiggles.Group), so a caller
		// can tell which avatar the flag applies to without holding this component.
		private string _group;

		// Whether the caller wants the jiggles updated (see SetEnabled).
		private bool _enabled = true;

		// False until the avatar is prepared (see Setup): other modules activate the prefab temporarily while
		// they initialise it, and the simulation must not start on those activations.
		private bool _ready;

		// False until the simulation has been seeded (see LateUpdate): while it is false the rigs are suspended.
		private bool _seeded;

		public int Priority
			=> 40;

		/// <inheritdoc />
		public string Group
			=> _group;

		/// <inheritdoc />
		public bool IsEnabled
			=> _enabled;

		public async UniTask<bool> Setup(IRuntimeAvatar runtimeAvatar, AvatarModulePhase phase, CancellationToken token = default) {
			if (phase == AvatarModulePhase.Pre && Application.isPlaying)
				JiggleConverter.ConvertAll(gameObject);

			if (phase == AvatarModulePhase.Post) {
				_ready = true;
				return true;
			}

			if (phase != AvatarModulePhase.Init)
				return true;

			_descriptor = runtimeAvatar?.Descriptor;
			_group = GetGroup(_descriptor);

			await UniTask.Yield(cancellationToken: token);
			return true;
		}

		private void OnEnable() {
			_descriptor ??= GetComponentInParent<IAvatarDescriptor>();
			_group ??= GetGroup(_descriptor);

			RefreshRigs();

			var colliders = GetComponentsInChildren<Collider>(true);
			foreach (var c in colliders)
				c.Register();

			_seeded = false;
			Apply();
		}

		private void LateUpdate() {
			PublishParameters();

			if (!_ready || _seeded || !IsPosed())
				return;

			Seed();
		}

		/// <summary>
		/// Whether the Animator is posing the bones. The avatar prefab is prepared while inactive and some modules
		/// (the playable layers) activate it temporarily during the preparation, so the preparation itself has to
		/// be over before the trees are sampled: a tree added on an activation that the loader undoes keeps the
		/// pose of the prefab and the simulation then converges on it instead of following the animation.
		/// </summary>
		private bool IsPosed() {
			var animator = _descriptor != null
				? _descriptor.Animator
				: null;

			return !animator
				|| (animator.isActiveAndEnabled && animator.playableGraph.IsPlaying());
		}

		/// <summary>
		/// Resumes the simulation on the pose the Animator wrote for this frame: the trees sample the bones when
		/// they are added, so this is the moment they must be built, and the only one the jiggles may start from.
		/// </summary>
		private void Seed() {
			RefreshRigs();

			_seeded = true;
			Apply();
			Logger.LogDebug($"Jiggle simulation seeded on the avatar pose ({_rigs.Count} rig(s)).", tag: LogTag);
		}

		private void OnDisable() {
			_seeded = false;

			var colliders = GetComponentsInChildren<Collider>(true);
			foreach (var c in colliders)
				c.Unregister();

			var jiggles = GetComponentsInChildren<JiggleRig>(true);
			foreach (var jiggle in jiggles)
				jiggle.Suspend();

			_publishers.Clear();
			_parameterModule = null;
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
			_publishers.Clear();

			foreach (var jiggle in GetComponentsInChildren<JiggleRig>(true)) {
				if (jiggle.IsDestroyed())
					continue;

				_rigs.Add(jiggle);

				if (jiggle is Jiggle cc)
					_publishers.Add(new JiggleParameterPublisher(cc));
			}
		}

		/// <summary>
		/// Writes the interaction parameters of every rig to the avatar's parameter module. Skipped while the
		/// avatar has no parameter module (or none above this component).
		/// </summary>
		private void PublishParameters() {
			if (_publishers.Count == 0)
				return;

			if (_parameterModule is Object moduleObject && !moduleObject)
				_parameterModule = null;

			_parameterModule ??= GetComponentInParent<IParameterModule>();
			if (_parameterModule == null)
				return;

			foreach (var publisher in _publishers) {
				if (!publisher.Rig)
					continue;

				publisher.Publish(_parameterModule);
			}
		}

		/// <summary>Group of an avatar's jiggles: the id of its descriptor, or <c>null</c> without one.</summary>
		private static string GetGroup(IAvatarDescriptor descriptor)
			=> descriptor != null && descriptor.Anchor
				? descriptor.Anchor.GetId().ToString()
				: null;

		/// <summary>
		/// Applies the flag to every rig: a rig is only simulated while the jiggles are enabled and the
		/// simulation has been seeded, so it is suspended the rest of the time (and re-seeded when it comes back).
		/// </summary>
		private void Apply() {
			foreach (var rig in _rigs) {
				if (rig.IsDestroyed())
					continue;

				if (_enabled && _seeded)
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
			if (Application.isPlaying && descriptor?.Anchor)
				JiggleConverter.ConvertAll(descriptor.Anchor);

			var modules = descriptor.GetModules<JiggleModule>();

			if (modules.Length > 1) {
				Logger.LogError($"{nameof(JiggleModule)} must be present only once on the avatar (found {modules.Length}).", descriptor.Anchor, tag: LogTag);
				return false;
			}

			var module = modules.Length switch {
				1 => modules.FirstOrDefault(),
				_ => descriptor.Anchor.AddComponent<JiggleModule>()
			};

			if (!module) {
				Logger.LogError($"Verify that the Avatar prefab has a valid {nameof(JiggleModule)} component.", descriptor.Anchor, tag: LogTag);
				return false;
			}

			foreach (var jiggle in descriptor.Anchor.GetComponentsInChildren<JiggleRig>(true)) {
				if (jiggle.IsDestroyed())
					continue;

				if (jiggle.GetJiggleRigData().GetHasRootTransformError()) {
					Logger.LogError("Jiggle component has no root bone assigned.", jiggle, tag: LogTag);
					return false;
				}
			}

			return true;
		}
	}
}
