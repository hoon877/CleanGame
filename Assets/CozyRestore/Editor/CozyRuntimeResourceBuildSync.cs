using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CozyRuntimeResourceBuildSync : IPreprocessBuildWithReport
{
    private static readonly string[] PrefabExtensions = { ".prefab" };
    private static readonly string[] SoundExtensions = { ".mp3", ".wav", ".ogg" };
    private static readonly string[] TextureExtensions = { ".png", ".jpg", ".jpeg" };
    private const string ShaderKeepaliveFolder = "Assets/Resources/CozyRestore/Shaders";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        SyncRuntimeResources();
    }

    [MenuItem("Cozy Restore/Sync Runtime Resources")]
    public static void SyncRuntimeResources()
    {
        CopyMatchingAssets("Assets/Prefabs", "Assets/Resources/Prefabs", PrefabExtensions);
        CopyMatchingAssets("Assets/Resource/Sound", "Assets/Resources/Sound", SoundExtensions);
        CopyMatchingAssets("Assets/Resource/stain", "Assets/Resources/stain", TextureExtensions);
        CopyAsset("Assets/Resource/\uBA54\uC778\uD654\uBA74.png", "Assets/Resources/LobbyMain.png");
        EnsureShaderKeepaliveMaterials();

        AssetDatabase.Refresh();
        Debug.Log("Cozy Restore runtime resources synced for build.");
    }

    private static void CopyMatchingAssets(string sourceAssetFolder, string targetAssetFolder, string[] extensions)
    {
        string sourceFolder = AssetPathToFullPath(sourceAssetFolder);
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }

        string targetFolder = AssetPathToFullPath(targetAssetFolder);
        Directory.CreateDirectory(targetFolder);

        string[] files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string extension = Path.GetExtension(files[i]).ToLowerInvariant();
            if (!ContainsExtension(extensions, extension))
            {
                continue;
            }

            string relativePath = files[i].Substring(sourceFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetPath = Path.Combine(targetFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.Copy(files[i], targetPath, true);
        }
    }

    private static void CopyAsset(string sourceAssetPath, string targetAssetPath)
    {
        string sourcePath = AssetPathToFullPath(sourceAssetPath);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        string targetPath = AssetPathToFullPath(targetAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        File.Copy(sourcePath, targetPath, true);
    }

    private static void EnsureShaderKeepaliveMaterials()
    {
        Directory.CreateDirectory(AssetPathToFullPath(ShaderKeepaliveFolder));
        CreateShaderKeepaliveMaterial("MaskedSurface", "CozyRestore/MaskedSurface");
        CreateShaderKeepaliveMaterial("MaskedGlass", "CozyRestore/MaskedGlass");
        CreateShaderKeepaliveMaterial("MaskBrush", "Hidden/CozyRestore/MaskBrush");
        CreateShaderKeepaliveMaterial("TransparentTexture", "CozyRestore/TransparentTexture");
        AssetDatabase.SaveAssets();
    }

    private static void CreateShaderKeepaliveMaterial(string materialName, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning("Cozy Restore build sync could not find shader: " + shaderName);
            return;
        }

        string materialPath = ShaderKeepaliveFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.shader = shader;
        EditorUtility.SetDirty(material);
    }

    private static bool ContainsExtension(string[] extensions, string extension)
    {
        for (int i = 0; i < extensions.Length; i++)
        {
            if (extensions[i] == extension)
            {
                return true;
            }
        }

        return false;
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        string relativePath = assetPath.StartsWith("Assets/")
            ? assetPath.Substring("Assets/".Length)
            : assetPath;
        return Path.Combine(Application.dataPath, relativePath);
    }
}
