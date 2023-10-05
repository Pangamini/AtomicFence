using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Profiling;

public class Chunk : MonoBehaviour
{
    private RectInt m_bounds;
    private CellData[] m_cells;
    
    public CellData GetCellData(int index) => m_cells[index];
    public RectInt Bounds => m_bounds;
    public World World => m_world;

    private bool m_mudDirty;
    private readonly HashSet<int> m_dirtyCellObjects = new();

    public event Action MudUpdated;

    public void SetAllCellsDirty()
    {
        for( int i = 0; i < m_cells.Length; ++i )
            m_dirtyCellObjects.Add(i);
    }
    
    public void Initialize(RectInt bounds, World world)
    {
        m_world = world;
        m_bounds = bounds;
        m_cells = new CellData[m_bounds.width * m_bounds.height];
    }
    
    
    /// <summary>
    /// Converts grid position to cell array index.
    /// Doesn't check for world bounds.
    /// </summary>
    private int GetCellIndexNoCheck(Vector2Int gridPos)
    {
        gridPos -= m_bounds.min;
        return gridPos.x + gridPos.y * m_bounds.width;
    }

    
    /// <summary>
    /// Attempts to convert grid position to cell array index.
    /// </summary>
    /// <returns>True if gridPos is within world bounds.</returns>
    public bool TryGetCellIndex(Vector2Int gridPos, out int index)
    {
        if(!m_bounds.Contains(gridPos))
        {
            index = -1;
            return false;
        }

        index = GetCellIndexNoCheck(gridPos);
        return true;
    }
    
    /// <summary>
    /// Converts cell array index grid position.
    /// </summary>
    public Vector2Int GetGridPos(int cellIndex)
    {
        return new Vector2Int(cellIndex % m_bounds.width, cellIndex / m_bounds.width) + m_bounds.min;
    }
    
    
    /// <summary>
    /// Ensures that the cell contains an instance of the prefab provided.
    /// If prefab is null, the cell is cleared.
    /// </summary>
    public void SetGridObject(Vector2Int gridPos, GridObject prefab)
    {
        if(!TryGetCellIndex(gridPos, out int index))
            return;
        
        ref CellData cell = ref m_cells[index];

        if(cell.GridObjectPrefab == prefab)
            return;

        cell.GridObjectPrefab = prefab;
        if(cell.GridObject)
        {
            Destroy(cell.GridObject.gameObject);
        }
        
        if(prefab != null)
        {
            cell.GridObject = Instantiate(prefab, World.GridToWorld(gridPos), Quaternion.identity, transform);
            cell.GridObject.Initialize(m_world);
        }
        else
        {
            cell.GridObject = null;
        }
        
        m_mudDirty = true;
        
        SetNeighborhoodDirty(gridPos);
    }

    /// <summary>
    /// Sets all cells around gridPos (including) as dirty
    /// </summary>
    /// <param name="gridPos"></param>
    private void SetNeighborhoodDirty(Vector2Int gridPos)
    {
        for( int y = gridPos.y - 1; y <= gridPos.y + 1; ++y )
        {
            for( int x = gridPos.x - 1; x <= gridPos.x + 1; ++x )
            {
                SetCellDirty(new Vector2Int(x, y));
            }
        }
    }
    
    public void SetCellDirty(Vector2Int vector2Int)
    {
        if(TryGetCellIndex(vector2Int, out int index))
            m_dirtyCellObjects.Add(index);
        else
            World.SetCellDirty(vector2Int);
    }

    protected void Update()
    {
        if(m_mudDirty)
        {
            UpdateMud();
            m_mudDirty = false;
        }

        if(m_dirtyCellObjects.Count > 0)
        {
            // Dirty cells get OnNeighborsChanged call
            foreach (int dirtyIndex in m_dirtyCellObjects)
            {
                var obj = m_cells[dirtyIndex].GridObject;
                if(obj == null)
                    continue;
                obj.OnNeighborsChanged(this, dirtyIndex);
            }
            m_dirtyCellObjects.Clear();
            
            // calc some stats
            int count = 0;
            float length = 0;
            for( int i = 0; i < m_cells.Length; ++i )
            {
                var cell = GetCellData(i);
                if(cell.GridObject is FencePole fencePole)
                {
                    count++;
                    length += fencePole.FenceLength;
                }
            }
            // m_fenceCount.Value = count;
            // m_fenceLength.Value = length;
        }
    }
    
    private static readonly CustomSampler s_updateMudSampler = CustomSampler.Create(nameof(UpdateMud));
    private World m_world;

    /// <summary>
    /// FloodFills the world from the edges, in order to find cells that are fully enclosed by fences.
    /// </summary>
    private void UpdateMud()
    {
        s_updateMudSampler.Begin();
        // Set all mud flags.
        // We will floodFill from outside.
        for(int i = 0; i< m_cells.Length; ++i)
        {
            m_cells[i].IsMud = true;
        }
        
        var queue = new Queue<Vector2Int>();
        var alreadyEnqueued = new bool[m_cells.Length];

        void TryEnqueue(Vector2Int cellPos)
        {
            if(!TryGetCellIndex(cellPos, out int index))
                return;
            
            CellData cell = m_cells[index];
            if(alreadyEnqueued[index] || !cell.IsMud || cell.BlocksMud)
                return;
            
            alreadyEnqueued[index] = true;
            queue.Enqueue(cellPos);
        }

        // enqueue all edge cells
        foreach(var edgeCell in EnumerateWorldEdgeCells())
        {
            TryEnqueue(edgeCell);
        }

        while(queue.TryDequeue(out Vector2Int cellIndex))
        {
            m_cells[GetCellIndexNoCheck(cellIndex)].IsMud = false;

            TryEnqueue(new Vector2Int(cellIndex.x+1, cellIndex.y));
            TryEnqueue(new Vector2Int(cellIndex.x-1, cellIndex.y));
            TryEnqueue(new Vector2Int(cellIndex.x, cellIndex.y+1));
            TryEnqueue(new Vector2Int(cellIndex.x, cellIndex.y-1));
        }
        s_updateMudSampler.End();

        MudUpdated?.Invoke();
    }

    /// <summary>
    /// Yields all cell positions that are on the edge of the world.
    /// </summary>
    IEnumerable<Vector2Int> EnumerateWorldEdgeCells()
    {
        for( int x = m_bounds.xMin; x < m_bounds.xMax; ++x )
        {
            yield return new Vector2Int(x, m_bounds.yMin);
            yield return new Vector2Int(x, m_bounds.yMax - 1);
        }
        
        for( int y = m_bounds.yMin+1; y < m_bounds.yMax-1; ++y )
        {
            yield return new Vector2Int(m_bounds.xMin, y);
            yield return new Vector2Int(m_bounds.xMax-1, y);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if(m_world == null)
            return;
        Gizmos.matrix = m_world.transform.localToWorldMatrix;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(m_bounds.center.X0Y(), m_bounds.size.X0Y());
    }

    /// <summary>
    /// Clears all world cells.
    /// </summary>
    public void Clear()
    {
        for( int i = 0; i < m_cells.Length; ++i )
        {
            SetGridObject(GetGridPos(i), null);
        }
    }

    public void SaveToDatabase(WriteBatch batch, CollectionReference collection)
    {
        for( int i = 0; i < m_cells.Length; ++i )
        {
            if(m_cells[i].GridObjectPrefab == m_world.FencePrefab)
            {
                Vector2Int position = GetGridPos(i);
                var data = new CellDTO() { Position = position };
                var reference = collection.Document();
                batch.Set(reference, data);
            }
        }
    }

    public void Destroy()
    {
        Destroy(gameObject);
    }
    
    public bool GetCellDataInNeighborChunks(Vector2Int position, out CellData cell)
    {
        if(TryGetCellIndex(position, out int index))
        {
            cell = GetCellData(index);
            return true;
        }

        return m_world.GetCellData(position, out cell);
    }
}
