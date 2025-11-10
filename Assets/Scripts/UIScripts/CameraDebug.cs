using UnityEngine;

public class CameraDebug : MonoBehaviour
{
    void Start()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>(true); // 비활성 카메라도 포함
        Debug.Log($"총 카메라 수: {allCameras.Length}");

        foreach (Camera cam in allCameras)
        {
            Debug.Log($"카메라: {cam.name}, " +
                      $"활성화: {cam.gameObject.activeInHierarchy}, " +
                      $"Enable: {cam.enabled}, " +
                      $"Target Display: {cam.targetDisplay}, " +
                      $"Target Texture: {cam.targetTexture}");
        }
    }
}
