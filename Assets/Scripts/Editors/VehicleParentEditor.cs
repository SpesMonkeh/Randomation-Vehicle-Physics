#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(VehicleParent))]
    [CanEditMultipleObjects]

    public class VehicleParentEditor : Editor
    {
        bool isPrefab = false;
        static bool showButtons = true;
        bool wheelMissing = false;

        public override void OnInspectorGUI() {
            GUIStyle boldFoldout = new(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            VehicleParent targetScript = (VehicleParent)target;
            VehicleParent[] allTargets = new VehicleParent[targets.Length];
            isPrefab = F.IsPrefab(targetScript);

            for (int i = 0; i < targets.Length; i++) {
                Undo.RecordObject(targets[i], "Vehicle Parent Change");
                allTargets[i] = targets[i] as VehicleParent;
            }

            wheelMissing = false;
            if (targetScript.WheelGroups != null && targetScript.WheelGroups.Length > 0) {
                if (targetScript.Hover) {
                    foreach (HoverWheel curWheel in targetScript.HoverWheels) {
                        bool wheelfound = false;
                        foreach (WheelCheckGroup curGroup in targetScript.WheelGroups) {
                            foreach (HoverWheel curWheelInstance in curGroup.hoverWheels) {
                                if (curWheel == curWheelInstance) {
                                    wheelfound = true;
                                }
                            }
                        }

                        if (wheelfound is false)
                        {
                            wheelMissing = true;
                            break;
                        }
                    }
                }
                else {
                    foreach (Wheel curWheel in targetScript.Wheels) {
                        bool wheelfound = false;
                        foreach (WheelCheckGroup curGroup in targetScript.WheelGroups) {
                            foreach (Wheel curWheelInstance in curGroup.wheels) {
                                if (curWheel == curWheelInstance) {
                                    wheelfound = true;
                                }
                            }
                        }

                        if (!wheelfound) {
                            wheelMissing = true;
                            break;
                        }
                    }
                }
            }

            if (wheelMissing) {
                EditorGUILayout.HelpBox("If there is at least one wheel group, all wheels must be part of a group.", MessageType.Error);
            }

            DrawDefaultInspector();

            if (!isPrefab && targetScript.gameObject.activeInHierarchy) {
                showButtons = EditorGUILayout.Foldout(showButtons, "Quick Actions", boldFoldout);
                EditorGUI.indentLevel++;
                if (showButtons) {
                    if (GUILayout.Button("Get Engine")) {
                        foreach (VehicleParent curTarget in allTargets) {
                            curTarget.Engine = curTarget.transform.GetComponentInChildren<Motor>();
                        }
                    }

                    if (GUILayout.Button("Get Wheels")) {
                        foreach (VehicleParent curTarget in allTargets) {
                            if (curTarget.Hover) {
                                curTarget.HoverWheels = curTarget.transform.GetComponentsInChildren<HoverWheel>();
                            }
                            else {
                                curTarget.Wheels = curTarget.transform.GetComponentsInChildren<Wheel>();
                            }
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(targetScript);
            }
        }
    }
}
#endif
