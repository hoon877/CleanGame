using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum CozyToolMode
{
    Inspect,
    Mop,
    WetMop,
    Paint,
    WindowWiper,
    Decorate,
    Move
}

public sealed class CozyRestoreBootstrap : MonoBehaviour
{
    private const string RollerVisualAssetPath = "Assets/Resource/paint roller 3d model/roller.fbx";
    private const string DecorPrefabFolder = "Assets/Prefabs";
    private const float DecorScaleMultiplier = 1.5f;
    private const float LargeDecorScaleMultiplier = 2f;
    public bool buildOnPlay = true;
    public bool rebuildExistingRoom = true;
    public string roomRootName = "CozyRestore_Room";

    private const float RoomWidth = 14f;
    private const float RoomDepth = 11f;
    private const float RoomHeight = 5.7f;
    private const float WindowCenterX = 3.2f;
    private const float WindowCenterY = RoomHeight * 0.5f;
    private const float WindowWidth = 3.0f;
    private const float WindowHeight = 1.65f;

    private Material floorMaterial;
    private Material wallMaterial;
    private Material paintedMaterial;
    private Material trimMaterial;
    private Material dirtMaterial;
    private Material glassMaterial;
    private Material previewMaterial;
    private Material woodMaterial;
    private Material fabricMaterial;
    private Material plantMaterial;
    private Material metalMaterial;
    private Material rugMaterial;
    private Material curtainMaterial;
    private Material clutterMaterial;
    private Material brushMaterial;
    private Material mopMaterial;
    private Material heldMaterial;
    private Texture2D rollerBrushTexture;

    private void Awake()
    {
        if (buildOnPlay && Application.isPlaying)
        {
            BuildPrototype();
        }
    }

    [ContextMenu("Build Cozy Restore Prototype")]
    public void BuildPrototype()
    {
        GameObject existing = GameObject.Find(roomRootName);
        if (existing != null)
        {
            if (!rebuildExistingRoom)
            {
                Debug.Log("CozyRestore room already exists.");
                return;
            }

            DestroyNow(existing);
        }

        CreateMaterials();

        GameObject root = new GameObject(roomRootName);
        CreateOpenRoom(root.transform);
        CreateStarterInterior(root.transform);
        CreateDust(root.transform);
        CreateObstacles(root.transform);
        GameObject[] templates = CreateDecorTemplates(root.transform);
        GameObject player = CreateCameraRig(root.transform, templates);
        CreateLighting(root.transform);

        CozyProgressTracker tracker = root.AddComponent<CozyProgressTracker>();
        tracker.RefreshTargets();
        player.GetComponent<CozyToolController>().progressTracker = tracker;
        player.GetComponent<CozyToolController>().heldMaterial = heldMaterial;

        Debug.Log("CozyRestore cutaway prototype ready. Drag to orbit, WASD pan, wheel zoom, 1-5 tools.");
    }

    private void CreateMaterials()
    {
        rollerBrushTexture = LoadBrushTexture();
        floorMaterial = MakeMaterial("Pastel Floor", new Color(0.86f, 0.74f, 0.66f), 0.8f);
        wallMaterial = MakeMaterial("Pastel Wall", new Color(0.95f, 0.90f, 0.88f), 0.95f);
        paintedMaterial = MakeMaterial("Fresh Paint", new Color(0.78f, 0.88f, 0.90f), 0.9f);
        trimMaterial = MakeMaterial("Trim", new Color(0.69f, 0.61f, 0.66f), 0.7f);
        dirtMaterial = MakeMaterial("Dust", new Color(0.43f, 0.34f, 0.31f, 0.78f), 0.95f);
        glassMaterial = MakeMaterial("Glass", new Color(0.80f, 0.92f, 0.98f, 0.33f), 0.35f);
        previewMaterial = MakeMaterial("Preview", new Color(0.52f, 0.95f, 0.80f, 0.45f), 0.55f);
        woodMaterial = MakeMaterial("Wood", new Color(0.86f, 0.68f, 0.56f), 0.75f);
        fabricMaterial = MakeMaterial("Fabric", new Color(0.78f, 0.68f, 0.85f), 0.95f);
        plantMaterial = MakeMaterial("Plant", new Color(0.63f, 0.82f, 0.63f), 0.85f);
        metalMaterial = MakeMaterial("Metal", new Color(0.98f, 0.86f, 0.63f), 0.4f);
        rugMaterial = MakeMaterial("Rug", new Color(0.94f, 0.82f, 0.84f), 0.95f);
        curtainMaterial = MakeMaterial("Curtain", new Color(0.87f, 0.84f, 0.95f), 0.95f);
        clutterMaterial = MakeMaterial("Clutter", new Color(0.81f, 0.73f, 0.63f), 0.88f);
        brushMaterial = MakeOverlayMaterial("Brush", new Color(0.78f, 0.88f, 0.90f, 0.88f));
        mopMaterial = MakeOverlayMaterial("Mop", new Color(0.88f, 0.77f, 0.68f, 0.96f));
        heldMaterial = MakeOverlayMaterial("Held", new Color(1f, 0.35f, 0.35f, 0.82f));

        if (rollerBrushTexture != null)
        {
            brushMaterial.mainTexture = rollerBrushTexture;
            mopMaterial.mainTexture = rollerBrushTexture;
            heldMaterial.mainTexture = rollerBrushTexture;
        }
    }

    private Texture2D LoadBrushTexture()
    {
        string[] candidates =
        {
            Path.Combine(Application.dataPath, "CozyRestore", "Generated", "roller_rect_alpha.png"),
            Path.Combine(Application.dataPath, "CozyRestore", "Generated", "soft_brush_alpha.png"),
            Path.Combine(Application.dataPath, "CozyRestore", "Generated", "roller_brush_alpha.png")
        };

        string path = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                path = candidates[i];
                break;
            }
        }

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = Path.GetFileNameWithoutExtension(path);
        texture.LoadImage(bytes);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private Material MakeMaterial(string materialName, Color color, float roughness)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = "CR_" + materialName;
        material.color = color;
        material.SetFloat("_Glossiness", Mathf.Clamp01(1f - roughness));

        if (color.a < 0.99f)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        return material;
    }

    private Material MakeOverlayMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Unlit/Transparent");
        Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.name = "CR_" + materialName;
        material.color = color;
        return material;
    }

    private void CreateOpenRoom(Transform root)
    {
        float halfWidth = RoomWidth * 0.5f;
        float halfDepth = RoomDepth * 0.5f;

        GameObject floor = Cube("Floor", root, new Vector3(0f, -0.05f, 0f), new Vector3(RoomWidth, 0.1f, RoomDepth), floorMaterial, null);
        CozyMoppableFloor moppableFloor = floor.AddComponent<CozyMoppableFloor>();
        moppableFloor.cleanMaterial = floorMaterial;
        moppableFloor.grimeMaterial = dirtMaterial;
        moppableFloor.brushMaterial = mopMaterial;
        moppableFloor.InitializeSurface();
        CreateBackWallWithWindowOpening(root, halfWidth, halfDepth);
        Cube("Left Wall", root, new Vector3(-halfWidth + 0.05f, RoomHeight * 0.5f, 0f), new Vector3(0.12f, RoomHeight, RoomDepth), wallMaterial, typeof(CozyPaintableSurface));

        SetupPaintable("Back Wall Lower");
        SetupPaintable("Back Wall Upper");
        SetupPaintable("Back Wall Left");
        SetupPaintable("Back Wall Right");
        SetupPaintable("Left Wall");

        Cube("Back Baseboard", root, new Vector3(0f, 0.12f, halfDepth - 0.12f), new Vector3(RoomWidth - 0.3f, 0.22f, 0.08f), trimMaterial, null);
        Cube("Left Baseboard", root, new Vector3(-halfWidth + 0.12f, 0.12f, 0f), new Vector3(0.08f, 0.22f, RoomDepth - 0.3f), trimMaterial, null);

        Cube("Back Beam", root, new Vector3(0f, RoomHeight + 0.02f, halfDepth - 0.15f), new Vector3(RoomWidth, 0.12f, 0.24f), trimMaterial, null);
        Cube("Left Beam", root, new Vector3(-halfWidth + 0.15f, RoomHeight + 0.02f, 0f), new Vector3(0.24f, 0.12f, RoomDepth), trimMaterial, null);

        GameObject windowGlass = Cube("Window Glass", root, new Vector3(WindowCenterX, WindowCenterY, halfDepth - 0.12f), new Vector3(WindowWidth, WindowHeight, 0.05f), glassMaterial, null);
        windowGlass.AddComponent<CozyCleanableWindow>();
        Cube("Window Top", root, new Vector3(WindowCenterX, WindowCenterY + WindowHeight * 0.5f + 0.025f, halfDepth - 0.16f), new Vector3(WindowWidth + 0.16f, 0.08f, 0.12f), trimMaterial, null);
        Cube("Window Bottom", root, new Vector3(WindowCenterX, WindowCenterY - WindowHeight * 0.5f - 0.025f, halfDepth - 0.16f), new Vector3(WindowWidth + 0.16f, 0.08f, 0.12f), trimMaterial, null);
        Cube("Window Left", root, new Vector3(WindowCenterX - WindowWidth * 0.5f - 0.05f, WindowCenterY, halfDepth - 0.16f), new Vector3(0.08f, WindowHeight + 0.08f, 0.12f), trimMaterial, null);
        Cube("Window Right", root, new Vector3(WindowCenterX + WindowWidth * 0.5f + 0.05f, WindowCenterY, halfDepth - 0.16f), new Vector3(0.08f, WindowHeight + 0.08f, 0.12f), trimMaterial, null);

        Cube("Curtain Left", root, new Vector3(WindowCenterX - WindowWidth * 0.5f + 0.1f, WindowCenterY - 0.1f, halfDepth - 0.22f), new Vector3(0.34f, 1.9f, 0.12f), curtainMaterial, null);
        Cube("Curtain Right", root, new Vector3(WindowCenterX + WindowWidth * 0.5f - 0.1f, WindowCenterY - 0.1f, halfDepth - 0.22f), new Vector3(0.34f, 1.9f, 0.12f), curtainMaterial, null);

        GameObject rug = Cube("Room Rug", root, new Vector3(1.2f, 0.015f, 0.45f), new Vector3(4.8f, 0.02f, 3.1f) * DecorScaleMultiplier, rugMaterial, null);
        CozyDecorTemplate rugTemplate = rug.AddComponent<CozyDecorTemplate>();
        rugTemplate.displayName = "Room Rug";
        rug.AddComponent<CozyMovableDecor>();
        CreateDecorTrashBin(root);
    }

    private void CreateBackWallWithWindowOpening(Transform root, float halfWidth, float halfDepth)
    {
        float wallZ = halfDepth - 0.05f;
        float openingPaddingX = 0.08f;
        float openingPaddingY = 0.04f;
        float openingLeft = WindowCenterX - WindowWidth * 0.5f - openingPaddingX;
        float openingRight = WindowCenterX + WindowWidth * 0.5f + openingPaddingX;
        float openingBottom = WindowCenterY - WindowHeight * 0.5f - openingPaddingY;
        float openingTop = WindowCenterY + WindowHeight * 0.5f + openingPaddingY;

        float lowerHeight = Mathf.Max(0.01f, openingBottom);
        float upperHeight = Mathf.Max(0.01f, RoomHeight - openingTop);
        Cube("Back Wall Lower", root, new Vector3(0f, lowerHeight * 0.5f, wallZ), new Vector3(RoomWidth, lowerHeight, 0.12f), wallMaterial, typeof(CozyPaintableSurface));
        Cube("Back Wall Upper", root, new Vector3(0f, openingTop + upperHeight * 0.5f, wallZ), new Vector3(RoomWidth, upperHeight, 0.12f), wallMaterial, typeof(CozyPaintableSurface));

        float sideHeight = Mathf.Max(0.01f, openingTop - openingBottom);
        float leftWidth = Mathf.Max(0.01f, openingLeft + halfWidth);
        float rightWidth = Mathf.Max(0.01f, halfWidth - openingRight);
        Cube("Back Wall Left", root, new Vector3(-halfWidth + leftWidth * 0.5f, WindowCenterY, wallZ), new Vector3(leftWidth, sideHeight, 0.12f), wallMaterial, typeof(CozyPaintableSurface));
        Cube("Back Wall Right", root, new Vector3(openingRight + rightWidth * 0.5f, WindowCenterY, wallZ), new Vector3(rightWidth, sideHeight, 0.12f), wallMaterial, typeof(CozyPaintableSurface));
    }

    private void CreateDecorTrashBin(Transform root)
    {
        GameObject trashBin = new GameObject("Decor Trash Bin");
        trashBin.transform.SetParent(root);
        trashBin.transform.localPosition = new Vector3(5.85f, 0f, -4.35f);
        trashBin.AddComponent<CozyDecorTrashBin>();

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Trash Bin Body";
        body.transform.SetParent(trashBin.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        body.transform.localScale = new Vector3(0.52f, 0.38f, 0.52f);
        body.GetComponent<Renderer>().sharedMaterial = metalMaterial;

        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Trash Bin Rim";
        rim.transform.SetParent(trashBin.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0.78f, 0f);
        rim.transform.localScale = new Vector3(0.64f, 0.05f, 0.64f);
        rim.GetComponent<Renderer>().sharedMaterial = trimMaterial;

        GameObject label = Cube("Trash Bin Label", trashBin.transform, new Vector3(0f, 0.42f, -0.53f), new Vector3(0.45f, 0.24f, 0.03f), previewMaterial, null);
        Collider labelCollider = label.GetComponent<Collider>();
        if (labelCollider != null)
        {
            DestroyNow(labelCollider);
        }
    }

    private void SetupPaintable(string objectName)
    {
        GameObject wall = GameObject.Find(roomRootName + "/" + objectName);
        if (wall == null)
        {
            return;
        }

        CozyPaintableSurface surface = wall.GetComponent<CozyPaintableSurface>();
        surface.oldMaterial = wallMaterial;
        surface.freshMaterial = paintedMaterial;
        surface.grimeMaterial = dirtMaterial;
        surface.brushMaterial = brushMaterial;
        surface.allowedPaintNormalLocal = objectName.StartsWith("Back Wall") ? Vector3.back : Vector3.right;
        surface.paintExclusionRectsLocal = new Rect[0];
        surface.InitializeSurface();
    }

    private void CreateStarterInterior(Transform root)
    {
        PlacePrefabDecor(root, "sofa", new Vector3(1.6f, 0f, 1.8f), 180f);
        PlacePrefabDecor(root, "table", new Vector3(1.7f, 0f, 0.1f), 0f);
        PlacePrefabDecor(root, "longlamp", new Vector3(4.6f, 0f, 2.5f), 0f);
        PlacePrefabDecor(root, "desk", new Vector3(-1.8f, 0f, 4.6f), 0f);
        PlacePrefabDecor(root, "chair", new Vector3(4.6f, 0f, -1.5f), 0f);
        PlacePrefabDecor(root, "flowerpot", new Vector3(-5.9f, 0f, 3.9f), 0f);
    }

    private GameObject PlacePrefabDecor(Transform parent, string prefabName, Vector3 position, float yaw)
    {
#if UNITY_EDITOR
        string prefabPath = DecorPrefabFolder + "/" + prefabName + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Missing decor prefab: " + prefabPath);
            return null;
        }

        GameObject placed = Instantiate(prefab, parent);
        PreparePrefabDecorTemplate(placed, GetPrefabDisplayName(prefabPath));
        placed.name = GetPrefabDisplayName(prefabPath) + " Placed";
        placed.transform.localPosition = position;
        placed.transform.localRotation = CozyDecorTemplate.GetPlacementRotation(placed, yaw);
        placed.SetActive(true);
        CozyDecorTemplate.SnapBottomToFloor(placed, parent.TransformPoint(new Vector3(position.x, 0f, position.z)).y);
        return placed;
#else
        return null;
#endif
    }

    private void CreateDust(Transform root)
    {
        Vector3[] positions =
        {
            new Vector3(-5.2f, 0.011f, 2.3f), new Vector3(-2.4f, 0.011f, -1.1f), new Vector3(1.9f, 0.011f, -1.4f),
            new Vector3(4.8f, 0.011f, 1.8f), new Vector3(4.2f, 0.011f, -3.0f), new Vector3(-0.2f, 0.011f, 2.8f),
            new Vector3(-6.88f, 1.3f, 2.2f), new Vector3(-6.88f, 2.5f, -1.8f), new Vector3(-2.5f, 1.1f, 5.35f),
            new Vector3(4.2f, 2.5f, 5.35f), new Vector3(1.8f, 0.58f, 0.1f), new Vector3(-4.0f, 0.58f, 2.6f),
            new Vector3(5.5f, 0.011f, 3.8f), new Vector3(-1.1f, 0.011f, -3.8f), new Vector3(-6.88f, 0.8f, 4.1f)
        };

        Vector3[] scales =
        {
            new Vector3(1.2f, 0.025f, 0.8f), new Vector3(0.9f, 0.025f, 1.0f), new Vector3(0.95f, 0.025f, 0.7f),
            new Vector3(0.8f, 0.025f, 0.65f), new Vector3(0.95f, 0.025f, 0.85f), new Vector3(1.25f, 0.025f, 0.9f),
            new Vector3(0.06f, 0.7f, 0.9f), new Vector3(0.06f, 0.8f, 0.7f), new Vector3(1.1f, 0.06f, 0.55f),
            new Vector3(0.75f, 0.06f, 0.9f), new Vector3(0.6f, 0.04f, 0.4f), new Vector3(0.65f, 0.04f, 0.42f),
            new Vector3(1.0f, 0.025f, 0.8f), new Vector3(1.2f, 0.025f, 1.1f), new Vector3(0.06f, 0.55f, 0.8f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject dirt = Cube("Dust Patch " + (i + 1), root, positions[i], scales[i], dirtMaterial, typeof(CozyDirtPatch));
            dirt.transform.rotation = Quaternion.Euler(0f, i * 24f, 0f);
        }
    }

    private void CreateObstacles(Transform root)
    {
        CreateCoveredPile(root, new Vector3(-2.2f, 0f, 1.2f), 18f);
        CreateCoveredPile(root, new Vector3(3.3f, 0f, -1.2f), -12f);
        CreateMovingBoxCluster(root, new Vector3(-0.9f, 0f, -3.4f));
        CreateTrashBagCluster(root, new Vector3(5.0f, 0f, -2.1f));
        CreatePlankStack(root, new Vector3(-5.8f, 0f, 0.2f), 90f);
        CreatePaintCanCluster(root, new Vector3(2.8f, 0f, 4.0f));
        CreateLaundryPile(root, new Vector3(-3.6f, 0f, 3.8f));
        CreateCrateBarrier(root, new Vector3(1.0f, 0f, -4.0f));
        CreateTrashBagCluster(root, new Vector3(6.0f, 0f, 3.0f));
        CreateMovingBoxCluster(root, new Vector3(-4.8f, 0f, -4.0f));
    }

    private GameObject[] CreateDecorTemplates(Transform root)
    {
        GameObject library = new GameObject("Decor Template Library");
        library.transform.SetParent(root);

        List<GameObject> templates = new List<GameObject>();
#if UNITY_EDITOR
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { DecorPrefabFolder });
        List<string> prefabPaths = new List<string>();
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefabPaths.Add(prefabPath);
            }
        }

        prefabPaths.Sort();
        for (int i = 0; i < prefabPaths.Count; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            if (prefab == null)
            {
                continue;
            }

            GameObject template = Instantiate(prefab, library.transform);
            PreparePrefabDecorTemplate(template, GetPrefabDisplayName(prefabPaths[i]));
            templates.Add(template);
        }
#endif

        if (templates.Count == 0)
        {
            Debug.LogWarning("No decor prefabs found in " + DecorPrefabFolder + ". Decorate mode will be empty.");
        }

        library.SetActive(false);
        return templates.ToArray();
    }

    private void PreparePrefabDecorTemplate(GameObject template, string displayName)
    {
        template.name = displayName;
        template.transform.localPosition = Vector3.zero;
        template.transform.localRotation = CozyDecorTemplate.GetPlacementRotation(displayName, 0f);
        template.transform.localScale *= GetDecorScaleMultiplier(displayName);

        CozyDecorTemplate marker = template.GetComponent<CozyDecorTemplate>();
        if (marker == null)
        {
            marker = template.AddComponent<CozyDecorTemplate>();
        }
        marker.displayName = displayName;

        if (template.GetComponent<CozyMovableDecor>() == null)
        {
            template.AddComponent<CozyMovableDecor>();
        }

        EnsurePlacementCollider(template);
        float floorY = template.transform.parent != null ? template.transform.parent.TransformPoint(Vector3.zero).y : 0f;
        CozyDecorTemplate.SnapBottomToFloor(template, floorY);
    }

    private float GetDecorScaleMultiplier(string displayName)
    {
        if (IsLargeDecor(displayName))
        {
            return LargeDecorScaleMultiplier;
        }

        return DecorScaleMultiplier;
    }

    private bool IsLargeDecor(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        string normalizedName = displayName.ToLowerInvariant();
        return normalizedName.Contains("desk") || normalizedName.Contains("sofa");
    }

    private string GetPrefabDisplayName(string prefabPath)
    {
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        if (string.IsNullOrEmpty(name))
        {
            return "Decor";
        }

        name = name.Replace('_', ' ').Replace('-', ' ');
        char[] chars = name.ToCharArray();
        bool capitalizeNext = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]))
            {
                capitalizeNext = true;
                continue;
            }

            chars[i] = capitalizeNext ? char.ToUpperInvariant(chars[i]) : chars[i];
            capitalizeNext = false;
        }

        return new string(chars);
    }

    private void EnsurePlacementCollider(GameObject target)
    {
        if (target.GetComponentsInChildren<Collider>(true).Length > 0)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.center = target.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = target.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }

    private GameObject CreateCameraRig(Transform root, GameObject[] templates)
    {
        GameObject rig = new GameObject("CozyRestore_CameraRig");
        rig.transform.SetParent(root);
        rig.transform.position = new Vector3(-0.4f, 1.25f, 0.45f);

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        camera.transform.SetParent(rig.transform);
        camera.transform.localPosition = new Vector3(0f, 0f, -12f);
        camera.transform.localRotation = Quaternion.identity;
        camera.fieldOfView = 50f;
        camera.clearFlags = CameraClearFlags.Skybox;

        CozyOrbitCameraController orbit = rig.AddComponent<CozyOrbitCameraController>();
        orbit.viewCamera = camera;
        orbit.target = new Vector3(0.5f, 1.35f, 0.9f);
        orbit.distance = 15f;
        orbit.minDistance = 8f;
        orbit.maxDistance = 18f;
        orbit.yaw = 50f;
        orbit.pitch = 24f;
        orbit.panMin = new Vector3(-4.0f, 0.8f, -2.4f);
        orbit.panMax = new Vector3(4.5f, 2.5f, 4.2f);

        CozyToolController tools = rig.AddComponent<CozyToolController>();
        tools.viewCamera = camera;
        tools.decorTemplates = templates;
        tools.previewMaterial = previewMaterial;
        tools.rollerVisualPrefab = LoadRollerVisualPrefab();
        tools.reach = 40f;

        return rig;
    }

    private GameObject LoadRollerVisualPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(RollerVisualAssetPath);
#else
        return null;
#endif
    }

    private void CreateLighting(Transform root)
    {
        GameObject sun = GameObject.Find("Directional Light");
        if (sun == null)
        {
            sun = new GameObject("Directional Light");
            sun.AddComponent<Light>().type = LightType.Directional;
        }

        sun.transform.SetParent(root);
        sun.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        Light directional = sun.GetComponent<Light>();
        directional.intensity = 0.82f;
        directional.color = new Color(1f, 0.94f, 0.90f);

        CreatePointLight(root, "Warm Room Light", new Vector3(-1.8f, 2.9f, 1.2f), new Color(1f, 0.84f, 0.72f), 2.0f, 10f);
        CreatePointLight(root, "Window Bounce", new Vector3(4.0f, 2.8f, 3.8f), new Color(0.84f, 0.92f, 1f), 1.2f, 9f);
        CreatePointLight(root, "Corner Fill", new Vector3(-6.0f, 2.2f, -2.6f), new Color(1f, 0.88f, 0.90f), 1.0f, 8f);

        RenderSettings.ambientLight = new Color(0.76f, 0.78f, 0.84f);
        RenderSettings.fog = false;
    }

    private void CreatePointLight(Transform root, string lightName, Vector3 position, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(lightName);
        go.transform.SetParent(root);
        go.transform.position = position;
        Light point = go.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = color;
        point.intensity = intensity;
        point.range = range;
    }

    private GameObject CreateBed(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject bed = NewPiece("Pastel Bed", asTemplate ? "Bed" : null, parent, position, yaw);
        Cube("Mattress", bed.transform, new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.28f, 1.55f), wallMaterial, null);
        Cube("Blanket", bed.transform, new Vector3(0f, 0.50f, -0.1f), new Vector3(2.0f, 0.16f, 1.25f), fabricMaterial, null);
        Cube("Headboard", bed.transform, new Vector3(0f, 0.82f, 0.72f), new Vector3(2.25f, 1.0f, 0.16f), woodMaterial, null);
        Cube("Pillow Left", bed.transform, new Vector3(-0.55f, 0.55f, 0.35f), new Vector3(0.6f, 0.16f, 0.36f), curtainMaterial, null);
        Cube("Pillow Right", bed.transform, new Vector3(0.55f, 0.55f, 0.35f), new Vector3(0.6f, 0.16f, 0.36f), curtainMaterial, null);
        return bed;
    }

    private GameObject CreateSofa(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject sofa = NewPiece("Soft Sofa", asTemplate ? "Sofa" : null, parent, position, yaw);
        Cube("Seat", sofa.transform, new Vector3(0f, 0.42f, 0f), new Vector3(2.2f, 0.32f, 0.92f), fabricMaterial, null);
        Cube("Back", sofa.transform, new Vector3(0f, 0.95f, 0.34f), new Vector3(2.2f, 0.84f, 0.18f), fabricMaterial, null);
        Cube("Arm Left", sofa.transform, new Vector3(-1.07f, 0.68f, 0f), new Vector3(0.18f, 0.5f, 0.92f), fabricMaterial, null);
        Cube("Arm Right", sofa.transform, new Vector3(1.07f, 0.68f, 0f), new Vector3(0.18f, 0.5f, 0.92f), fabricMaterial, null);
        Cube("Leg Left", sofa.transform, new Vector3(-0.82f, 0.12f, -0.25f), new Vector3(0.12f, 0.24f, 0.12f), woodMaterial, null);
        Cube("Leg Right", sofa.transform, new Vector3(0.82f, 0.12f, -0.25f), new Vector3(0.12f, 0.24f, 0.12f), woodMaterial, null);
        return sofa;
    }

    private GameObject CreateAccentChair(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject chair = NewPiece("Accent Chair", asTemplate ? "Chair" : null, parent, position, yaw);
        Cube("Seat", chair.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.9f, 0.18f, 0.88f), curtainMaterial, null);
        Cube("Back", chair.transform, new Vector3(0f, 0.93f, 0.35f), new Vector3(0.9f, 0.86f, 0.18f), curtainMaterial, null);
        Cube("Arm Left", chair.transform, new Vector3(-0.56f, 0.63f, 0f), new Vector3(0.16f, 0.42f, 0.82f), curtainMaterial, null);
        Cube("Arm Right", chair.transform, new Vector3(0.56f, 0.63f, 0f), new Vector3(0.16f, 0.42f, 0.82f), curtainMaterial, null);
        return chair;
    }

    private GameObject CreateCoffeeTable(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject table = NewPiece("Coffee Table", asTemplate ? "Table" : null, parent, position, yaw);
        Cube("Top", table.transform, new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 0.12f, 0.8f), woodMaterial, null);
        Cube("Leg A", table.transform, new Vector3(-0.6f, 0.18f, -0.25f), new Vector3(0.10f, 0.36f, 0.10f), metalMaterial, null);
        Cube("Leg B", table.transform, new Vector3(0.6f, 0.18f, -0.25f), new Vector3(0.10f, 0.36f, 0.10f), metalMaterial, null);
        Cube("Leg C", table.transform, new Vector3(-0.6f, 0.18f, 0.25f), new Vector3(0.10f, 0.36f, 0.10f), metalMaterial, null);
        Cube("Leg D", table.transform, new Vector3(0.6f, 0.18f, 0.25f), new Vector3(0.10f, 0.36f, 0.10f), metalMaterial, null);
        return table;
    }

    private GameObject CreateLowShelf(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject shelf = NewPiece("Low Shelf", asTemplate ? "Shelf" : null, parent, position, yaw);
        Cube("Body", shelf.transform, new Vector3(0f, 0.58f, 0f), new Vector3(1.6f, 1.16f, 0.28f), woodMaterial, null);
        Cube("Inner", shelf.transform, new Vector3(0f, 0.58f, -0.03f), new Vector3(1.28f, 0.84f, 0.32f), wallMaterial, null);
        for (int i = 0; i < 7; i++)
        {
            Material material = i % 3 == 0 ? curtainMaterial : (i % 2 == 0 ? paintedMaterial : fabricMaterial);
            Cube("Book " + i, shelf.transform, new Vector3(-0.52f + i * 0.17f, 0.50f + (i % 2) * 0.06f, -0.17f), new Vector3(0.12f, 0.42f + (i % 3) * 0.06f, 0.16f), material, null);
        }
        return shelf;
    }

    private GameObject CreateConsoleTable(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject table = NewPiece("Console Table", asTemplate ? "Console" : null, parent, position, yaw);
        Cube("Top", table.transform, new Vector3(0f, 0.74f, 0f), new Vector3(1.5f, 0.10f, 0.46f), woodMaterial, null);
        Cube("Leg Left", table.transform, new Vector3(-0.62f, 0.35f, 0f), new Vector3(0.10f, 0.72f, 0.10f), trimMaterial, null);
        Cube("Leg Right", table.transform, new Vector3(0.62f, 0.35f, 0f), new Vector3(0.10f, 0.72f, 0.10f), trimMaterial, null);
        Cube("Shelf", table.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.32f, 0.08f, 0.42f), woodMaterial, null);
        return table;
    }

    private GameObject CreateFloorLamp(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject lamp = NewPiece("Floor Lamp", asTemplate ? "Lamp" : null, parent, position, yaw);
        Cube("Base", lamp.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.55f, 0.10f, 0.55f), metalMaterial, null);
        Cube("Stem", lamp.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.08f, 1.8f, 0.08f), metalMaterial, null);
        Cube("Shade", lamp.transform, new Vector3(0f, 1.84f, 0f), new Vector3(0.72f, 0.42f, 0.72f), curtainMaterial, null);
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 4.5f;
        light.intensity = 1.1f;
        light.color = new Color(1f, 0.87f, 0.77f);
        return lamp;
    }

    private GameObject CreatePlant(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject plant = NewPiece("Plant", asTemplate ? "Plant" : null, parent, position, yaw);
        Cube("Pot", plant.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.46f, 0.42f, 0.46f), woodMaterial, null);
        Cube("Leaf A", plant.transform, new Vector3(-0.16f, 0.70f, 0f), new Vector3(0.14f, 0.78f, 0.18f), plantMaterial, null).transform.rotation = Quaternion.Euler(0f, 0f, -25f);
        Cube("Leaf B", plant.transform, new Vector3(0.16f, 0.76f, 0.02f), new Vector3(0.14f, 0.86f, 0.18f), plantMaterial, null).transform.rotation = Quaternion.Euler(0f, 0f, 22f);
        Cube("Leaf C", plant.transform, new Vector3(0f, 0.88f, -0.12f), new Vector3(0.12f, 0.78f, 0.18f), plantMaterial, null).transform.rotation = Quaternion.Euler(18f, 0f, 0f);
        return plant;
    }

    private GameObject CreatePouf(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject pouf = NewPiece("Pouf", asTemplate ? "Pouf" : null, parent, position, yaw);
        Cube("Body", pouf.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.68f, 0.44f, 0.68f), fabricMaterial, null);
        return pouf;
    }

    private GameObject CreateWallFrame(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject frame = NewPiece("Wall Frame", asTemplate ? "Frame" : null, parent, position, yaw);
        Cube("Border", frame.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.0f, 0.7f, 0.08f), woodMaterial, null);
        Cube("Art", frame.transform, new Vector3(0f, 1.1f, -0.01f), new Vector3(0.82f, 0.52f, 0.09f), paintedMaterial, null);
        return frame;
    }

    private void CreateBookStack(Transform parent, Vector3 position, float yaw, bool asTemplate)
    {
        GameObject stack = NewPiece("Books Stack", asTemplate ? "Books" : null, parent, position, yaw);
        Cube("Book Bottom", stack.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.08f, 0.28f), curtainMaterial, null);
        Cube("Book Middle", stack.transform, new Vector3(0.02f, 0.16f, -0.02f), new Vector3(0.46f, 0.08f, 0.30f), paintedMaterial, null);
        Cube("Book Top", stack.transform, new Vector3(-0.01f, 0.24f, 0.01f), new Vector3(0.40f, 0.08f, 0.26f), fabricMaterial, null);
    }

    private void CreateCoveredPile(Transform parent, Vector3 position, float yaw)
    {
        GameObject pile = NewObstacle("Covered Furniture", parent, position, yaw);
        Cube("Base", pile.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1.0f), clutterMaterial, null);
        Cube("Drape", pile.transform, new Vector3(0f, 0.72f, 0f), new Vector3(1.9f, 0.24f, 1.25f), curtainMaterial, null);
        Cube("Front Fold", pile.transform, new Vector3(0f, 0.36f, -0.48f), new Vector3(1.7f, 0.52f, 0.18f), curtainMaterial, null);
    }

    private void CreateMovingBoxCluster(Transform parent, Vector3 position)
    {
        GameObject cluster = new GameObject("Moving Boxes");
        cluster.transform.SetParent(parent);
        cluster.transform.localPosition = position;
        for (int i = 0; i < 4; i++)
        {
            GameObject box = NewObstacle("Moving Box " + (i + 1), cluster.transform, new Vector3((i % 2) * 0.62f, 0.22f + (i / 2) * 0.42f, (i % 3) * 0.18f), i * 11f);
            Cube("Body", box.transform, Vector3.zero, new Vector3(0.56f, 0.44f, 0.56f), clutterMaterial, null);
        }
    }

    private void CreateTrashBagCluster(Transform parent, Vector3 position)
    {
        GameObject cluster = new GameObject("Trash Bags");
        cluster.transform.SetParent(parent);
        cluster.transform.localPosition = position;
        for (int i = 0; i < 3; i++)
        {
            GameObject bag = NewObstacle("Trash Bag " + (i + 1), cluster.transform, new Vector3(i * 0.34f, 0.28f, (i % 2) * 0.26f), i * 18f);
            Cube("Bag", bag.transform, Vector3.zero, new Vector3(0.46f, 0.56f, 0.42f), trimMaterial, null);
        }
    }

    private void CreatePlankStack(Transform parent, Vector3 position, float yaw)
    {
        GameObject stack = NewObstacle("Plank Stack", parent, position, yaw);
        for (int i = 0; i < 4; i++)
        {
            Cube("Plank " + i, stack.transform, new Vector3(0f, 0.08f + i * 0.1f, -0.18f + i * 0.12f), new Vector3(1.6f, 0.06f, 0.22f), woodMaterial, null);
        }
    }

    private void CreatePaintCanCluster(Transform parent, Vector3 position)
    {
        GameObject cluster = new GameObject("Paint Cans");
        cluster.transform.SetParent(parent);
        cluster.transform.localPosition = position;
        for (int i = 0; i < 3; i++)
        {
            GameObject can = NewObstacle("Paint Can " + (i + 1), cluster.transform, new Vector3(i * 0.28f, 0.18f, (i % 2) * 0.22f), 0f);
            Cube("Can", can.transform, Vector3.zero, new Vector3(0.22f, 0.36f, 0.22f), metalMaterial, null);
        }
    }

    private void CreateLaundryPile(Transform parent, Vector3 position)
    {
        GameObject pile = NewObstacle("Laundry Pile", parent, position, 0f);
        Cube("Pile A", pile.transform, new Vector3(-0.18f, 0.16f, 0f), new Vector3(0.55f, 0.22f, 0.48f), curtainMaterial, null);
        Cube("Pile B", pile.transform, new Vector3(0.14f, 0.18f, 0.10f), new Vector3(0.52f, 0.24f, 0.44f), fabricMaterial, null);
        Cube("Pile C", pile.transform, new Vector3(0.05f, 0.26f, -0.16f), new Vector3(0.48f, 0.18f, 0.38f), paintedMaterial, null);
    }

    private void CreateCrateBarrier(Transform parent, Vector3 position)
    {
        GameObject barrier = new GameObject("Crate Barrier");
        barrier.transform.SetParent(parent);
        barrier.transform.localPosition = position;
        for (int i = 0; i < 3; i++)
        {
            GameObject crate = NewObstacle("Crate " + (i + 1), barrier.transform, new Vector3(i * 0.62f, 0.24f, 0f), i * 7f);
            Cube("Body", crate.transform, Vector3.zero, new Vector3(0.5f, 0.48f, 0.5f), woodMaterial, null);
        }
    }

    private GameObject NewPiece(string objectName, string displayName, Transform parent, Vector3 position, float yaw)
    {
        GameObject piece = new GameObject(objectName);
        piece.transform.SetParent(parent);
        piece.transform.localPosition = position;
        piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        if (!string.IsNullOrEmpty(displayName))
        {
            CozyDecorTemplate marker = piece.AddComponent<CozyDecorTemplate>();
            marker.displayName = displayName;
        }

        piece.AddComponent<CozyMovableDecor>();

        return piece;
    }

    private GameObject NewObstacle(string objectName, Transform parent, Vector3 position, float yaw)
    {
        GameObject obstacle = new GameObject(objectName);
        obstacle.transform.SetParent(parent);
        obstacle.transform.localPosition = position;
        obstacle.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        obstacle.AddComponent<CozyTidyObject>();
        return obstacle;
    }

    private GameObject Cube(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, System.Type componentType)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        if (componentType != null)
        {
            cube.AddComponent(componentType);
        }
        return cube;
    }

    private void DestroyNow(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

public sealed class CozyOrbitCameraController : MonoBehaviour
{
    public Camera viewCamera;
    public Vector3 target = new Vector3(0f, 1f, 0f);
    public float distance = 14f;
    public float minDistance = 8f;
    public float maxDistance = 17.5f;
    public float yaw = 50f;
    public float pitch = 21f;
    public float orbitSensitivity = 2.5f;
    public float panSpeed = 5.5f;
    public float zoomSpeed = 2f;
    public Vector3 panMin = new Vector3(-4.8f, 0.8f, -2.8f);
    public Vector3 panMax = new Vector3(3.4f, 2.4f, 3.7f);

    private void Awake()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }
        RefreshTransform();
    }

    private void Update()
    {
        HandleKeyboardPan();
        HandleMouseOrbit();
        HandleZoom();
        RefreshTransform();
    }

    private void HandleKeyboardPan()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        target += (forward.normalized * vertical + right.normalized * horizontal) * panSpeed * Time.deltaTime;

        if (Input.GetKey(KeyCode.R))
        {
            target += Vector3.up * panSpeed * 0.5f * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.F))
        {
            target += Vector3.down * panSpeed * 0.5f * Time.deltaTime;
        }
    }

    private void HandleMouseOrbit()
    {
        CozyToolController tools = GetComponent<CozyToolController>();
        if (tools != null && tools.BlocksRightMouseOrbit)
        {
            return;
        }

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * orbitSensitivity * 6f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * orbitSensitivity * 3f, 12f, 55f);
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 right = viewCamera.transform.right;
            Vector3 up = Vector3.up;
            target -= right * Input.GetAxis("Mouse X") * orbitSensitivity * 0.08f * distance;
            target -= up * Input.GetAxis("Mouse Y") * orbitSensitivity * 0.05f * distance;
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        target = new Vector3(
            Mathf.Clamp(target.x, panMin.x, panMax.x),
            Mathf.Clamp(target.y, panMin.y, panMax.y),
            Mathf.Clamp(target.z, panMin.z, panMax.z));
    }

    private void RefreshTransform()
    {
        if (viewCamera == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target;
        transform.rotation = rotation;
        viewCamera.transform.position = target - rotation * Vector3.forward * distance;
        viewCamera.transform.rotation = rotation;
    }
}

public sealed class CozyToolController : MonoBehaviour
{
    public Camera viewCamera;
    public CozyProgressTracker progressTracker;
    public GameObject[] decorTemplates;
    public Material previewMaterial;
    public Material heldMaterial;
    public GameObject rollerVisualPrefab;
    public float reach = 40f;
    public float cleanRate = 1.1f;
    public float paintRate = 0.7f;

    private CozyToolMode mode = CozyToolMode.Mop;
    private int decorIndex;
    private float decorYaw;
    private GameObject previewInstance;
    private CozyMovableDecor movingDecor;
    private CozyPaintableSurface activePaintSurface;
    private CozyMoppableFloor activeMopFloor;
    private CozyCleanableWindow activeWindow;
    private GameObject windowWiperVisual;
    private bool pickedThisFrame;
    private bool previewIsHeldDecor;
    private string status = "Restore the room.";
    private const float RollerVisualModelX = 100f;
    private readonly Vector3 rollerVisualModelOffset = new Vector3(0f, -0.01f, -0.14f);
    private RollerVisualState paintRollerVisual;
    private RollerVisualState wetMopRollerVisual;

    public bool BlocksRightMouseOrbit => false;

    private sealed class RollerVisualState
    {
        public string visualName;
        public float modelZ;
        public GameObject instance;
        public Transform pivot;
        public Vector3 centerOffset;
        public int quarterTurns;
        public float press;
        public float jolt;
        public float dynamicYaw = 45f;
        public Vector3 lastPoint;
        public bool hasLastPoint;
    }

    private string CurrentDecorName
    {
        get
        {
            if (decorTemplates == null || decorTemplates.Length == 0)
            {
                return "None";
            }

            CozyDecorTemplate template = decorTemplates[decorIndex].GetComponent<CozyDecorTemplate>();
            return template != null ? template.displayName : decorTemplates[decorIndex].name;
        }
    }

    private void Awake()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        InitializeRollerVisualStates();
        EnsureRollerVisual(paintRollerVisual);
        EnsureRollerVisual(wetMopRollerVisual);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        pickedThisFrame = false;
        HandleModeKeys();
        if (!Input.GetMouseButton(0))
        {
            EndContinuousActions();
        }
        HandleRayTools();
        HandleDecorPreview();
        UpdateCursorState();
        UpdateRollerVisual();
        UpdateWindowWiperVisual();
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(18, 18, 520, 150), "Cozy Restore - Cutaway Studio");
        GUI.Label(new Rect(32, 44, 480, 22), "Orbit RMB, Pan MMB/WASD, Zoom Wheel, Height R/F");
        GUI.Label(new Rect(32, 68, 480, 22), "1 Inspect  2 Mop  3 Wet Mop  4 Paint  5 Wiper  6 Decorate  7 Move   |   Tab brush, Q/E rotate, Z/X decor");
        GUI.Label(new Rect(32, 92, 480, 22), "Tool: " + mode + "   " + GetModeDetailLabel());
        GUI.Label(new Rect(32, 116, 480, 22), status);

        float progress = progressTracker != null ? progressTracker.NormalizedProgress : 0f;
        GUI.Label(new Rect(32, 140, 90, 20), "Progress");
        GUI.Box(new Rect(132, 143, 360, 12), string.Empty);
        GUI.Box(new Rect(132, 143, 360 * progress, 12), string.Empty);
    }

    private void HandleModeKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(CozyToolMode.Inspect);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(CozyToolMode.Mop);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(CozyToolMode.WetMop);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetMode(CozyToolMode.Paint);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetMode(CozyToolMode.WindowWiper);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SetMode(CozyToolMode.Decorate);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SetMode(CozyToolMode.Move);

        if (mode == CozyToolMode.Decorate && decorTemplates != null && decorTemplates.Length > 0)
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                decorIndex = (decorIndex - 1 + decorTemplates.Length) % decorTemplates.Length;
                RecreatePreview();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                decorIndex = (decorIndex + 1) % decorTemplates.Length;
                RecreatePreview();
            }
        }
    }

    private void SetMode(CozyToolMode nextMode)
    {
        EndContinuousActions();
        if (movingDecor != null && nextMode != CozyToolMode.Move)
        {
            movingDecor.gameObject.SetActive(true);
            movingDecor = null;
        }

        ClearPreview();
        mode = nextMode;
        status = "Switched to " + mode;
    }

    private void HandleRayTools()
    {
        if (viewCamera == null)
        {
            return;
        }

        Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);

        if (mode == CozyToolMode.WetMop)
        {
            if (TryGetWetMopTarget(ray, out CozyMoppableFloor floorMess, out Vector3 floorPoint, out Vector3 floorNormal))
            {
                floorMess.SetRollerQuarterTurns(wetMopRollerVisual != null ? wetMopRollerVisual.quarterTurns : 0);
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    RotateRollerVisual90();
                    floorMess.SetRollerQuarterTurns(wetMopRollerVisual != null ? wetMopRollerVisual.quarterTurns : 0);
                    if (activeMopFloor != null) activeMopFloor.EndStroke();
                    activeMopFloor = floorMess;
                    status = "Wet Mop: roller rotated";
                    return;
                }
                if (activeMopFloor != null && activeMopFloor != floorMess)
                {
                    activeMopFloor.EndStroke();
                }
                activeMopFloor = floorMess;
                status = "Wet Mop: " + Mathf.RoundToInt(floorMess.CleanPercent * 100f) + "% clean";
                if (Input.GetMouseButton(0))
                {
                    Vector3 floorTangent = RotateStampTangent90(floorNormal, ResolveRollerTangent(floorNormal, wetMopRollerVisual != null ? wetMopRollerVisual.quarterTurns : 0));
                    floorMess.CleanAt(floorPoint, floorNormal, floorTangent, cleanRate * Time.deltaTime);
                    progressTracker?.RefreshTargets();
                }
                return;
            }

            if (activeMopFloor != null)
            {
                activeMopFloor.EndStroke();
                activeMopFloor = null;
            }
            status = "Wet Mop: sweep across the floor.";
            return;
        }

        if (mode == CozyToolMode.Paint)
        {
            if (TryGetPaintTarget(ray, out CozyPaintableSurface surface, out Vector3 paintPoint, out Vector3 paintNormal))
            {
                surface.SetRollerQuarterTurns(paintRollerVisual != null ? paintRollerVisual.quarterTurns : 0);
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    RotateRollerVisual90();
                    surface.SetRollerQuarterTurns(paintRollerVisual != null ? paintRollerVisual.quarterTurns : 0);
                    if (activePaintSurface != null) activePaintSurface.EndStroke();
                    activePaintSurface = surface;
                    status = "Paint: roller rotated";
                    return;
                }
                if (activePaintSurface != null && activePaintSurface != surface)
                {
                    activePaintSurface.EndStroke();
                }
                activePaintSurface = surface;
                status = "Paint: " + Mathf.RoundToInt(surface.PaintPercent * 100f) + "% covered";
                if (Input.GetMouseButton(0))
                {
                    Vector3 paintTangent = RotateStampTangent90(paintNormal, ResolveRollerTangent(paintNormal, paintRollerVisual != null ? paintRollerVisual.quarterTurns : 0));
                    surface.PaintAt(paintPoint, paintNormal, paintTangent, paintRate * Time.deltaTime);
                    progressTracker?.RefreshTargets();
                }
                return;
            }

            if (activePaintSurface != null)
            {
                activePaintSurface.EndStroke();
                activePaintSurface = null;
            }
            status = "Paint: brush the exposed walls.";
            return;
        }

        if (mode == CozyToolMode.WindowWiper)
        {
            if (Physics.Raycast(ray, out RaycastHit windowHit, reach))
            {
                CozyCleanableWindow window = windowHit.collider.GetComponentInParent<CozyCleanableWindow>();
                if (window != null)
                {
                    if (activeWindow != null && activeWindow != window)
                    {
                        activeWindow.EndStroke();
                    }

                    activeWindow = window;
                    status = "Window Wiper: " + Mathf.RoundToInt(window.CleanPercent * 100f) + "% clean";
                    if (Input.GetMouseButton(0))
                    {
                        Vector3 tangent = ResolveRollerTangent(windowHit.normal, 0);
                        window.CleanAt(windowHit.point, windowHit.normal, tangent, cleanRate * Time.deltaTime);
                        progressTracker?.RefreshTargets();
                    }
                    return;
                }
            }

            if (activeWindow != null)
            {
                activeWindow.EndStroke();
                activeWindow = null;
            }
            status = "Window Wiper: drag across the dirty window.";
            return;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, reach))
        {
            if (mode != CozyToolMode.Decorate)
            {
                status = "Aim at dust, clutter, walls, or the floor.";
            }
            if (activePaintSurface != null)
            {
                activePaintSurface.EndStroke();
                activePaintSurface = null;
            }
            if (activeMopFloor != null)
            {
                activeMopFloor.EndStroke();
                activeMopFloor = null;
            }
            return;
        }

        if (mode == CozyToolMode.Inspect)
        {
            CozyTidyObject obstacle = hit.collider.GetComponentInParent<CozyTidyObject>();
            status = obstacle != null ? "Click to remove " + obstacle.name : "Inspect tool: remove clutter and obstacles.";
            if (obstacle != null && Input.GetMouseButtonDown(0))
            {
                obstacle.TidyAway();
                progressTracker?.RefreshTargets();
                status = "Removed " + obstacle.name;
            }
            return;
        }

        if (mode == CozyToolMode.Mop)
        {
            CozyDirtPatch dirt = hit.collider.GetComponentInParent<CozyDirtPatch>();
            if (activeMopFloor != null)
            {
                activeMopFloor.EndStroke();
                activeMopFloor = null;
            }
            if (dirt == null)
            {
                status = "Mop: scrub dust patches.";
                return;
            }

            status = "Mop: " + Mathf.RoundToInt(dirt.CleanPercent * 100f) + "% clean";
            if (Input.GetMouseButtonDown(0))
            {
                dirt.CleanStep();
                progressTracker?.RefreshTargets();
            }
            return;
        }

    }

    private void HandleDecorPreview()
    {
        if ((mode != CozyToolMode.Decorate && mode != CozyToolMode.Move) || viewCamera == null)
        {
            return;
        }

        if (Input.GetKey(KeyCode.Q)) decorYaw -= 95f * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) decorYaw += 95f * Time.deltaTime;

        if (mode == CozyToolMode.Decorate && (previewInstance == null || previewIsHeldDecor))
        {
            RecreatePreview();
        }
        else if (mode == CozyToolMode.Move && previewInstance != null && !previewIsHeldDecor)
        {
            ClearPreview();
        }

        Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
        if (mode == CozyToolMode.Move && movingDecor == null && Physics.Raycast(ray, out RaycastHit pickHit, reach))
        {
            CozyMovableDecor movable = pickHit.collider.GetComponentInParent<CozyMovableDecor>();
            if (movable != null && Input.GetMouseButtonDown(0))
            {
                movingDecor = movable;
                decorYaw = movingDecor.transform.eulerAngles.y;
                ClearPreview();
                previewInstance = Instantiate(movingDecor.gameObject);
                previewInstance.name = "Decor Move Preview";
                previewInstance.SetActive(true);
                SetColliders(previewInstance, false);
                previewIsHeldDecor = true;
                ApplyPreviewMaterial(previewInstance, heldMaterial != null ? heldMaterial : previewMaterial);
                Light[] previewLights = previewInstance.GetComponentsInChildren<Light>();
                for (int i = 0; i < previewLights.Length; i++)
                {
                    previewLights[i].enabled = false;
                }
                previewInstance.transform.position = movingDecor.transform.position;
                previewInstance.transform.rotation = movingDecor.transform.rotation;
                movingDecor.gameObject.SetActive(false);
                status = "Move: selected decor";
                pickedThisFrame = true;
                return;
            }
        }

        if (mode == CozyToolMode.Move && movingDecor != null && Physics.Raycast(ray, out RaycastHit trashHit, reach))
        {
            CozyDecorTrashBin trashBin = trashHit.collider.GetComponentInParent<CozyDecorTrashBin>();
            if (trashBin != null)
            {
                if (previewInstance != null)
                {
                    previewInstance.SetActive(true);
                    previewInstance.transform.position = trashBin.transform.position + Vector3.up * 0.18f;
                    previewInstance.transform.rotation = CozyDecorTemplate.GetPlacementRotation(previewInstance, decorYaw);
                    CozyDecorTemplate.SnapBottomToFloor(previewInstance, trashBin.transform.position.y);
                }

                status = "Move: click trash bin to discard selected decor";
                if (Input.GetMouseButtonDown(0) && !pickedThisFrame)
                {
                    DiscardMovingDecor();
                }
                return;
            }
        }

        bool floorHit = Physics.Raycast(ray, out RaycastHit hit, reach) && Vector3.Dot(hit.normal, Vector3.up) > 0.65f;
        if (!floorHit)
        {
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }
            status = mode == CozyToolMode.Move ? "Move: pick furniture or point at floor." : "Decorate: point at a floor spot.";
            return;
        }

        if (mode == CozyToolMode.Decorate)
        {
            if (decorTemplates == null || decorTemplates.Length == 0)
            {
                return;
            }
            if (previewInstance == null)
            {
                RecreatePreview();
            }
        }

        if (previewInstance == null)
        {
            if (mode == CozyToolMode.Move)
            {
                status = "Move: click furniture to pick it up.";
                return;
            }
            return;
        }

        previewInstance.SetActive(true);
        previewInstance.transform.position = hit.point;
        previewInstance.transform.rotation = CozyDecorTemplate.GetPlacementRotation(previewInstance, decorYaw);
        CozyDecorTemplate.SnapBottomToFloor(previewInstance, hit.point.y);
        status = mode == CozyToolMode.Move ? (movingDecor != null ? "Move: place selected decor or click trash bin to discard" : "Move: click furniture to pick it up") : "Decorate: place " + CurrentDecorName;

        if (Input.GetMouseButtonDown(0) && !pickedThisFrame)
        {
            if (mode == CozyToolMode.Move && movingDecor != null)
            {
                movingDecor.transform.position = hit.point;
                movingDecor.transform.rotation = CozyDecorTemplate.GetPlacementRotation(movingDecor.gameObject, decorYaw);
                CozyDecorTemplate.SnapBottomToFloor(movingDecor.gameObject, hit.point.y);
                movingDecor.gameObject.SetActive(true);
                status = "Moved decor";
                movingDecor = null;
                ClearPreview();
            }
            else if (mode == CozyToolMode.Decorate)
            {
                GameObject placed = Instantiate(decorTemplates[decorIndex], hit.point, CozyDecorTemplate.GetPlacementRotation(decorTemplates[decorIndex], decorYaw));
                placed.name = CurrentDecorName + " Placed";
                placed.SetActive(true);
                CozyDecorTemplate.SnapBottomToFloor(placed, hit.point.y);
                SetColliders(placed, true);
                progressTracker?.AddPlacedDecor(placed);
                status = "Placed " + CurrentDecorName;
            }
        }
    }

    private void DiscardMovingDecor()
    {
        if (movingDecor == null)
        {
            return;
        }

        GameObject discarded = movingDecor.gameObject;
        movingDecor = null;
        ClearPreview();
        progressTracker?.RemovePlacedDecor(discarded);
        Destroy(discarded);
        progressTracker?.RefreshTargets();
        status = "Move: discarded decor";
    }

    private void RecreatePreview()
    {
        ClearPreview();
        if (decorTemplates == null || decorTemplates.Length == 0)
        {
            return;
        }

        previewInstance = Instantiate(decorTemplates[decorIndex]);
        previewInstance.name = "Decor Placement Preview";
        previewIsHeldDecor = false;
        previewInstance.SetActive(false);
        SetColliders(previewInstance, false);
        ApplyPreviewMaterial(previewInstance, previewMaterial);

        Light[] lights = previewInstance.GetComponentsInChildren<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].enabled = false;
        }
    }

    private void SetColliders(GameObject target, bool enabled)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private string GetModeDetailLabel()
    {
        if (mode == CozyToolMode.Decorate)
        {
            return "New Decor: " + CurrentDecorName;
        }

        if (mode == CozyToolMode.Move)
        {
            return "Moving: " + (movingDecor != null ? movingDecor.name : "None");
        }

        if (mode == CozyToolMode.WindowWiper)
        {
            return "Window Wiper";
        }

        return "Decor: " + CurrentDecorName;
    }

    private void EndContinuousActions()
    {
        if (activePaintSurface != null)
        {
            activePaintSurface.EndStroke();
            activePaintSurface = null;
        }

        if (activeMopFloor != null)
        {
            activeMopFloor.EndStroke();
            activeMopFloor = null;
        }

        if (activeWindow != null)
        {
            activeWindow.EndStroke();
            activeWindow = null;
        }
    }

    private void ApplyPreviewMaterial(GameObject target, Material material)
    {
        if (target == null)
        {
            return;
        }

        Color tint = material != null ? material.color : Color.white;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] runtimeMaterials = renderers[i].materials;
            for (int j = 0; j < runtimeMaterials.Length; j++)
            {
                if (runtimeMaterials[j] == null)
                {
                    continue;
                }

                Material previewMaterial = new Material(runtimeMaterials[j]);
                if (previewMaterial.HasProperty("_Color"))
                {
                    Color baseColor = previewMaterial.color;
                    previewMaterial.color = new Color(
                        Mathf.Lerp(baseColor.r, tint.r, 0.28f),
                        Mathf.Lerp(baseColor.g, tint.g, 0.28f),
                        Mathf.Lerp(baseColor.b, tint.b, 0.28f),
                        Mathf.Min(baseColor.a, 0.48f));
                }
                MakePreviewMaterialTransparent(previewMaterial);
                runtimeMaterials[j] = previewMaterial;
            }
            renderers[i].materials = runtimeMaterials;
        }
    }

    private void MakePreviewMaterialTransparent(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private bool TryGetWetMopTarget(Ray ray, out CozyMoppableFloor floor, out Vector3 worldPoint, out Vector3 worldNormal)
    {
        floor = null;
        worldPoint = default;
        worldNormal = Vector3.up;

        CozyMoppableFloor[] floors = FindObjectsOfType<CozyMoppableFloor>(true);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < floors.Length; i++)
        {
            if (floors[i] == null)
            {
                continue;
            }

            if (floors[i].TryProjectRay(ray, out Vector3 candidatePoint, out Vector3 candidateNormal, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                floor = floors[i];
                worldPoint = candidatePoint;
                worldNormal = candidateNormal;
            }
        }

        return floor != null;
    }

    private bool TryGetPaintTarget(Ray ray, out CozyPaintableSurface surface, out Vector3 worldPoint, out Vector3 worldNormal)
    {
        surface = null;
        worldPoint = default;
        worldNormal = Vector3.back;

        CozyPaintableSurface[] surfaces = FindObjectsOfType<CozyPaintableSurface>(true);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] == null)
            {
                continue;
            }

            if (surfaces[i].TryProjectRay(ray, out Vector3 candidatePoint, out Vector3 candidateNormal, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                surface = surfaces[i];
                worldPoint = candidatePoint;
                worldNormal = candidateNormal;
            }
        }

        return surface != null;
    }

    private void ClearPreview()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }
        previewInstance = null;
        previewIsHeldDecor = false;
    }

    private void InitializeRollerVisualStates()
    {
        if (paintRollerVisual == null)
        {
            paintRollerVisual = new RollerVisualState
            {
                visualName = "Wall Roller Visual",
                modelZ = 0f
            };
        }

        if (wetMopRollerVisual == null)
        {
            wetMopRollerVisual = new RollerVisualState
            {
                visualName = "Floor Roller Visual",
                modelZ = 90f
            };
        }
    }

    private void EnsureRollerVisual(RollerVisualState state)
    {
        if (state == null || state.instance != null || rollerVisualPrefab == null || viewCamera == null)
        {
            return;
        }

        state.pivot = new GameObject(state.visualName + " Pivot").transform;
        state.pivot.SetParent(transform, false);

        state.instance = Instantiate(rollerVisualPrefab, state.pivot);
        state.instance.name = state.visualName;
        state.instance.transform.localPosition = rollerVisualModelOffset;
        state.instance.transform.localRotation = Quaternion.Euler(RollerVisualModelX, 45f, state.modelZ);
        NormalizeRollerVisualScale(state);
        SetVisualColliders(state.instance, false);
        SetVisualLights(state.instance, false);
        state.instance.SetActive(false);
    }

    private void NormalizeRollerVisualScale(RollerVisualState state)
    {
        if (state == null || state.instance == null || state.pivot == null)
        {
            return;
        }

        Renderer[] renderers = state.instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            state.instance.transform.localScale = Vector3.one * 0.2f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxExtent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float scale = maxExtent > 0.0001f ? 0.48f / maxExtent : 0.2f;
        state.instance.transform.localScale = Vector3.one * scale;

        renderers = state.instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            state.centerOffset = Vector3.zero;
            return;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = state.pivot.InverseTransformPoint(bounds.center);
        state.centerOffset = -localCenter;
        state.instance.transform.localPosition = state.centerOffset + rollerVisualModelOffset;
    }

    private void SetVisualColliders(GameObject target, bool enabled)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private void SetVisualLights(GameObject target, bool enabled)
    {
        Light[] lights = target.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].enabled = enabled;
        }
    }

    private void RotateRollerVisual90()
    {
        RollerVisualState state = mode == CozyToolMode.WetMop ? wetMopRollerVisual : paintRollerVisual;
        if (state == null)
        {
            return;
        }

        state.quarterTurns = (state.quarterTurns + 1) % 4;
        state.jolt = 1f;
    }

    private void UpdateCursorState()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UpdateRollerVisual()
    {
        EnsureRollerVisual(paintRollerVisual);
        EnsureRollerVisual(wetMopRollerVisual);

        bool showPaintRoller = false;
        bool showWetMopRoller = false;
        Vector3 targetPoint = Vector3.zero;
        Vector3 targetNormal = Vector3.up;
        Vector3 targetTangent = Vector3.right;
        Ray pointerRay = viewCamera != null ? viewCamera.ScreenPointToRay(Input.mousePosition) : default;

        if (mode == CozyToolMode.Paint && TryGetPaintTarget(pointerRay, out CozyPaintableSurface paintSurface, out Vector3 paintPoint, out Vector3 paintNormal))
        {
            showPaintRoller = true;
            targetPoint = paintPoint;
            targetNormal = ResolveVisibleSurfaceNormal(paintPoint, paintNormal);
            targetTangent = ResolveRollerTangent(targetNormal, paintRollerVisual != null ? paintRollerVisual.quarterTurns : 0);
            status = Input.GetMouseButton(0)
                ? status
                : "Paint: roller ready on " + paintSurface.name;
        }
        else if (mode == CozyToolMode.WetMop && TryGetWetMopTarget(pointerRay, out CozyMoppableFloor mopFloor, out Vector3 mopPoint, out Vector3 mopNormal))
        {
            showWetMopRoller = true;
            targetPoint = mopPoint;
            targetNormal = ResolveVisibleSurfaceNormal(mopPoint, mopNormal);
            targetTangent = ResolveRollerTangent(targetNormal, wetMopRollerVisual != null ? wetMopRollerVisual.quarterTurns : 0);
            status = Input.GetMouseButton(0)
                ? status
                : "Wet Mop: roller ready on " + mopFloor.name;
        }

        UpdateRollerVisualState(paintRollerVisual, showPaintRoller, targetPoint, targetNormal, targetTangent, 0.18f);
        UpdateRollerVisualState(wetMopRollerVisual, showWetMopRoller, targetPoint, targetNormal, targetTangent, 0.14f);
    }

    private void UpdateWindowWiperVisual()
    {
        EnsureWindowWiperVisual();
        if (windowWiperVisual == null)
        {
            return;
        }

        bool shouldShow = false;
        if (mode == CozyToolMode.WindowWiper && viewCamera != null)
        {
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, reach) && hit.collider.GetComponentInParent<CozyCleanableWindow>() != null)
            {
                shouldShow = true;
                Vector3 normal = ResolveVisibleSurfaceNormal(hit.point, hit.normal);
                Vector3 tangent = ResolveRollerTangent(normal, 0);
                windowWiperVisual.transform.position = hit.point + normal * 0.08f;
                windowWiperVisual.transform.rotation = BuildRollerVisualRotation(normal, tangent);
            }
        }

        windowWiperVisual.SetActive(shouldShow);
    }

    private void EnsureWindowWiperVisual()
    {
        if (windowWiperVisual != null)
        {
            return;
        }

        windowWiperVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        windowWiperVisual.name = "Window Wiper Placeholder";
        windowWiperVisual.transform.localScale = new Vector3(0.62f, 0.06f, 0.08f);
        Renderer renderer = windowWiperVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = heldMaterial != null ? heldMaterial : previewMaterial;
        }
        Collider collider = windowWiperVisual.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        windowWiperVisual.SetActive(false);
    }

    private void UpdateRollerVisualState(RollerVisualState state, bool shouldShow, Vector3 targetPoint, Vector3 targetNormal, Vector3 targetTangent, float surfaceOffset)
    {
        if (state == null || state.instance == null || state.pivot == null)
        {
            return;
        }

        if (state.instance.activeSelf != shouldShow)
        {
            state.instance.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            state.press = Mathf.MoveTowards(state.press, 0f, Time.deltaTime * 7f);
            state.jolt = Mathf.MoveTowards(state.jolt, 0f, Time.deltaTime * 8f);
            state.dynamicYaw = Mathf.MoveTowards(state.dynamicYaw, 45f, Time.deltaTime * 120f);
            state.hasLastPoint = false;
            return;
        }

        bool usingRoller = Input.GetMouseButton(0);
        state.press = Mathf.MoveTowards(state.press, usingRoller ? 1f : 0f, Time.deltaTime * 9f);
        state.jolt = Mathf.MoveTowards(state.jolt, 0f, Time.deltaTime * 7.5f);
        float movementFactor = 0f;
        if (state.hasLastPoint)
        {
            float travel = Vector3.Distance(state.lastPoint, targetPoint);
            movementFactor = Mathf.Clamp01(travel / 0.2f);
        }

        float targetYaw = Mathf.Lerp(30f, 60f, usingRoller ? movementFactor : 0f);
        state.dynamicYaw = Mathf.MoveTowards(state.dynamicYaw, targetYaw, Time.deltaTime * 180f);
        state.lastPoint = targetPoint;
        state.hasLastPoint = true;

        Vector3 pressOffset = -targetNormal * (0.08f * state.press);
        Vector3 joltOffset = targetTangent * (0.06f * state.jolt);
        Vector3 travelLift = Vector3.Cross(targetNormal, targetTangent).normalized * (0.025f * movementFactor);
        state.pivot.position = targetPoint + targetNormal * surfaceOffset + pressOffset + joltOffset + travelLift;
        state.pivot.rotation = BuildRollerVisualRotation(targetNormal, targetTangent);
        state.instance.transform.localPosition = state.centerOffset + rollerVisualModelOffset;
        state.instance.transform.localRotation = Quaternion.Euler(RollerVisualModelX, state.dynamicYaw, state.modelZ);
    }

    private Vector3 ResolveVisibleSurfaceNormal(Vector3 surfacePoint, Vector3 surfaceNormal)
    {
        if (viewCamera == null)
        {
            return surfaceNormal.normalized;
        }

        Vector3 toCamera = (viewCamera.transform.position - surfacePoint).normalized;
        if (Vector3.Dot(surfaceNormal, toCamera) < 0f)
        {
            surfaceNormal = -surfaceNormal;
        }

        return surfaceNormal.normalized;
    }

    private Vector3 ResolveRollerTangent(Vector3 normal, int quarterTurns)
    {
        Vector3 cameraRight = viewCamera != null ? Vector3.ProjectOnPlane(viewCamera.transform.right, normal).normalized : Vector3.zero;
        Vector3 cameraUp = viewCamera != null ? Vector3.ProjectOnPlane(viewCamera.transform.up, normal).normalized : Vector3.zero;
        Vector3 baseAxis = cameraRight.sqrMagnitude > 0.0001f
            ? cameraRight
            : (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.8f ? Vector3.right : Vector3.up);
        Vector3 altAxis = cameraUp.sqrMagnitude > 0.0001f ? cameraUp : Vector3.Cross(normal, baseAxis).normalized;
        if (altAxis.sqrMagnitude < 0.0001f)
        {
            altAxis = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
        }

        return (quarterTurns % 2 == 0 ? baseAxis : altAxis).normalized;
    }

    private Vector3 RotateStampTangent90(Vector3 normal, Vector3 tangent)
    {
        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveRollerTangent(safeNormal, 0);
        return (Quaternion.AngleAxis(90f, safeNormal) * safeTangent).normalized;
    }

    private Quaternion BuildRollerVisualRotation(Vector3 normal, Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveRollerTangent(normal, 0);
        Vector3 up = Vector3.Cross(-normal, safeTangent).normalized;
        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
        }

        Quaternion baseRotation = Quaternion.LookRotation(-normal, up);
        Quaternion cameraTilt = Quaternion.AngleAxis(-10f, Vector3.right);
        return baseRotation * cameraTilt;
    }
}

public sealed class CozyDirtPatch : MonoBehaviour
{
    public int clicksToClean = 5;

    private int clicks;
    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private Color baseColor;

    public bool IsClean => clicks >= clicksToClean;
    public float CleanPercent => Mathf.Clamp01(clicks / (float)Mathf.Max(1, clicksToClean));

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            runtimeMaterial = cachedRenderer.material;
            baseColor = runtimeMaterial.color;
        }
    }

    public void CleanStep()
    {
        if (IsClean)
        {
            return;
        }

        clicks = Mathf.Min(clicksToClean, clicks + 1);
        float remaining = 1f - CleanPercent;
        if (runtimeMaterial != null)
        {
            Color color = baseColor;
            color.a = Mathf.Lerp(0f, baseColor.a, remaining);
            runtimeMaterial.color = color;
        }

        if (IsClean)
        {
            gameObject.SetActive(false);
        }
    }
}

public sealed class CozyCleanableWindow : MonoBehaviour
{
    public int coverageGridX = 36;
    public int coverageGridY = 24;
    public float completionThreshold = 0.95f;
    public Vector2 wiperContactSize = new Vector2(0.55f, 0.18f);

    private float coverage;
    private Material runtimeMaterial;
    private RenderTexture dirtMask;
    private RenderTexture dirtMaskScratch;
    private Vector3 lastStrokePoint;
    private Vector3 lastStrokeNormal;
    private Vector3 lastStrokeTangent;
    private bool hasStrokePoint;
    private bool playedCleanFx;
    private readonly HashSet<int> cleanedCells = new HashSet<int>();

    public bool IsClean => coverage >= 1f;
    public float CleanPercent => Mathf.Clamp01(coverage);

    private void Awake()
    {
        InitializeSurface();
    }

    public void InitializeSurface()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
        dirtMask = CozyDirtMaskRenderer.CreateDirtyMask("CR_" + name + "_WindowMask");
        dirtMaskScratch = null;

        runtimeMaterial = CozyDirtMaskRenderer.CreateMaskedGlassMaterial("CR_" + name + "_MaskedGlass");
        runtimeMaterial.SetTexture("_DirtMask", dirtMask);
        ConfigureMaskProjection();
        renderer.material = runtimeMaterial;
    }

    private void OnDestroy()
    {
        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
    }

    public void CleanAt(Vector3 worldPoint, Vector3 normal, Vector3 tangent, float amount)
    {
        if (IsClean)
        {
            return;
        }

        Vector3 strokeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        if (!hasStrokePoint || Vector3.Distance(lastStrokePoint, worldPoint) > 1.1f || Vector3.Dot(lastStrokeNormal, normal) < 0.9f)
        {
            TryCreateCleanStamp(worldPoint, strokeTangent, 0.95f);
            lastStrokePoint = worldPoint;
            lastStrokeNormal = normal;
            lastStrokeTangent = strokeTangent;
            hasStrokePoint = true;
            return;
        }

        float distance = Vector3.Distance(lastStrokePoint, worldPoint);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.05f));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 point = Vector3.Lerp(lastStrokePoint, worldPoint, t);
            Vector3 stepTangent = Vector3.Slerp(lastStrokeTangent == Vector3.zero ? strokeTangent : lastStrokeTangent, strokeTangent, t).normalized;
            TryCreateCleanStamp(point, stepTangent, 0.92f);
        }

        lastStrokePoint = worldPoint;
        lastStrokeNormal = normal;
        lastStrokeTangent = strokeTangent;
        hasStrokePoint = true;
    }

    public void EndStroke()
    {
        hasStrokePoint = false;
        lastStrokeTangent = Vector3.zero;
    }

    private void TryCreateCleanStamp(Vector3 worldPoint, Vector3 tangent, float strength)
    {
        if (!TryGetWindowCoords(worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld))
        {
            return;
        }

        Vector2 finalStampSize = ClampRectSizeToEdge(wiperContactSize, edgeDistancesWorld);
        if (finalStampSize.x < 0.035f || finalStampSize.y < 0.018f)
        {
            return;
        }

        int newlyCovered = CoverRectCells(
            uv,
            Mathf.Max(1.2f, finalStampSize.x / Mathf.Max(0.001f, wiperContactSize.x) * 1.85f),
            Mathf.Max(1.0f, finalStampSize.y / Mathf.Max(0.001f, wiperContactSize.y) * 1.65f));
        coverage = cleanedCells.Count / (float)Mathf.Max(1, coverageGridX * coverageGridY);
        StampDirtMask(uv, finalStampSize, tangent, strength);

        if (coverage >= completionThreshold)
        {
            coverage = 1f;
            CozyDirtMaskRenderer.Fill(dirtMask, Color.black);
            PlayCleanCompleteFx();
        }
    }

    private void PlayCleanCompleteFx()
    {
        if (playedCleanFx)
        {
            return;
        }

        playedCleanFx = true;
        Vector3 normal = transform.forward.normalized;
        Vector3 center = transform.position - normal * 0.09f;
        Vector3[] offsets =
        {
            Vector3.zero,
            transform.right * -0.72f + transform.up * 0.42f,
            transform.right * 0.68f + transform.up * 0.34f,
            transform.right * -0.35f + transform.up * -0.38f,
            transform.right * 0.42f + transform.up * -0.28f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject sparkle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sparkle.name = "Window Clean Sparkle";
            sparkle.transform.position = center + offsets[i];
            sparkle.transform.rotation = Quaternion.LookRotation(-normal, transform.up);
            sparkle.transform.localScale = Vector3.one * (i == 0 ? 0.46f : 0.24f);

            Renderer renderer = sparkle.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Unlit/Transparent"));
            material.color = new Color(1f, 0.96f, 0.58f, i == 0 ? 0.85f : 0.72f);
            renderer.material = material;

            Collider collider = sparkle.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            CozyWindowSparkleFx fx = sparkle.AddComponent<CozyWindowSparkleFx>();
            fx.runtimeMaterial = material;
            fx.lifetime = i == 0 ? 0.62f : 0.48f;
            fx.pulseScale = i == 0 ? 1.85f : 1.55f;
        }
    }

    private bool TryGetWindowCoords(Vector3 worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld)
    {
        uv = default;
        edgeDistancesWorld = Vector2.zero;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (Mathf.Abs(localPoint.z) > 0.58f)
        {
            return false;
        }

        float min = -0.5f;
        float max = 0.5f;
        if (localPoint.x < min || localPoint.x > max || localPoint.y < min || localPoint.y > max)
        {
            return false;
        }

        edgeDistancesWorld = new Vector2(
            LocalDistanceToWorld(Vector3.right, Mathf.Min(localPoint.x - min, max - localPoint.x)),
            LocalDistanceToWorld(Vector3.up, Mathf.Min(localPoint.y - min, max - localPoint.y)));
        uv = new Vector2(Mathf.InverseLerp(min, max, localPoint.x), Mathf.InverseLerp(min, max, localPoint.y));
        return true;
    }

    private void StampDirtMask(Vector2 uv, Vector2 stampSizeWorld, Vector3 tangent, float strength)
    {
        Vector2 windowSize = new Vector2(LocalDistanceToWorld(Vector3.right, 1f), LocalDistanceToWorld(Vector3.up, 1f));
        Vector2 brushSizeUv = new Vector2(
            stampSizeWorld.x / Mathf.Max(0.001f, windowSize.x),
            stampSizeWorld.y / Mathf.Max(0.001f, windowSize.y));
        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, uv, brushSizeUv * 0.68f, WorldTangentToMaskTangent(tangent), Mathf.Max(0.08f, strength * 0.24f));
    }

    private Vector2 WorldTangentToMaskTangent(Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        Vector2 maskTangent = new Vector2(Vector3.Dot(safeTangent, transform.right.normalized), Vector3.Dot(safeTangent, transform.up.normalized));
        return maskTangent.sqrMagnitude > 0.0001f ? maskTangent.normalized : Vector2.right;
    }

    private void ConfigureMaskProjection()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetVector("_MaskOrigin", transform.position);
        runtimeMaterial.SetVector("_MaskAxisA", transform.right.normalized);
        runtimeMaterial.SetVector("_MaskAxisB", transform.up.normalized);
        runtimeMaterial.SetVector("_MaskSize", new Vector4(LocalDistanceToWorld(Vector3.right, 1f), LocalDistanceToWorld(Vector3.up, 1f), 0f, 0f));
    }

    private int CoverRectCells(Vector2 uv, float halfWidthCells, float halfHeightCells)
    {
        int centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (coverageGridX - 1)), 0, coverageGridX - 1);
        int centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (coverageGridY - 1)), 0, coverageGridY - 1);
        int radiusX = Mathf.Max(1, Mathf.CeilToInt(halfWidthCells));
        int radiusY = Mathf.Max(1, Mathf.CeilToInt(halfHeightCells));
        int added = 0;

        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            if (y < 0 || y >= coverageGridY) continue;
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || x >= coverageGridX) continue;
                float dx = x - centerX;
                float dy = y - centerY;
                if (Mathf.Abs(dx) > halfWidthCells || Mathf.Abs(dy) > halfHeightCells)
                {
                    continue;
                }

                int key = y * coverageGridX + x;
                if (cleanedCells.Add(key))
                {
                    added++;
                }
            }
        }

        return added;
    }

    private Vector2 ClampRectSizeToEdge(Vector2 desiredSize, Vector2 edgeDistancesWorld)
    {
        return new Vector2(
            Mathf.Min(desiredSize.x, edgeDistancesWorld.x * 2f * 0.92f),
            Mathf.Min(desiredSize.y, edgeDistancesWorld.y * 2f * 0.92f));
    }

    private float LocalDistanceToWorld(Vector3 localAxis, float localDistance)
    {
        return transform.TransformVector(localAxis.normalized * Mathf.Max(0f, localDistance)).magnitude;
    }
}

public static class CozyDirtMaskRenderer
{
    public const int MaskResolution = 512;
    private static Material brushMaterial;

    public static RenderTexture CreateDirtyMask(string maskName)
    {
        RenderTexture mask = new RenderTexture(MaskResolution, MaskResolution, 0, RenderTextureFormat.R8);
        mask.name = maskName;
        mask.wrapMode = TextureWrapMode.Clamp;
        mask.filterMode = FilterMode.Bilinear;
        mask.Create();
        Fill(mask, Color.white);
        return mask;
    }

    public static Material CreateMaskedSurfaceMaterial(Material cleanMaterial, Material dirtyMaterial, string materialName)
    {
        Shader shader = Shader.Find("CozyRestore/MaskedSurface");
        Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.name = materialName;

        Color cleanColor = cleanMaterial != null ? cleanMaterial.color : Color.white;
        Color dirtyColor = dirtyMaterial != null ? dirtyMaterial.color : new Color(0.24f, 0.19f, 0.16f, 1f);
        dirtyColor.a = 1f;

        material.SetColor("_CleanColor", cleanColor);
        material.SetColor("_DirtyColor", dirtyColor);
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", cleanMaterial != null ? cleanMaterial.GetFloat("_Glossiness") : 0.15f);
        }

        return material;
    }

    public static Material CreateMaskedGlassMaterial(string materialName)
    {
        Shader shader = Shader.Find("CozyRestore/MaskedGlass");
        Material material = new Material(shader != null ? shader : Shader.Find("Transparent/Diffuse"));
        material.name = materialName;
        material.SetColor("_CleanColor", new Color(0.80f, 0.94f, 1f, 0.14f));
        material.SetColor("_DirtyColor", new Color(0.42f, 0.46f, 0.42f, 0.76f));
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", 0.88f);
        }
        return material;
    }

    public static void Stamp(RenderTexture mask, ref RenderTexture scratch, Vector2 uv, Vector2 brushSizeUv, Vector2 brushTangentUv, float strength, Rect[] exclusionRectsUv = null)
    {
        if (mask == null)
        {
            return;
        }

        EnsureScratch(mask, ref scratch);
        EnsureBrushMaterial();
        if (brushMaterial == null)
        {
            return;
        }

        brushMaterial.SetVector("_BrushCenter", new Vector4(uv.x, uv.y, 0f, 0f));
        brushMaterial.SetVector("_BrushSize", new Vector4(Mathf.Max(0.001f, brushSizeUv.x), Mathf.Max(0.001f, brushSizeUv.y), 0f, 0f));
        brushMaterial.SetVector("_BrushTangent", new Vector4(brushTangentUv.x, brushTangentUv.y, 0f, 0f));
        brushMaterial.SetFloat("_BrushSoftness", 0.38f);
        brushMaterial.SetFloat("_BrushStrength", Mathf.Clamp01(strength));
        SetExclusionRects(exclusionRectsUv);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(mask, scratch, brushMaterial);
        Graphics.Blit(scratch, mask);
        RenderTexture.active = previous;
    }

    public static void Fill(RenderTexture target, Color color)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(false, true, color);
        RenderTexture.active = previous;
    }

    public static void Release(RenderTexture target)
    {
        if (target == null)
        {
            return;
        }

        if (RenderTexture.active == target)
        {
            RenderTexture.active = null;
        }
        target.Release();
        Object.Destroy(target);
    }

    private static void SetExclusionRects(Rect[] exclusionRectsUv)
    {
        int count = exclusionRectsUv != null ? Mathf.Min(4, exclusionRectsUv.Length) : 0;
        Vector4[] rectVectors = new Vector4[4];
        for (int i = 0; i < count; i++)
        {
            Rect rect = exclusionRectsUv[i];
            rectVectors[i] = new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
        }

        brushMaterial.SetInt("_ExclusionCount", count);
        brushMaterial.SetVectorArray("_ExclusionRects", rectVectors);
    }

    private static void EnsureScratch(RenderTexture mask, ref RenderTexture scratch)
    {
        if (scratch != null && scratch.width == mask.width && scratch.height == mask.height)
        {
            return;
        }

        Release(scratch);
        scratch = new RenderTexture(mask.width, mask.height, 0, mask.format);
        scratch.name = mask.name + " Scratch";
        scratch.wrapMode = TextureWrapMode.Clamp;
        scratch.filterMode = FilterMode.Bilinear;
        scratch.Create();
    }

    private static void EnsureBrushMaterial()
    {
        if (brushMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/CozyRestore/MaskBrush");
        if (shader != null)
        {
            brushMaterial = new Material(shader);
        }
    }
}

public sealed class CozyPaintableSurface : MonoBehaviour
{
    public Material oldMaterial;
    public Material freshMaterial;
    public Material grimeMaterial;
    public Material brushMaterial;
    public Vector3 allowedPaintNormalLocal = Vector3.back;
    public Rect[] paintExclusionRectsLocal = new Rect[0];
    public float faceInset = 0.0001f;
    public int coverageGridX = 44;
    public int coverageGridY = 30;
    public float completionThreshold = 0.95f;
    public Vector2 rollerContactSize = new Vector2(0.60f, 0.20f);
    private float progress;
    private Renderer cachedRenderer;
    private GameObject grimeOverlay;
    private Material runtimeSurfaceMaterial;
    private RenderTexture dirtMask;
    private RenderTexture dirtMaskScratch;
    private Vector3 lastStrokePoint;
    private Vector3 lastStrokeNormal;
    private Vector3 lastStrokeTangent;
    private bool hasStrokePoint;
    private float lastCleanShineTime = -1f;
    private readonly HashSet<int> coveredCells = new HashSet<int>();
    private int rollerQuarterTurns;

    public bool IsPainted => progress >= 1f;
    public float PaintPercent => progress;

    private void Awake()
    {
        InitializeSurface();
    }

    public void InitializeSurface()
    {
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            CozyDirtMaskRenderer.Release(dirtMask);
            CozyDirtMaskRenderer.Release(dirtMaskScratch);
            dirtMask = CozyDirtMaskRenderer.CreateDirtyMask(name + " Dirt Mask");
            dirtMaskScratch = null;
            runtimeSurfaceMaterial = CozyDirtMaskRenderer.CreateMaskedSurfaceMaterial(freshMaterial, oldMaterial != null ? oldMaterial : grimeMaterial, "CR_" + name + "_MaskedPaint");
            runtimeSurfaceMaterial.SetTexture("_DirtMask", dirtMask);
            ConfigureMaskProjection();
            cachedRenderer.material = runtimeSurfaceMaterial;
        }
    }

    private void OnDestroy()
    {
        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
    }

    public void PaintAt(Vector3 worldPoint, Vector3 normal, Vector3 tangent, float amount)
    {
        if (IsPainted)
        {
            return;
        }

        PaintStrokeSegment(worldPoint, normal, tangent);
    }

    public void EndStroke()
    {
        hasStrokePoint = false;
        lastStrokeTangent = Vector3.zero;
    }

    public bool CanAcceptPaintAt(Vector3 worldPoint, Vector3 worldNormal)
    {
        return TryGetSurfaceCoords(worldPoint, worldNormal, out _, out _);
    }

    public void RotateRoller90()
    {
        rollerQuarterTurns = (rollerQuarterTurns + 1) % 4;
        EndStroke();
    }

    public void SetRollerQuarterTurns(int quarterTurns)
    {
        rollerQuarterTurns = ((quarterTurns % 4) + 4) % 4;
    }

    public bool TryProjectRay(Ray ray, out Vector3 worldPoint, out Vector3 worldNormal, out float distance)
    {
        worldPoint = default;
        worldNormal = transform.TransformDirection(GetAllowedNormal()).normalized;
        distance = 0f;

        Vector3 localNormal = GetAllowedNormal();
        Vector3 localPlanePoint = GetPlanePoint(localNormal);
        Plane plane = new Plane(worldNormal, transform.TransformPoint(localPlanePoint));
        if (!plane.Raycast(ray, out float enter) || enter < 0f)
        {
            return false;
        }

        Vector3 candidatePoint = ray.GetPoint(enter);
        if (!TryGetSurfaceCoords(candidatePoint, worldNormal, out _, out _))
        {
            return false;
        }

        worldPoint = candidatePoint;
        distance = enter;
        return true;
    }

    private void PaintStrokeSegment(Vector3 worldPoint, Vector3 normal, Vector3 tangent)
    {
        Vector3 strokeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(worldPoint, normal);
        if (!hasStrokePoint || Vector3.Distance(lastStrokePoint, worldPoint) > 1.25f || Vector3.Dot(lastStrokeNormal, normal) < 0.92f)
        {
            TryCreateStamp(worldPoint, normal, strokeTangent, rollerContactSize, 0.93f);
            lastStrokePoint = worldPoint;
            lastStrokeNormal = normal;
            lastStrokeTangent = strokeTangent;
            hasStrokePoint = true;
            return;
        }

        float distance = Vector3.Distance(lastStrokePoint, worldPoint);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.05f));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 point = Vector3.Lerp(lastStrokePoint, worldPoint, t);
            Vector3 stepTangent = Vector3.Slerp(lastStrokeTangent == Vector3.zero ? strokeTangent : lastStrokeTangent, strokeTangent, t).normalized;
            TryCreateStamp(point, normal, stepTangent, rollerContactSize, 0.9f);
        }

        lastStrokePoint = worldPoint;
        lastStrokeNormal = normal;
        lastStrokeTangent = strokeTangent;
        hasStrokePoint = true;
    }

    private void TryCreateStamp(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 stampSize, float alpha)
    {
        if (!TryGetSurfaceCoords(worldPoint, normal, out Vector2 uv, out Vector2 edgeDistancesWorld))
        {
            return;
        }

        Vector2 finalStampSize = ClampRectSizeToEdge(stampSize, edgeDistancesWorld);
        if (finalStampSize.x < 0.035f || finalStampSize.y < 0.018f)
        {
            return;
        }

        int newlyCovered = CoverRectCells(
            uv,
            Mathf.Max(1.2f, finalStampSize.x / Mathf.Max(0.001f, rollerContactSize.x) * 1.85f),
            Mathf.Max(1.0f, finalStampSize.y / Mathf.Max(0.001f, rollerContactSize.y) * 1.65f));
        progress = coveredCells.Count / (float)Mathf.Max(1, CountPaintableCells());
        if (newlyCovered > 0)
        {
            UpdateGrimeOpacity();
        }
        StampDirtMask(uv, finalStampSize, tangent, alpha);
        if (newlyCovered > 0)
        {
            CreateCleanShine(worldPoint, normal, tangent, finalStampSize);
        }

        if (progress >= completionThreshold)
        {
            progress = 1f;
            CompletePaintMask();
            if (grimeOverlay != null)
            {
                grimeOverlay.SetActive(false);
            }
        }
    }

    private void CreateGrimeOverlay()
    {
        grimeOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        grimeOverlay.name = "Wall Grime Overlay";
        grimeOverlay.transform.SetParent(transform, false);

        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 localPosition = GetPlanePoint(allowedNormal) - allowedNormal * 0.01f;
        grimeOverlay.transform.localPosition = localPosition;
        grimeOverlay.transform.localRotation = Quaternion.LookRotation(-allowedNormal, Vector3.up);
        grimeOverlay.transform.localScale = new Vector3(0.985f, 0.985f, 1f);

        Renderer overlayRenderer = grimeOverlay.GetComponent<Renderer>();
        Material sourceMaterial = grimeMaterial != null ? grimeMaterial : (oldMaterial != null ? oldMaterial : freshMaterial);
        Material overlayMaterial = sourceMaterial != null
            ? new Material(sourceMaterial)
            : new Material(Shader.Find("Standard"));
        Color baseColor = oldMaterial != null ? oldMaterial.color : new Color(0.78f, 0.70f, 0.65f, 1f);
        overlayMaterial.color = new Color(baseColor.r * 0.84f, baseColor.g * 0.80f, baseColor.b * 0.76f, 0.9f);
        overlayRenderer.material = overlayMaterial;

        Collider overlayCollider = grimeOverlay.GetComponent<Collider>();
        if (overlayCollider != null)
        {
            Destroy(overlayCollider);
        }
    }

    private void UpdateGrimeOpacity()
    {
        if (grimeOverlay == null)
        {
            return;
        }

        Renderer overlayRenderer = grimeOverlay.GetComponent<Renderer>();
        if (overlayRenderer == null)
        {
            return;
        }

        Color color = overlayRenderer.material.color;
        color.a = Mathf.Lerp(0.9f, 0.08f, progress);
        overlayRenderer.material.color = color;
    }

    private Vector3 ResolveStrokeTangent(Vector3 worldPoint, Vector3 normal)
    {
        Vector3 baseAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.8f ? Vector3.right : Vector3.up;
        Vector3 altAxis = Vector3.Cross(normal, baseAxis).normalized;
        if (altAxis.sqrMagnitude < 0.0001f)
        {
            altAxis = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
        }

        return (rollerQuarterTurns % 2 == 0 ? baseAxis : altAxis).normalized;
    }

    private Quaternion BuildStrokeRotation(Vector3 normal, Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(transform.position, normal);
        Vector3 up = Vector3.Cross(-normal, safeTangent).normalized;
        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
        }
        return Quaternion.LookRotation(-normal, up);
    }

    private bool TryGetSurfaceCoords(Vector3 worldPoint, Vector3 worldNormal, out Vector2 uv, out Vector2 edgeDistancesWorld)
    {
        uv = default;
        edgeDistancesWorld = Vector2.zero;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 localNormal = transform.InverseTransformDirection(worldNormal).normalized;
        Vector3 allowedNormal = GetAllowedNormal();
        if (Vector3.Dot(localNormal, allowedNormal) < 0.82f)
        {
            return false;
        }

        float depth;
        float a;
        float b;
        Vector3 axisA;
        Vector3 axisB;

        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            float sign = Mathf.Sign(allowedNormal.x);
            depth = Mathf.Abs(localPoint.x - (0.5f * sign));
            a = localPoint.z;
            b = localPoint.y;
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            float sign = Mathf.Sign(allowedNormal.y);
            depth = Mathf.Abs(localPoint.y - (0.5f * sign));
            a = localPoint.x;
            b = localPoint.z;
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            float sign = Mathf.Sign(allowedNormal.z);
            depth = Mathf.Abs(localPoint.z - (0.5f * sign));
            a = localPoint.x;
            b = localPoint.y;
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        if (depth > 0.03f)
        {
            return false;
        }

        float min = -0.5f + faceInset;
        float max = 0.5f - faceInset;
        if (a < min || a > max || b < min || b > max)
        {
            return false;
        }
        if (IsInExclusionLocal(a, b))
        {
            return false;
        }

        float edgeA = Mathf.Min(a - min, max - a);
        float edgeB = Mathf.Min(b - min, max - b);
        edgeDistancesWorld = new Vector2(LocalDistanceToWorld(axisA, edgeA), LocalDistanceToWorld(axisB, edgeB));
        uv = new Vector2(Mathf.InverseLerp(min, max, a), Mathf.InverseLerp(min, max, b));
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private void StampDirtMask(Vector2 uv, Vector2 stampSizeWorld, Vector3 tangent, float strength)
    {
        Vector2 surfaceSize = GetSurfaceWorldSize();
        bool rotated = rollerQuarterTurns % 2 == 0;
        Vector2 brushSizeUv = rotated
            ? new Vector2(
                stampSizeWorld.y / Mathf.Max(0.001f, surfaceSize.x),
                stampSizeWorld.x / Mathf.Max(0.001f, surfaceSize.y))
            : new Vector2(
                stampSizeWorld.x / Mathf.Max(0.001f, surfaceSize.x),
                stampSizeWorld.y / Mathf.Max(0.001f, surfaceSize.y));
        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, uv, brushSizeUv * 0.62f, Vector2.right, Mathf.Max(0.08f, strength * 0.24f), GetExclusionUvRects());
    }

    private void CreateCleanShine(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 brushSize)
    {
        if (Time.time - lastCleanShineTime < 0.055f)
        {
            return;
        }

        lastCleanShineTime = Time.time;
        GameObject shine = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shine.name = "Wall Clean Shine";
        shine.transform.SetParent(transform.parent, true);
        shine.transform.position = worldPoint + normal.normalized * 0.018f;
        shine.transform.rotation = BuildStrokeRotation(normal, tangent);
        shine.transform.localScale = new Vector3(Mathf.Max(0.04f, brushSize.x * 0.78f), Mathf.Max(0.025f, brushSize.y * 0.92f), 1f);

        Renderer shineRenderer = shine.GetComponent<Renderer>();
        Material shineMaterial = new Material(Shader.Find("Unlit/Transparent"));
        shineMaterial.color = new Color(1f, 1f, 1f, 0.72f);
        shineRenderer.material = shineMaterial;

        Collider collider = shine.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        CozyCleanShineFx fx = shine.AddComponent<CozyCleanShineFx>();
        fx.runtimeMaterial = shineMaterial;
        fx.lifetime = 0.32f;
        fx.pulseScale = 1.18f;
    }

    private void CompletePaintMask()
    {
        Rect[] exclusionRectsUv = GetExclusionUvRects();
        if (exclusionRectsUv == null || exclusionRectsUv.Length == 0)
        {
            CozyDirtMaskRenderer.Fill(dirtMask, Color.black);
            return;
        }

        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), Vector2.right, 1f, exclusionRectsUv);
    }

    private Rect[] GetExclusionUvRects()
    {
        if (paintExclusionRectsLocal == null || paintExclusionRectsLocal.Length == 0)
        {
            return null;
        }

        float min = -0.5f + faceInset;
        float max = 0.5f - faceInset;
        Rect[] rects = new Rect[paintExclusionRectsLocal.Length];
        for (int i = 0; i < paintExclusionRectsLocal.Length; i++)
        {
            Rect local = paintExclusionRectsLocal[i];
            float xMin = Mathf.InverseLerp(min, max, local.xMin);
            float yMin = Mathf.InverseLerp(min, max, local.yMin);
            float xMax = Mathf.InverseLerp(min, max, local.xMax);
            float yMax = Mathf.InverseLerp(min, max, local.yMax);
            rects[i] = Rect.MinMaxRect(
                Mathf.Clamp01(Mathf.Min(xMin, xMax)),
                Mathf.Clamp01(Mathf.Min(yMin, yMax)),
                Mathf.Clamp01(Mathf.Max(xMin, xMax)),
                Mathf.Clamp01(Mathf.Max(yMin, yMax)));
        }

        return rects;
    }

    private Vector2 WorldTangentToMaskTangent(Vector3 tangent)
    {
        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 axisA;
        Vector3 axisB;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        Vector3 worldAxisA = transform.TransformDirection(axisA).normalized;
        Vector3 worldAxisB = transform.TransformDirection(axisB).normalized;
        Vector2 maskTangent = new Vector2(Vector3.Dot(tangent.normalized, worldAxisA), Vector3.Dot(tangent.normalized, worldAxisB));
        return maskTangent.sqrMagnitude > 0.0001f ? maskTangent.normalized : Vector2.right;
    }

    private Vector2 GetSurfaceWorldSize()
    {
        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 axisA;
        Vector3 axisB;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        float localSize = Mathf.Max(0.001f, 1f - faceInset * 2f);
        return new Vector2(LocalDistanceToWorld(axisA, localSize), LocalDistanceToWorld(axisB, localSize));
    }

    private void ConfigureMaskProjection()
    {
        if (runtimeSurfaceMaterial == null)
        {
            return;
        }

        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 axisA;
        Vector3 axisB;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        Vector2 surfaceSize = GetSurfaceWorldSize();
        runtimeSurfaceMaterial.SetVector("_MaskOrigin", transform.TransformPoint(GetPlanePoint(allowedNormal)));
        runtimeSurfaceMaterial.SetVector("_MaskAxisA", transform.TransformDirection(axisA).normalized);
        runtimeSurfaceMaterial.SetVector("_MaskAxisB", transform.TransformDirection(axisB).normalized);
        runtimeSurfaceMaterial.SetVector("_MaskSize", new Vector4(surfaceSize.x, surfaceSize.y, 0f, 0f));
    }

    private Vector2 ClampRectSizeToEdge(Vector2 desiredSize, Vector2 edgeDistancesWorld)
    {
        return new Vector2(
            Mathf.Min(desiredSize.x, edgeDistancesWorld.x * 2f * 0.92f),
            Mathf.Min(desiredSize.y, edgeDistancesWorld.y * 2f * 0.92f));
    }

    private float LocalDistanceToWorld(Vector3 localAxis, float localDistance)
    {
        return transform.TransformVector(localAxis.normalized * Mathf.Max(0f, localDistance)).magnitude;
    }

    private Vector3 GetAllowedNormal()
    {
        return allowedPaintNormalLocal.sqrMagnitude > 0.0001f ? allowedPaintNormalLocal.normalized : Vector3.back;
    }

    private Vector3 GetPlanePoint(Vector3 allowedNormal)
    {
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            return new Vector3(0.5f * Mathf.Sign(allowedNormal.x), 0f, 0f);
        }

        if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            return new Vector3(0f, 0.5f * Mathf.Sign(allowedNormal.y), 0f);
        }

        return new Vector3(0f, 0f, 0.5f * Mathf.Sign(allowedNormal.z));
    }

    private int CoverRectCells(Vector2 uv, float halfWidthCells, float halfHeightCells)
    {
        int centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (coverageGridX - 1)), 0, coverageGridX - 1);
        int centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (coverageGridY - 1)), 0, coverageGridY - 1);
        int radiusX = Mathf.Max(1, Mathf.CeilToInt(halfWidthCells));
        int radiusY = Mathf.Max(1, Mathf.CeilToInt(halfHeightCells));
        int added = 0;

        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            if (y < 0 || y >= coverageGridY) continue;
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || x >= coverageGridX) continue;
                float dx = x - centerX;
                float dy = y - centerY;
                if (Mathf.Abs(dx) > halfWidthCells || Mathf.Abs(dy) > halfHeightCells)
                {
                    continue;
                }
                if (IsExcludedCell(x, y))
                {
                    continue;
                }

                int key = y * coverageGridX + x;
                if (coveredCells.Add(key))
                {
                    added++;
                }
            }
        }

        return added;
    }

    private int CountPaintableCells()
    {
        int count = 0;
        for (int y = 0; y < coverageGridY; y++)
        {
            for (int x = 0; x < coverageGridX; x++)
            {
                if (!IsExcludedCell(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsExcludedCell(int x, int y)
    {
        if (paintExclusionRectsLocal == null || paintExclusionRectsLocal.Length == 0)
        {
            return false;
        }

        float min = -0.5f + faceInset;
        float max = 0.5f - faceInset;
        float a = Mathf.Lerp(min, max, x / (float)Mathf.Max(1, coverageGridX - 1));
        float b = Mathf.Lerp(min, max, y / (float)Mathf.Max(1, coverageGridY - 1));
        return IsInExclusionLocal(a, b);
    }

    private bool IsInExclusionLocal(float a, float b)
    {
        if (paintExclusionRectsLocal == null)
        {
            return false;
        }

        for (int i = 0; i < paintExclusionRectsLocal.Length; i++)
        {
            Rect rect = paintExclusionRectsLocal[i];
            if (a >= rect.xMin && a <= rect.xMax && b >= rect.yMin && b <= rect.yMax)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class CozyMoppableFloor : MonoBehaviour
{
    public Material cleanMaterial;
    public Material grimeMaterial;
    public Material brushMaterial;
    public int coverageGridX = 44;
    public int coverageGridY = 34;
    public float completionThreshold = 0.95f;
    public float edgePadding = 0.04f;
    public Vector2 rollerContactSize = new Vector2(0.60f, 0.20f);

    private float coverage;
    private GameObject grimeOverlay;
    private Material runtimeSurfaceMaterial;
    private RenderTexture dirtMask;
    private RenderTexture dirtMaskScratch;
    private Vector3 lastStrokePoint;
    private Vector3 lastStrokeNormal;
    private Vector3 lastStrokeTangent;
    private bool hasStrokePoint;
    private float lastCleanShineTime = -1f;
    private readonly HashSet<int> cleanedCells = new HashSet<int>();
    private int rollerQuarterTurns;

    public bool IsClean => coverage >= 1f;
    public float CleanPercent => Mathf.Clamp01(coverage);

    private void Awake()
    {
        InitializeSurface();
    }

    public void InitializeSurface()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            CozyDirtMaskRenderer.Release(dirtMask);
            CozyDirtMaskRenderer.Release(dirtMaskScratch);
            dirtMask = CozyDirtMaskRenderer.CreateDirtyMask(name + " Dirt Mask");
            dirtMaskScratch = null;
            runtimeSurfaceMaterial = CozyDirtMaskRenderer.CreateMaskedSurfaceMaterial(cleanMaterial, grimeMaterial, "CR_" + name + "_MaskedClean");
            runtimeSurfaceMaterial.SetTexture("_DirtMask", dirtMask);
            ConfigureMaskProjection();
            renderer.material = runtimeSurfaceMaterial;
        }
    }

    private void OnDestroy()
    {
        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
    }

    public void CleanAt(Vector3 worldPoint, Vector3 normal, Vector3 tangent, float amount)
    {
        if (IsClean)
        {
            return;
        }

        CreateCleanStroke(worldPoint, normal, tangent);
    }

    private void CreateGrimeOverlay()
    {
        grimeOverlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grimeOverlay.name = "Floor Grime Overlay";
        grimeOverlay.transform.SetParent(transform, false);
        grimeOverlay.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        grimeOverlay.transform.localScale = new Vector3(0.985f, 0.04f, 0.985f);
        Renderer renderer = grimeOverlay.GetComponent<Renderer>();
        renderer.sharedMaterial = grimeMaterial;
        renderer.material.color = new Color(0.38f, 0.31f, 0.28f, 0.92f);
        Collider collider = grimeOverlay.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private void CreateCleanStroke(Vector3 worldPoint, Vector3 normal, Vector3 tangent)
    {
        Vector3 strokeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(worldPoint, normal);
        if (!hasStrokePoint || Vector3.Distance(lastStrokePoint, worldPoint) > 1.4f || Vector3.Dot(lastStrokeNormal, normal) < 0.9f)
        {
            TryCreateCleanStamp(worldPoint, normal, strokeTangent, rollerContactSize, 0.98f);
            lastStrokePoint = worldPoint;
            lastStrokeNormal = normal;
            lastStrokeTangent = strokeTangent;
            hasStrokePoint = true;
            return;
        }

        float distance = Vector3.Distance(lastStrokePoint, worldPoint);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.018f));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 point = Vector3.Lerp(lastStrokePoint, worldPoint, t);
            Vector3 stepTangent = Vector3.Slerp(lastStrokeTangent == Vector3.zero ? strokeTangent : lastStrokeTangent, strokeTangent, t).normalized;
            TryCreateCleanStamp(point, normal, stepTangent, rollerContactSize, 0.96f);
        }

        lastStrokePoint = worldPoint;
        lastStrokeNormal = normal;
        lastStrokeTangent = strokeTangent;
        hasStrokePoint = true;
    }

    public void EndStroke()
    {
        hasStrokePoint = false;
        lastStrokeTangent = Vector3.zero;
    }

    public bool TryProjectRay(Ray ray, out Vector3 worldPoint, out Vector3 worldNormal, out float distance)
    {
        worldPoint = default;
        worldNormal = Vector3.up;
        distance = 0f;

        Vector3 planeNormal = transform.up;
        Vector3 planePoint = transform.TransformPoint(new Vector3(0f, 0.5f, 0f));
        Plane plane = new Plane(planeNormal, planePoint);
        if (!plane.Raycast(ray, out float enter) || enter < 0f)
        {
            return false;
        }

        Vector3 candidatePoint = ray.GetPoint(enter);
        Vector3 localPoint = transform.InverseTransformPoint(candidatePoint);
        float limitX = 0.5f + edgePadding;
        float limitZ = 0.5f + edgePadding;
        if (Mathf.Abs(localPoint.y - 0.5f) > 0.04f || localPoint.x < -limitX || localPoint.x > limitX || localPoint.z < -limitZ || localPoint.z > limitZ)
        {
            return false;
        }

        worldPoint = candidatePoint;
        worldNormal = planeNormal;
        distance = enter;
        return true;
    }

    public void RotateRoller90()
    {
        rollerQuarterTurns = (rollerQuarterTurns + 1) % 4;
        EndStroke();
    }

    public void SetRollerQuarterTurns(int quarterTurns)
    {
        rollerQuarterTurns = ((quarterTurns % 4) + 4) % 4;
    }

    private void TryCreateCleanStamp(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 stampSize, float alpha)
    {
        if (!TryGetFloorCoords(worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld))
        {
            return;
        }

        Vector2 finalStampSize = ClampRectSizeToEdge(stampSize, edgeDistancesWorld);
        if (finalStampSize.x < 0.035f || finalStampSize.y < 0.018f)
        {
            return;
        }

        int newlyCovered = CoverRectCells(
            uv,
            Mathf.Max(1.25f, finalStampSize.x / Mathf.Max(0.001f, rollerContactSize.x) * 1.95f),
            Mathf.Max(1.0f, finalStampSize.y / Mathf.Max(0.001f, rollerContactSize.y) * 1.7f));
        coverage = cleanedCells.Count / (float)Mathf.Max(1, coverageGridX * coverageGridY);
        if (newlyCovered > 0)
        {
            UpdateGrimeOpacity();
        }
        StampDirtMask(uv, finalStampSize, tangent, alpha);
        if (newlyCovered > 0)
        {
            CreateCleanShine(worldPoint, normal, tangent, finalStampSize);
        }

        if (coverage >= completionThreshold)
        {
            coverage = 1f;
            CozyDirtMaskRenderer.Fill(dirtMask, Color.black);
            if (grimeOverlay != null)
            {
                grimeOverlay.SetActive(false);
            }
        }
    }

    private void CreateWetSheen(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 brushSize)
    {
        GameObject wet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wet.name = "Wet Sheen";
        wet.transform.SetParent(transform.parent, true);
        wet.transform.position = worldPoint + normal * 0.01f;
        wet.transform.rotation = BuildStrokeRotation(normal, tangent);
        Vector2 wetSize = brushSize * Random.Range(0.72f, 0.9f);
        wet.transform.localScale = new Vector3(Mathf.Max(0.03f, wetSize.x), Mathf.Max(0.016f, wetSize.y), 1f);
        Renderer wetRenderer = wet.GetComponent<Renderer>();
        Material wetMaterial = new Material(brushMaterial != null ? brushMaterial : cleanMaterial);
        wetMaterial.color = new Color(0.84f, 0.95f, 1f, 0.24f);
        wetRenderer.material = wetMaterial;
        Collider collider = wet.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        CozyWetPatchFx fx = wet.AddComponent<CozyWetPatchFx>();
        fx.runtimeMaterial = wetMaterial;
    }

    private void UpdateGrimeOpacity()
    {
        if (grimeOverlay == null)
        {
            return;
        }

        Renderer renderer = grimeOverlay.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.material.color;
        color.a = Mathf.Lerp(0.92f, 0.18f, CleanPercent);
        renderer.material.color = color;
    }

    private bool TryGetFloorCoords(Vector3 worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld)
    {
        uv = default;
        edgeDistancesWorld = Vector2.zero;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (Mathf.Abs(localPoint.y) > 0.56f)
        {
            return false;
        }

        float min = -0.5f - edgePadding;
        float max = 0.5f + edgePadding;
        if (localPoint.x < min || localPoint.x > max || localPoint.z < min || localPoint.z > max)
        {
            return false;
        }

        edgeDistancesWorld = new Vector2(
            LocalDistanceToWorld(Vector3.right, Mathf.Min(localPoint.x - min, max - localPoint.x)),
            LocalDistanceToWorld(Vector3.forward, Mathf.Min(localPoint.z - min, max - localPoint.z)));
        uv = new Vector2(Mathf.InverseLerp(min, max, localPoint.x), Mathf.InverseLerp(min, max, localPoint.z));
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private void StampDirtMask(Vector2 uv, Vector2 stampSizeWorld, Vector3 tangent, float strength)
    {
        Vector3 floorSize = transform.lossyScale;
        Vector2 brushSizeUv = new Vector2(
            stampSizeWorld.x / Mathf.Max(0.001f, floorSize.x),
            stampSizeWorld.y / Mathf.Max(0.001f, floorSize.z));
        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, uv, brushSizeUv * 0.62f, WorldTangentToMaskTangent(tangent), Mathf.Max(0.08f, strength * 0.24f));
    }

    private void CreateCleanShine(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 brushSize)
    {
        if (Time.time - lastCleanShineTime < 0.055f)
        {
            return;
        }

        lastCleanShineTime = Time.time;
        GameObject shine = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shine.name = "Floor Clean Shine";
        shine.transform.SetParent(transform.parent, true);
        shine.transform.position = worldPoint + normal.normalized * 0.018f;
        shine.transform.rotation = BuildStrokeRotation(normal, tangent);
        shine.transform.localScale = new Vector3(Mathf.Max(0.04f, brushSize.x * 0.78f), Mathf.Max(0.025f, brushSize.y * 0.92f), 1f);

        Renderer shineRenderer = shine.GetComponent<Renderer>();
        Material shineMaterial = new Material(Shader.Find("Unlit/Transparent"));
        shineMaterial.color = new Color(1f, 1f, 1f, 0.68f);
        shineRenderer.material = shineMaterial;

        Collider collider = shine.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        CozyCleanShineFx fx = shine.AddComponent<CozyCleanShineFx>();
        fx.runtimeMaterial = shineMaterial;
        fx.lifetime = 0.34f;
        fx.pulseScale = 1.16f;
    }

    private Vector2 WorldTangentToMaskTangent(Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        Vector2 maskTangent = new Vector2(Vector3.Dot(safeTangent, transform.right.normalized), Vector3.Dot(safeTangent, transform.forward.normalized));
        return maskTangent.sqrMagnitude > 0.0001f ? maskTangent.normalized : Vector2.right;
    }

    private void ConfigureMaskProjection()
    {
        if (runtimeSurfaceMaterial == null)
        {
            return;
        }

        Vector3 axisA = transform.right.normalized;
        Vector3 axisB = transform.forward.normalized;
        Vector3 size = transform.lossyScale;
        float surfaceWidth = Mathf.Max(0.001f, size.x * (1f + edgePadding * 2f));
        float surfaceDepth = Mathf.Max(0.001f, size.z * (1f + edgePadding * 2f));
        runtimeSurfaceMaterial.SetVector("_MaskOrigin", transform.TransformPoint(new Vector3(0f, 0.5f, 0f)));
        runtimeSurfaceMaterial.SetVector("_MaskAxisA", axisA);
        runtimeSurfaceMaterial.SetVector("_MaskAxisB", axisB);
        runtimeSurfaceMaterial.SetVector("_MaskSize", new Vector4(surfaceWidth, surfaceDepth, 0f, 0f));
    }

    private int CoverRectCells(Vector2 uv, float halfWidthCells, float halfHeightCells)
    {
        int centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (coverageGridX - 1)), 0, coverageGridX - 1);
        int centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (coverageGridY - 1)), 0, coverageGridY - 1);
        int radiusX = Mathf.Max(1, Mathf.CeilToInt(halfWidthCells));
        int radiusY = Mathf.Max(1, Mathf.CeilToInt(halfHeightCells));
        int added = 0;

        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            if (y < 0 || y >= coverageGridY) continue;
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || x >= coverageGridX) continue;
                float dx = x - centerX;
                float dy = y - centerY;
                if (Mathf.Abs(dx) > halfWidthCells || Mathf.Abs(dy) > halfHeightCells)
                {
                    continue;
                }

                int key = y * coverageGridX + x;
                if (cleanedCells.Add(key))
                {
                    added++;
                }
            }
        }

        return added;
    }

    private Vector3 ResolveStrokeTangent(Vector3 worldPoint, Vector3 normal)
    {
        Vector3 baseAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.8f ? Vector3.right : Vector3.up;
        Vector3 altAxis = Vector3.Cross(normal, baseAxis).normalized;
        if (altAxis.sqrMagnitude < 0.0001f)
        {
            altAxis = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
        }

        return (rollerQuarterTurns % 2 == 0 ? baseAxis : altAxis).normalized;
    }

    private Quaternion BuildStrokeRotation(Vector3 normal, Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(transform.position, normal);
        Vector3 up = Vector3.Cross(-normal, safeTangent).normalized;
        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
        }
        return Quaternion.LookRotation(-normal, up);
    }

    private Vector2 ClampRectSizeToEdge(Vector2 desiredSize, Vector2 edgeDistancesWorld)
    {
        return new Vector2(
            Mathf.Min(desiredSize.x, edgeDistancesWorld.x * 2f * 0.92f),
            Mathf.Min(desiredSize.y, edgeDistancesWorld.y * 2f * 0.92f));
    }

    private float LocalDistanceToWorld(Vector3 localAxis, float localDistance)
    {
        return transform.TransformVector(localAxis.normalized * Mathf.Max(0f, localDistance)).magnitude;
    }
}

public sealed class CozyWetPatchFx : MonoBehaviour
{
    public Material runtimeMaterial;
    private float elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / 0.7f);
        if (runtimeMaterial != null)
        {
            Color color = runtimeMaterial.color;
            color.a = Mathf.Lerp(0.32f, 0f, t);
            runtimeMaterial.color = color;
        }

        transform.localScale = new Vector3(transform.localScale.x * 0.998f, transform.localScale.y, transform.localScale.z * 0.998f);
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class CozyCleanShineFx : MonoBehaviour
{
    public Material runtimeMaterial;
    public float lifetime = 0.32f;
    public float pulseScale = 1.16f;

    private float elapsed;
    private Vector3 startScale;
    private Color startColor;

    private void Awake()
    {
        startScale = transform.localScale;
        startColor = runtimeMaterial != null ? runtimeMaterial.color : Color.white;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        float pulse = Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.Lerp(startScale, startScale * pulseScale, pulse);

        if (runtimeMaterial != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            runtimeMaterial.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class CozyWindowSparkleFx : MonoBehaviour
{
    public Material runtimeMaterial;
    public float lifetime = 0.55f;
    public float pulseScale = 1.6f;

    private float elapsed;
    private Vector3 startScale;
    private Quaternion startRotation;

    private void Awake()
    {
        startScale = transform.localScale;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        float pulse = Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.Lerp(startScale, startScale * pulseScale, pulse);
        transform.rotation = startRotation * Quaternion.AngleAxis(t * 80f, Vector3.forward);

        if (runtimeMaterial != null)
        {
            Color color = runtimeMaterial.color;
            color.a = Mathf.Lerp(color.a, 0f, t);
            runtimeMaterial.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class CozyTransientStrokeFx : MonoBehaviour
{
    public Material runtimeMaterial;
    public float lifetime = 1.35f;
    public float startAlpha = 1f;

    private float elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        if (runtimeMaterial != null)
        {
            Color color = runtimeMaterial.color;
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            runtimeMaterial.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class CozyTidyObject : MonoBehaviour
{
    public bool IsTidied { get; private set; }

    public void TidyAway()
    {
        if (IsTidied)
        {
            return;
        }
        IsTidied = true;
        CozyRemovalFx fx = gameObject.GetComponent<CozyRemovalFx>();
        if (fx == null)
        {
            fx = gameObject.AddComponent<CozyRemovalFx>();
        }
        fx.Play();
    }
}

public sealed class CozyRemovalFx : MonoBehaviour
{
    private bool isPlaying;
    private float elapsed;
    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer[] renderers;
    private Material[] runtimeMaterials;
    private readonly List<Transform> particles = new List<Transform>();

    public void Play()
    {
        if (isPlaying)
        {
            return;
        }

        isPlaying = true;
        elapsed = 0f;
        startScale = transform.localScale;
        startPosition = transform.position;
        renderers = GetComponentsInChildren<Renderer>(true);
        runtimeMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            runtimeMaterials[i] = renderers[i].material;
        }

        for (int i = 0; i < 8; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            particle.name = "Removal Particle";
            particle.transform.position = transform.position + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.6f), Random.Range(-0.25f, 0.25f));
            particle.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);
            Renderer particleRenderer = particle.GetComponent<Renderer>();
            particleRenderer.material.color = new Color(1f, 0.95f, 0.82f, 0.9f);
            Destroy(particle.GetComponent<Collider>());
            particle.transform.SetParent(transform.parent, true);
            particles.Add(particle.transform);
        }
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / 0.32f);
        transform.localScale = Vector3.Lerp(startScale, startScale * 0.7f, t);
        transform.position = Vector3.Lerp(startPosition, startPosition + Vector3.up * 0.18f, t);

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Color color = runtimeMaterials[i].color;
            color.a = Mathf.Lerp(1f, 0f, t);
            runtimeMaterials[i].color = color;
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            if (particles[i] == null)
            {
                particles.RemoveAt(i);
                continue;
            }

            particles[i].position += new Vector3(Random.Range(-0.15f, 0.15f), 0.8f, Random.Range(-0.15f, 0.15f)) * Time.deltaTime;
            particles[i].localScale *= 0.985f;
            Renderer particleRenderer = particles[i].GetComponent<Renderer>();
            if (particleRenderer != null)
            {
                Color color = particleRenderer.material.color;
                color.a = Mathf.Lerp(0.9f, 0f, t);
                particleRenderer.material.color = color;
            }
        }

        if (t >= 1f)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i] != null)
                {
                    Destroy(particles[i].gameObject);
                }
            }
            gameObject.SetActive(false);
        }
    }
}

public sealed class CozyDecorTemplate : MonoBehaviour
{
    public string displayName;

    public static Quaternion GetPlacementRotation(GameObject decorObject, float yaw)
    {
        CozyDecorTemplate marker = decorObject != null ? decorObject.GetComponent<CozyDecorTemplate>() : null;
        string name = marker != null && !string.IsNullOrEmpty(marker.displayName)
            ? marker.displayName
            : (decorObject != null ? decorObject.name : string.Empty);
        return GetPlacementRotation(name, yaw);
    }

    public static Quaternion GetPlacementRotation(string displayName, float yaw)
    {
        return KeepsOriginalRotation(displayName)
            ? Quaternion.Euler(0f, yaw, 0f)
            : Quaternion.Euler(270f, yaw, 0f);
    }

    public static void SnapBottomToFloor(GameObject decorObject, float floorY)
    {
        if (decorObject == null)
        {
            return;
        }

        Renderer[] renderers = decorObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        decorObject.transform.position += Vector3.up * (floorY - bounds.min.y);
    }

    private static bool KeepsOriginalRotation(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        string normalizedName = displayName.ToLowerInvariant();
        return normalizedName.Contains("lamp") || normalizedName.Contains("chair") || normalizedName.Contains("rug");
    }
}

public sealed class CozyMovableDecor : MonoBehaviour
{
}

public sealed class CozyDecorTrashBin : MonoBehaviour
{
}

public sealed class CozyProgressTracker : MonoBehaviour
{
    public int decorGoal = 6;

    private CozyDirtPatch[] dirtPatches = new CozyDirtPatch[0];
    private CozyCleanableWindow[] windows = new CozyCleanableWindow[0];
    private CozyMoppableFloor[] floors = new CozyMoppableFloor[0];
    private CozyPaintableSurface[] paintableSurfaces = new CozyPaintableSurface[0];
    private CozyTidyObject[] tidyObjects = new CozyTidyObject[0];
    private readonly List<GameObject> placedDecor = new List<GameObject>();

    public float NormalizedProgress
    {
        get
        {
            int total = dirtPatches.Length + windows.Length + floors.Length + paintableSurfaces.Length + tidyObjects.Length + decorGoal;
            if (total == 0)
            {
                return 1f;
            }

            int done = 0;
            for (int i = 0; i < dirtPatches.Length; i++) if (dirtPatches[i] == null || dirtPatches[i].IsClean) done++;
            for (int i = 0; i < windows.Length; i++) if (windows[i] == null || windows[i].IsClean) done++;
            for (int i = 0; i < floors.Length; i++) if (floors[i] == null || floors[i].IsClean) done++;
            for (int i = 0; i < paintableSurfaces.Length; i++) if (paintableSurfaces[i] == null || paintableSurfaces[i].IsPainted) done++;
            for (int i = 0; i < tidyObjects.Length; i++) if (tidyObjects[i] == null || tidyObjects[i].IsTidied) done++;
            done += Mathf.Min(decorGoal, placedDecor.Count);
            return Mathf.Clamp01(done / (float)total);
        }
    }

    public void RefreshTargets()
    {
        dirtPatches = FindObjectsOfType<CozyDirtPatch>(true);
        windows = FindObjectsOfType<CozyCleanableWindow>(true);
        floors = FindObjectsOfType<CozyMoppableFloor>(true);
        paintableSurfaces = FindObjectsOfType<CozyPaintableSurface>(true);
        tidyObjects = FindObjectsOfType<CozyTidyObject>(true);
    }

    public void AddPlacedDecor(GameObject placed)
    {
        if (placed != null && !placedDecor.Contains(placed))
        {
            placedDecor.Add(placed);
        }
    }

    public void RemovePlacedDecor(GameObject placed)
    {
        if (placed != null)
        {
            placedDecor.Remove(placed);
        }
    }
}
