// Assets/Editor/WholePrefabSkinnedBaker.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class WholePrefabSkinnedBaker : EditorWindow
{
    public GameObject sourcePrefab;
    public string outputPrefabName = "Corpse_Static";
    public string meshSaveFolder = "Assets/BakedMeshes";
    public bool combineByMaterial = false; // 같은 머티리얼끼리 메쉬 결합(선택)

    [MenuItem("Tools/Corpse/Bake Selected Prefab (Skinned→Static)")]
    static void Open() => GetWindow<WholePrefabSkinnedBaker>("Skinned→Static Baker");

    void OnGUI()
    {
        sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", sourcePrefab, typeof(GameObject), false);
        outputPrefabName = EditorGUILayout.TextField("Output Prefab Name", outputPrefabName);
        meshSaveFolder = EditorGUILayout.TextField("Mesh Save Folder", meshSaveFolder);
        combineByMaterial = EditorGUILayout.ToggleLeft("Combine baked meshes by material (optional)", combineByMaterial);

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake Now"))
        {
            if (!sourcePrefab)
            {
                EditorUtility.DisplayDialog("Error", "프리팹을 지정하세요.", "OK");
                return;
            }
            Bake();
        }
    }

    void Bake()
    {
        Directory.CreateDirectory(meshSaveFolder);

        // 1) 프리팹 임시 인스턴스
        var inst = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
        if (!inst) { EditorUtility.DisplayDialog("Error", "인스턴스 생성 실패", "OK"); return; }

        // 2) 결과 루트 생성
        var bakedRoot = new GameObject(sourcePrefab.name + "_Static");
        bakedRoot.transform.SetPositionAndRotation(inst.transform.position, inst.transform.rotation);
        bakedRoot.transform.localScale = inst.transform.localScale;

        var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs.Length == 0)
        {
            Object.DestroyImmediate(inst);
            Object.DestroyImmediate(bakedRoot);
            EditorUtility.DisplayDialog("Info", "SkinnedMeshRenderer가 없습니다.", "OK");
            return;
        }

        // 파트별로 MeshRenderer 생성 + Mesh 저장
        var createdRenderers = new List<MeshRenderer>();
        foreach (var smr in smrs)
        {
            if (!smr || !smr.sharedMesh) continue;

            // 현재 포즈 베이크
            var bakedMesh = new Mesh { name = smr.sharedMesh.name + "_Baked" };
            smr.BakeMesh(bakedMesh, true);
            bakedMesh.RecalculateBounds();

            // Mesh를 에셋으로 저장
            var meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshSaveFolder}/{smr.name}_Baked.asset");
            AssetDatabase.CreateAsset(bakedMesh, meshPath);

            // 동일 경로 재현
            var parent = EnsurePath(bakedRoot.transform, inst.transform, smr.transform.parent);

            // 정적 렌더러 생성
            var go = new GameObject(smr.name + "_Baked");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = smr.transform.localPosition;
            go.transform.localRotation = smr.transform.localRotation;
            go.transform.localScale = smr.transform.localScale;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            mr.sharedMaterials = smr.sharedMaterials;

            // 인스턴싱 권장
            foreach (var m in mr.sharedMaterials) if (m) m.enableInstancing = true;

            createdRenderers.Add(mr);
        }

        var staticMrs = inst.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mrSrc in staticMrs)
        {
            // SkinnedMeshRenderer가 달려있는 애는 이미 위에서 처리했으니 스킵
            if (mrSrc.GetComponent<SkinnedMeshRenderer>()) continue;

            var mfSrc = mrSrc.GetComponent<MeshFilter>();
            if (!mfSrc || !mfSrc.sharedMesh) continue;   // 메쉬가 없으면 스킵

            // 원본 부모 경로/로컬 변환 유지
            var parent = EnsurePath(bakedRoot.transform, inst.transform, mrSrc.transform.parent);

            var go = new GameObject(mrSrc.name + "_StaticCopy");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = mrSrc.transform.localPosition;
            go.transform.localRotation = mrSrc.transform.localRotation;
            go.transform.localScale = mrSrc.transform.localScale;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mfSrc.sharedMesh;                 // 원본 메쉬 자산 재사용
            mr.sharedMaterials = mrSrc.sharedMaterials;       // 머티리얼 그대로

            foreach (var m in mr.sharedMaterials) if (m) m.enableInstancing = true;

            createdRenderers.Add(mr); // (선택) combineByMaterial 사용 시 결합 대상에 포함
        }

        // (선택) 같은 머티리얼끼리 결합하여 드로우콜 감소
        if (combineByMaterial)
            CombineByMaterial(bakedRoot, createdRenderers);

        // 보기 전용으로 경량화(원하면 주석처리)
        StripComponents(bakedRoot);

        // 3) 프리팹 저장
        var prefabPath = EditorUtility.SaveFilePanelInProject(
            "Save Static Prefab",
            $"{(string.IsNullOrEmpty(outputPrefabName) ? "Corpse_Static" : outputPrefabName)}.prefab",
            "prefab",
            "저장 경로를 선택하세요."
        );
        if (!string.IsNullOrEmpty(prefabPath))
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(bakedRoot, prefabPath);
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[Baker] Saved prefab: {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        Object.DestroyImmediate(inst);
        Object.DestroyImmediate(bakedRoot);
    }

    // 'srcRoot' 기준으로 'srcParent'까지의 경로를 bakedRoot 아래에 재현하면서
    // 각 노드의 localPosition/Rotation/Scale을 그대로 복사해야 함.
    static Transform EnsurePath(Transform bakedRoot, Transform srcRoot, Transform srcParent)
    {
        if (srcParent == null || srcParent == srcRoot) return bakedRoot;

        var stack = new Stack<Transform>();
        for (var t = srcParent; t && t != srcRoot; t = t.parent) stack.Push(t);

        var curBaked = bakedRoot;
        var curSrc = srcRoot;

        while (stack.Count > 0)
        {
            var srcNode = stack.Pop();                 // 원본 부모 노드
            var child = curBaked.Find(srcNode.name); // 이미 생성됐는지 확인

            if (!child)
            {
                var go = new GameObject(srcNode.name);
                child = go.transform;
                child.SetParent(curBaked, false);
            }

            // 🔥 핵심: 원본 부모 노드의 "로컬" 변환을 그대로 복사
            child.localPosition = srcNode.localPosition;
            child.localRotation = srcNode.localRotation;
            child.localScale = srcNode.localScale;

            // 다음 단계로 진행
            curBaked = child;
            curSrc = srcNode;
        }
        return curBaked;
    }

    // 같은 머티리얼끼리 합치기(정지 오브젝트 전용)
    static void CombineByMaterial(GameObject root, List<MeshRenderer> parts)
    {
        var groups = new Dictionary<Material, List<MeshFilter>>();
        foreach (var mr in parts)
        {
            var mfPart = mr.GetComponent<MeshFilter>();
            if (!mfPart || !mfPart.sharedMesh) continue;

            // 서브메시가 1개인 경우만 안전하게 결합
            if (mfPart.sharedMesh.subMeshCount != 1) continue;

            var mat = mr.sharedMaterial;
            if (!groups.ContainsKey(mat)) groups[mat] = new List<MeshFilter>();
            groups[mat].Add(mfPart);

            // 개별 렌더러는 일단 비활성
            mr.enabled = false;
        }

        foreach (var kv in groups)
        {
            var mat = kv.Key;
            var list = kv.Value;
            if (list.Count < 2)
            {
                // 하나뿐이면 다시 활성화
                foreach (var f in list) f.GetComponent<MeshRenderer>().enabled = true;
                continue;
            }

            var combines = new CombineInstance[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                combines[i].mesh = list[i].sharedMesh;
                combines[i].transform = list[i].transform.localToWorldMatrix;
            }

            var combined = new Mesh { name = $"Combined_{(mat ? mat.name : "NoMat")}" };
            combined.CombineMeshes(combines, true, true);
            combined.RecalculateBounds();

            // 메시 에셋 저장
            var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/BakedMeshes/Combined_{(mat ? mat.name : "NoMat")}.asset");
            AssetDatabase.CreateAsset(combined, path);

            // 배치용 GO
            var go = new GameObject($"Combined_{(mat ? mat.name : "NoMat")}");
            go.transform.SetParent(root.transform, false);

            var mfCombined = go.AddComponent<MeshFilter>();
            var mrCombined = go.AddComponent<MeshRenderer>();
            mfCombined.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mrCombined.sharedMaterial = mat;
            if (mat) mat.enableInstancing = true;
        }
    }

    static string GetFolder(GameObject root)
    {
        var path = AssetDatabase.GetAssetPath(root);
        if (string.IsNullOrEmpty(path)) return "Assets/BakedMeshes";
        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        return string.IsNullOrEmpty(dir) ? "Assets/BakedMeshes" : dir;
    }

    // 보기 전용 경량화
    static void StripComponents(GameObject root)
    {
        foreach (var a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Object.DestroyImmediate(s);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var col in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
    }
}
#endif
