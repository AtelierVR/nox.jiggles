using System.Reflection;
using GatorDragonGames.JigglePhysics;
using Nox.CCK.Utils;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Jiggles {
	/// <summary>
	/// Converts the raw JigglePhysics components of a hierarchy into the CCK ones (<see cref="Jiggle"/> and
	/// <see cref="Collider"/>): the avatar module only drives CCK rigs, and only they declare the interaction
	/// parameters.
	/// <para>
	/// Runtime only — the editor converts with Undo (see <c>GameObject/Nox/Convert to CCK Jiggles</c>). A CCK
	/// rig initializes from its data in <c>OnEnable</c>, so a live GameObject is briefly deactivated while the
	/// data is copied.
	/// </para>
	/// </summary>
	public static class JiggleConverter {
		public const string LogTag = nameof(JiggleConverter);

		/// <summary>The package collider keeps its data in a private serialized field.</summary>
		private static readonly FieldInfo ColliderDataField =
			typeof(JiggleColliderExample).GetField("jiggleCollider", BindingFlags.Instance | BindingFlags.NonPublic);

		/// <summary>Converts every rig and collider below <paramref name="root"/> (and on it).</summary>
		/// <returns>Number of converted components.</returns>
		public static int ConvertAll(GameObject root) {
			if (!root || !Application.isPlaying)
				return 0;

			var converted = 0;

			foreach (var example in root.GetComponentsInChildren<JiggleColliderExample>(true)) {
				if (Convert(example))
					converted++;
			}

			foreach (var rig in root.GetComponentsInChildren<JiggleRig>(true)) {
				if (Convert(rig))
					converted++;
			}

			if (converted > 0)
				Logger.LogDebug($"Converted {converted} JigglePhysics component(s) to their CCK equivalent.", root, tag: LogTag);

			return converted;
		}

		/// <summary>
		/// Converts a package rig into a <see cref="Jiggle"/>, carrying its rig data and its animated-parameters
		/// flag. No-op on a rig that is already a <see cref="Jiggle"/>.
		/// </summary>
		public static bool Convert(JiggleRig rig) {
			if (!rig || rig is Jiggle || !Application.isPlaying)
				return false;

			var host     = rig.gameObject;
			var data     = rig.GetJiggleRigData();
			var animated = rig.HasAnimatedParameters;

			// Disabling unregisters the rig from the simulation, so the new one never shares the root with it.
			rig.enabled = false;
			rig.DestroyImmediate();

			// The CCK rig initializes from its data in OnEnable: keep the GameObject inactive until it is set.
			var wasActive = host.activeSelf;
			if (wasActive)
				host.SetActive(false);

			var jiggle = host.AddComponent<Jiggle>();
			jiggle.Configure(data);
			jiggle.HasAnimatedParameters = animated;

			if (wasActive)
				host.SetActive(true);

			return true;
		}

		/// <summary>
		/// Converts a package collider into a CCK <see cref="Collider"/>. The CCK collider is passive: the avatar
		/// module registers it when the avatar is enabled.
		/// </summary>
		public static bool Convert(JiggleColliderExample example) {
			if (!example || !Application.isPlaying)
				return false;

			var host = example.gameObject;

			// Already converted: the two components would register the same collider.
			if (host.GetComponent<Collider>())
				return false;

			if (ColliderDataField == null) {
				Logger.LogError($"Cannot convert '{example.name}': '{nameof(JiggleColliderExample)}.jiggleCollider' was not found.", tag: LogTag);
				return false;
			}

			var data = (JiggleColliderSerializable)ColliderDataField.GetValue(example);

			// Disabling unregisters the collider from the simulation.
			example.enabled = false;
			example.DestroyImmediate();

			// The package collider is already placed on its transform: no additional offset is needed.
			host.AddComponent<Collider>().Configure(data, Vector3.zero, Quaternion.identity);
			return true;
		}
	}
}
