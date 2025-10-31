using UnityEngine;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    [Header("Character Settings")]
    [SerializeField] private GameObject[] characterPrefabs; // 캐릭터 프리팹 배열
    [SerializeField] private Transform spawnPoint; // 캐릭터 생성 위치

    [Header("UI References")]
    [SerializeField] private RawImage characterDisplay; // 캐릭터 보여주는 RawImage
    [SerializeField] private Button leftButton; // 이전 캐릭터 버튼 (선택사항)
    [SerializeField] private Button rightButton; // 다음 캐릭터 버튼 (선택사항)
    [SerializeField] private Text characterNameText; // 캐릭터 이름 (선택사항)

    private int currentCharacterIndex = 0;
    private GameObject currentCharacterInstance;

    void Start()
    {
        // 첫 번째 캐릭터 생성
        SpawnCharacter(currentCharacterIndex);

        // RawImage 클릭 이벤트 추가
        characterDisplay.GetComponent<Button>().onClick.AddListener(NextCharacter);

        // 화살표 버튼 이벤트 (있으면)
        if (leftButton != null)
            leftButton.onClick.AddListener(PreviousCharacter);

        if (rightButton != null)
            rightButton.onClick.AddListener(NextCharacter);

        UpdateUI();
    }

    // 다음 캐릭터
    public void NextCharacter()
    {
        currentCharacterIndex = (currentCharacterIndex + 1) % characterPrefabs.Length;
        SpawnCharacter(currentCharacterIndex);
        UpdateUI();
    }

    // 이전 캐릭터
    public void PreviousCharacter()
    {
        currentCharacterIndex--;
        if (currentCharacterIndex < 0)
            currentCharacterIndex = characterPrefabs.Length - 1;

        SpawnCharacter(currentCharacterIndex);
        UpdateUI();
    }

    // 캐릭터 생성
    void SpawnCharacter(int index)
    {
        // 기존 캐릭터 삭제
        if (currentCharacterInstance != null)
        {
            Destroy(currentCharacterInstance);
        }

        // 새 캐릭터 생성
        currentCharacterInstance = Instantiate(characterPrefabs[index], spawnPoint.position, spawnPoint.rotation);
        currentCharacterInstance.transform.SetParent(spawnPoint);

        // 애니메이션 자동 재생
        Animator animator = currentCharacterInstance.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("Run", true);
        }
    }

    // UI 업데이트
    void UpdateUI()
    {
        if (characterNameText != null)
        {
            characterNameText.text = characterPrefabs[currentCharacterIndex].name;
        }
    }

    // 선택된 캐릭터 인덱스 가져오기
    public int GetSelectedCharacterIndex()
    {
        return currentCharacterIndex;
    }

    // 선택된 캐릭터 프리팹 가져오기
    public GameObject GetSelectedCharacterPrefab()
    {
        return characterPrefabs[currentCharacterIndex];
    }
}