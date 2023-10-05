using System;
using System.Collections.Generic;
using System.Text;
using Firebase.Firestore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
public class World : MonoBehaviour
{
    [SerializeField] private string m_fenceCollectionName = "Fence";
    [SerializeField] private RectInt m_bounds;
    [SerializeField] private FencePole m_fencePrefab;
    
    private CellData[] m_cells;
    private bool m_mudDirty;

    public CellData GetCellData(int index) => m_cells[index];
    
    public Plane WorldPlane => new Plane(transform.up, transform.position);
    public FencePole FencePrefab => m_fencePrefab;

    protected void Awake()
    {
        m_cells = new CellData[m_bounds.width * m_bounds.height];
    }

    public int GetCellIndexNoCheck(Vector2Int gridPos)
    {
        gridPos -= m_bounds.min;
        return gridPos.x + gridPos.y * m_bounds.width;
    }

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

    private Vector2Int GetGridPos(int cellIndex)
    {
        return new Vector2Int(cellIndex % m_bounds.width, cellIndex / m_bounds.width) + m_bounds.min;
    }
    
    public Vector3 GridToWorld(Vector2Int gridPos) => gridPos.X0Y() + new Vector3(0.5f,0f,0.5f);

    public Vector2 WorldToGrid(Vector3 worldPos) => worldPos.XZ() - new Vector2(0.5f,0.5f);
    
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
        
        cell.GridObject = prefab == null? null : Instantiate(prefab, GridToWorld(gridPos), Quaternion.identity, transform);
        m_mudDirty = true;
    }

    private void ClearGridObject(int index)
    {
        ref CellData cell = ref m_cells[index];
        if(cell.GridObject)
        {
            Destroy(cell.GridObject.gameObject);
            cell.GridObject = null;
            cell.GridObjectPrefab = null;
        }
        m_mudDirty = true;
    }
    
    
    [Preserve]
    public async void LoadFromDatabase()
    {
        var firestore = FirebaseFirestore.DefaultInstance;
        QuerySnapshot snapshot = await firestore.Collection(m_fenceCollectionName).GetSnapshotAsync();
        Clear();
        foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
        {
            CellDTO cellDto = documentSnapshot.ConvertTo<CellDTO>();
            SetGridObject(cellDto.Position, m_fencePrefab);
        }
    }

    public async void SaveToDatabase()
    {
        var firestore = FirebaseFirestore.DefaultInstance;
        var collection = firestore.Collection(m_fenceCollectionName);
        
        WriteBatch batch = firestore.StartBatch();
        
        // clear the collection. The only way seems to be to iterate over all documents.
        QuerySnapshot snapshot = await collection.GetSnapshotAsync();
        foreach (var documentSnapshot in snapshot.Documents)
        {
            batch.Delete(documentSnapshot.Reference);
        }

        for( int i = 0; i < m_cells.Length; ++i )
        {
            if(m_cells[i].GridObjectPrefab == m_fencePrefab)
            {
                Vector2Int position = GetGridPos(i);
                var data = new CellDTO() { Position = position };
                var reference = collection.Document();
                batch.Set(reference, data);
            }
        }

        await batch.CommitAsync();
    }
    
    public void Clear()
    {
        for( int i = 0; i < m_cells.Length; ++i )
        {
            ClearGridObject(i);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        
        Gizmos.DrawWireCube(m_bounds.center.X0Y(), m_bounds.size.X0Y());
    }

    protected void Update()
    {
        if(m_mudDirty)
        {
            UpdateMud();
            m_mudDirty = false;
        }
    }

    private static readonly CustomSampler s_updateMudSampler = CustomSampler.Create(nameof(UpdateMud));
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
    }

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
}
