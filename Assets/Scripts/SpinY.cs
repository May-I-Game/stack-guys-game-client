using Unity.VisualScripting;
using UnityEngine;

public class SpinY : MonoBehaviour
{
    public enum Axis { X, Y, Z}
    public enum SpaceMode { Local, World }

    [Header("Spin Settings")]
    [SerializeField] Axis axis = Axis.Y;                        // 회전 할 축 Y
    [SerializeField] float degreesPerSecond = 180f;             // 초당 각도
    [SerializeField] SpaceMode spaceMode = SpaceMode.Local;     // 로컬/월드 기준
    [SerializeField] bool clockWise = true;                     // 시계/반시계 기준
    [SerializeField] bool randomizeStartAngle = false;          // 시작 각도 랜덤

    Vector3 axisVector;

    private void Awake()
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


    // Update is called once per frame
    void Update()
    {
        float dir = clockWise ? -1f : 1f;           // Unity의 오른손좌표계 기준 감각상 시계/반시계 보정
        float delta = degreesPerSecond * dir * Time.deltaTime;

        if (spaceMode == SpaceMode.Local)
            transform.Rotate(axisVector, delta, Space.Self);
        else
            transform.Rotate(axisVector, delta, Space.World);
    }
}
