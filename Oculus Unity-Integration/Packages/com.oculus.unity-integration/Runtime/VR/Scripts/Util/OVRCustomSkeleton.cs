/************************************************************************************
Copyright : Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Your use of this SDK or tool is subject to the Oculus SDK License Agreement, available at
https://developer.oculus.com/licenses/oculussdk/

Unless required by applicable law or agreed to in writing, the Utilities SDK distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
ANY KIND, either express or implied. See the License for the specific language governing
permissions and limitations under the License.
************************************************************************************/

using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-80)]
public class OVRCustomSkeleton : OVRSkeleton, ISerializationCallbackReceiver
{
	[HideInInspector]
	[SerializeField]
	private List<Transform> _customBones_V2;

#if UNITY_EDITOR

	private static readonly string[] _fbxHandSidePrefix = { "l_", "r_" };
	private static readonly string _fbxHandBonePrefix = "b_";

	private static readonly string[] _fbxHandBoneNames =
	{
		"wrist",
		"forearm_stub",
		"thumb0",
		"thumb1",
		"thumb2",
		"thumb3",
		"index1",
		"index2",
		"index3",
		"middle1",
		"middle2",
		"middle3",
		"ring1",
		"ring2",
		"ring3",
		"pinky0",
		"pinky1",
		"pinky2",
		"pinky3"
	};

	private static readonly string[] _fbxHandFingerNames =
	{
		"thumb",
		"index",
		"middle",
		"ring",
		"pinky"
	};
#endif // UNITY_EDITOR

	public List<Transform> CustomBones => _customBones_V2;

#if UNITY_EDITOR
	public void TryAutoMapBonesByName()
	{
		BoneId start = GetCurrentStartBoneId();
		BoneId end = GetCurrentEndBoneId();
		SkeletonType skeletonType = GetSkeletonType();
		if (start != BoneId.Invalid && end != BoneId.Invalid)
		{
			for (int bi = (int)start; bi < (int)end; ++bi)
			{
				string fbxBoneName = FbxBoneNameFromBoneId(skeletonType, (BoneId)bi);
				Transform t = transform.FindChildRecursive(fbxBoneName);


				if (t != null)
				{
					_customBones_V2[(int)bi] = t;
				}
			}
		}
	}

	private static string FbxBoneNameFromBoneId(SkeletonType skeletonType, BoneId bi)
	{
		{
			if (bi >= BoneId.Hand_ThumbTip && bi <= BoneId.Hand_PinkyTip)
			{
				return _fbxHandSidePrefix[(int)skeletonType] + _fbxHandFingerNames[(int)bi - (int)BoneId.Hand_ThumbTip] + "_finger_tip_marker";
			}
			else
			{
				return _fbxHandBonePrefix + _fbxHandSidePrefix[(int)skeletonType] + _fbxHandBoneNames[(int)bi];
			}
		}
	}
#endif

	protected override Transform GetBoneTransform(BoneId boneId) => _customBones_V2[(int)boneId];

#if UNITY_EDITOR
	private bool _shouldSetDirty;

	private void OnValidate()
	{
		if (!_shouldSetDirty)
        {
            return;
        }

        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
		UnityEditor.EditorUtility.SetDirty(this);
		_shouldSetDirty = false;
	}
#endif

	void ISerializationCallbackReceiver.OnBeforeSerialize() { }

	void ISerializationCallbackReceiver.OnAfterDeserialize()
	{
		if (_customBones_V2.Count == (int) BoneId.Max)
        {
            return;
        }

        // Make sure we have the right number of bones
        while (_customBones_V2.Count < (int) BoneId.Max)
		{
			_customBones_V2.Add(null);
		}

#if UNITY_EDITOR
		_shouldSetDirty = true;
#endif
	}
}
