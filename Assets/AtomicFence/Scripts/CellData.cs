using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct CellData
{
    public GridObject GridObject;
    public GridObject GridObjectPrefab;
    public bool IsMud;
    public bool BlocksMud => GridObject != null && GridObject.BlocksMud;
}
