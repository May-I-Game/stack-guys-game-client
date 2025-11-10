using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

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
