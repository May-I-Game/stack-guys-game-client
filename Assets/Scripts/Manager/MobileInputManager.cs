using UnityEngine;
using UnityEngine.UI;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance; //싱글톤

    public VariableJoystick joystick;
    public Button jumpButton;
    public Button grabButton;

    void Awake()
    {
        //싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

}
