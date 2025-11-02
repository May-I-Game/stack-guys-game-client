using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NameTag : MonoBehaviour
{
    [SerializeField]
    TMP_Text label;

    [SerializeField]
    Vector3 worldOffset = new Vector3(0, 2.0f, 0);

    private void Awake()
    {
        string playerName = PlayerPrefs.GetString("player_name", "Player");

        if (label)
            label.text = playerName;
    }

    private void LateUpdate()
    {
        // 항상 카메라를 보게 함
        if (Camera.main && label != null)
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
    }
}
