using Nox.Avatars.Parameters;
using UnityEngine;

namespace Nox.CCK.Jiggles.Avatars {
	/// <summary>
	/// Publishes the interaction parameters of one <see cref="Jiggle"/> to the avatar Animator:
	/// <c>{prefix}/IsGrabbed</c>, <c>{prefix}/IsPosed</c>, <c>{prefix}/Angle</c>, <c>{prefix}/Stretch</c> and
	/// <c>{prefix}/Squish</c> (see <see cref="JiggleParameterSettings"/>).
	/// <para>
	/// Owned by <see cref="JiggleModule"/>, which resolves the avatar's parameter module and calls
	/// <see cref="Publish"/> after the simulation ran (the values are measured on the simulated transforms,
	/// see <see cref="JiggleChainMetrics"/>).
	/// </para>
	/// <para>
	/// A parameter is written only when the Animator declares it: the parameter module registers the
	/// parameters the avatar controllers declare, nothing else.
	/// </para>
	/// </summary>
	internal sealed class JiggleParameterPublisher {
		private readonly Jiggle _rig;

		// Parameter names are rebuilt only when the prefix changes: no string concatenation per frame.
		private string _prefix;
		private string _isGrabbedName;
		private string _isPosedName;
		private string _angleName;
		private string _stretchName;
		private string _squishName;

		public JiggleParameterPublisher(Jiggle rig)
			=> _rig = rig;

		/// <summary>Rig whose values are published (a destroyed rig is dropped by the module).</summary>
		public Jiggle Rig
			=> _rig;

		/// <summary>Publishes the values of the rig, when it declares a prefix.</summary>
		public void Publish(IParameterModule module) {
			if (!_rig || module == null)
				return;

			var settings = _rig.Parameters;
			if (!settings.IsEnabled)
				return;

			RefreshNames(settings);

			if (!JiggleChainMetrics.Evaluate(_rig, settings.maxStretch, settings.maxSquish, out var angle, out var stretch, out var squish))
				return;

			WriteBool(module, _isGrabbedName, _rig.IsGrabbed);
			WriteBool(module, _isPosedName, _rig.IsPosed);
			WriteFloat(module, _angleName, angle);
			WriteFloat(module, _stretchName, stretch);
			WriteFloat(module, _squishName, squish);
		}

		private void RefreshNames(JiggleParameterSettings settings) {
			if (_prefix == settings.prefix && _angleName != null)
				return;

			_prefix        = settings.prefix;
			_isGrabbedName = settings.IsGrabbedParameter;
			_isPosedName   = settings.IsPosedParameter;
			_angleName     = settings.AngleParameter;
			_stretchName   = settings.StretchParameter;
			_squishName    = settings.SquishParameter;
		}

		/// <summary>
		/// Writes a bool, skipping the write when the Animator does not declare the parameter and when the value
		/// did not change.
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
