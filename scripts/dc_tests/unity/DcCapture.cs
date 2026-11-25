#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.U2D.Animation;
using UnityEngine.Rendering.Universal;

// Test only (2026-11-24): Dragon Crashers' knight skeleton playing its own attack clip, as shipped,
// then the same skeleton and clip with the other characters' Sprite Libraries (the clothes).
public class DcCapture : MonoBehaviour
{
    public string status = "starting";
    public int cols = 10, cellW = 640, cellH = 400;
    Texture2D sheet;
    RenderTexture live;

    IEnumerator Start()
    {
        Application.runInBackground = true;
        string baseDir = "Assets/Test2D/dc/";
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(baseDir + "Art/Characters/_Prefabs/PV_Character_Knight.prefab");
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(baseDir + "Animations/Characters/AnimClips/Knight/Anim_Character_Knight_Attack.anim");
        string[] lookNames = { "Knight", "Witch", "Wolfman", "Skeleton" };
        var looks = new SpriteLibraryAsset[lookNames.Length];
        for (int i = 0; i < lookNames.Length; i++)
            looks[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(baseDir + "Art/Characters/" + lookNames[i] + "/SpriteLibrary_Character_" + lookNames[i] + ".spriteLib");

        var lightGo = new GameObject("GlobalLight");
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        var hero = Instantiate(prefab, new Vector3(2000f, 2000f, 0f), Quaternion.identity);
        var animator = hero.GetComponentInChildren<Animator>();
        animator.runtimeAnimatorController = null;
        var lib = hero.GetComponentInChildren<SpriteLibrary>();
        var resolvers = hero.GetComponentsInChildren<SpriteResolver>(true);
        var cats = new string[resolvers.Length]; var labs = new string[resolvers.Length];
        for (int i = 0; i < resolvers.Length; i++) { cats[i] = resolvers[i].GetCategory(); labs[i] = resolvers[i].GetLabel(); }
        int mapped = 0;
        for (int i = 0; i < resolvers.Length; i++)
        {
            if (!string.IsNullOrEmpty(cats[i])) { mapped++; continue; }
            var cur = resolvers[i].GetComponent<SpriteRenderer>().sprite;
            foreach (var cat in looks[0].GetCategoryNames())
                foreach (var lab in looks[0].GetCategoryLabelNames(cat))
                    if (cur != null && looks[0].GetSprite(cat, lab) == cur) { cats[i] = cat; labs[i] = lab; }
            if (!string.IsNullOrEmpty(cats[i])) mapped++;
        }

        var camGo = new GameObject("DcCam");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.36f, 0.42f, 0.40f);
        var rt = new RenderTexture(cellW, cellH, 24);
        cam.targetTexture = rt;

        yield return null;
        yield return null;
        var b = new Bounds(hero.transform.position, Vector3.zero);
        bool first = true;
        foreach (var sr in hero.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.enabled || sr.sprite == null) continue;
            if (first) { b = sr.bounds; first = false; } else b.Encapsulate(sr.bounds);
        }
        cam.orthographicSize = Mathf.Max(0.5f, b.size.y * 0.85f);
        cam.transform.position = new Vector3(b.center.x, b.center.y, -10f);

        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Build", "dc_test");
        Directory.CreateDirectory(dir);
        Time.captureFramerate = 60;
        int total = Mathf.CeilToInt(clip.length * 60f);
        sheet = new Texture2D(cellW * cols, cellH * looks.Length, TextureFormat.RGB24, false);
        var frame = new Texture2D(cellW, cellH, TextureFormat.RGB24, false);
        string info = "resolvers " + resolvers.Length + " mapped " + mapped + " " + "bounds " + b.size + " clip " + clip.length + "s frames " + total + "\n";
        for (int l = 0; l < looks.Length; l++)
        {
            if (looks[l] == null) { info += lookNames[l] + ": library missing\n"; continue; }
            lib.spriteLibraryAsset = looks[l];
            for (int i = 0; i < resolvers.Length; i++) { if (!string.IsNullOrEmpty(cats[i])) resolvers[i].SetCategoryAndLabel(cats[i], labs[i]); resolvers[i].ResolveSpriteToSpriteRenderer(); }
            PlayableGraph graph;
            AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
            int nextCol = 0;
            for (int f = 0; f <= total; f++)
            {
                status = lookNames[l] + " frame " + f + "/" + total;
                yield return new WaitForEndOfFrame();
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, cellW, cellH), 0, 0);
                frame.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, lookNames[l] + "_" + f.ToString("000") + ".png"), frame.EncodeToPNG());
                int target = Mathf.RoundToInt(nextCol * total / (float)(cols - 1));
                if (nextCol < cols && f >= target)
                {
                    sheet.SetPixels(nextCol * cellW, (looks.Length - 1 - l) * cellH, cellW, cellH, frame.GetPixels());
                    nextCol++;
                }
            }
            graph.Destroy();
            int nullSprites = 0;
            foreach (var sr in hero.GetComponentsInChildren<SpriteRenderer>()) if (sr.sprite == null) nullSprites++;
            info += lookNames[l] + ": ok, renderers without sprite " + nullSprites + "\n";
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(dir, "sheet.png"), sheet.EncodeToPNG());
        File.WriteAllText(Path.Combine(dir, "info.txt"), info);
        Time.captureFramerate = 0;
        // Live view: the four looks side by side on the same skeleton, looping the same attack at game speed.
        Destroy(hero);
        var anims = new Animator[looks.Length];
        for (int i = 0; i < looks.Length; i++)
        {
            var h = Instantiate(prefab, new Vector3(2000f + i * 7f, 2000f, 0f), Quaternion.identity);
            anims[i] = h.GetComponentInChildren<Animator>();
            anims[i].runtimeAnimatorController = null;
            h.GetComponentInChildren<SpriteLibrary>().spriteLibraryAsset = looks[i];
            var rs = h.GetComponentsInChildren<SpriteResolver>(true);
            for (int j = 0; j < rs.Length && j < cats.Length; j++) { if (!string.IsNullOrEmpty(cats[j])) rs[j].SetCategoryAndLabel(cats[j], labs[j]); rs[j].ResolveSpriteToSpriteRenderer(); }
        }
        live = new RenderTexture(1920, 540, 24);
        cam.targetTexture = live;
        cam.transform.position = new Vector3(b.center.x + 10.5f, b.center.y, -10f);
        status = "done";
        for (int loop = 0; loop < 100000; loop++)
        {
            var graphs = new PlayableGraph[anims.Length];
            for (int i = 0; i < anims.Length; i++) AnimationPlayableUtilities.PlayClip(anims[i], clip, out graphs[i]);
            yield return new WaitForSeconds(clip.length + 0.8f);
            for (int i = 0; i < graphs.Length; i++) graphs[i].Destroy();
        }
    }

    void OnGUI()
    {
        if (status == "done" && live != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), live, ScaleMode.ScaleToFit);
        }
        else GUI.Label(new Rect(10, 10, 600, 30), "DcCapture: " + status);
    }
}
#endif
