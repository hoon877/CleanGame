using UnityEngine;

public static class CozyRuntimeShaders
{
    private const string KeepaliveMaterialFolder = "CozyRestore/Shaders/";

    public static Shader Find(string shaderName, string keepaliveMaterialName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader != null)
        {
            return shader;
        }

        Material keepaliveMaterial = Resources.Load<Material>(KeepaliveMaterialFolder + keepaliveMaterialName);
        if (keepaliveMaterial != null && keepaliveMaterial.shader != null)
        {
            return keepaliveMaterial.shader;
        }

        return null;
    }
}
