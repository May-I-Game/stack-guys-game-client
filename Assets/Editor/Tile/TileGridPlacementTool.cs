using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TileGridPlacementTool : EditorWindow
{
    private GameObject selectedPrefab;

    private int rows = 3;
    private int columns = 3;
    private float spacingX = 2f;
    private float spacingZ = 2f;
    private bool previewMode = true;

    private List<Vector3> previewPositions = new List<Vector3>();

    [MenuItem("Tools/Tile/Grid Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<TileGridPlacementTool>("Grid Placement");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= Repaint;
    }

    private void OnGUI()
    {
        GUILayout.Label("Tile Grid Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        rows = EditorGUILayout.IntField("Rows", rows);
        columns = EditorGUILayout.IntField("Columns", columns);
        spacingX = EditorGUILayout.FloatField("Spacing X", spacingX);
        spacingZ = EditorGUILayout.FloatField("Spacing Z", spacingZ);
        previewMode = EditorGUILayout.Toggle("Show Preview", previewMode);

        EditorGUILayout.Space();

        GUI.enabled = Selection.activeGameObject != null;
        if (GUILayout.Button("Apply Grid Placement", GUILayout.Height(30)))
        {
            ApplyPlacement();
        }
        GUI.enabled = true;

        EditorGUILayout.HelpBox(
            "Select a prefab instance in Scene.\n" +
            "Grid will be placed on XZ plane based on that position.\n" +
            "Use Spacing/Rows/Columns to adjust layout.",
            MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        selectedPrefab = selected;
        Vector3 basePos = selected.transform.position;
        previewPositions.Clear();

        // 미리보기 위치 계산
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 pos = basePos + new Vector3(c * spacingX, 0f, r * spacingZ);
                previewPositions.Add(pos);
            }
        }

        // Scene 미리보기 렌더링
        if (previewMode)
        {
            Handles.color = new Color(0f, 1f, 0f, 0.25f);
            foreach (var pos in previewPositions)
            {
                Handles.DrawWireCube(pos, selected.transform.localScale);
            }
        }

        // 축 시각화
        Handles.color = Color.yellow;
        Handles.ArrowHandleCap(0, basePos, Quaternion.LookRotation(Vector3.right), spacingX, EventType.Repaint);
        Handles.ArrowHandleCap(0, basePos, Quaternion.LookRotation(Vector3.forward), spacingZ, EventType.Repaint);

        SceneView.RepaintAll();
    }

    private void ApplyPlacement()
    {
        if (selectedPrefab == null)
        {
            Debug.LogWarning("No prefab selected for grid placement!");
            return;
        }

        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selectedPrefab);
        if (prefabSource == null)
        {
            Debug.LogWarning("Selected object is not a prefab instance.");
            return;
        }

        Undo.IncrementCurrentGroup();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 pos = selectedPrefab.transform.position + new Vector3(c * spacingX, 0f, r * spacingZ);
                GameObject newTile = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
                newTile.transform.position = pos;
                newTile.transform.rotation = selectedPrefab.transform.rotation;
                Undo.RegisterCreatedObjectUndo(newTile, "Place Tile Grid");
            }
        }

        Debug.Log($"✅ Placed {rows * columns} tiles from {selectedPrefab.name}");
    }
}
