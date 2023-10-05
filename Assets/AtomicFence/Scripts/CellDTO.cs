using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;

[FirestoreData]
public struct CellDTO
{
    private Vector2Int m_position;

    [FirestoreProperty]
    public int X
    {
        get => m_position.x;
        set => m_position.x = value;
    }
    
    [FirestoreProperty]
    public int Y
    {
        get => m_position.y;
        set => m_position.y = value;
    }
    
    public Vector2Int Position
    {
        get => m_position;
        set => m_position = value;
    }

    public override string ToString() => $"Cell (X:{X}, Y:{Y})";
}
