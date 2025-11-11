using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EditorManager : MonoBehaviour
{
    [Header("에디터 설정")]
    public float gridSize = 1.0f;
    public float rotationStep = 45.0f;

    [Header("현재 상태")]
    public GameObject currentSelectedPrefab;
    public Material previewMaterial;

    [Header("팔레트 UI 설정")]
    public Transform paletteContentArea;
    public GameObject paletteButtonPrefab;

    [Header("Save/Load UI")]
    public GameObject saveLoadPanel;
    public InputField mapNameInput;
    public Transform fileListContentArea;
    public GameObject fileButtonPrefab;

    [Header("팔레트 데이터")]
    public List<PaletteCategory> allCategories = new List<PaletteCategory>();

    private GameObject previewInstance; // 미리보기 오브젝트 인스턴스
    private Vector3 currentGridPosition; // X, Z는 마우스, Y는 Q/E로 조절될 위치
    private Quaternion currentRotation = Quaternion.identity; // 현재 회전 값
    private bool canPlace = false; // 배치 가능한 위치인지 여부
    private bool isFirstHitAfterSelect = true; // 새 오브젝트 선택 후 첫 레이캐스트인지 확인

    private Stack<IEditorAction> undoStack = new Stack<IEditorAction>();
    private Stack<IEditorAction> redoStack = new Stack<IEditorAction>();

    private Dictionary<string, GameObject> prefabDatabase = new Dictionary<string, GameObject>();

    private void Awake()
    {
        // 프리팹 데이터베이스 구축
        foreach (PaletteCategory category in allCategories)
        {
            if (category == null) continue;

            foreach (PaletteItem item in category.items)
            {
                if (item == null || item.prefab == null) continue;

                GameObject prefab = item.prefab;

                // 데이터베이스에 프리팹 이름이 이미 등록되지 않은 경우에만 추가
                // (서로 다른 카테고리에 동일한 프리팹이 있을 수 있으므로)
                if (!prefabDatabase.ContainsKey(prefab.name))
                {
                    prefabDatabase.Add(prefab.name, prefab);
                }
            }
        }

        Debug.Log($"프리팹 데이터베이스 자동 구축 완료. {prefabDatabase.Count}개 항목 로드됨.");
    }

    private void Start()
    {
        // 씬이 시작되면 기본으로 0번째 카테고리를 보여줍니다.
        if (allCategories.Count > 0)
        {
            ShowCategory(0);
        }
    }

    private void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            // UI 위에 마우스가 있으니, 미리보기를 숨깁니다.
            if (previewInstance != null && previewInstance.activeSelf)
            {
                previewInstance.SetActive(false);
            }
            return;
        }

        // Undo/Redo (T, Y 키)
        if (Input.GetKeyDown(KeyCode.T))
        {
            PerformUndo();
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            PerformRedo();
        }

        // 우클릭 삭제
        // 프리팹이 선택되지 않아도 삭제는 가능해야 하므로, 삭제 로직은 위에 있음
        if (Input.GetMouseButtonDown(1))
        {
            DeleteObject();
        }

        // B 키로 커서 토글시 선택해제
        if (Input.GetKeyDown(KeyCode.B))
        {
            currentSelectedPrefab = null;
        }

        // 배치 로직
        if (currentSelectedPrefab == null)
        {
            if (previewInstance != null && previewInstance.activeSelf)
                previewInstance.SetActive(false);
            return;
        }

        // 마우스 위치로 레이캐스팅
        HandlePlacementRaycast();

        // Q, E로 높이 조절
        if (Input.GetKeyDown(KeyCode.Q))
        {
            float heightStep = (gridSize > 0) ? gridSize : 1.0f;
            currentGridPosition.y -= heightStep;
            UpdatePreviewPosition();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            float heightStep = (gridSize > 0) ? gridSize : 1.0f;
            currentGridPosition.y += heightStep;
            UpdatePreviewPosition();
        }

        // R키로 회전
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotatePreview();
        }

        // 좌클릭으로 배치
        if (Input.GetMouseButtonDown(0) && canPlace)
        {
            PlaceObject();
        }
    }

    // 레이캐스트로 위치조정
    private void HandlePlacementRaycast()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // [변경] LayerMask 없이 모든 레이어와 충돌을 감지합니다.
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            canPlace = true;
            Vector3 hitPoint = hit.point;

            // 그리드 스냅 적용 (X, Z만)
            float snappedX = (gridSize > 0) ? Mathf.Round(hitPoint.x / gridSize) * gridSize : hitPoint.x;
            float snappedZ = (gridSize > 0) ? Mathf.Round(hitPoint.z / gridSize) * gridSize : hitPoint.z;

            // 새 오브젝트 선택 후 첫 히트일 경우, Y 높이 초기화
            if (isFirstHitAfterSelect)
            {
                currentGridPosition.y = 0;
                isFirstHitAfterSelect = false; // 플래그 해제
            }

            // X, Z 값만 갱신 (Y는 Q/E로 설정된 값 유지)
            currentGridPosition.x = snappedX;
            currentGridPosition.z = snappedZ;

            UpdatePreviewPosition();
        }
        else
        {
            // 허공에 마우스가 있을 경우 배치 비활성화
            canPlace = false;
            UpdatePreviewPosition();
        }
    }

    private void RotatePreview()
    {
        if (previewInstance == null) return;

        // Y축으로 누적 회전
        currentRotation *= Quaternion.Euler(0, rotationStep, 0);

        // 미리보기 위치/회전 즉시 갱신
        UpdatePreviewPosition();
    }

    // 배치
    private void PlaceObject()
    {
        if (!canPlace) return;

        // 직접 Instantiate 하는 대신, PlaceObjectAction을 생성하고 등록
        IEditorAction action = new PlaceObjectAction(currentSelectedPrefab, currentGridPosition, currentRotation);
        RegisterAction(action);
    }

    // 삭제
    private void DeleteObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f))
        {
            // "MapObjectInfo" 컴포넌트가 있고, 현재 활성화된(삭제되지 않은) 오브젝트만 삭제
            MapObjectInfo info = hit.collider.GetComponent<MapObjectInfo>();

            if (info != null && hit.collider.gameObject.activeSelf)
            {
                // 직접 Destroy 하는 대신, DeleteObjectAction을 생성하고 등록
                IEditorAction action = new DeleteObjectAction(hit.collider.gameObject);
                RegisterAction(action);
            }
        }
    }

    // --- [수정] 액션 등록 및 실행 ---

    // 새로운 행동을 등록하고 실행합니다.
    private void RegisterAction(IEditorAction action)
    {
        // 1. 행동을 즉시 실행
        action.Execute();
        // 2. Undo 스택에 추가
        undoStack.Push(action);
        // 3. 새 행동을 했으므로 Redo 스택은 비워야 함
        foreach (IEditorAction oldAction in redoStack)
        {
            oldAction.Cleanup();
        }
        redoStack.Clear();
    }

    // 마지막 행동을 취소 (Undo)
    private void PerformUndo()
    {
        if (undoStack.Count > 0)
        {
            IEditorAction action = undoStack.Pop();
            action.Undo();
            redoStack.Push(action);
            Debug.Log("Action Undone.");
        }
    }

    // 취소했던 행동을 다시 실행 (Redo)
    private void PerformRedo()
    {
        if (redoStack.Count > 0)
        {
            IEditorAction action = redoStack.Pop();
            action.Execute(); // 행동을 다시 실행
            undoStack.Push(action);
            Debug.Log("Action Redone.");
        }
    }

    // 프리팹 선택
    public void SelectObjectToPlace(GameObject prefab)
    {
        // 토글 기능
        if (currentSelectedPrefab == prefab)
        {
            // 이미 선택된 프리팹이므로, 선택 해제
            currentSelectedPrefab = null;

            // 미리보기가 남아있으면 파괴
            if (previewInstance != null)
            {
                Destroy(previewInstance);
            }

            // 모든 상태 초기화
            canPlace = false;
            isFirstHitAfterSelect = true;

            return;
        }

        // 새 프리팹 선택
        currentSelectedPrefab = prefab;

        // 이전 미리보기가 있다면 파괴
        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }

        // 새 프리팹으로 미리보기 인스턴스 생성
        previewInstance = Instantiate(prefab);
        previewInstance.name = "PREVIEW";

        // 레이캐스트에 방해되지 않도록 콜라이더를 끈다
        foreach (Collider c in previewInstance.GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        // 반투명 미리보기 머티리얼 적용
        if (previewMaterial != null)
        {
            // GetComponentsInChildren로 모든 자식의 Renderer를 가져옵니다.
            Renderer[] allRenderers = previewInstance.GetComponentsInChildren<Renderer>();

            // 모든 Renderer를 순회하며 머티리얼을 적용합니다.
            foreach (Renderer r in allRenderers)
            {
                // .material 대신 .sharedMaterial을 사용하면 임시 인스턴스에서 더 효율적입니다.
                r.sharedMaterial = previewMaterial;
            }
        }

        // 일단 숨김
        previewInstance.SetActive(false);

        // 회전 값 초기화
        currentRotation = Quaternion.identity;

        isFirstHitAfterSelect = true;
        canPlace = false;
    }

    // 오브젝트 상태 갱신
    private void UpdatePreviewPosition()
    {
        if (previewInstance == null) return;

        if (canPlace)
        {
            previewInstance.SetActive(true);
            previewInstance.transform.position = currentGridPosition;
            previewInstance.transform.rotation = currentRotation;
        }
        else
        {
            previewInstance.SetActive(false);
        }
    }

    // 지정된 인덱스의 카테고리를 상단 팔레트에 표시
    public void ShowCategory(int categoryIndex)
    {
        if (categoryIndex < 0 || categoryIndex >= allCategories.Count)
        {
            Debug.LogError("유효하지 않은 카테고리 인덱스입니다: " + categoryIndex);
            return;
        }

        // 기존 팔레트 버튼들을 모두 삭제합니다.
        foreach (Transform child in paletteContentArea)
        {
            Destroy(child.gameObject);
        }

        // 선택된 카테고리의 아이템 리스트를 가져옵니다.
        PaletteCategory selectedCategory = allCategories[categoryIndex];

        // 리스트를 순회하며 새 버튼을 생성합니다.
        foreach (PaletteItem item in selectedCategory.items)
        {
            // paletteButtonPrefab을 Content 영역 자식으로 Instantiate
            GameObject newButtonGO = Instantiate(paletteButtonPrefab, paletteContentArea);

            // 버튼 아이콘 설정
            Image buttonIcon = newButtonGO.GetComponent<Image>();
            if (buttonIcon != null)
            {
                buttonIcon.sprite = item.icon;
            }

            // 생성된 버튼에 OnClick 이벤트 추가
            Button buttonComponent = newButtonGO.GetComponent<Button>();

            // 루프 안에서 람다(Lambda)를 사용할 때 변수 문제를 피하기 위해
            // 프리팹을 로컬 변수로 복사합니다.
            GameObject prefabToPlace = item.prefab;

            buttonComponent.onClick.AddListener(() =>
            {
                // 이 버튼이 눌리면 EditorManager의 SelectObjectToPlace를 호출
                SelectObjectToPlace(prefabToPlace);
            });
        }

        // (선택 사항) 팔레트가 바뀌었으니, 현재 선택된 프리팹을 초기화합니다.
        SelectObjectToPlace(null); // 토글 기능이 null을 처리해 줌
    }

    // --- [추가] 저장 / 불러오기 로직 ---

    // 현재 맵을 JSON 파일로 저장
    public void SaveMap(string mapName)
    {
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogError("맵 이름이 비어있습니다!");
            return;
        }

        MapData mapData = new MapData();
        mapData.mapName = mapName;
        // TODO: mapData.startPos 저장 로직 (예: "StartPoint" objectId를 가진 오브젝트 찾기)

        // 씬에 있는 모든 MapObjectInfo 컴포넌트를 찾습니다.
        MapObjectInfo[] allMapObjects = FindObjectsByType<MapObjectInfo>(FindObjectsSortMode.None);

        foreach (MapObjectInfo objInfo in allMapObjects)
        {
            // 비활성화된(Undo된) 오브젝트는 저장하지 않습니다.
            if (!objInfo.gameObject.activeSelf)
            {
                continue;
            }

            MapObjectData data = new MapObjectData();
            data.objectId = objInfo.objectId; // 프리팹 이름 ("platform_basic")
            data.position = objInfo.transform.position;
            data.rotation = objInfo.transform.rotation;
            data.scale = objInfo.transform.localScale;

            mapData.objects.Add(data);
        }

        // MapData 객체를 JSON 문자열로 직렬화
        string json = JsonUtility.ToJson(mapData, true); // true: 보기 좋게 포맷팅

        // 저장 경로 설정 (예: C:/Users/YourName/AppData/LocalLow/DefaultCompany/YourGame/MyMap.json)
        string path = Path.Combine(Application.persistentDataPath, mapName + ".json");

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"맵 저장 성공: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"맵 저장 실패: {e.Message}");
        }
    }

    // (UI 버튼용) JSON 파일에서 맵을 불러옵니다.
    public void LoadMap(string mapName)
    {
        string path = Path.Combine(Application.persistentDataPath, mapName + ".json");

        if (!File.Exists(path))
        {
            Debug.LogError($"불러올 맵 파일을 찾을 수 없습니다: {path}");
            return;
        }

        try
        {
            // 파일에서 JSON 텍스트 읽기
            string json = File.ReadAllText(path);

            // JSON을 MapData 객체로 역직렬화
            MapData mapData = JsonUtility.FromJson<MapData>(json);

            // 현재 맵 상태 초기화 (오브젝트, Undo/Redo 스택)
            ClearCurrentMap();

            // 맵 데이터로 오브젝트 다시 생성
            foreach (MapObjectData data in mapData.objects)
            {
                GameObject prefabToLoad;

                // 데이터베이스에서 objectId로 원본 프리팹 찾기
                if (prefabDatabase.TryGetValue(data.objectId, out prefabToLoad))
                {
                    // 프리팹 생성 및 위치/회전/크기 설정
                    GameObject newObj = Instantiate(prefabToLoad, data.position, data.rotation);
                    newObj.transform.localScale = data.scale;

                    // 다시 편집/저장/삭제할 수 있도록 MapObjectInfo 컴포넌트 추가
                    newObj.AddComponent<MapObjectInfo>().objectId = data.objectId;
                }
                else
                {
                    Debug.LogWarning($"데이터베이스에 '{data.objectId}' 프리팹이 없습니다. 로드 생략.");
                }
            }

            // TODO: mapData.startPos 로드 로직

            Debug.Log($"맵 불러오기 성공: {mapName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"맵 불러오기 실패: {e.Message}");
        }
    }

    // 맵 로드 전에 현재 씬의 모든 맵 오브젝트와 히스토리를 초기화합니다.
    private void ClearCurrentMap()
    {
        // 현재 배치된 모든 맵 오브젝트 파괴
        MapObjectInfo[] allMapObjects = FindObjectsByType<MapObjectInfo>(FindObjectsSortMode.None);
        foreach (MapObjectInfo objInfo in allMapObjects)
        {
            Destroy(objInfo.gameObject);
        }

        // Undo/Redo 스택 비우기 (Cleanup 호출)
        foreach (IEditorAction action in undoStack) action.Cleanup();
        foreach (IEditorAction action in redoStack) action.Cleanup();

        undoStack.Clear();
        redoStack.Clear();

        // 현재 선택된 프리팹/미리보기 초기화
        SelectObjectToPlace(null);
    }

    public void OpenSaveLoadPanel()
    {
        if (saveLoadPanel == null) return;

        saveLoadPanel.SetActive(true);
        RefreshFileList();
    }

    public void CloseSaveLoadPanel()
    {
        if (saveLoadPanel == null) return;
        saveLoadPanel.SetActive(false);
    }

    public void OnSaveButtonPressed()
    {
        string mapName = mapNameInput.text;
        if (string.IsNullOrWhiteSpace(mapName))
        {
            Debug.LogError("맵 이름이 비어있습니다. InputField를 확인하세요.");
            return;
        }

        SaveMap(mapName);
        RefreshFileList(); // 저장 후 목록 새로고침
    }

    // (UI 버튼용) '불러오기' 버튼을 눌렀을 때 호출됩니다.
    public void OnLoadButtonPressed()
    {
        string mapName = mapNameInput.text;
        if (string.IsNullOrWhiteSpace(mapName))
        {
            Debug.LogError("맵 이름이 비어있습니다. 불러올 맵을 선택하거나 입력하세요.");
            return;
        }

        LoadMap(mapName);
        CloseSaveLoadPanel(); // 불러오기 후 패널 닫기
    }

    private void RefreshFileList()
    {
        if (fileListContentArea == null) return;

        // 1. 기존 목록 버튼 모두 삭제
        foreach (Transform child in fileListContentArea)
        {
            Destroy(child.gameObject);
        }

        // 2. persistentDataPath에서 .json 파일 모두 찾기
        string[] allFiles = Directory.GetFiles(Application.persistentDataPath, "*.json");

        // 3. 각 파일을 버튼으로 생성
        foreach (string filePath in allFiles)
        {
            // "C:/.../MyMap.json" -> "MyMap"
            string mapName = Path.GetFileNameWithoutExtension(filePath);

            GameObject newButtonGO = Instantiate(fileButtonPrefab, fileListContentArea);

            // 버튼 텍스트 설정
            Text buttonText = newButtonGO.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = mapName;
            }

            // 버튼 클릭 이벤트 연결
            Button buttonComponent = newButtonGO.GetComponent<Button>();
            if (buttonComponent != null)
            {
                // 람다(Lambda)를 사용해 클릭 시 OnFileSelected 함수에 mapName 전달
                buttonComponent.onClick.AddListener(() =>
                {
                    OnFileSelected(mapName);
                });
            }
        }
    }

    private void OnFileSelected(string mapName)
    {
        // InputField에 맵 이름을 채워넣어 사용자가 Load/Save(덮어쓰기) 할 수 있게 함
        if (mapNameInput != null)
        {
            mapNameInput.text = mapName;
        }
    }
}