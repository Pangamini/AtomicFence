using System;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Scripting;

public class World : MonoBehaviour
{
    [SerializeField] private string m_fenceCollectionName = "Fence";
    [SerializeField] private Chunk m_chunkPrefab;
    [SerializeField] private Vector2Int m_chunkSize = new Vector2Int(8, 8);
    [SerializeField] private FencePole m_fencePrefab;
    [SerializeField] private ColorVariant[] m_colorVariants;

    private GameObject m_disabledDummy;
    private float m_colorVariantChanceSum;

    private readonly Dictionary<Vector2Int, Chunk> m_chunks = new();

    [Serializable]
    public struct ColorVariant
    {
        [ColorUsage(false,true)]
        [SerializeField] 
        private Color m_color;
        
        [SerializeField] 
        private float m_chance;
        
        public Color Color => m_color;
        public float Chance => m_chance;
    }
    
    public Plane WorldPlane => new Plane(transform.up, transform.position);
    public FencePole FencePrefab => m_fencePrefab;
    
    protected void Awake()
    {
        m_disabledDummy = new GameObject("DisabledDummy");
        m_disabledDummy.SetActive(false);
        m_disabledDummy.transform.SetParent(transform);
        
        m_colorVariantChanceSum = 0f;
        foreach(var variant in m_colorVariants)
        {
            m_colorVariantChanceSum += variant.Chance;
        }
        
        m_reduceFenceConnections.Changed += (_) =>
        {
            foreach(var chunkPair in m_chunks)
                chunkPair.Value.SetAllCellsDirty();
        };
    }

    public Color PickRandomColor()
    {
        float rand = UnityEngine.Random.Range(0f, m_colorVariantChanceSum);
        float chanceSum = 0;
        foreach (var variant in m_colorVariants)
        {
            chanceSum += variant.Chance;
            if(rand < chanceSum)
                return variant.Color;
        }
        return m_colorVariants[^1].Color;
    }


    /// <summary>
    /// Clears the world and loads the content of the database.
    /// </summary>
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

        m_dbUpdateTime.Value = DateTime.Now;
    }
    
    public void SetGridObject(Vector2Int cellPosition, FencePole fencePrefab)
    {
        Vector2Int chunkId = GetChunkId(cellPosition);
        Chunk chunk = GetOrCreateChunk(chunkId);
        chunk.SetGridObject(cellPosition, fencePrefab);
    }
    
    private Chunk GetOrCreateChunk(Vector2Int chunkId)
    {
        if(!m_chunks.TryGetValue(chunkId, out Chunk chunk))
        {
            Vector3 chunkWorldPos = (chunkId * m_chunkSize).X0Y();
            chunk = Instantiate(m_chunkPrefab, chunkWorldPos, Quaternion.identity, m_disabledDummy.transform);
            
            chunk.Initialize(new RectInt(chunkId * m_chunkSize, m_chunkSize), this);
            m_chunks.Add(chunkId, chunk);
            chunk.transform.SetParent(transform);
        }
        return chunk;
    }

    private Vector2Int GetChunkId(Vector2Int cellPosition)
    {
        //TODO figure out how to do this with integer arithmetics
        var chunkId = Vector2Int.FloorToInt(new Vector2((float)cellPosition.x / m_chunkSize.x, (float)cellPosition.y / m_chunkSize.y));
        return chunkId;
    }

    public void Clear()
    {
        foreach (var chunkPair in m_chunks)
        {
            chunkPair.Value.Destroy();
        }
        m_chunks.Clear();
    }

    /// <summary>
    /// Saves the world to the database.
    /// </summary>
    [Preserve]
    public async void SaveToDatabase()
    {
        var firestore = FirebaseFirestore.DefaultInstance;
        CollectionReference collection = firestore.Collection(m_fenceCollectionName);
        
        WriteBatch batch = firestore.StartBatch();
        
        // clear the collection. The only way seems to be to iterate over all documents.
        QuerySnapshot snapshot = await collection.GetSnapshotAsync();
        foreach (var documentSnapshot in snapshot.Documents)
        {
            batch.Delete(documentSnapshot.Reference);
        }

        foreach (var chunkPair in m_chunks)
        {
            chunkPair.Value.SaveToDatabase(batch, collection);
        }

        await batch.CommitAsync();
    }
    
    private readonly Observable<DateTime> m_dbUpdateTime = new();
    private readonly Observable<int> m_fenceCount = new();
    private readonly Observable<float> m_fenceLength = new();
    private readonly Observable<bool> m_reduceFenceConnections = new(true);

    /// <summary>
    /// Converts world position to grid position.
    /// </summary>
    public Vector2 WorldToGrid(Vector3 worldPos) => worldPos.XZ() - new Vector2(0.5f,0.5f);

    /// <summary>
    /// Converts grid position to world position.
    /// </summary>
    /// <param name="gridPos"></param>
    /// <returns></returns>
    public Vector3 GridToWorld(Vector2 gridPos) => gridPos.X0Y() + new Vector3(0.5f,0f,0.5f);

    public Observable<DateTime>.View DatabaseUpdateTime => m_dbUpdateTime.GetView();
    public Observable<int>.View FenceCount => m_fenceCount.GetView();
    public Observable<float>.View FenceLength => m_fenceLength.GetView();
    public Observable<bool> ReduceFenceConnections => m_reduceFenceConnections;

    public bool GetCellData(Vector2Int position, out CellData cell)
    {
        if(m_chunks.TryGetValue(GetChunkId(position), out var chunk) && chunk.TryGetCellIndex(position, out int index))
        {
            cell = chunk.GetCellData(index);
            return true;
        }
        cell = default;
        return false;
    }
    
    public void SetCellDirty(Vector2Int position)
    {
        if(m_chunks.TryGetValue(GetChunkId(position), out var chunk))
        {
            chunk.SetCellDirty(position);
        }
    }
}
