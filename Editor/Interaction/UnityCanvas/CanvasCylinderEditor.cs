/************************************************************************************
Copyright : Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Your use of this SDK or tool is subject to the Oculus SDK License Agreement, available at
https://developer.oculus.com/licenses/oculussdk/

Unless required by applicable law or agreed to in writing, the Utilities SDK distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
ANY KIND, either express or implied. See the License for the specific language governing
permissions and limitations under the License.
************************************************************************************/

using UnityEditor;
using UnityEngine;

namespace Oculus.Interaction.UnityCanvas.Editor
{
    [CustomEditor(typeof(CanvasCylinder))]
    public class CanvasCylinderEditor : UnityEditor.Editor
    {
        private SerializedProperty _meshColliderProp;

        private void OnEnable()
        {
            _meshColliderProp = serializedObject.FindProperty("_meshCollider");
        }

        public override void OnInspectorGUI()
        {
            CanvasCylinder canvasCylinder = target as CanvasCylinder;

            if (canvasCylinder != null)
            {
                if (canvasCylinder.Cylinder != null &&
                    canvasCylinder.Cylinder.transform.IsChildOf(canvasCylinder.transform))
                {
                    EditorGUILayout.HelpBox($"{nameof(CanvasCylinder)} must be " +
                        $"a child or sibling of its {nameof(Cylinder)}", MessageType.Error);
                }

                if (_meshColliderProp != null &&
                    _meshColliderProp.objectReferenceValue is MeshCollider col &&
                    canvasCylinder.transform != col.transform &&
                    canvasCylinder.transform.IsChildOf(col.transform))
                {
                    EditorGUILayout.HelpBox($"{nameof(CanvasCylinder)} cannot be a " +
                        $"child of its {nameof(MeshCollider)}. It must be a parent, " +
                        $"sibling, or share a GameObject.", MessageType.Error);
                }
            }

            base.OnInspectorGUI();
        }
    }
}
