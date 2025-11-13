using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TilePlacementTool : EditorWindow
{
    private GameObject selectedPrefab;
    private Vector3 direction = Vector3.right;
    private float spacing = 2f;
    private int count = 5;
    private bool previewMode = true;

    private List<Vector3> previewPositions = new List<Vector3>();
    private bool dragging = false;
    private Vector3 dragStart;

    private enum Axis { Custom, X, Y, Z }
    private Axis lockAxis = Axis.Custom;

    [MenuItem("Tools/Tile/Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<TilePlacementTool>("Tile Placement");
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
        GUILayout.Label("Tile Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Direction & Axis Controls
        GUILayout.Label("Direction Control", EditorStyles.miniBoldLabel);
        direction = EditorGUILayout.Vector3Field("Direction", direction);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Lock Axis:", GUILayout.Width(70));
        if (GUILayout.Toggle(lockAxis == Axis.Custom, "Free", "Button")) lockAxis = Axis.Custom;
        if (GUILayout.Toggle(lockAxis == Axis.X, "X", "Button")) lockAxis = Axis.X;
        if (GUILayout.Toggle(lockAxis == Axis.Y, "Y", "Button")) lockAxis = Axis.Y;
        if (GUILayout.Toggle(lockAxis == Axis.Z, "Z", "Button")) lockAxis = Axis.Z;
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        spacing = EditorGUILayout.FloatField("Spacing", spacing);
        count = EditorGUILayout.IntField("Count", count);
        previewMode = EditorGUILayout.Toggle("Show Preview", previewMode);

        EditorGUILayout.Space();

        GUI.enabled = Selection.activeGameObject != null;
        if (GUILayout.Button("Apply Placement", GUILayout.Height(30)))
        {
            ApplyPlacement();
        }
        GUI.enabled = true;

        EditorGUILayout.HelpBox("Select a prefab instance in Scene.\nDrag in Scene view to adjust direction.\nClick Apply to create tiles.", MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;
        selectedPrefab = selected;

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // 클릭으로 드래그 시작
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                dragging = true;
                dragStart = hit.point;
                e.Use();
            }
        }

        // 드래그 중 방향 계산
        if (dragging && e.type == EventType.MouseDrag)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 dragEnd = hit.point;
                direction = (dragEnd - dragStart).normalized;

                // 축 고정 옵션
                if (lockAxis == Axis.X) direction = Vector3.right * Mathf.Sign(direction.x);
                else if (lockAxis == Axis.Y) direction = Vector3.up * Mathf.Sign(direction.y);
                else if (lockAxis == Axis.Z) direction = Vector3.forward * Mathf.Sign(direction.z);
            }
            e.Use();
        }

        // 드래그 종료
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            dragging = false;
            e.Use();
        }

        // 화살표 표시
        Handles.color = Color.yellow;
        Handles.ArrowHandleCap(0, selected.transform.position, Quaternion.LookRotation(direction), spacing, EventType.Repaint);

        // 미리보기 계산
        previewPositions.Clear();
        for (int i = 1; i <= count; i++)
        {
            Vector3 pos = selected.transform.position + direction.normalized * spacing * i;
            previewPositions.Add(pos);
        }

        // 미리보기 표시
        if (previewMode)
        {
            Handles.color = new Color(0f, 1f, 0f, 0.25f);
            foreach (var pos in previewPositions)
            {
                Handles.DrawWireCube(pos, selected.transform.localScale);
            }
        }

        SceneView.RepaintAll();
    }

    private void ApplyPlacement()
    {
        if (selectedPrefab == null)
        {
            Debug.LogWarning("No prefab selected!");
            return;
        }

        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selectedPrefab);
        if (prefabSource == null)
        {
            Debug.LogWarning("Selected object is not a prefab instance.");
            return;
        }

        Undo.IncrementCurrentGroup();
        for (int i = 1; i <= count; i++)
        {
            Vector3 pos = selectedPrefab.transform.position + direction.normalized * spacing * i;
            GameObject newTile = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
            newTile.transform.position = pos;
            newTile.transform.rotation = selectedPrefab.transform.rotation;
            Undo.RegisterCreatedObjectUndo(newTile, "Place Tile");
        }

        Debug.Log($"✅ Placed {count} tiles from {selectedPrefab.name}");
    }
}
