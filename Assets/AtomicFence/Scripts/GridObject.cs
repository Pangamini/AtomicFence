using UnityEngine;

public abstract class GridObject : MonoBehaviour
{
    public abstract bool BlocksMud { get; }
    public abstract void OnNeighborsChanged(World index, int dirtyIndex);
}
