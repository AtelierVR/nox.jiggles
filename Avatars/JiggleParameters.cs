using System.Collections.Generic;
using Nox.Avatars.Parameters;
using UnityEngine;

namespace Nox.CCK.Jiggles.Avatars {
	/// <summary>
	/// Publishes the interaction parameters of the avatar jiggle rigs to the avatar Animator:
	/// <c>{prefix}/IsGrabbed</c>, <c>{prefix}/IsPosed</c>, <c>{prefix}/Angle</c>, <c>{prefix}/Stretch</c> and
	/// <c>{prefix}/Squish</c> (see <see cref="JiggleParameterSettings"/>).
	/// <para>
	/// The rigs are registered by <see cref="JiggleModule"/>, which owns the avatar's jiggles. The values are
	/// published by <c>Nox.Jiggles.Runtime</c> from its own updater (<c>JiggleParameterUpdater</c>): they are
	/// read from the simulated transforms, so they are only valid once the jiggle updater posed the bones in
	/// its <c>LateUpdate</c>.
	/// </para>
	/// <para>
	/// A parameter is written only when the Animator declares it: the avatar parameter module registers the
	/// parameters the avatar controllers declare, nothing else.
	/// </para>
	/// </summary>
	public static class JiggleParameters {
		/// <summary>A registered rig, with its parameter names (rebuilt when the prefix changes).</summary>
		private sealed class Entry {
			public readonly Jiggle Rig;

			public string Prefix;
			public string IsGrabbedName;
			public string IsPosedName;
			public string AngleName;
			public string StretchName;
			public string SquishName;

			/// <summary>Avatar parameter module above the rig: resolved once, the avatar does not move.</summary>
			public IParameterModule Module;

			public Entry(Jiggle rig) {
				Rig = rig;
			}

			public void RefreshNames(JiggleParameterSettings settings) {
				if (Prefix == settings.prefix && AngleName != null)
					return;

				Prefix = settings.prefix;
				IsGrabbedName = settings.IsGrabbedParameter;
				IsPosedName = settings.IsPosedParameter;
				AngleName = settings.AngleParameter;
				StretchName = settings.StretchParameter;
				SquishName = settings.SquishParameter;
			}
		}

		private static readonly List<Entry> Entries = new();

		/// <summary>Drops the registry: a play mode restart without a domain reload would keep stale rigs.</summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Initialize()
			=> Entries.Clear();

		/// <summary>Declares a rig whose interaction parameters are published each frame. Idempotent.</summary>
		public static void Register(Jiggle rig) {
			if (!rig)
				return;

			foreach (var entry in Entries) {
				if (entry.Rig == rig)
					return;
			}

			Entries.Add(new Entry(rig));
		}

		/// <summary>Stops publishing a rig (avatar unloaded/disabled). Idempotent.</summary>
		public static void Unregister(Jiggle rig) {
			if (!rig)
				return;

			for (var i = Entries.Count - 1; i >= 0; i--) {
				if (Entries[i].Rig != rig)
					continue;

				Entries.RemoveAt(i);
				return;
			}
		}

		/// <summary>
		/// Publishes every registered rig. Called by the runtime updater after the simulation ran, never by the
		/// avatar modules (their <c>LateUpdate</c> may run before the jiggle updater).
		/// </summary>
		public static void PublishAll() {
			// Reverse: publishing prunes the destroyed rigs.
			for (var i = Entries.Count - 1; i >= 0; i--) {
				var entry = Entries[i];

				// A destroyed rig (avatar unloaded without a proper OnDisable) is dropped here.
				if (!entry.Rig) {
					Entries.RemoveAt(i);
					continue;
				}

				Publish(entry);
			}
		}

		private static void Publish(Entry entry) {
			var rig = entry.Rig;

			if (!rig.isActiveAndEnabled)
				return;

			var settings = rig.Parameters;
			if (!settings.IsEnabled)
				return;

			if (entry.Module is Object moduleObject && !moduleObject)
				entry.Module = null;

			entry.Module ??= rig.GetComponentInParent<IParameterModule>();
			if (entry.Module == null)
				return;

			entry.RefreshNames(settings);

			if (!JiggleChainMetrics.Evaluate(rig, settings.maxStretch, settings.maxSquish, out var angle, out var stretch, out var squish))
				return;

			WriteBool(entry.Module, entry.IsGrabbedName, rig.IsGrabbed);
			WriteBool(entry.Module, entry.IsPosedName, rig.IsPosed);
			WriteFloat(entry.Module, entry.AngleName, angle);
			WriteFloat(entry.Module, entry.StretchName, stretch);
			WriteFloat(entry.Module, entry.SquishName, squish);
		}

		/// <summary>
		/// Writes a bool, skipping the write when the Animator does not declare the parameter and when the value
		/// did not change (the parameters are synchronized, they must not be spammed).
		/// </summary>
		private static void WriteBool(IParameterModule module, string name, bool value) {
			var parameter = module.GetParameter(name);
			if (parameter == null || parameter.ValueType != ParameterType.Bool)
				return;

			if (parameter.Value is bool current && current == value)
				return;

			parameter.Value = value;
		}

		/// <inheritdoc cref="WriteBool" />
		private static void WriteFloat(IParameterModule module, string name, float value) {
			var parameter = module.GetParameter(name);
			if (parameter == null || parameter.ValueType != ParameterType.Float)
				return;

			if (parameter.Value is float current && Mathf.Approximately(current, value))
				return;

			parameter.Value = value;
		}
	}
}
