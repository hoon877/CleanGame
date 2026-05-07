using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyProgressTracker : MonoBehaviour
{
    private CozyDirtPatch[] dirtPatches = new CozyDirtPatch[0];
    private CozyCleanableWindow[] windows = new CozyCleanableWindow[0];
    private CozyMoppableFloor[] floors = new CozyMoppableFloor[0];
    private CozyPaintableSurface[] paintableSurfaces = new CozyPaintableSurface[0];
    private CozyTidyObject[] tidyObjects = new CozyTidyObject[0];
    private readonly HashSet<int> placedDecorIds = new HashSet<int>();

    public bool ObstaclesCleared => AreDirtPatchesClean() && AreTidyObjectsCleared();
    public bool SurfaceWorkComplete => AreFloorsClean() && AreWindowsClean() && ArePaintTasksComplete();
    public bool HasPlacedDecor => placedDecorIds.Count > 0;

    public float NormalizedProgress
    {
        get
        {
            int paintTaskCount = CountPaintTasks();
            int total = dirtPatches.Length + windows.Length + floors.Length + paintTaskCount + tidyObjects.Length;
            if (total == 0)
            {
                return 1f;
            }

            int done = 0;
            for (int i = 0; i < dirtPatches.Length; i++) if (dirtPatches[i] == null || dirtPatches[i].IsClean) done++;
            for (int i = 0; i < windows.Length; i++) if (windows[i] == null || windows[i].IsClean) done++;
            for (int i = 0; i < floors.Length; i++) if (floors[i] == null || floors[i].IsClean) done++;
            done += CountCompletedPaintTasks();
            for (int i = 0; i < tidyObjects.Length; i++) if (tidyObjects[i] == null || tidyObjects[i].IsTidied) done++;
            return Mathf.Clamp01(done / (float)total);
        }
    }

    private int CountPaintTasks()
    {
        int count = 0;
        HashSet<string> countedGroups = new HashSet<string>();
        for (int i = 0; i < paintableSurfaces.Length; i++)
        {
            CozyPaintableSurface surface = paintableSurfaces[i];
            if (surface == null)
            {
                continue;
            }

            if (surface.HasPaintGroup)
            {
                if (countedGroups.Add(surface.paintGroupId))
                {
                    count++;
                }
            }
            else
            {
                count++;
            }
        }

        return count;
    }

    private int CountCompletedPaintTasks()
    {
        int done = 0;
        HashSet<string> countedGroups = new HashSet<string>();
        for (int i = 0; i < paintableSurfaces.Length; i++)
        {
            CozyPaintableSurface surface = paintableSurfaces[i];
            if (surface == null)
            {
                continue;
            }

            if (surface.HasPaintGroup)
            {
                if (countedGroups.Add(surface.paintGroupId) && surface.DisplayIsPainted)
                {
                    done++;
                }
            }
            else if (surface.IsPainted)
            {
                done++;
            }
        }

        return done;
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
        if (placed != null)
        {
            placedDecorIds.Add(placed.GetInstanceID());
        }
    }

    public void RemovePlacedDecor(GameObject placed)
    {
        if (placed != null)
        {
            placedDecorIds.Remove(placed.GetInstanceID());
        }
    }

    private bool AreDirtPatchesClean()
    {
        for (int i = 0; i < dirtPatches.Length; i++)
        {
            if (dirtPatches[i] != null && !dirtPatches[i].IsClean)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreTidyObjectsCleared()
    {
        for (int i = 0; i < tidyObjects.Length; i++)
        {
            if (tidyObjects[i] != null && !tidyObjects[i].IsTidied)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreFloorsClean()
    {
        for (int i = 0; i < floors.Length; i++)
        {
            if (floors[i] != null && !floors[i].IsClean)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreWindowsClean()
    {
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] != null && !windows[i].IsClean)
            {
                return false;
            }
        }

        return true;
    }

    private bool ArePaintTasksComplete()
    {
        return CountCompletedPaintTasks() >= CountPaintTasks();
    }
}
