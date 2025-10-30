using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LoginUI : MonoBehaviour
{
    [SerializeField]
    TMP_InputField nameInput;

    public void OnClickStart()
    {
        var name = (nameInput?.text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
            return;

        PlayerPrefs.SetString("player_name", name);
        PlayerPrefs.Save();
        SceneManager.LoadScene("DoHoon");
    }
}
