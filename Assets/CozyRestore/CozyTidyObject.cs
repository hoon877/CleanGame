using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
