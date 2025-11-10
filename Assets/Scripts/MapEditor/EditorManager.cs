using UnityEngine;

public class EditorManager : MonoBehaviour
{
    [Header("에디터 설정")]
    [Tooltip("오브젝트를 배치할 수 있는 표면(지형, 바닥 등)의 레이어")]
    public LayerMask placementLayerMask;

    [Tooltip("배치 시 스냅할 그리드 크기 (0이면 스냅 없음)")]
    public float gridSize = 1.0f;

    [Header("현재 상태")]
    [Tooltip("UI에서 선택되어 현재 배치할 프리팹")]
    public GameObject currentSelectedPrefab;

    [Tooltip("미리보기를 위한 반투명 머티리얼")]
    public Material previewMaterial;

    private GameObject previewInstance; // 미리보기 오브젝트 인스턴스
    private Vector3 currentGridPosition; // 현재 계산된 그리드 위치
    private bool canPlace = false; // 배치 가능한 위치인지 여부

    // --- 핵심 로직 ---

    private void Update()
    {
        // 1. 삭제 로직 (마우스 우클릭)
        if (Input.GetMouseButtonDown(1))
        {
            DeleteObject();
        }

        // 2. 배치 로직 (선택된 프리팹이 있을 때만)
        if (currentSelectedPrefab == null)
        {
            // 선택된 프리팹이 없으면 미리보기도 끈다
            if (previewInstance != null)
                previewInstance.SetActive(false);
            return;
        }

        // 마우스 위치로 레이캐스팅
        HandlePlacementRaycast();

        // 3. 배치 실행 (마우스 좌클릭)
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

        // placementLayerMask에 설정된 레이어하고만 충돌을 감지합니다.
        if (Physics.Raycast(ray, out hit, 1000f, placementLayerMask))
        {
            canPlace = true;
            // 그리드 스냅 적용
            currentGridPosition = SnapToGrid(hit.point);
            UpdatePreviewPosition();
        }
        else
        {
            canPlace = false;
            UpdatePreviewPosition(); // canPlace가 false이므로 미리보기가 숨겨집니다.
        }
    }


    // 배치
    private void PlaceObject()
    {
        if (!canPlace) return;

        // 실제 프리팹을 인스턴스화합니다.
        GameObject newObj = Instantiate(currentSelectedPrefab, currentGridPosition, Quaternion.identity);

        // [중요] 나중에 저장/로드를 위해 이 오브젝트를 식별할 태그를 지정합니다.
        newObj.tag = "EditableObject";

        // objectId를 저장하기 위해 간단한 컴포넌트를 붙여둘 수 있습니다.
        // 예: newObj.AddComponent<ObjectIdentifier>().id = currentSelectedPrefab.name;
    }

    // 삭제
    private void DeleteObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 삭제는 모든 레이어를 대상으로 하되, 태그로 필터링합니다.
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            // "EditableObject" 태그가 있는 오브젝트만 삭제
            if (hit.collider.CompareTag("EditableObject"))
            {
                Destroy(hit.collider.gameObject);
            }
        }
    }


    // --- 유틸리티 및 UI 연동 ---

    // 프리팹 선택
    public void SelectObjectToPlace(GameObject prefab)
    {
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

        // 반투명 미리보기 머티리얼 적용 (선택 사항)
        if (previewMaterial != null)
        {
            Renderer r = previewInstance.GetComponent<Renderer>();
            if (r != null) r.material = previewMaterial;
        }

        previewInstance.SetActive(false); // 일단 숨김
    }

    void UpdatePreviewPosition()
    {
        if (previewInstance == null) return;

        if (canPlace)
        {
            previewInstance.SetActive(true);
            previewInstance.transform.position = currentGridPosition;
            // TODO: Q, E 키 등으로 회전 로직
        }
        else
        {
            previewInstance.SetActive(false);
        }
    }

    // 그리드 보정
    Vector3 SnapToGrid(Vector3 position)
    {
        if (gridSize <= 0) return position; // 그리드 0이면 스냅 안함

        float x = Mathf.Round(position.x / gridSize) * gridSize;
        float y = Mathf.Round(position.y / gridSize) * gridSize;
        float z = Mathf.Round(position.z / gridSize) * gridSize;

        return new Vector3(x, y, z);
    }
}