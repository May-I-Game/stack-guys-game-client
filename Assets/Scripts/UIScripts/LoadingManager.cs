using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using Unity.Netcode;
using System.Collections.Generic;

public class LoadingManager : MonoBehaviour
{
    [Header("러너 재활용 설정")]
    [Tooltip("로딩 화면에 사용할 모든 캐릭터 오브젝트 (씬에 미리 배치)")]
    [SerializeField] private List<GameObject> runnersInScene;
    [SerializeField] private Transform runnerSpawnPoint;
    [SerializeField] private Transform runnerEndPoint;
    [SerializeField] private float runDuration = 3f;

    // 모든 캐릭터의 원래 위치를 저장할 딕셔너리
    private Dictionary<GameObject, Vector3> originalRunnerPositions = new Dictionary<GameObject, Vector3>();

    void Start()
    {
        if (runnersInScene == null || runnersInScene.Count == 0)
        {
            Debug.LogError("씬에 할당된 러너가 없습니다! 인스펙터에 캐릭터를 할당해주세요.");
            return;
        }

        // 1. 모든 캐릭터의 원래 위치를 저장하고, 모두 비활성화합니다.
        foreach (GameObject runner in runnersInScene)
        {
            // 키 값이 중복되지 않도록 확인 후 추가 (혹시 모를 오류 방지)
            if (!originalRunnerPositions.ContainsKey(runner))
            {
                originalRunnerPositions.Add(runner, runner.transform.position);
            }

            // 시작할 때 모든 캐릭터는 숨겨져 대기열에 있게 됩니다.
            runner.SetActive(false);
        }

        // 2. 로딩이 끝날 때까지 무한 반복 코루틴 시작
        StartCoroutine(RepeatRunCyclesForRandomRunner());
    }

    /// <summary>
    /// 랜덤 캐릭터를 선택하여 트랙을 달린 후, 원래 위치로 복귀시키는 사이클을 반복합니다.
    /// </summary>
    private IEnumerator RepeatRunCyclesForRandomRunner()
    {
        while (true) // 씬이 전환될 때까지 무한 반복
        {
            // 1. 랜덤 캐릭터 선택
            int randomIndex = Random.Range(0, runnersInScene.Count);
            GameObject selectedRunner = runnersInScene[randomIndex];
            Transform runnerTransform = selectedRunner.transform;

            // 2. 원래 위치와 회전 정보를 가져옵니다.
            Vector3 originalPos = originalRunnerPositions[selectedRunner];
            Quaternion originalRot = runnerTransform.rotation;

            // 3. 캐릭터를 활성화하고, 트랙 시작 지점(runnerSpawnPoint)으로 순간 이동시킵니다.
            selectedRunner.SetActive(true);
            runnerTransform.position = runnerSpawnPoint.position;
            runnerTransform.rotation = runnerSpawnPoint.rotation; // 트랙 방향으로 회전

            // 4. 달리기 로직 (StartPoint -> EndPoint)
            float time = 0;
            Vector3 startPos = runnerSpawnPoint.position;
            Vector3 endPos = runnerEndPoint.position;

            while (time < runDuration)
            {
                runnerTransform.position = Vector3.Lerp(startPos, endPos, time / runDuration);
                time += Time.deltaTime;
                yield return null;
            }

            // 5. EndPoint에 정확히 멈춥니다.
            runnerTransform.position = endPos;

            // 6. 캐릭터를 원래 대기 위치로 복귀시키고 비활성화합니다.
            runnerTransform.position = originalPos;
            runnerTransform.rotation = originalRot; // 원래 회전으로 복구
            selectedRunner.SetActive(false);

            // 7. 짧은 대기 후 다음 캐릭터 사이클 시작
            yield return new WaitForSeconds(0.1f);
        }
    }
}