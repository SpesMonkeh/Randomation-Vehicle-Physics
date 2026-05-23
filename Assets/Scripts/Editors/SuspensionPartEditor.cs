#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(SuspensionPart))]
    [CanEditMultipleObjects]

    public class SuspensionPartEditor : Editor
    {
        static bool showHandles = true;

        public override void OnInspectorGUI() {
            showHandles = EditorGUILayout.Toggle("Show Handles", showHandles);
            SceneView.RepaintAll();

            DrawDefaultInspector();
        }

        public void OnSceneGUI() {
            SuspensionPart targetScript = (SuspensionPart)target;
            Undo.RecordObject(targetScript, "Suspension Part Change");

            if (showHandles && targetScript.gameObject.activeInHierarchy) {
                if (targetScript.connectObject && !targetScript.isHub && !targetScript.solidAxle && Tools.current == Tool.Move) {
                    targetScript.connectPoint = targetScript.connectObject.InverseTransformPoint(Handles.PositionHandle(targetScript.connectObject.TransformPoint(targetScript.connectPoint), Tools.pivotRotation == PivotRotation.Local ? targetScript.connectObject.rotation : Quaternion.identity));
                }
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(targetScript);
            }
        }
    }
}
#endif