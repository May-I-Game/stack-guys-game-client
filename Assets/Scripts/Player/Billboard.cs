// 📁 Billboard.cs (Y축 고정 버전 + 거리 기반 컬링)
using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private bool lockY = true; // Y축 고정 여부
    [SerializeField] private float cullingDistance = 15f; // 이 거리 이상이면 업데이트 안 함

    private Camera mainCamera;
    private float cullingDistanceSqr; // 제곱값 캐싱
    private Canvas canvasComponent; // Canvas 컴포넌트 캐싱

    private void Start()
    {
        mainCamera = Camera.main;
        cullingDistanceSqr = cullingDistance * cullingDistance;

        // Canvas 컴포넌트 찾기 (자식에 있을 수도 있음)
        canvasComponent = GetComponentInChildren<Canvas>();
        if (canvasComponent == null)
        {
            Debug.LogWarning($"[Billboard] {gameObject.name}에 Canvas 컴포넌트가 없습니다. 거리 컬링이 작동하지 않습니다.");
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // 거리 체크: 너무 멀면 Canvas만 비활성화 (렌더링 OFF, 하지만 LateUpdate는 계속 실행)
        float distanceSqr = (transform.position - mainCamera.transform.position).sqrMagnitude;
        bool isFar = distanceSqr > cullingDistanceSqr;

        if (canvasComponent != null)
        {
            // Canvas enabled 상태를 거리에 따라 변경
            bool shouldBeEnabled = !isFar;
            if (canvasComponent.enabled != shouldBeEnabled)
            {
                canvasComponent.enabled = shouldBeEnabled;
            }
        }

        // 거리가 멀면 회전 계산 스킵
        if (isFar)
            return;

        if (lockY)
        {
            // Y축은 고정하고 X, Z만 회전 (수평 회전만)
            Vector3 direction = mainCamera.transform.position - transform.position;
            direction.y = 0; // Y축 무시

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-direction);
            }
        }
        else
        {
            // 완전히 카메라를 향함
            transform.LookAt(mainCamera.transform);
            transform.Rotate(0, 180, 0); // 뒤집기
        }
    }
}