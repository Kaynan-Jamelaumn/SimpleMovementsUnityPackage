using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wraps a worn piece around a character's body (<see cref="ArmorAttachMode.FitToBody"/>): every mesh of the piece, as
/// it is placed right now, becomes a skinned mesh of the character's own skeleton — each vertex takes the bone weights
/// of the closest point of the body (in the body's current pose) and sits <c>offset</c> metres out along its normal.
/// The piece then bends with the body (arms, legs, chest), whatever skeleton or none it was made with.
/// <para>Meshes must be readable (Read/Write in the import settings) in builds; in the editor they always are.</para>
/// </summary>
public static class BodyFitter
{
    private sealed class Body
    {
        public readonly List<Vector3> positions = new List<Vector3>();
        public readonly List<BoneWeight> weights = new List<BoneWeight>();
        public readonly List<Transform> bones = new List<Transform>();
        public readonly Dictionary<Transform, int> boneIndex = new Dictionary<Transform, int>();
        public readonly Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
        public Transform rootBone;
        public const float Cell = 0.05f;

        public int Bone(Transform t)
        {
            if (t == null) return -1;
            if (boneIndex.TryGetValue(t, out int i)) return i;
            i = bones.Count;
            bones.Add(t);
            boneIndex[t] = i;
            return i;
        }

        public static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));

        public void Index()
        {
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3Int k = Key(positions[i]);
                if (!grid.TryGetValue(k, out List<int> list)) grid[k] = list = new List<int>();
                list.Add(i);
            }
        }

        /// <summary>The closest body point (searching outwards ring by ring), or -1 farther than about 0.6 m.</summary>
        public int Nearest(Vector3 p)
        {
            Vector3Int c = Key(p);
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int ring = 0; ring <= 12; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                    for (int y = -ring; y <= ring; y++)
                        for (int z = -ring; z <= ring; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != ring) continue; // the shell of this ring only
                            if (!grid.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out List<int> list)) continue;
                            foreach (int i in list)
                            {
                                float d = (positions[i] - p).sqrMagnitude;
                                if (d < bestSqr) { bestSqr = d; best = i; }
                            }
                        }
                // A point found in ring r can still be beaten by one in ring r + 1: stop one ring after the first hit.
                if (best >= 0 && Mathf.Sqrt(bestSqr) <= ring * Cell)
                    break;
            }
            return best;
        }
    }

    /// <summary>
    /// Skins every mesh of <paramref name="piece"/> to <paramref name="bodies"/>. Returns how many meshes were fitted;
    /// <paramref name="problem"/> says why something could not be (unreadable meshes...).
    /// </summary>
    public static int Fit(GameObject piece, IList<SkinnedMeshRenderer> bodies, float offset, out string problem)
    {
        problem = null;
        if (piece == null || bodies == null || bodies.Count == 0)
        {
            problem = "no body to fit to";
            return 0;
        }
        Body body = BuildBody(bodies, ref problem);
        if (body.positions.Count == 0)
            return 0;
        body.Index();

        int fitted = 0;
        var baked = new Mesh();
        // Plain meshes.
        foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            Mesh source = mf.sharedMesh;
            if (mr == null || source == null) continue;
            if (!Readable(source, ref problem)) continue;
            Mesh m = FitMesh(source, source.vertices, source.normals, mf.transform.localToWorldMatrix, body, offset);
            Material[] mats = mr.sharedMaterials;
            GameObject go = mf.gameObject;
            Object.DestroyImmediate(mr);
            Object.DestroyImmediate(mf);
            MakeSkinned(go, m, mats, body);
            fitted++;
        }
        // Skinned meshes of another skeleton: taken in their current pose, then fitted the same way.
        foreach (SkinnedMeshRenderer smr in piece.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh source = smr.sharedMesh;
            if (source == null || BelongsToBody(smr, body)) continue;
            if (!Readable(source, ref problem)) continue;
            smr.BakeMesh(baked, true);
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Mesh m = FitMesh(source, baked.vertices, baked.normals, toWorld, body, offset);
            // The vertices were taken in world orientation without scale: the renderer's own scale is undone in the bind poses.
            Material[] mats = smr.sharedMaterials;
            GameObject go = smr.gameObject;
            Object.DestroyImmediate(smr);
            var fresh = MakeSkinned(go, m, mats, body);
            // Bind poses were computed from toWorld (no scale): rebuild them with that matrix.
            var bp = new Matrix4x4[body.bones.Count];
            for (int b = 0; b < bp.Length; b++)
                bp[b] = body.bones[b].worldToLocalMatrix * toWorld;
            m.bindposes = bp;
            fresh.sharedMesh = m;
            fitted++;
        }
        Object.DestroyImmediate(baked);
        return fitted;
    }

    private static bool BelongsToBody(SkinnedMeshRenderer smr, Body body)
    {
        Transform[] bones = smr.bones;
        if (bones == null || bones.Length == 0) return false;
        foreach (Transform b in bones)
            if (b != null && !body.boneIndex.ContainsKey(b)) return false;
        return true;
    }

    private static bool Readable(Mesh m, ref string problem)
    {
        if (m.isReadable || Application.isEditor)
            return true;
        problem = $"mesh '{m.name}' is not readable: tick Read/Write in its model's import settings";
        return false;
    }

    private static Body BuildBody(IList<SkinnedMeshRenderer> bodies, ref string problem)
    {
        var body = new Body();
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer smr in bodies)
        {
            if (smr == null || smr.sharedMesh == null || !smr.gameObject.activeInHierarchy) continue;
            Mesh src = smr.sharedMesh;
            if (!Readable(src, ref problem)) continue;
            BoneWeight[] bw = src.boneWeights;
            if (bw == null || bw.Length == 0) continue;
            smr.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            if (v.Length != bw.Length) continue;
            Transform[] bones = smr.bones;
            var remap = new int[bones.Length];
            for (int i = 0; i < bones.Length; i++) remap[i] = body.Bone(bones[i]);
            if (body.rootBone == null) body.rootBone = smr.rootBone;
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            for (int i = 0; i < v.Length; i++)
            {
                body.positions.Add(toWorld.MultiplyPoint3x4(v[i]));
                body.weights.Add(Remap(bw[i], remap));
            }
        }
        Object.DestroyImmediate(baked);
        if (body.positions.Count == 0 && problem == null)
            problem = "the character has no skinned body mesh to fit to";
        return body;
    }

    private static BoneWeight Remap(BoneWeight w, int[] remap)
    {
        int R(int i) => i >= 0 && i < remap.Length && remap[i] >= 0 ? remap[i] : 0;
        return new BoneWeight
        {
            boneIndex0 = R(w.boneIndex0), weight0 = w.weight0,
            boneIndex1 = R(w.boneIndex1), weight1 = w.weight1,
            boneIndex2 = R(w.boneIndex2), weight2 = w.weight2,
            boneIndex3 = R(w.boneIndex3), weight3 = w.weight3,
        };
    }

    private static Mesh FitMesh(Mesh source, Vector3[] vertices, Vector3[] normals, Matrix4x4 toWorld, Body body, float offset)
    {
        bool hasNormals = normals != null && normals.Length == vertices.Length;
        Matrix4x4 toLocal = toWorld.inverse;
        var outV = new Vector3[vertices.Length];
        var outW = new BoneWeight[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
            int near = body.Nearest(world);
            outW[i] = near >= 0 ? body.weights[near] : new BoneWeight { boneIndex0 = Mathf.Max(0, body.Bone(body.rootBone)), weight0 = 1f };
            if (hasNormals && offset != 0f)
                world += toWorld.MultiplyVector(normals[i]).normalized * offset;
            outV[i] = toLocal.MultiplyPoint3x4(world);
        }
        Mesh m = Object.Instantiate(source);
        m.name = source.name + " (fitted)";
        m.hideFlags = HideFlags.DontSave;
        m.vertices = outV;
        m.boneWeights = outW;
        var bindposes = new Matrix4x4[body.bones.Count];
        for (int b = 0; b < bindposes.Length; b++)
            bindposes[b] = body.bones[b].worldToLocalMatrix * toWorld;
        m.bindposes = bindposes;
        // A mirrored piece (the second glove or boot) would be drawn inside out once skinned: turn its triangles.
        if (toWorld.determinant < 0f)
            for (int sub = 0; sub < m.subMeshCount; sub++)
            {
                int[] t = m.GetTriangles(sub);
                for (int i = 0; i + 2 < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
                m.SetTriangles(t, sub);
            }
        m.RecalculateBounds();
        return m;
    }

    private static SkinnedMeshRenderer MakeSkinned(GameObject go, Mesh mesh, Material[] materials, Body body)
    {
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMaterials = materials;
        smr.bones = body.bones.ToArray();
        smr.rootBone = body.rootBone != null ? body.rootBone : body.bones.Count > 0 ? body.bones[0] : null;
        smr.sharedMesh = mesh;
        smr.updateWhenOffscreen = true;
        return smr;
    }
}
