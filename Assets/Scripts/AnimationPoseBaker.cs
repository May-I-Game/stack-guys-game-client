using System.IO;   // 폴더 생성을 위해 필요
using UnityEditor; // 에셋 저장을 위해 필요
using UnityEngine;

// "Editor" 폴더가 *아닌* 곳에 있어야 합니다.
// 애니메이션이 있는 오브젝트(Player_1_body)에 붙여서 사용합니다.

public class AnimationPoseBaker : MonoBehaviour
{
    //[Header("1. 여기에 애니메이션 클립을 넣으세요.")]
    //public AnimationClip clipToBake;

    //[Header("2. 포즈를 가져올 시점 (0=시작, 1=끝)")]
    //[Range(0f, 1f)]
    //public float normalizedTimeToSample = 1.0f; // 1.0 = 클립의 맨 끝

    //// ------------------------------------------------------------------
    //// ★★★ 이게 바로 유저님이 제안하신 확실한 방법입니다 ★★★
    //// ------------------------------------------------------------------
    //[ContextMenu("★ (추천) 이 포즈로 '스켈레톤' 복제 (Duplicate Skeleton Pose)")]
    //private void DuplicatePosedSkeleton()
    //{
    //    if (clipToBake == null)
    //    {
    //        Debug.LogError("먼저 'Clip To Bake' 슬롯에 애니메이션 클립을 할당해주세요.");
    //        return;
    //    }

    //    // 1. 이 오브젝트에 'clipToBake'의 포즈를 강제로 적용합니다.
    //    // (Bip... 본들의 Transform이 이 순간 변경됩니다)
    //    clipToBake.SampleAnimation(gameObject, clipToBake.length * normalizedTimeToSample);

    //    // 2. 현재 포즈 상태 그대로 오브젝트를 즉시 복제합니다.
    //    GameObject duplicate = Instantiate(gameObject, transform.position, transform.rotation);
    //    duplicate.name = gameObject.name + "_" + clipToBake.name + "_Posed";

    //    // 3. 복제된 오브젝트에서 Animator 컴포넌트를 제거합니다.
    //    // (이걸 안하면 다시 T-Pose로 돌아갑니다)
    //    Animator anim = duplicate.GetComponent<Animator>();
    //    if (anim != null)
    //    {
    //        // 에디터 스크립트에서는 DestroyImmediate를 사용해야 합니다.
    //        DestroyImmediate(anim);
    //        Debug.Log($"[Duplicate Pose] '{duplicate.name}'에서 Animator 컴포넌트를 제거했습니다.");
    //    }

    //    // 4. 복제된 오브젝트에서 이 도우미 스크립트도 제거합니다.
    //    AnimationPoseBaker baker = duplicate.GetComponent<AnimationPoseBaker>();
    //    if (baker != null)
    //    {
    //        DestroyImmediate(baker);
    //    }

    //    Debug.Log($"[Duplicate Pose] ★★★ 성공! '{duplicate.name}'이(가) 생성되었습니다. 포즈가 완벽히 고정되었습니다.");
    //    Selection.activeGameObject = duplicate; // 새로 만든 오브젝트를 자동 선택
    //}

    //// ------------------------------------------------------------------
    //// (대안) '스태틱 메시'로 굽는 이전 방식 (MeshFilter 사용)
    //// ------------------------------------------------------------------
    //[ContextMenu("★ (대안) 이 포즈로 '스태틱 메시' 굽기 (Bake to Static Mesh)")]
    //private void BakePoseToStaticMesh()
    //{
    //    if (clipToBake == null)
    //    {
    //        Debug.LogError("먼저 'Clip To Bake' 슬롯에 애니메이션 클립을 할당해주세요.");
    //        return;
    //    }

    //    // 1. 이 오브젝트에 'clipToBake'의 포즈를 강제로 적용합니다.
    //    clipToBake.SampleAnimation(gameObject, clipToBake.length * normalizedTimeToSample);
    //    Debug.Log($"[Bake Mesh] '{clipToBake.name}' 클립 포즈를 샘플링했습니다.");

    //    // --- 2. 스태틱 메시로 굽기 (월드 공간 로직) ---
    //    SkinnedMeshRenderer[] allRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
    //    if (allRenderers.Length == 0)
    //    {
    //        Debug.LogError("하위 오브젝트에서 SkinnedMeshRenderer를 찾을 수 없습니다.");
    //        return;
    //    }

    //    GameObject bakedRoot = new GameObject(gameObject.name + "_" + clipToBake.name + "_BakedMesh");
    //    bakedRoot.transform.position = Vector3.zero;
    //    bakedRoot.transform.rotation = Quaternion.identity;
    //    bakedRoot.transform.localScale = Vector3.one;

    //    // --- 에셋 저장 폴더 준비 ---
    //    string rootAssetPath = $"Assets/BakedMeshes";
    //    string modelAssetFolder = $"{gameObject.name}_{clipToBake.name}_BakedMeshes";
    //    string assetFolderPath = Path.Combine(rootAssetPath, modelAssetFolder);

    //    if (!Directory.Exists(Application.dataPath + "/BakedMeshes")) AssetDatabase.CreateFolder("Assets", "BakedMeshes");
    //    if (!Directory.Exists(Application.dataPath + "/BakedMeshes/" + modelAssetFolder)) AssetDatabase.CreateFolder(rootAssetPath, modelAssetFolder);

    //    Debug.Log($"[Bake Mesh] {allRenderers.Length}개의 메시를 굽습니다...");

    //    foreach (SkinnedMeshRenderer skin in allRenderers)
    //    {
    //        Mesh newMesh = new Mesh();
    //        skin.BakeMesh(newMesh); // 월드 공간 기준으로 굽기

    //        GameObject partObject = new GameObject(skin.gameObject.name + "_Baked");
    //        partObject.transform.position = Vector3.zero;
    //        partObject.transform.rotation = Quaternion.identity;
    //        partObject.transform.localScale = Vector3.one;

    //        MeshFilter meshFilter = partObject.AddComponent<MeshFilter>();
    //        MeshRenderer meshRenderer = partObject.AddComponent<MeshRenderer>();
    //        meshFilter.sharedMesh = newMesh;
    //        meshRenderer.sharedMaterials = skin.sharedMaterials;

    //        partObject.transform.SetParent(bakedRoot.transform, false);

    //        string meshName = $"{partObject.name}_{newMesh.GetInstanceID()}.asset";
    //        string assetPath = Path.Combine(assetFolderPath, meshName);
    //        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
    //        AssetDatabase.CreateAsset(newMesh, assetPath);
    //    }

    //    AssetDatabase.SaveAssets();

    //    bakedRoot.transform.position = transform.position;
    //    bakedRoot.transform.rotation = transform.rotation;

    //    Debug.Log($"[Bake Mesh] ★★★ 성공! '{bakedRoot.name}'이 생성되었습니다. (스태틱 메시)");
    //    Selection.activeGameObject = bakedRoot;
    //}
}