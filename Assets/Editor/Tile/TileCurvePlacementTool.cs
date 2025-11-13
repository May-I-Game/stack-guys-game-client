using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TileCurvePlacementTool : EditorWindow
{
    private GameObject selectedPrefab;

    private float spacing = 2f;
    private bool previewMode = true;
    private List<Vector3> controlPoints = new List<Vector3>();
    private List<Vector3> previewPositions = new List<Vector3>();

    [MenuItem("Tools/Tile/Curve Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<TileCurvePlacementTool>("Curve Placement");
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
        GUILayout.Label("Tile Curve Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        spacing = EditorGUILayout.FloatField("Spacing", spacing);
        previewMode = EditorGUILayout.Toggle("Show Preview", previewMode);

        EditorGUILayout.Space();

        if (GUILayout.Button("Clear Points"))
        {
            controlPoints.Clear();
            previewPositions.Clear();
            SceneView.RepaintAll();
        }

        GUI.enabled = Selection.activeGameObject != null && controlPoints.Count >= 2;
        if (GUILayout.Button("Apply Placement", GUILayout.Height(30)))
        {
            ApplyPlacement();
        }
        GUI.enabled = true;

        EditorGUILayout.HelpBox(
            "Click in Scene to add curve points.\n" +
            "Hold Shift + Click to remove last point.\n" +
            "When 2 or more points exist, curve preview will appear.\n" +
            "Select a prefab in Scene before applying.",
            MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // 클릭으로 포인트 추가
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (e.shift) // Shift + Click → 마지막 점 삭제
                {
                    if (controlPoints.Count > 0)
                        controlPoints.RemoveAt(controlPoints.Count - 1);
                }
                else
                {
                    controlPoints.Add(hit.point);
                }
                e.Use();
            }
        }

        // 포인트 시각화
        Handles.color = Color.yellow;
        for (int i = 0; i < controlPoints.Count; i++)
        {
            Handles.SphereHandleCap(0, controlPoints[i], Quaternion.identity, 0.2f, EventType.Repaint);
            if (i > 0)
                Handles.DrawLine(controlPoints[i - 1], controlPoints[i]);
        }

        // 곡선 그리기
        if (controlPoints.Count >= 2)
        {
            Handles.color = Color.cyan;
            Vector3 prevPos = controlPoints[0];
            for (float t = 0; t <= 1; t += 0.01f)
            {
                Vector3 pos = EvaluateCurve(controlPoints, t);
                Handles.DrawLine(prevPos, pos);
                prevPos = pos;
            }

            // 미리보기 계산
            UpdatePreviewPositions();

            // 미리보기 표시
            if (previewMode)
            {
                Handles.color = new Color(0f, 1f, 0f, 0.25f);
                foreach (var pos in previewPositions)
                    Handles.DrawWireCube(pos, Vector3.one * 1.0f);
            }
        }

        SceneView.RepaintAll();
    }

    private Vector3 EvaluateCurve(List<Vector3> points, float t)
    {
        // n차 베지어 곡선 (De Casteljau 알고리즘)
        List<Vector3> temp = new List<Vector3>(points);
        for (int k = points.Count - 1; k > 0; k--)
        {
            for (int i = 0; i < k; i++)
                temp[i] = Vector3.Lerp(temp[i], temp[i + 1], t);
        }
        return temp[0];
    }

    private void UpdatePreviewPositions()
    {
        previewPositions.Clear();

        if (controlPoints.Count < 2) return;

        float curveLength = EstimateCurveLength();
        int tileCount = Mathf.Max(1, Mathf.FloorToInt(curveLength / spacing));

        for (int i = 0; i <= tileCount; i++)
        {
            float t = (float)i / tileCount;
            Vector3 pos = EvaluateCurve(controlPoints, t);
            previewPositions.Add(pos);
        }
    }

    private float EstimateCurveLength(int samples = 50)
    {
        float length = 0f;
        Vector3 prev = controlPoints[0];
        for (int i = 1; i <= samples; i++)
        {
            Vector3 pos = EvaluateCurve(controlPoints, (float)i / samples);
            length += Vector3.Distance(prev, pos);
            prev = pos;
        }
        return length;
    }

    private void ApplyPlacement()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("No prefab selected for placement!");
            return;
        }

        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selected);
        if (prefabSource == null)
        {
            Debug.LogWarning("Selected object is not a prefab instance.");
            return;
        }

        Undo.IncrementCurrentGroup();

        foreach (var pos in previewPositions)
        {
            GameObject newTile = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
            newTile.transform.position = pos;
            newTile.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(newTile, "Place Tile Curve");
        }

        Debug.Log($"✅ Placed {previewPositions.Count} tiles along curve from {selected.name}");
    }
}
