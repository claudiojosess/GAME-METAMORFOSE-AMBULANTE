using AeroMorphET;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoiceInputManager))]
[CanEditMultipleObjects]
public sealed class VoiceInputManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (!GUILayout.Button("Clear Calibration")) return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Clear Calibration",
            "Apagar todas as gravações de calibração salvas neste computador? Esta ação não pode ser desfeita.",
            "Clear Calibration",
            "Cancel");

        if (!confirmed) return;

        foreach (Object selected in targets)
        {
            var voiceInputManager = (VoiceInputManager)selected;
            Undo.RecordObject(voiceInputManager, "Clear voice calibration");
            voiceInputManager.ClearCalibration();
            EditorUtility.SetDirty(voiceInputManager);
        }
    }
}
