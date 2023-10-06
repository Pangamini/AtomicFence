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

    private readonly Observable<DateTime> m_dbUpdateTime = new();
    private readonly Observable<int> m_fenceCount = new();
    private readonly Observable<float> m_fenceLength = new();
    private readonly Observable<bool> m_reduceFenceConnections = new(true);
    
    private RectInt m_chunkIdBounds;
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
                chunkPair.Value.SetAllCellObjectsDirty();
        };
    }

    protected void Update()
    {
        int count = 0;
        float length = 0;
        foreach (var chunkPair in m_chunks)
        {
            count += chunkPair.Value.ChunkFenceCount;
            length += chunkPair.Value.ChunkFenceLength;
        }
        
        m_fenceLength.Value = length;
        m_fenceCount.Value = count;
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
        if(!TryGetChunk(chunkId, out Chunk chunk))
        {
            Vector3 chunkWorldPos = (chunkId * m_chunkSize).X0Y();
            chunk = Instantiate(m_chunkPrefab, chunkWorldPos, Quaternion.identity, m_disabledDummy.transform);
            chunk.gameObject.name = $"{nameof(Chunk)} {chunkId.x} / {chunkId.y}";
            chunk.Initialize(new RectInt(chunkId * m_chunkSize, m_chunkSize), chunkId, this);
            m_chunks.Add(chunkId, chunk);
            chunk.transform.SetParent(transform);

            OnChunkMapChanged();
        }
        
        return chunk;
    }

    private bool TryGetChunk(Vector2Int chunkId, out Chunk chunk)
    {
        return m_chunks.TryGetValue(chunkId, out chunk);
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
        OnChunkMapChanged();
    }
    
    private void OnChunkMapChanged()
    {
        // calculate chunk bounds
        using var enumerator = m_chunks.GetEnumerator();
        if(enumerator.MoveNext())
        {
            Vector2Int first = enumerator.Current.Key;
            m_chunkIdBounds = new RectInt(first, Vector2Int.one);
        }
        while(enumerator.MoveNext())
        {
            Vector2Int next = enumerator.Current.Key;

            var min = Vector2Int.Min(m_chunkIdBounds.min, next);
            var max = Vector2Int.Max(m_chunkIdBounds.max, next + Vector2Int.one);
            m_chunkIdBounds = new RectInt( min, max - min );
        }
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
    
    public void SetCellObjectDirty(Vector2Int position)
    {
        if(m_chunks.TryGetValue(GetChunkId(position), out var chunk))
        {
            chunk.SetCellObjectDirty(position);
        }
    }
    
    /// <summary>
    /// Used to remember the current chunk along with the position, to reduce dictionary lookups.
    /// </summary>
    private class GridWalker
    {
        private Vector2Int m_chunkId;
        private Vector2Int m_position;
        private World m_world;
        private Chunk m_chunk;
        
        public static readonly Vector2Int[] Directions =
        {
            new (0,1),
            new (1,1),
            new (1,0),
            new (1,-1),
            new (0,-1),
            new (-1,-1),
            new (-1,0),
            new (-1,1)
        };
        
        public void Initialize(World world, Vector2Int position)
        {
            m_world = world;
            m_position = position;
            m_chunkId = m_world.GetChunkId(position);
            m_world.TryGetChunk(m_chunkId, out m_chunk);
        }

        public void Translate(Vector2Int offset) => Position += offset;

        public Vector2Int Position
        {
            get => m_position;
            set
            {
                if(m_position == value)
                    return;
                
                m_position = value;

                Vector2Int newChunkId = m_world.GetChunkId(m_position);
                if(newChunkId == m_chunkId)
                    return;

                m_chunkId = newChunkId;
                m_world.TryGetChunk(m_chunkId, out m_chunk);
            }
        }
        
        public Vector2Int ChunkId => m_chunkId;

        public bool GetCellData(out CellData cellData) => GetCellData(Position, out cellData);
        
        public bool GetCellData(Vector2Int position, out CellData cellData)
        {
            if(m_chunk == null || !m_chunk.TryGetCellIndex(position, out int index))
            {
                return m_world.GetCellData(position, out cellData);
            }
            cellData = m_chunk.GetCellData(index);
            return true;
        }

        public bool FindNextMarchDirection(Vector2Int currentPosition, int currentDirection, out int newDirection)
        {
            for( int i = 0; i < Directions.Length; ++i )
            {
                int newDir = (Directions.Length + currentDirection + 2 - i) % Directions.Length;
                Vector2Int dirOffset = Directions[newDir];
                Vector2Int checkPos = currentPosition + dirOffset;

                if(GetCellData(checkPos, out CellData cellData) && cellData.BlocksMud)
                {
                    newDirection = newDir;
                    return true;
                }
            }

            newDirection = default;
            return false;
        }
    }
    
    public bool GetIsInFence(Vector2Int cellPosition)
    {
        var march = new GridWalker();
        march.Initialize(this, cellPosition);

        // check if we started on a fence
        {
            if(march.GetCellData(out var cellData) && cellData.BlocksMud)
                return true;
        }
        
        int sanityCheck = 10000;
        
        while(true) // march through multiple loops
        {
            
            // first, march to the left to meet a fence
            while(true)
            {
                if(!m_chunkIdBounds.Contains(march.ChunkId))
                    return false;

                bool blocksMud = march.GetCellData(out var cellData) && cellData.BlocksMud;
                if(blocksMud)
                {
                    break;
                }

                march.Translate(Vector2Int.left);
            }

            Debug.DrawLine(GridToWorld(cellPosition), GridToWorld(march.Position), Color.cyan);

            // march around the cell to find a loop
            Vector2Int startPosition = march.Position;
            int leftmostSectionPosition = int.MaxValue;
            int direction = 0; // up
            
            int contactVertical = 0;

            while(true)
            {
                // find the next march direction
                if(march.FindNextMarchDirection(march.Position, direction, out int newDir))
                {
                    Vector2Int dirOffset = GridWalker.Directions[newDir];
                    Vector2Int checkPos = march.Position + dirOffset;

                    Debug.DrawLine(GridToWorld(march.Position), GridToWorld(checkPos), new Color(0.13f, 0.63f, 0.1f, 0.44f));

                    if(dirOffset.y != 0)
                    {
                        if(checkPos.x < cellPosition.x && checkPos.y == startPosition.y)
                        {
                            contactVertical += dirOffset.y;
                            var worldP = GridToWorld(checkPos);
                            Debug.DrawLine(worldP, worldP + (Vector3)dirOffset.X0Y() * 0.5f, Color.yellow);
                            Debug.DrawLine(worldP, worldP + new Vector3(0.25f, 0f, -0.25f), Color.green);
                            Debug.DrawLine(worldP, worldP + new Vector3(-0.25f, 0f, -0.25f), Color.green);
                            Debug.DrawLine(worldP, worldP + new Vector3(0.25f, 0f, -0.25f), Color.green);
                            Debug.DrawLine(worldP, worldP + new Vector3(-0.25f, 0f, 0.25f), Color.green);
                        }
                        if(march.Position.x < cellPosition.x && march.Position.y == startPosition.y)
                        {
                            contactVertical += dirOffset.y;
                            var worldP = GridToWorld(march.Position);
                            Debug.DrawLine(worldP, worldP + (Vector3)dirOffset.X0Y() * 0.5f, Color.yellow);
                            Debug.DrawLine(worldP, worldP + new Vector3(0.25f, 0f, -0.25f), Color.green);
                            Debug.DrawLine(worldP, worldP + new Vector3(-0.25f, 0f, -0.25f), Color.green);
                            Debug.DrawLine(worldP, worldP + new Vector3(0.25f, 0f, -0.25f), Color.green);
                            Debug.DrawLine(worldP, worldP + new Vector3(-0.25f, 0f, 0.25f), Color.green);
                        }
                    }

                    direction = newDir;
                    march.Position = checkPos;
                }

                leftmostSectionPosition = Mathf.Min(leftmostSectionPosition, march.Position.x);
                
                if(startPosition == march.Position)
                {
                    if(contactVertical != 0)
                        return true;
                    else
                        break;
                }

                sanityCheck--;
                if(sanityCheck <= 0)
                    throw new Exception("Sanity check!");
            }

            march.Position = new Vector2Int(leftmostSectionPosition-1, march.Position.y);
            // march.Translate(Vector2Int.left);
        }
    }
}
