using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkProximityManager의 실시간 통계를 화면에 표시하는 디버그 UI
/// </summary>
public class ProximityDebugUI : MonoBehaviour
{
        [Header("Settings")]
        [Tooltip("디버그 UI 표시")]
        [SerializeField] private bool showDebugUI = true;

        [Tooltip("UI 업데이트 주기 (초)")]
        [SerializeField] private float updateInterval = 0.5f;

        [Tooltip("UI 위치 (0-1 정규화)")]
        [SerializeField] private Vector2 uiPosition = new Vector2(0.01f, 0.01f);

        [Tooltip("UI 크기")]
        [SerializeField] private Vector2 uiSize = new Vector2(400, 200);

        private NetworkProximityManager proximityManager;
        private NetworkManager networkManager;
        private float nextUpdateTime;
        private string cachedStats = "";
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        private void Start()
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogWarning("[ProximityDebugUI] NetworkManager not found");
                enabled = false;
                return;
            }

            // ProximityManager 찾기
            proximityManager = FindAnyObjectByType<NetworkProximityManager>();
            if (proximityManager == null)
            {
                Debug.LogWarning("[ProximityDebugUI] NetworkProximityManager not found - UI disabled");
                enabled = false;
            }
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            // 박스 스타일
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.7f));
            boxStyle.alignment = TextAnchor.UpperLeft;
            boxStyle.padding = new RectOffset(10, 10, 10, 10);

            // 라벨 스타일
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;
            labelStyle.wordWrap = false;
            labelStyle.richText = true;

            stylesInitialized = true;
        }

        private void Update()
        {
            if (!showDebugUI || proximityManager == null) return;
            if (!networkManager.IsServer) return;

            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + updateInterval;
                UpdateStats();
            }
        }

        private void UpdateStats()
        {
            if (proximityManager == null) return;

            // Proximity Manager 통계
            string proximityStats = proximityManager.GetStats();

            // 추가 네트워크 통계
            int totalPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length;
            int connectedClients = networkManager.ConnectedClientsIds.Count;

            cachedStats = $"<b>=== Network Proximity Stats ===</b>\n" +
                          $"<color=yellow>Time:</color> {System.DateTime.Now:HH:mm:ss}\n" +
                          $"\n" +
                          $"<color=cyan><b>Server Info</b></color>\n" +
                          $"Connected Clients: {connectedClients}\n" +
                          $"Total Players: {totalPlayers}\n" +
                          $"\n" +
                          $"<color=cyan><b>Visibility Optimization</b></color>\n" +
                          $"{proximityStats}\n" +
                          $"\n" +
                          $"<color=lime>Press F3 to toggle this UI</color>";
        }

        private void OnGUI()
        {
            if (!showDebugUI || !networkManager.IsServer) return;
            if (proximityManager == null) return;

            InitializeStyles();

            // 키 입력 처리
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F3)
            {
                showDebugUI = !showDebugUI;
            }

            if (!showDebugUI) return;

            // UI 위치 계산
            float x = Screen.width * uiPosition.x;
            float y = Screen.height * uiPosition.y;
            Rect rect = new Rect(x, y, uiSize.x, uiSize.y);

            // 배경 박스
            GUI.Box(rect, "", boxStyle);

            // 텍스트 영역
            Rect labelRect = new Rect(rect.x + 10, rect.y + 10, rect.width - 20, rect.height - 20);
            GUI.Label(labelRect, cachedStats, labelStyle);

            // 실시간 가시성 정보 (클라이언트별)
            if (GUI.Button(new Rect(rect.x, rect.y + rect.height + 5, rect.width, 25), "Show Detailed Stats"))
            {
                ShowDetailedStats();
            }
        }

        private void ShowDetailedStats()
        {
            if (proximityManager == null) return;

            Debug.Log("=== Detailed Proximity Stats ===");
            foreach (var clientId in networkManager.ConnectedClientsIds)
            {
                int visibleCount = proximityManager.GetVisibleObjectCount(clientId);
                Debug.Log($"Client {clientId}: {visibleCount} visible objects");
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        /// <summary>
        /// 외부에서 UI 토글
        /// </summary>
        public void ToggleUI()
        {
            showDebugUI = !showDebugUI;
        }

        /// <summary>
        /// 외부에서 UI 활성화 설정
        /// </summary>
        public void SetUIEnabled(bool enabled)
        {
            showDebugUI = enabled;
        }
}
