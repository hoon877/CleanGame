using System.IO;
using System.Collections.Generic;
using UnityEngine;

public sealed class CozyToolController : MonoBehaviour
{
    public Camera viewCamera;
    public CozyRestoreBootstrap lobbyBootstrap;
    public CozyProgressTracker progressTracker;
    public GameObject[] decorTemplates;
    public string decorThemeFolder = "gothic";
    public Material previewMaterial;
    public Material heldMaterial;
    public GameObject paintRollerVisualPrefab;
    public GameObject floorMopVisualPrefab;
    public GameObject windowWiperVisualPrefab;
    public GameObject floorFinishPrefab;
    public CozyGameAudio gameAudio;
    public float reach = 40f;
    public float cleanRate = 1.1f;
    public float paintRate = 0.7f;

    private CozyToolMode mode = CozyToolMode.Inspect;
    private int decorIndex;
    private float decorYaw;
    private GameObject previewInstance;
    private GameObject floorFinishPreview;
    private Transform floorFinishRoot;
    private readonly Dictionary<Vector2Int, GameObject> installedFloorFinishes = new Dictionary<Vector2Int, GameObject>();
    private CozyMovableDecor movingDecor;
    private CozyPaintableSurface activePaintSurface;
    private CozyMoppableFloor activeMopFloor;
    private CozyCleanableWindow activeWindow;
    private GameObject windowWiperVisual;
    private int windowWiperQuarterTurns;
    private Vector3 windowWiperLastPoint;
    private bool hasWindowWiperLastPoint;
    private float windowWiperWobble;
    private bool pickedThisFrame;
    private bool previewIsHeldDecor;
    private string status = "Restore the room.";
    private string targetProgressLabel = "Target: -";
    private float toolHotbarVisibleUntil;
    private const float RollerVisualModelX = 100f;
    private const float ToolHotbarDuration = 1.25f;
    private const float DecorPlacementRoomHalfWidth = 6.82f;
    private const float DecorPlacementRoomHalfDepth = 5.32f;
    private const float DecorPlacementPadding = 0.08f;
    private const float ModernFloorFinishCellSize = 1.24f;
    private const float GothicWoodTileSpacingX = 0.6f;
    private const float GothicWoodTileSpacingZ = 1.12f;
    private const float FloorFinishThickness = 0.035f;
    private const float FloorFinishBorderOverhang = 0.65f;
    private int floorFinishQuarterTurns;
    private bool floorFinishPreviewHasValidity;
    private bool floorFinishPreviewLastValid;
    private readonly Vector3 rollerVisualModelOffset = new Vector3(0f, -0.01f, -0.14f);
    private RollerVisualState paintRollerVisual;
    private RollerVisualState wetMopRollerVisual;
    private CozyCleaningAudio cleaningAudio;
    private bool controlsGuideVisible = true;
    private bool objectivesPanelVisible;
    private bool pausePanelVisible;
    private float lastNonZeroVolume = 1f;
    private const string VolumePrefsKey = "CozyRestore.MasterVolume";
    private const string WindowedPrefsKey = "CozyRestore.Windowed";

    public bool BlocksRightMouseOrbit => controlsGuideVisible || pausePanelVisible;

    private sealed class RollerVisualState
    {
        public string visualName;
        public GameObject prefab;
        public float modelZ;
        public float fixedLocalScale;
        public bool useFixedLocalRotation;
        public Vector3 fixedLocalEuler;
        public bool alignBottomToSurface;
        public GameObject instance;
        public Transform pivot;
        public Vector3 centerOffset;
        public int quarterTurns;
        public float press;
        public float jolt;
        public float dynamicYaw = 45f;
        public float wobble;
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
        cleaningAudio = GetComponent<CozyCleaningAudio>();
        if (cleaningAudio == null)
        {
            cleaningAudio = gameObject.AddComponent<CozyCleaningAudio>();
        }
        cleaningAudio.EnsureReady();
        if (gameAudio == null)
        {
            gameAudio = FindObjectOfType<CozyGameAudio>();
        }
        lastNonZeroVolume = Mathf.Max(0.01f, PlayerPrefs.GetFloat(VolumePrefsKey, Mathf.Max(0.01f, AudioListener.volume)));
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        HandleControlsGuideInput();
        if (!controlsGuideVisible)
        {
            HandlePausePanelInput();
        }

        if (controlsGuideVisible || pausePanelVisible)
        {
            EndContinuousActions();
            cleaningAudio?.StopLoop();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        pickedThisFrame = false;
        HandleModeKeys();
        if (!Input.GetMouseButton(0))
        {
            EndContinuousActions();
            cleaningAudio?.StopLoop();
        }
        HandleRayTools();
        HandleDecorPreview();
        UpdateCursorState();
        UpdateRollerVisual();
        UpdateWindowWiperVisual();
    }

    private void OnGUI()
    {
        GUIStyle statusLabelStyle = new GUIStyle(GUI.skin.label);
        statusLabelStyle.alignment = TextAnchor.MiddleLeft;
        statusLabelStyle.clipping = TextClipping.Overflow;

        GUI.Box(new Rect(14, 14, 230, 76), "Cleaning Status");
        GUI.Label(new Rect(24, 34, 210, 24), targetProgressLabel, statusLabelStyle);
        GUI.Label(new Rect(24, 58, 210, 24), "Tool: " + GetToolDisplayName(mode), statusLabelStyle);

        float progress = progressTracker != null ? progressTracker.NormalizedProgress : 0f;
        GUI.Box(new Rect(14, 96, 230, 22), string.Empty);
        GUI.Box(new Rect(22, 103, 214, 8), string.Empty);
        GUI.Box(new Rect(22, 103, 214 * progress, 8), string.Empty);

        if (controlsGuideVisible)
        {
            DrawControlsGuidePanel();
            return;
        }

        if (pausePanelVisible)
        {
            DrawPausePanel();
            return;
        }

        DrawObjectivesButton();
        if (objectivesPanelVisible)
        {
            DrawObjectivesPanel();
        }

        if (progress >= 0.999f)
        {
            DrawCompletionLobbyButton();
        }

        if (Time.time <= toolHotbarVisibleUntil)
        {
            DrawToolHotbar();
        }
    }

    private void HandleControlsGuideInput()
    {
        if (!controlsGuideVisible)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Escape))
        {
            controlsGuideVisible = false;
        }
    }

    private void HandlePausePanelInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pausePanelVisible = !pausePanelVisible;
            objectivesPanelVisible = false;
        }
    }

    private void DrawPausePanel()
    {
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.46f);
        GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        float panelWidth = Mathf.Min(360f, Screen.width - 40f);
        float panelHeight = 250f;
        Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.fontSize = 20;
        panelStyle.fontStyle = FontStyle.Bold;
        GUI.Box(panelRect, "옵션", panelStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 15;
        buttonStyle.fontStyle = FontStyle.Bold;

        float buttonX = panelRect.x + 42f;
        float buttonWidth = panelRect.width - 84f;
        float y = panelRect.y + 52f;

        if (GUI.Button(new Rect(buttonX, y, buttonWidth, 34f), "게임 계속하기", buttonStyle))
        {
            pausePanelVisible = false;
        }

        y += 44f;
        if (GUI.Button(new Rect(buttonX, y, buttonWidth, 34f), AudioListener.volume > 0.001f ? "볼륨 끄기" : "볼륨 켜기", buttonStyle))
        {
            ToggleGameVolume();
        }

        y += 44f;
        bool isWindowed = Screen.fullScreenMode == FullScreenMode.Windowed;
        if (GUI.Button(new Rect(buttonX, y, buttonWidth, 34f), isWindowed ? "전체화면으로 전환" : "창모드로 전환", buttonStyle))
        {
            ToggleWindowMode();
        }

        y += 44f;
        if (GUI.Button(new Rect(buttonX, y, buttonWidth, 34f), "로비로 돌아가기", buttonStyle))
        {
            pausePanelVisible = false;
            lobbyBootstrap?.ReturnToLobby();
        }

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.alignment = TextAnchor.MiddleCenter;
        hintStyle.fontSize = 12;
        hintStyle.normal.textColor = new Color(0.80f, 0.86f, 0.90f);
        GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + panelRect.height - 28f, panelRect.width - 36f, 18f), "ESC를 다시 누르면 닫힙니다.", hintStyle);
    }

    private void ToggleGameVolume()
    {
        if (AudioListener.volume > 0.001f)
        {
            lastNonZeroVolume = Mathf.Max(0.01f, AudioListener.volume);
            AudioListener.volume = 0f;
        }
        else
        {
            AudioListener.volume = Mathf.Clamp01(lastNonZeroVolume);
        }

        PlayerPrefs.SetFloat(VolumePrefsKey, AudioListener.volume);
        PlayerPrefs.Save();
    }

    private void ToggleWindowMode()
    {
        bool nextWindowed = Screen.fullScreenMode != FullScreenMode.Windowed;
        Screen.SetResolution(Screen.width, Screen.height, nextWindowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
        PlayerPrefs.SetInt(WindowedPrefsKey, nextWindowed ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void DrawCompletionLobbyButton()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 13;
        buttonStyle.fontStyle = FontStyle.Bold;
        if (GUI.Button(new Rect(Screen.width - 110f, 56f, 92f, 32f), "로비", buttonStyle))
        {
            lobbyBootstrap?.ReturnToLobby();
        }
    }

    private void DrawObjectivesButton()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 13;
        buttonStyle.fontStyle = FontStyle.Bold;
        if (GUI.Button(new Rect(Screen.width - 110f, 18f, 92f, 32f), objectivesPanelVisible ? "목표 닫기" : "목표", buttonStyle))
        {
            objectivesPanelVisible = !objectivesPanelVisible;
        }
    }

    private void DrawObjectivesPanel()
    {
        float panelWidth = 374f;
        float panelHeight = 244f;
        Rect panelRect = new Rect(Screen.width - panelWidth - 18f, 58f, panelWidth, panelHeight);

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.fontSize = 18;
        panelStyle.fontStyle = FontStyle.Bold;
        GUI.Box(panelRect, "목표", panelStyle);

        bool obstacleDone = progressTracker != null && progressTracker.ObstaclesCleared;
        bool surfaceDone = progressTracker != null && progressTracker.SurfaceWorkComplete;
        bool floorFinishDone = IsFloorFinishObjectiveComplete();
        bool decorDone = progressTracker != null && progressTracker.HasPlacedDecor;

        float rowX = panelRect.x + 18f;
        float rowY = panelRect.y + 42f;
        float rowWidth = panelRect.width - 36f;
        DrawObjectiveRow(new Rect(rowX, rowY, rowWidth, 40f), "1. 장애물 치우기", obstacleDone);
        DrawObjectiveRow(new Rect(rowX, rowY + 44f, rowWidth, 48f), "2. 바닥 청소 / 창문 닦기 / 벽 페인트칠", surfaceDone);
        DrawObjectiveRow(new Rect(rowX, rowY + 96f, rowWidth, 40f), "3. 바닥 타일깔기", floorFinishDone);
        DrawObjectiveRow(new Rect(rowX, rowY + 140f, rowWidth, 40f), "4. 인테리어 배치", decorDone);
    }

    private void DrawObjectiveRow(Rect rect, string label, bool completed, string detail = null)
    {
        Color previousColor = GUI.color;
        GUI.color = completed ? new Color(0.20f, 0.42f, 0.26f, 0.58f) : new Color(0.06f, 0.07f, 0.09f, 0.52f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleLeft;
        labelStyle.fontSize = rect.height > 42f ? 13 : 14;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.wordWrap = true;
        labelStyle.normal.textColor = completed ? new Color(0.74f, 0.84f, 0.75f) : Color.white;

        Rect labelRect = new Rect(rect.x + 12f, rect.y + 4f, rect.width - 96f, rect.height - 8f);
        GUI.Label(labelRect, label, labelStyle);

        if (!string.IsNullOrEmpty(detail) && !completed)
        {
            GUIStyle detailStyle = new GUIStyle(GUI.skin.label);
            detailStyle.alignment = TextAnchor.LowerLeft;
            detailStyle.fontSize = 11;
            detailStyle.normal.textColor = new Color(0.78f, 0.84f, 0.90f);
            GUI.Label(new Rect(labelRect.x, rect.y + rect.height - 18f, labelRect.width, 16f), detail, detailStyle);
        }

        if (completed)
        {
            GUI.color = new Color(0.82f, 1f, 0.80f, 0.95f);
            GUI.DrawTexture(new Rect(labelRect.x, rect.y + rect.height * 0.50f, labelRect.width, 2f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        GUIStyle doneStyle = new GUIStyle(GUI.skin.label);
        doneStyle.alignment = TextAnchor.MiddleCenter;
        doneStyle.fontSize = 12;
        doneStyle.fontStyle = FontStyle.Bold;
        doneStyle.normal.textColor = completed ? new Color(0.70f, 1f, 0.70f) : new Color(1f, 0.86f, 0.58f);
        GUI.Label(new Rect(rect.xMax - 78f, rect.y + 4f, 66f, rect.height - 8f), completed ? "[완료]" : "진행중", doneStyle);
    }

    private bool IsFloorFinishObjectiveComplete()
    {
        return installedFloorFinishes.Count >= GetFloorFinishObjectiveRequiredCount();
    }

    private int GetFloorFinishObjectiveRequiredCount()
    {
        CozyMoppableFloor floor = FindObjectOfType<CozyMoppableFloor>();
        if (floor == null)
        {
            return 1;
        }

        Vector2Int cellCounts = GetFloorFinishCellCounts(floor.transform.lossyScale, GetFloorFinishCellSize());
        int totalCells = Mathf.Max(1, cellCounts.x * cellCounts.y);
        return totalCells;
    }

    private void DrawControlsGuidePanel()
    {
        float panelWidth = Mathf.Min(520f, Screen.width - 40f);
        float panelHeight = 356f;
        Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.46f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.fontSize = 22;
        panelStyle.fontStyle = FontStyle.Bold;
        panelStyle.normal.textColor = Color.white;
        GUI.Box(panelRect, "조작 안내", panelStyle);

        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = 16;
        bodyStyle.fontStyle = FontStyle.Bold;
        bodyStyle.normal.textColor = new Color(0.96f, 0.97f, 1f);
        bodyStyle.richText = true;
        bodyStyle.wordWrap = true;

        string guideText =
            "<color=#ffd98a>1 ~ 7 숫자키</color> : 도구 변경\n" +
            "<color=#ffd98a>마우스 좌클릭 / 드래그</color> : 청소, 페인트칠, 배치 실행\n" +
            "<color=#ffd98a>마우스 우클릭 드래그</color> : 카메라 각도 변경\n" +
            "<color=#ffd98a>마우스 휠</color> : 줌 인 / 줌 아웃\n" +
            "<color=#ffd98a>W A S D</color> : 카메라 위치 이동\n" +
            "<color=#ffd98a>Tab</color> : 브러시 또는 타일 방향 90도 회전\n" +
            "<color=#ffd98a>Z / X</color> : 인테리어 종류 이전 / 다음 변경\n" +
            "<color=#ffd98a>Q / E</color> : 인테리어 배치 방향 회전";

        GUI.Label(new Rect(panelRect.x + 34f, panelRect.y + 58f, panelRect.width - 68f, 218f), guideText, bodyStyle);

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.alignment = TextAnchor.MiddleCenter;
        hintStyle.fontSize = 13;
        hintStyle.normal.textColor = new Color(0.78f, 0.84f, 0.90f);
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + panelRect.height - 74f, panelRect.width - 56f, 24f), "Enter / Space / Esc 로도 닫을 수 있습니다.", hintStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 16;
        buttonStyle.fontStyle = FontStyle.Bold;
        if (GUI.Button(new Rect(panelRect.center.x - 55f, panelRect.y + panelRect.height - 44f, 110f, 30f), "확인", buttonStyle))
        {
            controlsGuideVisible = false;
        }
    }

    private void DrawToolHotbar()
    {
        CozyToolMode[] modes =
        {
            CozyToolMode.Inspect,
            CozyToolMode.WetMop,
            CozyToolMode.Paint,
            CozyToolMode.WindowWiper,
            CozyToolMode.Decorate,
            CozyToolMode.Move,
            CozyToolMode.FloorFinish
        };

        float slotSize = 58f;
        float spacing = 6f;
        float totalWidth = modes.Length * slotSize + (modes.Length - 1) * spacing;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - 92f;
        GUIStyle toolLabelStyle = new GUIStyle(GUI.skin.label);
        toolLabelStyle.alignment = TextAnchor.MiddleCenter;
        toolLabelStyle.fontSize = 12;
        toolLabelStyle.fontStyle = FontStyle.Bold;
        toolLabelStyle.wordWrap = true;
        toolLabelStyle.normal.textColor = Color.white;

        GUIStyle numberStyle = new GUIStyle(GUI.skin.label);
        numberStyle.alignment = TextAnchor.MiddleCenter;
        numberStyle.fontSize = 13;
        numberStyle.fontStyle = FontStyle.Bold;
        numberStyle.normal.textColor = new Color(1f, 0.90f, 0.68f);

        for (int i = 0; i < modes.Length; i++)
        {
            Rect slot = new Rect(startX + i * (slotSize + spacing), y, slotSize, slotSize);
            GUI.Box(slot, string.Empty);
            if (modes[i] == mode)
            {
                GUI.Box(new Rect(slot.x - 4f, slot.y - 4f, slot.width + 8f, slot.height + 8f), string.Empty);
            }

            GUI.Label(new Rect(slot.x + 4f, slot.y + 8f, slot.width - 8f, 28f), GetToolGlyph(modes[i]), toolLabelStyle);
            GUI.Label(new Rect(slot.x + 6f, slot.y + 38f, slot.width - 12f, 16f), (i + 1).ToString(), numberStyle);
        }
    }

    private string GetToolGlyph(CozyToolMode toolMode)
    {
        switch (toolMode)
        {
            case CozyToolMode.Inspect: return "Clean";
            case CozyToolMode.WetMop: return "Floor";
            case CozyToolMode.Paint: return "Roller";
            case CozyToolMode.WindowWiper: return "Wiper";
            case CozyToolMode.Decorate: return "Decor";
            case CozyToolMode.Move: return "Move";
            case CozyToolMode.FloorFinish: return "Finish";
            default: return toolMode.ToString();
        }
    }

    private string GetToolDisplayName(CozyToolMode toolMode)
    {
        switch (toolMode)
        {
            case CozyToolMode.Inspect: return "Cleanup";
            case CozyToolMode.WetMop: return "Floor Mop";
            case CozyToolMode.Paint: return "Roller";
            case CozyToolMode.WindowWiper: return "Squeegee";
            case CozyToolMode.Decorate: return "Decorate";
            case CozyToolMode.Move: return "Move";
            case CozyToolMode.FloorFinish: return "Floor Finish";
            default: return toolMode.ToString();
        }
    }

    private string GetPaintTargetDisplayName(CozyPaintableSurface surface)
    {
        if (surface == null)
        {
            return "-";
        }

        return surface.HasPaintGroup ? surface.paintGroupId : surface.name;
    }

    private void HandleModeKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(CozyToolMode.Inspect);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(CozyToolMode.WetMop);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(CozyToolMode.Paint);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetMode(CozyToolMode.WindowWiper);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetMode(CozyToolMode.Decorate);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SetMode(CozyToolMode.Move);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SetMode(CozyToolMode.FloorFinish);

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

        if (mode == CozyToolMode.FloorFinish)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                floorFinishQuarterTurns = (floorFinishQuarterTurns + 1) % 4;
                status = "Floor Finish: rotated";
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
        ClearFloorFinishPreview();
        mode = nextMode;
        toolHotbarVisibleUntil = Time.time + ToolHotbarDuration;
        status = "Switched to " + mode;
    }

    private void HandleRayTools()
    {
        if (viewCamera == null)
        {
            return;
        }

        Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);

        if (mode == CozyToolMode.FloorFinish)
        {
            HandleFloorFinishTool(ray);
            return;
        }

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
                targetProgressLabel = "Floor: " + Mathf.RoundToInt(floorMess.CleanPercent * 100f) + "%";
                status = "Wet Mop: " + Mathf.RoundToInt(floorMess.CleanPercent * 100f) + "% clean";
                if (Input.GetMouseButton(0))
                {
                    bool wasClean = floorMess.IsClean;
                    Vector3 floorTangent = RotateStampTangent90(floorNormal, ResolveRollerTangent(floorNormal, wetMopRollerVisual != null ? wetMopRollerVisual.quarterTurns : 0));
                    floorMess.CleanAt(floorPoint, floorNormal, floorTangent, cleanRate * Time.deltaTime);
                    if (!wasClean)
                    {
                        cleaningAudio?.PlayLoop(CozyCleaningLoopSound.FloorMop);
                    }
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
            targetProgressLabel = "Floor: -";
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
                targetProgressLabel = "Wall: " + GetPaintTargetDisplayName(surface) + "  " + Mathf.RoundToInt(surface.DisplayPaintPercent * 100f) + "%";
                status = "Paint: " + Mathf.RoundToInt(surface.DisplayPaintPercent * 100f) + "% covered";
                if (Input.GetMouseButton(0))
                {
                    bool wasPainted = surface.DisplayIsPainted;
                    Vector3 paintTangent = RotateStampTangent90(paintNormal, ResolveRollerTangent(paintNormal, paintRollerVisual != null ? paintRollerVisual.quarterTurns : 0));
                    surface.PaintAt(paintPoint, paintNormal, paintTangent, paintRate * Time.deltaTime);
                    if (!wasPainted)
                    {
                        cleaningAudio?.PlayLoop(CozyCleaningLoopSound.PaintRoller);
                    }
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
            targetProgressLabel = "Wall: -";
            return;
        }

        if (mode == CozyToolMode.WindowWiper)
        {
            if (TryGetWindowTarget(ray, out CozyCleanableWindow window, out Vector3 windowPoint, out Vector3 windowNormal))
            {
                if (activeWindow != null && activeWindow != window)
                {
                    activeWindow.EndStroke();
                }

                activeWindow = window;
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    windowWiperQuarterTurns = (windowWiperQuarterTurns + 1) % 4;
                    activeWindow.EndStroke();
                    status = "Window Wiper: squeegee rotated";
                    return;
                }

                targetProgressLabel = "Window: " + Mathf.RoundToInt(window.CleanPercent * 100f) + "%";
                status = "Window Wiper: " + Mathf.RoundToInt(window.CleanPercent * 100f) + "% clean";
                if (Input.GetMouseButton(0))
                {
                    bool wasClean = window.IsClean;
                    Vector3 tangent = ResolveRollerTangent(windowNormal, windowWiperQuarterTurns);
                    window.CleanAt(windowPoint, windowNormal, tangent, cleanRate * Time.deltaTime);
                    if (!wasClean)
                    {
                        cleaningAudio?.PlayLoop(CozyCleaningLoopSound.WindowWiper);
                    }
                    progressTracker?.RefreshTargets();
                }
                return;
            }

            if (activeWindow != null)
            {
                activeWindow.EndStroke();
                activeWindow = null;
            }
            status = "Window Wiper: drag across the dirty window.";
            targetProgressLabel = "Window: -";
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
            if (obstacle != null)
            {
                status = "Cleanup: click to remove " + obstacle.name;
                if (Input.GetMouseButtonDown(0))
                {
                    cleaningAudio?.PlayOneShot(CozyCleaningOneShotSound.ObstacleRemove);
                    obstacle.TidyAway();
                    progressTracker?.RefreshTargets();
                    status = "Removed " + obstacle.name;
                }
                return;
            }

            CozyDirtPatch dirt = hit.collider.GetComponentInParent<CozyDirtPatch>();
            status = dirt != null ? "Cleanup: " + Mathf.RoundToInt(dirt.CleanPercent * 100f) + "% clean" : "Cleanup: remove dust, clutter, and obstacles.";
            if (dirt != null && Input.GetMouseButtonDown(0))
            {
                cleaningAudio?.PlayOneShot(CozyCleaningOneShotSound.DustClean);
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
                previewInstance.transform.SetParent(GetRuntimeObjectRoot(), true);
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

        bool surfaceHit = TryGetDecorPlacementSurface(ray, out RaycastHit hit, out GameObject supportObject, out string surfaceReason);
        if (!surfaceHit)
        {
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }
            status = mode == CozyToolMode.Move ? "Move: pick furniture or point at a valid surface." : "Decorate: " + surfaceReason;
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
        bool canPlaceDecor = IsDecorPlacementValid(previewInstance, movingDecor != null ? movingDecor.gameObject : null, supportObject, out string placementReason);
        status = canPlaceDecor
            ? (mode == CozyToolMode.Move ? (movingDecor != null ? "Move: place selected decor or click trash bin to discard" : "Move: click furniture to pick it up") : "Decorate: place " + CurrentDecorName)
            : "Cannot place here: " + placementReason;

        if (Input.GetMouseButtonDown(0) && !pickedThisFrame)
        {
            if (!canPlaceDecor)
            {
                return;
            }

            if (mode == CozyToolMode.Move && movingDecor != null)
            {
                movingDecor.transform.SetParent(GetRuntimeObjectRoot(), true);
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
                placed.transform.SetParent(GetRuntimeObjectRoot(), true);
                placed.SetActive(true);
                CozyDecorTemplate.SnapBottomToFloor(placed, hit.point.y);
                SetColliders(placed, true);
                if (!IsDecorPlacementValid(placed, null, supportObject, out _))
                {
                    Destroy(placed);
                    status = "Cannot place here: blocked";
                    return;
                }

                progressTracker?.AddPlacedDecor(placed);
                status = "Placed " + CurrentDecorName;
            }
        }
    }

    private void HandleFloorFinishTool(Ray ray)
    {
        if (!TryGetWetMopTarget(ray, out CozyMoppableFloor floor, out Vector3 hitPoint, out _))
        {
            ClearFloorFinishPreview();
            targetProgressLabel = "Finish: -";
            status = "Floor Finish: point at the floor.";
            return;
        }

        EnsureFloorFinishPreview();
        Vector2Int cell = GetFloorFinishCell(floor.transform, hitPoint);
        Quaternion rotation = Quaternion.Euler(0f, floorFinishQuarterTurns * 90f, 0f);
        Vector3 cellCenter = GetFloorFinishCellCenter(floor.transform, cell, rotation);
        bool occupied = installedFloorFinishes.ContainsKey(cell);
        string reason = occupied ? "already installed" : string.Empty;
        bool canInstall = !occupied && IsFloorFinishPlacementValid(floor.transform, cellCenter, rotation, out reason);

        floorFinishPreview.SetActive(true);
        PrepareFloorFinishObject(floorFinishPreview, cellCenter, rotation);
        if (!floorFinishPreviewHasValidity || floorFinishPreviewLastValid != canInstall)
        {
            ApplyPreviewMaterial(floorFinishPreview, canInstall ? previewMaterial : heldMaterial);
            floorFinishPreviewHasValidity = true;
            floorFinishPreviewLastValid = canInstall;
        }

        targetProgressLabel = GetFloorFinishDisplayName() + ": " + installedFloorFinishes.Count + " installed";
        status = canInstall ? "Floor Finish: click/drag to install " + GetFloorFinishDisplayName() : "Cannot install: " + reason;

        if (Input.GetMouseButton(0) && canInstall)
        {
            InstallFloorFinish(cellCenter, rotation, cell);
        }
    }

    private bool TryGetDecorPlacementSurface(Ray ray, out RaycastHit hit, out GameObject supportObject, out string reason)
    {
        hit = default;
        supportObject = null;
        reason = "point at a valid surface.";

        RaycastHit[] hits = Physics.RaycastAll(ray, reach);
        if (hits == null || hits.Length == 0)
        {
            reason = "point at a floor spot.";
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            if (Vector3.Dot(candidate.normal, Vector3.up) <= 0.65f)
            {
                continue;
            }

            if (candidate.collider.GetComponentInParent<CozyMoppableFloor>() != null)
            {
                hit = candidate;
                return true;
            }

            CozyMovableDecor decor = candidate.collider.GetComponentInParent<CozyMovableDecor>();
            if (decor == null)
            {
                continue;
            }

            if (movingDecor != null && candidate.collider.transform.IsChildOf(movingDecor.transform))
            {
                continue;
            }

            if (IsCurrentPlacementRug())
            {
                continue;
            }

            string supportName = GetDecorObjectName(decor.gameObject);
            if (IsRugDecorName(supportName))
            {
                hit = candidate;
                supportObject = decor.gameObject;
                return true;
            }

            if (IsTabletopDecorName(supportName))
            {
                if (IsCurrentPlacementTabletopItem())
                {
                    hit = candidate;
                    supportObject = decor.gameObject;
                    return true;
                }

                reason = "only flowerpots or TVs can be placed on desks or tables.";
                return false;
            }
        }

        reason = "point at the floor, carpet, tile, or a table for small decor.";
        return false;
    }

    private bool IsCurrentPlacementRug()
    {
        GameObject target = movingDecor != null ? movingDecor.gameObject : (decorTemplates != null && decorTemplates.Length > 0 ? decorTemplates[decorIndex] : null);
        return IsRugDecorName(GetDecorObjectName(target));
    }

    private bool IsCurrentPlacementTabletopItem()
    {
        GameObject target = movingDecor != null ? movingDecor.gameObject : (decorTemplates != null && decorTemplates.Length > 0 ? decorTemplates[decorIndex] : null);
        string decorName = GetDecorObjectName(target);
        return IsFlowerpotDecorName(decorName) || IsTvDecorName(decorName);
    }

    private string GetDecorObjectName(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        CozyDecorTemplate template = target.GetComponent<CozyDecorTemplate>();
        return template != null && !string.IsNullOrEmpty(template.displayName) ? template.displayName : target.name;
    }

    private bool IsFlowerpotDecorName(string decorName)
    {
        string normalizedName = string.IsNullOrEmpty(decorName) ? string.Empty : decorName.ToLowerInvariant();
        return normalizedName.Contains("flower") || normalizedName.Contains("pot") || normalizedName.Contains("plant");
    }

    private bool IsTvDecorName(string decorName)
    {
        string normalizedName = string.IsNullOrEmpty(decorName) ? string.Empty : decorName.ToLowerInvariant();
        return normalizedName == "tv" || normalizedName.Contains(" tv") || normalizedName.Contains("television");
    }

    private bool IsRugDecorName(string decorName)
    {
        string normalizedName = string.IsNullOrEmpty(decorName) ? string.Empty : decorName.ToLowerInvariant();
        return normalizedName.Contains("rug") || normalizedName.Contains("carpet");
    }

    private bool IsTabletopDecorName(string decorName)
    {
        string normalizedName = string.IsNullOrEmpty(decorName) ? string.Empty : decorName.ToLowerInvariant();
        return normalizedName.Contains("desk") || normalizedName.Contains("table");
    }

    private void EnsureFloorFinishPreview()
    {
        if (floorFinishPreview != null)
        {
            return;
        }

        floorFinishPreview = floorFinishPrefab != null ? Instantiate(floorFinishPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorFinishPreview.name = "Floor Finish Preview";
        SetColliders(floorFinishPreview, false);
        SetVisualLights(floorFinishPreview, false);
        floorFinishPreview.SetActive(false);
        ApplyPreviewMaterial(floorFinishPreview, previewMaterial);
    }

    private void RecreateFloorFinishPreview()
    {
        ClearFloorFinishPreview();
        EnsureFloorFinishPreview();
    }

    private void ClearFloorFinishPreview()
    {
        if (floorFinishPreview != null)
        {
            Destroy(floorFinishPreview);
            floorFinishPreview = null;
        }
        floorFinishPreviewHasValidity = false;
    }

    private Vector2Int GetFloorFinishCell(Transform floorTransform, Vector3 worldPoint)
    {
        Vector3 localPoint = floorTransform.InverseTransformPoint(worldPoint);
        Vector3 floorSize = floorTransform.lossyScale;
        Vector2 cellSize = GetFloorFinishCellSize();
        float x = localPoint.x * Mathf.Max(0.001f, floorSize.x);
        float z = localPoint.z * Mathf.Max(0.001f, floorSize.z);
        int col = Mathf.FloorToInt((x + floorSize.x * 0.5f) / cellSize.x);
        int row = Mathf.FloorToInt((z + floorSize.z * 0.5f) / cellSize.y);
        Vector2Int cellCounts = GetFloorFinishCellCounts(floorSize, cellSize);
        col = Mathf.Clamp(col, 0, Mathf.Max(0, cellCounts.x - 1));
        row = Mathf.Clamp(row, 0, Mathf.Max(0, cellCounts.y - 1));
        return new Vector2Int(col, row);
    }

    private Vector2Int GetFloorFinishCellCounts(Vector3 floorSize, Vector2 cellSize)
    {
        return new Vector2Int(
            Mathf.Max(1, Mathf.CeilToInt(floorSize.x / Mathf.Max(0.001f, cellSize.x))),
            Mathf.Max(1, Mathf.CeilToInt(floorSize.z / Mathf.Max(0.001f, cellSize.y))));
    }

    private Vector3 GetFloorFinishCellCenter(Transform floorTransform, Vector2Int cell)
    {
        return GetFloorFinishCellCenter(floorTransform, cell, Quaternion.identity);
    }

    private Vector3 GetFloorFinishCellCenter(Transform floorTransform, Vector2Int cell, Quaternion rotation)
    {
        Vector3 floorSize = floorTransform.lossyScale;
        Vector2 cellSize = GetFloorFinishCellSize();
        float x = -floorSize.x * 0.5f + cell.x * cellSize.x + cellSize.x * 0.5f;
        float z = -floorSize.z * 0.5f + cell.y * cellSize.y + cellSize.y * 0.5f;
        Vector3 halfExtents = rotation * (GetFloorFinishScale() * 0.5f);
        halfExtents = new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z));
        float maxCenterX = Mathf.Max(0f, floorSize.x * 0.5f - halfExtents.x);
        float maxCenterZ = Mathf.Max(0f, floorSize.z * 0.5f - halfExtents.z);
        x = Mathf.Clamp(x, -maxCenterX, maxCenterX);
        z = Mathf.Clamp(z, -maxCenterZ, maxCenterZ);
        Vector3 localCenter = new Vector3(
            x / Mathf.Max(0.001f, floorSize.x),
            0.5f + FloorFinishThickness * 0.5f,
            z / Mathf.Max(0.001f, floorSize.z));
        return floorTransform.TransformPoint(localCenter);
    }

    private Vector3 GetFloorFinishScale()
    {
        Vector2 cellSize = GetFloorFinishCellSize();
        return new Vector3(cellSize.x * 1.03f, FloorFinishThickness, cellSize.y * 1.03f);
    }

    private Vector2 GetFloorFinishCellSize()
    {
        return IsModernFloorFinish()
            ? new Vector2(ModernFloorFinishCellSize, ModernFloorFinishCellSize)
            : new Vector2(GothicWoodTileSpacingX, GothicWoodTileSpacingZ);
    }

    private bool IsFloorFinishPlacementValid(Transform floorTransform, Vector3 center, Quaternion rotation, out string reason)
    {
        reason = string.Empty;
        Vector3 floorSize = floorTransform.lossyScale;
        Vector3 halfExtents = rotation * (GetFloorFinishScale() * 0.5f);
        halfExtents = new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z));
        float halfWidth = floorSize.x * 0.5f + FloorFinishBorderOverhang;
        float halfDepth = floorSize.z * 0.5f + FloorFinishBorderOverhang;
        if (center.x - halfExtents.x < -halfWidth || center.x + halfExtents.x > halfWidth ||
            center.z - halfExtents.z < -halfDepth || center.z + halfExtents.z > halfDepth)
        {
            reason = "too close to wall";
            return false;
        }

        Collider[] hits = Physics.OverlapBox(center + Vector3.up * 0.18f, new Vector3(halfExtents.x, 0.18f, halfExtents.z), rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !hit.enabled)
            {
                continue;
            }

            if (hit.GetComponentInParent<CozyMoppableFloor>() != null || hit.GetComponentInParent<CozyDirtPatch>() != null)
            {
                continue;
            }

            if (hit.GetComponentInParent<CozyTidyObject>() != null)
            {
                reason = "blocked by obstacle";
                return false;
            }

            if (hit.GetComponentInParent<CozyMovableDecor>() != null)
            {
                reason = "blocked by decor";
                return false;
            }
        }

        return true;
    }

    private void InstallFloorFinish(Vector3 position, Quaternion rotation, Vector2Int cell)
    {
        if (installedFloorFinishes.ContainsKey(cell))
        {
            return;
        }

        if (floorFinishRoot == null)
        {
            GameObject root = new GameObject("Installed Floor Finishes");
            floorFinishRoot = root.transform;
            floorFinishRoot.SetParent(GetRuntimeObjectRoot(), true);
        }

        GameObject finish = floorFinishPrefab != null ? Instantiate(floorFinishPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        finish.name = GetFloorFinishDisplayName() + " " + cell.x + "_" + cell.y;
        finish.transform.SetParent(floorFinishRoot);
        PrepareFloorFinishObject(finish, position, rotation);
        SetColliders(finish, false);

        Renderer renderer = finish.GetComponent<Renderer>();
        if (renderer != null && floorFinishPrefab == null)
        {
            renderer.sharedMaterial = CreateFloorFinishMaterial();
        }

        installedFloorFinishes[cell] = finish;
        CozyFloorFinishPlacementFx.Play(finish);
        gameAudio?.PlayFloorFinishInstall(IsModernFloorFinish());
        status = "Installed " + GetFloorFinishDisplayName();
    }

    private Material CreateFloorFinishMaterial()
    {
        Color color = IsModernFloorFinish() ? new Color(0.78f, 0.80f, 0.78f, 1f) : new Color(0.63f, 0.42f, 0.26f, 1f);
        Material material = new Material(Shader.Find("Standard"));
        material.name = IsModernFloorFinish() ? "Runtime Marble Tile Finish" : "Runtime Gothic Wood Finish";
        material.color = color;
        material.SetFloat("_Glossiness", IsModernFloorFinish() ? 0.42f : 0.18f);
        return material;
    }

    private string GetFloorFinishDisplayName()
    {
        return IsModernFloorFinish() ? "Marble Tile" : "Wood Tile";
    }

    private bool IsModernFloorFinish()
    {
        return !string.IsNullOrEmpty(decorThemeFolder) && decorThemeFolder.ToLowerInvariant().Contains("modern");
    }

    private void PrepareFloorFinishObject(GameObject target, Vector3 position, Quaternion rotation)
    {
        target.transform.position = position;
        target.transform.rotation = rotation;
        if (floorFinishPrefab == null)
        {
            target.transform.localScale = GetFloorFinishScale();
            return;
        }

        target.transform.localScale = Vector3.one;
        FitFloorFinishPrefabToFootprint(target);
        SnapFloorFinishBottomToY(target, position.y - FloorFinishThickness * 0.5f + GetFloorFinishBottomOffset());
    }

    private void FitFloorFinishPrefabToFootprint(GameObject target)
    {
        if (!TryGetDecorWorldBounds(target, out Bounds bounds))
        {
            return;
        }

        Vector3 targetSize = GetFloorFinishScale();
        float scaleX = targetSize.x / Mathf.Max(0.001f, bounds.size.x);
        float scaleZ = targetSize.z / Mathf.Max(0.001f, bounds.size.z);
        float scaleY = FloorFinishThickness / Mathf.Max(0.001f, bounds.size.y);
        target.transform.localScale = Vector3.Scale(target.transform.localScale, new Vector3(scaleX, scaleY, scaleZ));
    }

    private void SnapFloorFinishBottomToY(GameObject target, float floorY)
    {
        if (!TryGetDecorWorldBounds(target, out Bounds bounds))
        {
            return;
        }

        target.transform.position += Vector3.up * (floorY - bounds.min.y);
    }

    private float GetFloorFinishBottomOffset()
    {
        return IsModernFloorFinish() ? 0f : 0.01f;
    }

    private bool IsDecorPlacementValid(GameObject candidate, GameObject ignoredObject, out string reason)
    {
        return IsDecorPlacementValid(candidate, ignoredObject, null, out reason);
    }

    private bool IsDecorPlacementValid(GameObject candidate, GameObject ignoredObject, GameObject supportObject, out string reason)
    {
        reason = string.Empty;
        if (!TryGetDecorWorldBounds(candidate, out Bounds bounds))
        {
            return true;
        }

        bool candidateIsRug = IsRugDecorName(GetDecorObjectName(candidate));
        if (bounds.min.x < -DecorPlacementRoomHalfWidth || bounds.max.x > DecorPlacementRoomHalfWidth ||
            bounds.min.z < -DecorPlacementRoomHalfDepth || bounds.max.z > DecorPlacementRoomHalfDepth)
        {
            reason = "too close to wall";
            return false;
        }

        Vector3 halfExtents = bounds.extents + new Vector3(DecorPlacementPadding, 0.03f, DecorPlacementPadding);
        Collider[] hits = Physics.OverlapBox(bounds.center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !hit.enabled)
            {
                continue;
            }

            if (hit.transform.IsChildOf(candidate.transform))
            {
                continue;
            }

            if (ignoredObject != null && hit.transform.IsChildOf(ignoredObject.transform))
            {
                continue;
            }

            if (supportObject != null && hit.transform.IsChildOf(supportObject.transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<CozyMoppableFloor>() != null || hit.GetComponentInParent<CozyDirtPatch>() != null)
            {
                continue;
            }

            if (hit.GetComponentInParent<CozyPaintableSurface>() != null)
            {
                reason = "too close to wall";
                return false;
            }

            if (hit.GetComponentInParent<CozyTidyObject>() != null)
            {
                reason = "blocked by obstacle";
                return false;
            }

            if (hit.GetComponentInParent<CozyDecorTrashBin>() != null)
            {
                reason = "blocked by trash bin";
                return false;
            }

            CozyMovableDecor hitDecor = hit.GetComponentInParent<CozyMovableDecor>();
            if (hitDecor != null)
            {
                if (candidateIsRug || IsRugDecorName(GetDecorObjectName(hitDecor.gameObject)))
                {
                    continue;
                }

                reason = "blocked by decor";
                return false;
            }
        }

        return true;
    }

    private bool TryGetDecorWorldBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
        {
            return false;
        }

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
        previewInstance.transform.SetParent(GetRuntimeObjectRoot(), true);
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

    private Transform GetRuntimeObjectRoot()
    {
        return transform.root != null ? transform.root : transform;
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

        if (mode == CozyToolMode.FloorFinish)
        {
            return "Finish: " + GetFloorFinishDisplayName();
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

        cleaningAudio?.StopLoop();
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

    private bool TryGetWindowTarget(Ray ray, out CozyCleanableWindow window, out Vector3 worldPoint, out Vector3 worldNormal)
    {
        window = null;
        worldPoint = default;
        worldNormal = Vector3.back;

        CozyCleanableWindow[] windows = FindObjectsOfType<CozyCleanableWindow>(true);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] == null)
            {
                continue;
            }

            if (windows[i].TryProjectRay(ray, out Vector3 candidatePoint, out Vector3 candidateNormal, out float distance) && distance <= reach && distance < bestDistance)
            {
                bestDistance = distance;
                window = windows[i];
                worldPoint = candidatePoint;
                worldNormal = candidateNormal;
            }
        }

        return window != null;
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
                prefab = paintRollerVisualPrefab,
                modelZ = 0f,
                fixedLocalScale = 0.8f
            };
        }

        if (wetMopRollerVisual == null)
        {
            wetMopRollerVisual = new RollerVisualState
            {
                visualName = "Floor Mop Visual",
                prefab = floorMopVisualPrefab,
                modelZ = 90f,
                fixedLocalScale = 50f,
                useFixedLocalRotation = true,
                fixedLocalEuler = new Vector3(180f, 0f, 0f),
                alignBottomToSurface = true
            };
        }
    }

    private void EnsureRollerVisual(RollerVisualState state)
    {
        if (state == paintRollerVisual && state != null && state.prefab == null)
        {
            state.prefab = paintRollerVisualPrefab;
        }
        else if (state == wetMopRollerVisual && state != null && state.prefab == null)
        {
            state.prefab = floorMopVisualPrefab;
        }

        if (state == null || state.instance != null || state.prefab == null || viewCamera == null)
        {
            return;
        }

        state.pivot = new GameObject(state.visualName + " Pivot").transform;
        state.pivot.SetParent(transform, false);

        state.instance = Instantiate(state.prefab, state.pivot);
        state.instance.name = state.visualName;
        state.instance.transform.localPosition = rollerVisualModelOffset;
        state.instance.transform.localRotation = state.useFixedLocalRotation
            ? Quaternion.Euler(state.fixedLocalEuler)
            : Quaternion.Euler(RollerVisualModelX, 45f, state.modelZ);
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
        float scale = state.fixedLocalScale > 0f
            ? state.fixedLocalScale
            : (maxExtent > 0.0001f ? 0.48f / maxExtent : 0.2f);
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
            targetTangent = RotateStampTangent90(targetNormal, ResolveRollerTangent(targetNormal, wetMopRollerVisual != null ? wetMopRollerVisual.quarterTurns : 0));
            status = Input.GetMouseButton(0)
                ? status
                : "Wet Mop: roller ready on " + mopFloor.name;
        }

        UpdateRollerVisualState(paintRollerVisual, showPaintRoller, targetPoint, targetNormal, targetTangent, 0.24f);
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
            if (TryGetWindowTarget(ray, out _, out Vector3 windowPoint, out Vector3 windowNormal))
            {
                shouldShow = true;
                Vector3 normal = ResolveVisibleSurfaceNormal(windowPoint, windowNormal);
                Vector3 tangent = ResolveRollerTangent(normal, windowWiperQuarterTurns);
                float movementFactor = 0f;
                if (hasWindowWiperLastPoint)
                {
                    movementFactor = Mathf.Clamp01(Vector3.Distance(windowWiperLastPoint, windowPoint) / 0.18f);
                }

                float targetWobble = Input.GetMouseButton(0) ? movementFactor : movementFactor * 0.35f;
                windowWiperWobble = Mathf.MoveTowards(windowWiperWobble, targetWobble, Time.deltaTime * 9f);
                windowWiperLastPoint = windowPoint;
                hasWindowWiperLastPoint = true;
                windowWiperVisual.transform.position = windowPoint + normal * 0.08f;
                windowWiperVisual.transform.rotation = BuildRollerVisualRotation(normal, tangent)
                    * Quaternion.AngleAxis(Mathf.Sin(Time.time * 22f) * windowWiperWobble * 7f, Vector3.forward);
            }
        }

        if (!shouldShow)
        {
            windowWiperWobble = Mathf.MoveTowards(windowWiperWobble, 0f, Time.deltaTime * 8f);
            hasWindowWiperLastPoint = false;
        }

        windowWiperVisual.SetActive(shouldShow);
    }

    private void EnsureWindowWiperVisual()
    {
        if (windowWiperVisual != null)
        {
            return;
        }

        windowWiperVisual = new GameObject("Window Squeegee Visual");
        windowWiperVisual.transform.SetParent(transform, false);

        GameObject model;
        if (windowWiperVisualPrefab != null)
        {
            model = Instantiate(windowWiperVisualPrefab, windowWiperVisual.transform);
            model.name = "Window Squeegee Model";
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Window Squeegee Fallback";
            model.transform.SetParent(windowWiperVisual.transform, false);
            model.transform.localScale = new Vector3(0.62f, 0.06f, 0.08f);
            Renderer renderer = model.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = heldMaterial != null ? heldMaterial : previewMaterial;
            }
        }

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        NormalizeToolVisualScale(model, windowWiperVisual.transform, 0.62f, windowWiperVisualPrefab != null ? 35f : 0f);
        SetVisualColliders(model, false);
        SetVisualLights(model, false);
        windowWiperVisual.SetActive(false);
    }

    private void NormalizeToolVisualScale(GameObject model, Transform pivot, float targetMaxExtent, float fixedLocalScale)
    {
        if (model == null || pivot == null)
        {
            return;
        }

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            model.transform.localScale = Vector3.one * 0.2f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxExtent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float scale = fixedLocalScale > 0f
            ? fixedLocalScale
            : (maxExtent > 0.0001f ? targetMaxExtent / maxExtent : 0.2f);
        model.transform.localScale = Vector3.one * scale;

        renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = pivot.InverseTransformPoint(bounds.center);
        model.transform.localPosition -= localCenter;
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
            state.wobble = Mathf.MoveTowards(state.wobble, 0f, Time.deltaTime * 8f);
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
        float targetWobble = usingRoller ? movementFactor : movementFactor * 0.35f;
        state.wobble = Mathf.MoveTowards(state.wobble, targetWobble, Time.deltaTime * 9f);
        state.lastPoint = targetPoint;
        state.hasLastPoint = true;

        Vector3 pressOffset = -targetNormal * (0.08f * state.press);
        Vector3 joltOffset = targetTangent * (0.06f * state.jolt);
        Vector3 travelLift = Vector3.Cross(targetNormal, targetTangent).normalized * (0.025f * movementFactor);
        state.pivot.position = targetPoint + targetNormal * surfaceOffset + pressOffset + joltOffset + travelLift;
        state.pivot.rotation = BuildRollerVisualRotation(targetNormal, targetTangent);
        state.instance.transform.localPosition = state.centerOffset + rollerVisualModelOffset;
        state.instance.transform.localRotation = state.useFixedLocalRotation
            ? Quaternion.Euler(state.fixedLocalEuler) * Quaternion.AngleAxis(Mathf.Sin(Time.time * 22f) * state.wobble * 7f, Vector3.forward)
            : Quaternion.Euler(RollerVisualModelX, state.dynamicYaw, state.modelZ);
        if (state.alignBottomToSurface)
        {
            AlignVisualBottomToSurface(state, targetPoint, targetNormal, 0.01f);
        }
    }

    private void AlignVisualBottomToSurface(RollerVisualState state, Vector3 surfacePoint, Vector3 surfaceNormal, float clearance)
    {
        if (state == null || state.instance == null)
        {
            return;
        }

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Renderer[] renderers = state.instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        float minProjection = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds bounds = renderers[i].bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                minProjection = Mathf.Min(minProjection, Vector3.Dot(corners[cornerIndex], normal));
            }
        }

        if (float.IsInfinity(minProjection))
        {
            return;
        }

        float targetProjection = Vector3.Dot(surfacePoint + normal * clearance, normal);
        state.instance.transform.position += normal * (targetProjection - minProjection);
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
