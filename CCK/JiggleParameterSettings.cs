using System;
using UnityEngine;

namespace Nox.CCK.Jiggles {
	/// <summary>
	/// Interaction parameters a <see cref="Jiggle"/> exposes to the avatar Animator: setting a
	/// <see cref="prefix"/> declares the whole set <c>{prefix}/IsGrabbed</c> (bool), <c>{prefix}/IsPosed</c>
	/// (bool), <c>{prefix}/Angle</c> (float), <c>{prefix}/Stretch</c> (float) and <c>{prefix}/Squish</c>
	/// (float). An empty prefix disables them.
	/// <para>
	/// The parameters have to exist in the Avatar's Animator for the values to reach it (the avatar
	/// parameter module only registers the parameters the controllers declare).
	/// </para>
	/// </summary>
	[Serializable]
	public struct JiggleParameterSettings {
		/// <summary>Default stretch range: the chain may grow by half its rest length before reaching 1.</summary>
		public const float DefaultMaxStretch = 1.5f;

		/// <summary>Default squish range: the chain may lose half of its rest length before reaching 1.</summary>
		public const float DefaultMaxSquish = 0.5f;

		[Tooltip("Prefix of the animator parameters. Empty disables the parameters.")]
		public string prefix;

		[Tooltip(
			"Multiple of the rest length at which {prefix}/Stretch reaches 1 (max stretch). " +
			"1 or less disables the stretch range."
		)]
		public float maxStretch;

		[Tooltip(
			"Multiple of the rest length at which {prefix}/Squish reaches 1 (max squish). " +
			"0 or 1 disables the squish range."
		)]
		public float maxSquish;

		/// <summary>True when a prefix is set: the rig publishes its interaction parameters.</summary>
		public bool IsEnabled
			=> !string.IsNullOrEmpty(prefix);

		public string IsGrabbedParameter => prefix + "/IsGrabbed";

		public string IsPosedParameter => prefix + "/IsPosed";

		public string AngleParameter => prefix + "/Angle";

		public string StretchParameter => prefix + "/Stretch";

		public string SquishParameter => prefix + "/Squish";

		public static JiggleParameterSettings Default()
			=> new() {
				prefix = string.Empty,
				maxStretch = DefaultMaxStretch,
				maxSquish = DefaultMaxSquish
			};

		/// <summary>
		/// Repairs a component serialized before the feature existed (every field zeroed): 0 is not a valid
		/// limit, so it is replaced by the default. 1 is a real value and is kept (no range).
		/// </summary>
		public void Sanitize() {
			if (maxStretch <= 0f)
				maxStretch = DefaultMaxStretch;

			if (maxSquish <= 0f)
				maxSquish = DefaultMaxSquish;
		}
	}
}
