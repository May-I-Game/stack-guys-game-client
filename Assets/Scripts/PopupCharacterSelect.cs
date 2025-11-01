using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;
public class PopupCharacterSelect : MonoBehaviour
{
    [Header("References")]
    public Transform characterGrid; // Grid Layout Group이 있는 Transform
    public Camera characterCamera; // 이동할 카메라

    private int currentSelectedIndex = 0;
    [SerializeField] private List<GameObject> characterPrefab = new List<GameObject>();

    void OnEnable()
    {
        // 팝업이 켜질 때마다 버튼 이벤트 설정
        SetupCharacterButtons();
    }

    void SetupCharacterButtons()
    {
        if (characterGrid == null || characterCamera == null)
        {
            Debug.LogError("CharacterGrid 또는 CharacterCamera가 할당되지 않았습니다!");
            return;
        }

        // Grid의 모든 자식에서 Button 컴포넌트 찾기
        Button[] buttons = characterGrid.GetComponentsInChildren<Button>();

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i; // 클로저 문제 방지 (중요!)

            // 기존 이벤트 제거 후 새로 추가
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => OnCharacterSelected(index));
        }

    }

    void OnCharacterSelected(int index)
    {
        currentSelectedIndex = index;

        // 카메라 X 위치 변경: -2 * index
        characterCamera.transform.localPosition = new Vector3(-2f * index, 0, 0);

        // PlayerPrefs에 선택한 캐릭터 저장
        PlayerPrefs.SetInt("selected_character", index);
        PlayerPrefs.Save();

        // 클라이언트 전용 모드 호환성 유지
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = characterPrefab[index];
        }

        // 팝업 닫기
        gameObject.SetActive(false);
    }
}
