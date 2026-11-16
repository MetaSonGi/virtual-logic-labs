using UnityEngine;

/// <summary>Raycast target in the 3D lab. kind: 0 pin, 1 part body, 2 wire, 3 power button</summary>
public class Pickable : MonoBehaviour
{
    public int kind;
    public int id;
    public int pin;
    public bool isOut;
}
