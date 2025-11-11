using System.Collections.Generic;
using UnityEngine;

public class MapObjectInfo : MonoBehaviour
{
    // 이 오브젝트가 어떤 프리팹으로부터 생성되었는지 식별하는 ID (프리팹 이름)
    public string objectId;
}

[System.Serializable]
public class MapObjectData
{
    public string objectId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

[System.Serializable]
public class MapData
{
    public string mapName;
    public List<MapObjectData> objects = new List<MapObjectData>();
    public Vector3 startPos;
}
