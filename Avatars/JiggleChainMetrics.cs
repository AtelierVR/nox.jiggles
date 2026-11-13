using System.Collections.Generic;
using GatorDragonGames.JigglePhysics;
using UnityEngine;

namespace Nox.CCK.Jiggles.Avatars {
	/// <summary>
	/// Measures a simulated rig: the "end bone" of the longest branch gives <c>/Angle</c> (how far the chain
	/// points away from its rest direction) and the chain length ratio gives <c>/Stretch</c> and <c>/Squish</c>.
	/// <para>
	/// The rest pose is the rig's cached rest (see <see cref="JiggleRigData.transformCachedData"/>) composed
	/// under the <b>current</b> parent transform: the chain follows its animated parents while its own bones
	/// keep their rest orientation.
	/// </para>
	/// </summary>
	internal static class JiggleChainMetrics {
		/// <summary>Shorter segments are merged away by JigglePhysics, they are not part of the chain.</summary>
		private const float MinSegmentLength = 0.0001f;

		// Reused between calls: the values are computed every frame for every rig, they must not allocate.
		private static readonly List<Transform> Bones = new();
		private static readonly List<int> Parents = new();
		private static readonly List<Vector3> RestPositions = new();
		private static readonly List<Vector3> CurrentPositions = new();
		private static readonly List<Matrix4x4> RestMatrices = new();
		private static readonly List<float> RestDistances = new();
		private static readonly List<float> CurrentDistances = new();

		/// <summary>
		/// Computes the interaction values of <paramref name="rig"/> (all normalized to 0..1).
		/// Returns <c>false</c> when the rig cannot be measured (no root bone, no rest data).
		/// </summary>
		public static bool Evaluate(
			JiggleRig rig,
			float maxStretch,
			float maxSquish,
			out float angle,
			out float stretch,
			out float squish
		) {
			angle = 0f;
			stretch = 0f;
			squish = 0f;

			if (!rig)
				return false;

			var data = rig.GetJiggleRigData();
			var root = data.rootBone;

			if (!root)
				return false;

			if (!data.GetCacheIsValid()) {
				if (data.transformCachedData is not { Length: > 0 })
					return false;

				data.RegenerateCacheLookup();
			}

			Bones.Clear();
			Parents.Clear();
			RestPositions.Clear();
			CurrentPositions.Clear();
			RestMatrices.Clear();
			RestDistances.Clear();
			CurrentDistances.Clear();

			var parentMatrix = root.parent ? root.parent.localToWorldMatrix : Matrix4x4.identity;
			Collect(data, root, parentMatrix, -1, 0f, 0f);

			if (Bones.Count == 0)
				return false;

			// The end of the chain is the furthest bone of the longest branch.
			var end = 0;
			for (var i = 1; i < Bones.Count; i++) {
				if (RestDistances[i] > RestDistances[end])
					end = i;
			}

			var parent = Parents[end];

			Vector3 currentDirection;
			Vector3 restDirection;

			if (parent >= 0) {
				currentDirection = CurrentPositions[end] - CurrentPositions[parent];
				restDirection = RestPositions[end] - RestPositions[parent];
			} else {
				// Single bone chain: its own up axis is the only direction available.
				currentDirection = Bones[end].up;
				restDirection = RestMatrices[end].MultiplyVector(Vector3.up);
			}

			angle = Mathf.Clamp01(Vector3.Angle(currentDirection, restDirection) / 180f);

			var restLength = RestDistances[end];
			var currentLength = CurrentDistances[end];

			if (restLength <= MinSegmentLength)
				return true;

			var ratio = currentLength / restLength;

			// The limits are multiples of the rest length (max stretch / max squish): 1 or less for the stretch
			// and 1 for the squish means the value has no range.
			if (maxStretch > 1f)
				stretch = Mathf.Clamp01((ratio - 1f) / (maxStretch - 1f));

			if (maxSquish > 0f && maxSquish < 1f)
				squish = Mathf.Clamp01((1f - ratio) / (1f - maxSquish));

			return true;
		}

		/// <summary>
		/// Walks the chain exactly like JigglePhysics does (<see cref="JiggleRigData.GetValidChildrenCount"/>),
		/// accumulating the rest and the simulated length from the root.
		/// </summary>
		private static void Collect(
			JiggleRigData data,
			Transform bone,
			Matrix4x4 parentMatrix,
			int parent,
			float restDistance,
			float currentDistance
		) {
			var cached = data.GetCache(bone);
			var restRotation = new Quaternion(cached.restLocalRotation.x, cached.restLocalRotation.y, cached.restLocalRotation.z, cached.restLocalRotation.w);
			var restMatrix = parentMatrix * Matrix4x4.TRS(cached.restLocalPosition, restRotation, bone.localScale);
			var restPosition = new Vector3(restMatrix.m03, restMatrix.m13, restMatrix.m23);
			var currentPosition = bone.position;

			var index = -1;

			if (parent < 0) {
				index = Add(bone, parent, restPosition, currentPosition, restDistance, currentDistance, restMatrix);
			} else {
				var restSegment = Vector3.Distance(restPosition, RestPositions[parent]);
				var currentSegment = Vector3.Distance(currentPosition, CurrentPositions[parent]);

				// A zero-length segment is merged upward by JigglePhysics: it must not add a point here either.
				if (restSegment > MinSegmentLength || currentSegment > MinSegmentLength)
					index = Add(bone, parent, restPosition, currentPosition, restDistance + restSegment, currentDistance + currentSegment, restMatrix);
			}

			// Children of a merged (skipped) bone attach to its last kept ancestor, as in the package tree.
			var keptParent = index >= 0 ? index : parent;
			var keptRestDistance = index >= 0 ? RestDistances[index] : restDistance;
			var keptCurrentDistance = index >= 0 ? CurrentDistances[index] : currentDistance;

			var count = data.GetValidChildrenCount(bone);
			for (var i = 0; i < count; i++) {
				var child = data.GetValidChild(bone, i);
				if (!child)
					continue;

				Collect(data, child, restMatrix, keptParent, keptRestDistance, keptCurrentDistance);
			}
		}

		private static int Add(
			Transform bone,
			int parent,
			Vector3 restPosition,
			Vector3 currentPosition,
			float restDistance,
			float currentDistance,
			Matrix4x4 restMatrix
		) {
			Bones.Add(bone);
			Parents.Add(parent);
			RestPositions.Add(restPosition);
			CurrentPositions.Add(currentPosition);
			RestMatrices.Add(restMatrix);
			RestDistances.Add(restDistance);
			CurrentDistances.Add(currentDistance);
			return Bones.Count - 1;
		}
	}
}
