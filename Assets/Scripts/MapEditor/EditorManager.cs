using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EditorManager : MonoBehaviour
{
    [Header("에디터 설정")]
    public float gridSize = 1.0f;
    public float rotationStep = 45.0f;

    [Header("현재 상태")]
    [Tooltip("UI에서 선택되어 현재 배치할 프리팹")]
    public GameObject currentSelectedPrefab;
    [Tooltip("미리보기를 위한 반투명 머티리얼")]
    public Material previewMaterial;

    private GameObject previewInstance; // 미리보기 오브젝트 인스턴스
    private Vector3 currentGridPosition; // X, Z는 마우스, Y는 Q/E로 조절될 위치
    private Quaternion currentRotation = Quaternion.identity; // 현재 회전 값
    private bool canPlace = false; // 배치 가능한 위치인지 여부
    private bool isFirstHitAfterSelect = true; // 새 오브젝트 선택 후 첫 레이캐스트인지 확인

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

            // 새 오브젝트 선택 후 첫 히트일 경우, Y 높이를 바닥 높이로 초기화
            if (isFirstHitAfterSelect)
            {
                float snappedY = (gridSize > 0) ? Mathf.Round(hitPoint.y / gridSize) * gridSize : hitPoint.y;
                currentGridPosition.y = snappedY;
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


        isFirstHitAfterSelect = true;
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
                Destroy(hit.collider.gameObject);
            }
        }
    }

    // --- [수정] 액션 등록 및 실행 ---

    // --- 유틸리티 및 UI 연동 ---

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

        // 반투명 미리보기 머티리얼 적용 (선택 사항)
        if (previewMaterial != null)
        {
            Renderer r = previewInstance.GetComponent<Renderer>();
            if (r != null) r.material = previewMaterial;
        }

        previewInstance.SetActive(false); // 일단 숨김
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

    {
        if (gridSize <= 0) return position; // 그리드 0이면 스냅 안함


        return new Vector3(x, y, z);
    }
}