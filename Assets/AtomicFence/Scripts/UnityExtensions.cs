using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class UnityExtensions
{
    public static Vector3Int X0Y(this Vector2Int vector) => new Vector3Int(vector.x, 0, vector.y);
    public static Vector3 X0Y(this Vector2 vector) => new Vector3(vector.x, 0, vector.y);
    
    public static Vector2 XZ(this Vector3 vector) => new Vector2(vector.x, vector.z);
}
