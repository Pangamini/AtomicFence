using UnityEngine;

public abstract class GridObject : MonoBehaviour
{
    public abstract bool BlocksMud { get; }
    public abstract void OnNeighborsChanged(Chunk chunk, int dirtyIndex);
    public abstract void Initialize(World world);
}
