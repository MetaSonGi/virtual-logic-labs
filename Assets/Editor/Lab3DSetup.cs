// 시작 화면·3D 실험실 장면과 재질을 만들고 빌드 장면 순서를 정리
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Lab3DSetup
{
    const string StartScene = "Assets/Scenes/Start.unity";
    const string LabScene = "Assets/Scenes/Lab3D.unity";

    [MenuItem("Build/Setup Lab3D scenes")]
    public static void Setup()
    {
        MakeMaterials();
        MakeScene(StartScene, typeof(StartMenu));
        MakeScene(LabScene, typeof(Lab3D));

        // Start first, then the original scenes (without the old typing login), then the 3D lab
        var old = EditorBuildSettings.scenes
            .Where(s => s.path != StartScene && s.path != LabScene && !s.path.EndsWith("/Login.unity"))
            .Select(s => s.path);
        var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(StartScene, true) };
        list.AddRange(old.Select(p => new EditorBuildSettingsScene(p, true)));
        list.Add(new EditorBuildSettingsScene(LabScene, true));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Lab3D setup: " + string.Join(", ", list.Select(s => Path.GetFileNameWithoutExtension(s.path))));
    }

    static void MakeScene(string path, System.Type bootstrap)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // dark lighting: no default sky, dark reflections (the default sky washes the dark theme out)
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.22f, 0.27f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.09f, 0.11f, 0.18f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.035f, 0.06f);
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = DarkCubemap();
        RenderSettings.reflectionIntensity = 0.5f;
        new GameObject(bootstrap.Name, bootstrap);
        EditorSceneManager.SaveScene(scene, path);
    }

    static Cubemap DarkCubemap()
    {
        const string path = "Assets/Resources/Lab3D/DarkReflection.cubemap";
        var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if (existing != null) return existing;
        var cube = new Cubemap(8, TextureFormat.RGBA32, false);
        foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
        {
            if (face == CubemapFace.Unknown) continue;
            var c = face == CubemapFace.PositiveY ? new Color(0.10f, 0.12f, 0.22f) : new Color(0.03f, 0.04f, 0.08f);
            cube.SetPixels(Enumerable.Repeat(c, 64).ToArray(), face);
        }
        cube.Apply();
        AssetDatabase.CreateAsset(cube, path);
        return cube;
    }

    static void MakeMaterials()
    {
        const string dir = "Assets/Resources/Lab3D";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Resources", "Lab3D");

        var standard = Shader.Find("Standard");
        Save(dir + "/Body.mat", new Material(standard) { color = Color.gray });

        var emissive = new Material(standard) { color = Color.black };
        emissive.EnableKeyword("_EMISSION");
        emissive.SetColor("_EmissionColor", Color.white);
        emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        Save(dir + "/Emissive.mat", emissive);

        Save(dir + "/Unlit.mat", new Material(Shader.Find("Unlit/Color")) { color = Color.white });

        var glowShader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Unlit/Transparent");
        Debug.Log("Lab3D glow shader: " + (glowShader != null ? glowShader.name : "none"));
        Save(dir + "/Glow.mat", new Material(glowShader));
    }

    static void Save(string path, Material m)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            existing.shaderKeywords = m.shaderKeywords;
            existing.globalIlluminationFlags = m.globalIlluminationFlags;
            EditorUtility.SetDirty(existing);
        }
        else AssetDatabase.CreateAsset(m, path);
    }
}
