using Unity.VisualScripting;
using UnityEngine;

public class SpinY : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    public enum SpaceMode { Local, World }

    [Header("Spin Settings")]
    [SerializeField] Axis axis = Axis.Y;                        // ȸ�� �� �� Y
    [SerializeField] float degreesPerSecond = 180f;             // �ʴ� ����
    [SerializeField] SpaceMode spaceMode = SpaceMode.Local;     // ����/���� ����
    [SerializeField] bool clockWise = true;                     // �ð�/�ݽð� ����
    [SerializeField] bool randomizeStartAngle = false;          // ���� ���� ����

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
        //if (IsServer)
        //{
        //    Spin();
        //}
    }

    private void Spin()
    {
        float dir = clockWise ? -1f : 1f;           // Unity�� ��������ǥ�� ���� ������ �ð�/�ݽð� ����
        float delta = degreesPerSecond * dir * Time.deltaTime;

        if (spaceMode == SpaceMode.Local)
            transform.Rotate(axisVector, delta, Space.Self);
        else
            transform.Rotate(axisVector, delta, Space.World);
    }
}
