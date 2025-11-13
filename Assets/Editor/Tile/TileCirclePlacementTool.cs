using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TileCirclePlacementTool : EditorWindow
{
    private float radius = 5f;
    private float angleRange = 180f;
    private int count = 5;
    private bool faceCenter = true;
    private bool previewMode = true;

    // 축 선택 및 방향 전환
    private enum Axis { X, Y, Z }
    private Axis selectedAxis = Axis.Y;
    private bool invertDirection = false;

    // 클릭 지점 모드
    private enum ClickMode { Center, EdgePoint }
    private ClickMode clickMode = ClickMode.Center;

    // Prefab 사용 여부 (기본 ON 고정)
    private bool usePrefab = true;

    // 클릭 모드: true = 위치 선택, false = 오브젝트 선택
    private bool isPlacementMode = false;

    private List<Vector3> previewPositions = new List<Vector3>();

    [MenuItem("Tools/Tile/Circle Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<TileCirclePlacementTool>("Circle Placement");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label("Tile Circle Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 클릭 모드 전환
        isPlacementMode = EditorGUILayout.Toggle("Placement Mode", isPlacementMode);

        string placementModeDescription = isPlacementMode
            ? "Scene 클릭으로 배치 위치를 선택합니다"
            : "Scene 클릭으로 오브젝트를 선택합니다";
        EditorGUILayout.HelpBox(placementModeDescription, MessageType.None);

        EditorGUILayout.Space();

        radius = EditorGUILayout.FloatField("Radius", radius);
        angleRange = EditorGUILayout.Slider("Angle Range (°)", angleRange, 0f, 360f);
        count = EditorGUILayout.IntSlider("Count", count, 1, 100);
        faceCenter = EditorGUILayout.Toggle("Face Center", faceCenter);
        previewMode = EditorGUILayout.Toggle("Show Preview", previewMode);

        EditorGUILayout.Space();
        GUILayout.Label("Axis Settings", EditorStyles.boldLabel);

        selectedAxis = (Axis)EditorGUILayout.EnumPopup("Rotation Axis", selectedAxis);
        invertDirection = EditorGUILayout.Toggle("Invert Direction", invertDirection);

        EditorGUILayout.Space();
        GUILayout.Label("Click Mode", EditorStyles.boldLabel);

        clickMode = (ClickMode)EditorGUILayout.EnumPopup("Click Point Mode", clickMode);

        string modeDescription = clickMode == ClickMode.Center
            ? "클릭 지점이 원의 중심이 됩니다"
            : "클릭 지점이 원 둘레의 시작점이 됩니다";
        EditorGUILayout.HelpBox(modeDescription, MessageType.None);

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply Placement", GUILayout.Height(30)))
        {
            ApplyPlacement();
        }

        EditorGUILayout.HelpBox(
            "1️⃣ Placement Mode 끄고 Scene에서 Prefab 선택\n" +
            "2️⃣ Placement Mode 켜고 Scene에서 배치 위치 클릭\n" +
            "3️⃣ Click Point Mode, 축, Radius 등 조정\n" +
            "4️⃣ Apply Placement 버튼 클릭",
            MessageType.Info);
    }

    private Vector3 clickPoint;
    private bool hasClickPoint = false;

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        // Placement Mode가 켜져있을 때만 클릭 제어
        if (isPlacementMode)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && isPlacementMode)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                clickPoint = hit.point;
                hasClickPoint = true;
                e.Use();
            }
        }

        if (!hasClickPoint) return;

        // 실제 중심점 계산
        Vector3 centerPoint = GetActualCenterPoint();

        // 원 시각화
        Handles.color = Color.yellow;
        Vector3 normal = GetAxisVector(selectedAxis);
        Handles.DrawWireDisc(centerPoint, normal, radius);

        // 클릭 지점 표시
        Handles.color = clickMode == ClickMode.Center ? Color.cyan : Color.magenta;
        Handles.DrawSolidDisc(clickPoint, normal, 0.2f);
        Handles.Label(clickPoint + Vector3.up * 0.5f,
            clickMode == ClickMode.Center ? "Center" : "Start Point");

        // 프리뷰 계산
        UpdatePreviewPositions();

        if (previewMode)
        {
            Handles.color = new Color(0f, 1f, 0f, 0.25f);
            foreach (var pos in previewPositions)
                Handles.DrawWireCube(pos, Vector3.one * 0.5f);
        }

        SceneView.RepaintAll();
    }

    private Vector3 GetActualCenterPoint()
    {
        if (clickMode == ClickMode.Center)
        {
            return clickPoint;
        }
        else // EdgePoint
        {
            // Z축일 때는 클릭 지점에서 Z축 방향으로 radius만큼 이동
            if (selectedAxis == Axis.Z)
            {
                float direction = invertDirection ? -1f : 1f;
                return clickPoint + new Vector3(0, 0, radius * direction);
            }

            // X, Y축일 때는 원의 시작점 기준으로 계산
            float startAngle = -angleRange / 2f;
            Vector3 offset = GetOffsetVector(startAngle);

            // 클릭 지점에서 offset의 반대 방향으로 이동하면 중심
            return clickPoint - offset;
        }
    }

    private Vector3 GetAxisVector(Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return invertDirection ? Vector3.left : Vector3.right;
            case Axis.Y: return invertDirection ? Vector3.down : Vector3.up;
            case Axis.Z: return invertDirection ? Vector3.back : Vector3.forward;
            default: return Vector3.up;
        }
    }

    private Vector3 GetOffsetVector(float angle)
    {
        float angleRad = angle * Mathf.Deg2Rad;
        float direction = invertDirection ? -1f : 1f;

        switch (selectedAxis)
        {
            case Axis.X:
                // X축 회전: YZ 평면에서 원
                return new Vector3(0,
                    Mathf.Sin(angleRad) * radius * direction,
                    Mathf.Cos(angleRad) * radius);

            case Axis.Y:
                // Y축 회전: XZ 평면에서 원 (수평)
                return new Vector3(
                    Mathf.Sin(angleRad) * radius * direction,
                    0,
                    Mathf.Cos(angleRad) * radius);

            case Axis.Z:
                // Z축 회전: XY 평면에서 원 (Z축은 고정, XY만 변함)
                return new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    Mathf.Sin(angleRad) * radius,
                    0);

            default:
                return Vector3.forward * radius;
        }
    }

    private void UpdatePreviewPositions()
    {
        previewPositions.Clear();
        if (!hasClickPoint || count <= 0) return;

        Vector3 centerPoint = GetActualCenterPoint();
        float startAngle = -angleRange / 2f;

        // 360도일 때는 마지막이 처음과 겹치지 않도록 count개로 나눔
        float step = (angleRange >= 360f) ? angleRange / count :
                     (count > 1) ? angleRange / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i;
            Vector3 offset = GetOffsetVector(angle);
            Vector3 pos = centerPoint + offset;
            previewPositions.Add(pos);
        }
    }

    private void ApplyPlacement()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("⚠️ Scene에서 오브젝트를 선택해주세요!");
            return;
        }

        if (!hasClickPoint)
        {
            Debug.LogWarning("⚠️ Scene에서 배치할 위치를 클릭해주세요!");
            return;
        }

        Vector3 centerPoint = GetActualCenterPoint();
        Undo.IncrementCurrentGroup();

        if (!usePrefab)
        {
            // 일반 모드: 선택한 오브젝트를 첫 번째 위치로 이동
            if (previewPositions.Count > 0)
            {
                Undo.RecordObject(selected.transform, "Move Object");
                selected.transform.position = previewPositions[0];

                if (faceCenter)
                {
                    Vector3 dir = (centerPoint - previewPositions[0]).normalized;
                    if (dir != Vector3.zero)
                    {
                        selected.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    }
                }

                // 나머지 위치에는 복제본 생성
                for (int i = 1; i < previewPositions.Count; i++)
                {
                    Vector3 pos = previewPositions[i];
                    GameObject newObj = Instantiate(selected);
                    newObj.transform.position = pos;

                    if (faceCenter)
                    {
                        Vector3 dir = (centerPoint - pos).normalized;
                        if (dir != Vector3.zero)
                        {
                            newObj.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                        }
                    }

                    Undo.RegisterCreatedObjectUndo(newObj, "Place Tile Circle");
                }

                Debug.Log($"✅ [Object Mode] Moved 1 + Created {previewPositions.Count - 1} objects");
            }
        }
        else
        {
            // Prefab 모드: 원본 Prefab으로부터 생성
            GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selected);
            if (prefabSource == null)
            {
                Debug.LogWarning($"⚠️ 선택한 오브젝트 '{selected.name}'가 Prefab이 아닙니다! Use Prefab 옵션을 끄거나 Prefab을 선택하세요.");
                return;
            }

            for (int i = 0; i < previewPositions.Count; i++)
            {
                Vector3 pos = previewPositions[i];
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
                newObj.transform.position = pos;

                if (faceCenter)
                {
                    Vector3 dir = (centerPoint - pos).normalized;
                    if (dir != Vector3.zero)
                    {
                        newObj.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    }
                }

                Undo.RegisterCreatedObjectUndo(newObj, "Place Tile Circle");
            }

            Debug.Log($"✅ [Prefab Mode] Created {previewPositions.Count} prefabs from: {prefabSource.name}");
        }
    }
}