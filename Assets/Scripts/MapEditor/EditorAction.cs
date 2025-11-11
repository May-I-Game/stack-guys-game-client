using UnityEngine;

public interface IEditorAction
{
    void Execute(); // 행동 실행 (또는 Redo)
    void Undo();    // 행동 취소
    void Cleanup(); // 스택에서 제거될 때 호출될 정리 메서드
}


// 오브젝트 배치 액션
public class PlaceObjectAction : IEditorAction
{
    private GameObject prefab;
    private Vector3 position;
    private Quaternion rotation;
    // 이 액션으로 생성된 오브젝트
    private GameObject instantiatedObject;

    public PlaceObjectAction(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        this.prefab = prefab;
        this.position = pos;
        this.rotation = rot;
    }

    public void Execute()
    {
        if (instantiatedObject == null)
        {
            // 처음 실행될 때 (Instantiate)
            instantiatedObject = GameObject.Instantiate(prefab, position, rotation);
            instantiatedObject.AddComponent<MapObjectInfo>().objectId = this.prefab.name;
        }
        else
        {
            // Redo일 때 (비활성화된 것을 다시 활성화)
            instantiatedObject.SetActive(true);
        }
    }

    public void Undo()
    {
        // 되돌리기 (활성화된 것을 비활성화)
        if (instantiatedObject != null)
        {
            instantiatedObject.SetActive(false);
        }
    }

    public void Cleanup()
    {
        // Undo에 의해 비활성화된 상태(activeSelf == false)일 때만 파괴
        if (instantiatedObject != null && !instantiatedObject.activeSelf)
        {
            GameObject.Destroy(instantiatedObject);
        }
    }
}

// 오브젝트 삭제 액션
public class DeleteObjectAction : IEditorAction
{
    private GameObject objectToDelete;

    public DeleteObjectAction(GameObject obj)
    {
        this.objectToDelete = obj;
    }

    public void Execute()
    {
        // 행동 실행 (삭제 = 비활성화)
        // 실제 삭제는 redo 스택이 비워질때 이뤄짐
        if (objectToDelete != null)
        {
            objectToDelete.SetActive(false);
        }
    }

    public void Undo()
    {
        // 행동 취소 (삭제 취소 = 다시 활성화)
        if (objectToDelete != null)
        {
            objectToDelete.SetActive(true);
        }
    }

    public void Cleanup()
    {
        // 이 액션은 참조만 하므로, 여기서 오브젝트를 파괴하면 안 됩니다.
    }
}