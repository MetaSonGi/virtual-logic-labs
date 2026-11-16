using UnityEngine;

/// <summary>Keeps labels and glow sprites facing the camera.</summary>
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
