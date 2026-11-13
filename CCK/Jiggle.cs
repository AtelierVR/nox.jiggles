using System;
using System.Collections.Generic;
using System.Reflection;
using GatorDragonGames.JigglePhysics;
using Nox.Jiggles;
using UnityEngine;
using Gizmos = Nox.CCK.Development.Gizmos;

namespace Nox.CCK.Jiggles {
	/// <summary>
	/// CCK component that extends <see cref="JiggleRig"/> for use as an avatar jiggle rig.
	/// Inherits all JiggleRig configuration and behaviour, plus the CCK collider gizmos.
	/// Lifecycle is driven by <c>JiggleModule</c> (see <see cref="JiggleRigExtensions"/>).
	/// </summary>
	[DisallowMultipleComponent]
	public class Jiggle : JiggleRig, IJiggle {
		[SerializeField]
		private List<Collider> jiggleColliders = new();

		[SerializeField]
		private JiggleParameterSettings parameters = JiggleParameterSettings.Default();

		/// <inheritdoc />
		public bool IsSuspended
			=> JiggleRigExtensions.IsSuspended(this);

		#region Interaction parameters

		/// <summary>
		/// Interaction parameters exposed to the Animator (see <see cref="JiggleParameterSettings"/>). Setting a
		/// prefix makes the rig publish <c>{prefix}/IsGrabbed</c>, <c>{prefix}/IsPosed</c>, <c>{prefix}/Angle</c>,
		/// <c>{prefix}/Stretch</c> and <c>{prefix}/Squish</c>; they have to exist in the avatar controllers.
		/// </summary>
		public JiggleParameterSettings Parameters {
			get => parameters;
			set {
				parameters = value;
				parameters.Sanitize();
			}
		}

		/// <summary>Sets the interaction parameters in one call (used by converters rebuilding a rig).</summary>
		public void SetParameters(string prefix, float maxStretch, float maxSquish)
			=> Parameters = new JiggleParameterSettings {
				prefix = prefix,
				maxStretch = maxStretch,
				maxSquish = maxSquish
			};

		/// <summary>True while the chain is held by an interaction (see <see cref="SetGrabbed"/>).</summary>
		public bool IsGrabbed { get; private set; }

		/// <summary>Marks the chain as grabbed (<c>{prefix}/IsGrabbed</c>). Driven by the grab system.</summary>
		public void SetGrabbed(bool grabbed)
			=> IsGrabbed = grabbed;

		/// <summary>True while the chain is locked in a pose (see <see cref="SetPosed"/>).</summary>
		public bool IsPosed { get; private set; }

		/// <summary>Marks the chain as posed (<c>{prefix}/IsPosed</c>). Driven by the grab system.</summary>
		public void SetPosed(bool posed)
			=> IsPosed = posed;

		#endregion

		/// <inheritdoc />
		public void Suspend()
			=> JiggleRigExtensions.Suspend(this);

		/// <inheritdoc />
		public void Resume()
			=> JiggleRigExtensions.Resume(this);

		/// <inheritdoc />
		public void Resync()
			=> JiggleRigExtensions.Resync(this);

		/// <summary>
		/// Replaces the serialized rig data. Used by converters that build a rig at runtime: call it
		/// <b>before</b> the component is enabled, as enablement initializes the simulation from
		/// <see cref="JiggleRig.GetJiggleRigData"/>. The base <see cref="JiggleRig"/> keeps its data in a private
		/// serialized field, hence the reflection.
		/// </summary>
		public void Configure(JiggleRigData data) {
			var field = typeof(JiggleRig).GetField("jiggleRigData", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
				throw new MissingFieldException(typeof(JiggleRig).FullName, "jiggleRigData");

			field.SetValue(this, data);
		}

		private void Reset() {
			parameters = JiggleParameterSettings.Default();
			FallbackRootBone();
		}

		private void OnValidate() {
			parameters.Sanitize();
			FallbackRootBone();
		}

		private void FallbackRootBone() {
			var data = GetJiggleRigData();
			if (data.rootBone == null) {
				data.rootBone = transform;
			}
		}

		private void OnDrawGizmosSelect() { }

		private void OnDrawGizmos() {
			FallbackRootBone();
			DrawColliderGizmos();
			DrawBoneGizmos();
		}

		private void DrawColliderGizmos() {
			if (jiggleColliders == null) return;
			Gizmos.Color = new Color(0.854902f, 0.6470588f, 0.1254902f, 1f);
			foreach (var cc in jiggleColliders) {
				if (cc == null) continue;
				var c = cc.ColliderData;
				var t = c.transform != null ? c.transform : cc.transform;
				var col = c.collider;
				var pos = t.position + cc.offset;
				var rot = t.rotation * cc.offsetRotation;
				var avgScale = t.lossyScale.x;
				var r = Mathf.Max(0f, col.radius) * avgScale;

				switch (col.type) {
					case JiggleCollider.JiggleColliderType.Sphere:
						Gizmos.DrawWireSphere(pos, r);
						break;
					case JiggleCollider.JiggleColliderType.Capsule: {
						var axisDir = col.capsuleAxis switch {
							JiggleCollider.CapsuleAxis.X => rot * Vector3.right,
							JiggleCollider.CapsuleAxis.Y => rot * Vector3.up,
							JiggleCollider.CapsuleAxis.Z => rot * Vector3.forward,
							_ => rot * Vector3.up
						};
						var halfH = Mathf.Max(0f, col.height) * avgScale * 0.5f;
						Gizmos.DrawWireCapsule(pos - axisDir * halfH, pos + axisDir * halfH, r);
						break;
					}
					case JiggleCollider.JiggleColliderType.Plane: {
						var right = rot * Vector3.right;
						var forward = rot * Vector3.forward;
						var s = 1f;
						var p1 = pos + (right + forward) * s;
						var p2 = pos + (right - forward) * s;
						var p3 = pos + (-right - forward) * s;
						var p4 = pos + (-right + forward) * s;
						Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3);
						Gizmos.DrawLine(p3, p4); Gizmos.DrawLine(p4, p1);
						Gizmos.DrawLine(p1, p3); Gizmos.DrawLine(p2, p4);
						Gizmos.DrawLine(pos, pos + rot * Vector3.up * s * 0.5f);
						break;
					}
				}
			}
		}

		private void DrawBoneGizmos() {
			var data = GetJiggleRigData();
			if (data.rootBone == null) return;

			var jiggleTree = JigglePhysics.CreateJiggleTree(data, null);
			var points = jiggleTree.points;
			var parameters = jiggleTree.parameters;
			var bones = jiggleTree.bones;

			Gizmos.Color = new Color(0.9607844f, 0.9607844f, 0.9607844f, 1f);

			for (var i = 0; i < points.Length; i++) {
				var pt = points[i];
				if (pt.parentIndex == -1) continue;
				if (!points[pt.parentIndex].hasTransform) continue;

				var parent = points[pt.parentIndex];
				var parentPos = (Vector3)parent.position;
				var childPos = (Vector3)pt.position;
				var boneScale = (Vector3)bones[i].lossyScale;
				var avgBoneScale = (boneScale.x + boneScale.y + boneScale.z) / 3f;
				var parentRadius = parameters[pt.parentIndex].collisionRadius * avgBoneScale;
				var childRadius = parameters[i].collisionRadius * avgBoneScale;

				Gizmos.DrawWireCapsule(parentPos, childPos, parentRadius, childRadius);

				var boneDir = (childPos - parentPos).normalized;
				var angleLimit = parameters[pt.parentIndex].angleLimit;
				if (angleLimit > 0f) {
					Gizmos.Color = new Color(0.3f, 0.85f, 0.3f, 0.3f);
					Gizmos.DrawSolidCone(parentPos, boneDir, angleLimit, 0.05f);
					Gizmos.Color = new Color(0.9607844f, 0.9607844f, 0.9607844f, 1f);
				}
			}
		}
	}
}
