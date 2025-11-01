using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class SpinY : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    public enum SpaceMode { Local, World }

    [Header("Spin Settings")]
    [SerializeField] Axis axis = Axis.Y;
    [SerializeField] float degreesPerSecond = 180f;
    [SerializeField] SpaceMode spaceMode = SpaceMode.Local;
    [SerializeField] bool clockWise = true;
    [SerializeField] bool randomizeStartAngle = false;

    Vector3 axisVector;

    private void Start()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Init();
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsServer)
        {
            Spin();
        }
    }

    private void Init()
    {
        axisVector = axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            Axis.Z => Vector3.forward,
            _ => Vector3.up
        };

        if (randomizeStartAngle)
        {
            float startAngle = Random.Range(0f, 360f);
            if (spaceMode == SpaceMode.Local)
                transform.Rotate(axisVector, startAngle, Space.Self);
            else
                transform.Rotate(axisVector, startAngle, Space.World);
        }
    }

    private void Spin()
    {
        float dir = clockWise ? -1f : 1f;
        float delta = degreesPerSecond * dir * Time.deltaTime;

        if (spaceMode == SpaceMode.Local)
            transform.Rotate(axisVector, delta, Space.Self);
        else
            transform.Rotate(axisVector, delta, Space.World);
    }
}
