using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyRestoreBootstrap : MonoBehaviour
{
    private const string DecorPrefabFolder = "Assets/Prefabs";
    private const string CleaningToolPrefabFolder = "Assets/Prefabs/cleanning_tool";
    private const float DecorScaleMultiplier = 1.5f;
    private const float LargeDecorScaleMultiplier = 2f;
    public bool buildOnPlay = true;
    public bool rebuildExistingRoom = true;
    public string roomRootName = "CozyRestore_Room";
    public string decorThemeFolder = "gothic";
    private bool showingLobby;
    private Camera lobbyCamera;
    private Light lobbyLight;

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
            ShowLobby();
        }
    }

    [ContextMenu("Build Cozy Restore Prototype")]
    public void BuildPrototype()
    {
        showingLobby = false;
        ClearLobbyObjects();

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
        CreateDust(root.transform);
        CreateObstacles(root.transform);
        CreateDecorTrashBin(root.transform);
        GameObject[] templates = CreateDecorTemplates(root.transform);
        GameObject player = CreateCameraRig(root.transform, templates);
        CreateLighting(root.transform);

        CozyProgressTracker tracker = root.AddComponent<CozyProgressTracker>();
        tracker.RefreshTargets();
        player.GetComponent<CozyToolController>().progressTracker = tracker;
        player.GetComponent<CozyToolController>().heldMaterial = heldMaterial;

        Debug.Log("CozyRestore cutaway prototype ready. Drag to orbit, WASD pan, wheel zoom, 1-7 tools.");
    }

    private void ShowLobby()
    {
        showingLobby = true;

        GameObject existingRoom = GameObject.Find(roomRootName);
        if (existingRoom != null && rebuildExistingRoom)
        {
            DestroyNow(existingRoom);
        }

        ClearLobbyObjects();

        GameObject cameraObject = new GameObject("CozyRestore_LobbyCamera");
        lobbyCamera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        lobbyCamera.transform.position = new Vector3(0f, 2.1f, -7.2f);
        lobbyCamera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        lobbyCamera.fieldOfView = 48f;
        lobbyCamera.clearFlags = CameraClearFlags.SolidColor;
        lobbyCamera.backgroundColor = new Color(0.12f, 0.15f, 0.18f);

        GameObject lightObject = new GameObject("CozyRestore_LobbyLight");
        lobbyLight = lightObject.AddComponent<Light>();
        lobbyLight.type = LightType.Directional;
        lobbyLight.intensity = 0.65f;
        lobbyLight.color = new Color(1f, 0.91f, 0.82f);
        lobbyLight.transform.rotation = Quaternion.Euler(35f, -28f, 0f);
    }

    private void StartLevel(string themeFolder)
    {
        decorThemeFolder = themeFolder;
        BuildPrototype();
    }

    public void ReturnToLobby()
    {
        GameObject existingRoom = GameObject.Find(roomRootName);
        if (existingRoom != null)
        {
            DestroyNow(existingRoom);
        }

        ShowLobby();
    }

    private void ClearLobbyObjects()
    {
        if (lobbyCamera != null)
        {
            RetireLobbyObject(lobbyCamera.gameObject);
            DestroyNow(lobbyCamera.gameObject);
            lobbyCamera = null;
        }

        if (lobbyLight != null)
        {
            RetireLobbyObject(lobbyLight.gameObject);
            DestroyNow(lobbyLight.gameObject);
            lobbyLight = null;
        }

        GameObject oldCamera = GameObject.Find("CozyRestore_LobbyCamera");
        if (oldCamera != null)
        {
            RetireLobbyObject(oldCamera);
            DestroyNow(oldCamera);
        }

        GameObject oldLight = GameObject.Find("CozyRestore_LobbyLight");
        if (oldLight != null)
        {
            RetireLobbyObject(oldLight);
            DestroyNow(oldLight);
        }
    }

    private void RetireLobbyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (target.CompareTag("MainCamera"))
        {
            target.tag = "Untagged";
        }

        target.SetActive(false);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showingLobby)
        {
            return;
        }

        float panelWidth = Mathf.Min(520f, Screen.width - 48f);
        float panelHeight = 330f;
        Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.color = new Color(0.05f, 0.06f, 0.07f, 0.92f);
        GUI.Box(panelRect, GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(panelRect.x + 32f, panelRect.y + 28f, panelRect.width - 64f, panelRect.height - 56f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 34;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(1f, 0.88f, 0.68f);
        GUILayout.Label("Cozy Restore", titleStyle, GUILayout.Height(54f));

        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = 16;
        bodyStyle.alignment = TextAnchor.MiddleCenter;
        bodyStyle.wordWrap = true;
        bodyStyle.normal.textColor = new Color(0.86f, 0.88f, 0.88f);
        GUILayout.Label("디자인할 방의 분위기를 선택하세요.", bodyStyle, GUILayout.Height(42f));

        GUILayout.Space(18f);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 20;
        buttonStyle.fontStyle = FontStyle.Bold;
        if (GUILayout.Button("모던풍 레벨 시작", buttonStyle, GUILayout.Height(58f)))
        {
            StartLevel("modern");
        }

        GUILayout.Space(12f);

        if (GUILayout.Button("고딕풍 레벨 시작", buttonStyle, GUILayout.Height(58f)))
        {
            StartLevel("gothic");
        }

        GUILayout.Space(16f);
        GUILayout.Label("선택한 테마 폴더의 프리팹만 인테리어 목록에 표시됩니다.", bodyStyle, GUILayout.Height(34f));
        GUILayout.EndArea();
    }

    private void CreateMaterials()
    {
        rollerBrushTexture = LoadBrushTexture();
        if (IsModernTheme())
        {
            floorMaterial = MakeMaterial("Modern Floor", new Color(0.78f, 0.79f, 0.76f), 0.58f);
            wallMaterial = MakeMaterial("Modern Wall", new Color(0.91f, 0.93f, 0.92f), 0.82f);
            paintedMaterial = MakeMaterial("Modern Fresh Paint", new Color(0.70f, 0.78f, 0.82f), 0.68f);
            trimMaterial = MakeMaterial("Modern Trim", new Color(0.18f, 0.20f, 0.22f), 0.35f);
            woodMaterial = MakeMaterial("Modern Wood", new Color(0.70f, 0.58f, 0.45f), 0.55f);
            fabricMaterial = MakeMaterial("Modern Fabric", new Color(0.64f, 0.70f, 0.73f), 0.82f);
            metalMaterial = MakeMaterial("Modern Metal", new Color(0.74f, 0.76f, 0.75f), 0.22f);
            rugMaterial = MakeMaterial("Modern Rug", new Color(0.56f, 0.63f, 0.65f), 0.72f);
            curtainMaterial = MakeMaterial("Modern Curtain", new Color(0.82f, 0.86f, 0.88f), 0.72f);
        }
        else
        {
            floorMaterial = MakeMaterial("Pastel Floor", new Color(0.86f, 0.74f, 0.66f), 0.8f);
            wallMaterial = MakeMaterial("Pastel Wall", new Color(0.95f, 0.90f, 0.88f), 0.95f);
            paintedMaterial = MakeMaterial("Fresh Paint", new Color(0.78f, 0.88f, 0.90f), 0.9f);
            trimMaterial = MakeMaterial("Trim", new Color(0.69f, 0.61f, 0.66f), 0.7f);
            woodMaterial = MakeMaterial("Wood", new Color(0.86f, 0.68f, 0.56f), 0.75f);
            fabricMaterial = MakeMaterial("Fabric", new Color(0.78f, 0.68f, 0.85f), 0.95f);
            metalMaterial = MakeMaterial("Metal", new Color(0.98f, 0.86f, 0.63f), 0.4f);
            rugMaterial = MakeMaterial("Rug", new Color(0.94f, 0.82f, 0.84f), 0.95f);
            curtainMaterial = MakeMaterial("Curtain", new Color(0.87f, 0.84f, 0.95f), 0.95f);
        }

        dirtMaterial = MakeMaterial("Dust", new Color(0.43f, 0.34f, 0.31f, 0.78f), 0.95f);
        glassMaterial = MakeMaterial("Glass", new Color(0.80f, 0.92f, 0.98f, 0.33f), 0.35f);
        previewMaterial = MakeMaterial("Preview", new Color(0.52f, 0.95f, 0.80f, 0.45f), 0.55f);
        plantMaterial = MakeMaterial("Plant", new Color(0.63f, 0.82f, 0.63f), 0.85f);
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
        surface.paintGroupId = objectName.StartsWith("Back Wall") ? "Back Wall" : string.Empty;
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
        PlacePrefabDecor(root, "bed", new Vector3(-3.8f, 0f, -2.8f), 90f);
    }

    private GameObject PlacePrefabDecor(Transform parent, string prefabName, Vector3 position, float yaw)
    {
#if UNITY_EDITOR
        string prefabPath = ResolveExistingDecorPrefabPath(prefabName);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
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

    private string GetActiveDecorPrefabFolder()
    {
        string theme = string.IsNullOrEmpty(decorThemeFolder) ? string.Empty : decorThemeFolder.Trim('/', '\\');
        return string.IsNullOrEmpty(theme) ? DecorPrefabFolder : DecorPrefabFolder + "/" + theme;
    }

    private string ResolveDecorPrefabPath(string prefabName)
    {
        string cleanName = string.IsNullOrEmpty(prefabName) ? string.Empty : prefabName.Trim('/', '\\');
        if (cleanName.Contains("/"))
        {
            return DecorPrefabFolder + "/" + cleanName + ".prefab";
        }

        return GetActiveDecorPrefabFolder() + "/" + cleanName + ".prefab";
    }

#if UNITY_EDITOR
    private string ResolveExistingDecorPrefabPath(string prefabName)
    {
        string prefabPath = ResolveDecorPrefabPath(prefabName);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            return prefabPath;
        }

        string cleanName = Path.GetFileNameWithoutExtension(prefabName);
        if (string.IsNullOrEmpty(cleanName))
        {
            return prefabPath;
        }

        string needle = cleanName.ToLowerInvariant();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { GetActiveDecorPrefabFolder() });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            string candidateName = Path.GetFileNameWithoutExtension(candidatePath);
            if (string.IsNullOrEmpty(candidateName))
            {
                continue;
            }

            string normalizedName = candidateName.ToLowerInvariant();
            if (normalizedName == needle || normalizedName.EndsWith("_" + needle))
            {
                return candidatePath;
            }
        }

        return prefabPath;
    }
#endif

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
        ResolveInitialObstacleOverlaps(root);
    }

    private void ResolveInitialObstacleOverlaps(Transform root)
    {
        CozyTidyObject[] obstacles = root.GetComponentsInChildren<CozyTidyObject>(true);
        for (int pass = 0; pass < 6; pass++)
        {
            bool changed = false;
            for (int i = 0; i < obstacles.Length; i++)
            {
                CozyTidyObject obstacle = obstacles[i];
                if (obstacle == null)
                {
                    continue;
                }

                changed |= KeepObstacleInsideRoom(obstacle.gameObject, root);
                changed |= PushObstacleAwayFromDecor(obstacle.gameObject, root);
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private bool KeepObstacleInsideRoom(GameObject obstacle, Transform root)
    {
        if (!TryGetWorldBounds(obstacle, out Bounds bounds))
        {
            return false;
        }

        const float wallPadding = 0.18f;
        Vector3 localCenter = root.InverseTransformPoint(bounds.center);
        Vector3 localExtents = root.InverseTransformVector(bounds.extents);
        localExtents = new Vector3(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y), Mathf.Abs(localExtents.z));

        float minX = -RoomWidth * 0.5f + wallPadding + localExtents.x;
        float maxX = RoomWidth * 0.5f - wallPadding - localExtents.x;
        float minZ = -RoomDepth * 0.5f + wallPadding + localExtents.z;
        float maxZ = RoomDepth * 0.5f - wallPadding - localExtents.z;
        Vector3 clampedCenter = new Vector3(
            Mathf.Clamp(localCenter.x, minX, maxX),
            localCenter.y,
            Mathf.Clamp(localCenter.z, minZ, maxZ));

        Vector3 localDelta = clampedCenter - localCenter;
        if (localDelta.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        obstacle.transform.position += root.TransformVector(localDelta);
        return true;
    }

    private bool PushObstacleAwayFromDecor(GameObject obstacle, Transform root)
    {
        if (!TryGetWorldBounds(obstacle, out Bounds obstacleBounds))
        {
            return false;
        }

        bool moved = false;
        CozyMovableDecor[] decorObjects = root.GetComponentsInChildren<CozyMovableDecor>(true);
        for (int i = 0; i < decorObjects.Length; i++)
        {
            CozyMovableDecor decor = decorObjects[i];
            if (decor == null || decor.transform.IsChildOf(obstacle.transform))
            {
                continue;
            }

            if (!TryGetWorldBounds(decor.gameObject, out Bounds decorBounds))
            {
                continue;
            }

            Vector2 obstacleMin = new Vector2(obstacleBounds.min.x, obstacleBounds.min.z);
            Vector2 obstacleMax = new Vector2(obstacleBounds.max.x, obstacleBounds.max.z);
            Vector2 decorMin = new Vector2(decorBounds.min.x, decorBounds.min.z);
            Vector2 decorMax = new Vector2(decorBounds.max.x, decorBounds.max.z);
            const float padding = 0.16f;
            float overlapX = Mathf.Min(obstacleMax.x, decorMax.x + padding) - Mathf.Max(obstacleMin.x, decorMin.x - padding);
            float overlapZ = Mathf.Min(obstacleMax.y, decorMax.y + padding) - Mathf.Max(obstacleMin.y, decorMin.y - padding);
            if (overlapX <= 0f || overlapZ <= 0f)
            {
                continue;
            }

            Vector3 pushDirection = obstacleBounds.center - decorBounds.center;
            if (Mathf.Abs(pushDirection.x) < 0.001f && Mathf.Abs(pushDirection.z) < 0.001f)
            {
                pushDirection = Vector3.right;
            }

            Vector3 push = Mathf.Abs(pushDirection.x) > Mathf.Abs(pushDirection.z)
                ? new Vector3(Mathf.Sign(pushDirection.x) * overlapX, 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(pushDirection.z) * overlapZ);
            obstacle.transform.position += push;
            obstacleBounds.center += push;
            moved = true;
        }

        return moved;
    }

    private bool TryGetWorldBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private GameObject[] CreateDecorTemplates(Transform root)
    {
        GameObject library = new GameObject("Decor Template Library");
        library.transform.SetParent(root);

        List<GameObject> templates = new List<GameObject>();
#if UNITY_EDITOR
        string activeDecorFolder = GetActiveDecorPrefabFolder();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { activeDecorFolder });
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
            if (IsFloorFinishPrefabPath(prefabPaths[i]))
            {
                continue;
            }

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

        GameObject rugTemplate = CreateRugDecorTemplate(library.transform);
        templates.Add(rugTemplate);

        if (templates.Count == 0)
        {
            Debug.LogWarning("No decor prefabs found in " + GetActiveDecorPrefabFolder() + ". Decorate mode will be empty.");
        }

        library.SetActive(false);
        return templates.ToArray();
    }

    private bool IsFloorFinishPrefabPath(string prefabPath)
    {
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        string normalizedName = name.ToLowerInvariant();
        return normalizedName.Contains("woodtile")
            || normalizedName.Contains("marbletile")
            || normalizedName.Contains("moderntile")
            || normalizedName == "tile";
    }

    private GameObject CreateRugDecorTemplate(Transform library)
    {
        GameObject rug = Cube("Room Rug", library, Vector3.zero, new Vector3(4.8f, 0.02f, 3.1f), rugMaterial, null);
        PreparePrefabDecorTemplate(rug, "Room Rug");
        return rug;
    }

    private void PreparePrefabDecorTemplate(GameObject template, string displayName)
    {
        template.name = displayName;
        template.transform.localPosition = Vector3.zero;
        template.transform.localRotation = CozyDecorTemplate.GetPlacementRotation(displayName, 0f);
        ApplyDecorScale(template, displayName);

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

    private void ApplyDecorScale(GameObject template, string displayName)
    {
        if (TryGetFixedDecorLocalScale(displayName, out float fixedScale))
        {
            template.transform.localScale = Vector3.one * fixedScale;
            return;
        }

        template.transform.localScale *= GetDecorScaleMultiplier(displayName);
    }

    private bool TryGetFixedDecorLocalScale(string displayName, out float scale)
    {
        scale = 0f;
        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        string normalizedName = displayName.ToLowerInvariant();
        if (normalizedName.Contains("tvtable") || normalizedName.Contains("tv table"))
        {
            scale = 150f;
            return true;
        }

        if (normalizedName.Contains("bed"))
        {
            scale = 150f;
            return true;
        }

        if (normalizedName.Contains("sofa"))
        {
            scale = 150f;
            return true;
        }

        if (normalizedName.Contains("refrigerator"))
        {
            scale = 2f;
            return true;
        }

        if (normalizedName.Contains("desk"))
        {
            scale = 75f;
            return true;
        }

        if (normalizedName.Contains("chair"))
        {
            scale = 50f;
            return true;
        }

        if (normalizedName.Contains("table"))
        {
            scale = 75f;
            return true;
        }

        return false;
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
        tools.lobbyBootstrap = this;
        tools.decorTemplates = templates;
        tools.decorThemeFolder = decorThemeFolder;
        tools.previewMaterial = previewMaterial;
        tools.paintRollerVisualPrefab = LoadCleaningToolPrefab("roller");
        tools.floorMopVisualPrefab = LoadCleaningToolPrefab("floor_mop");
        tools.windowWiperVisualPrefab = LoadCleaningToolPrefab("Squeegee");
        tools.floorFinishPrefab = LoadFloorFinishPrefab();
        tools.reach = 40f;

        return rig;
    }

    private GameObject LoadFloorFinishPrefab()
    {
#if UNITY_EDITOR
        string[] candidateNames = IsModernTheme() ? new[] { "MarbleTile", "ModernTile", "Tile" } : new[] { "WoodTile" };
        for (int i = 0; i < candidateNames.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetActiveDecorPrefabFolder() + "/" + candidateNames[i] + ".prefab");
            if (prefab != null)
            {
                return prefab;
            }
        }
#endif
        return null;
    }

    private bool IsModernTheme()
    {
        return !string.IsNullOrEmpty(decorThemeFolder) && decorThemeFolder.ToLowerInvariant().Contains("modern");
    }

    private GameObject LoadCleaningToolPrefab(string prefabName)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(CleaningToolPrefabFolder + "/" + prefabName + ".prefab");
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
