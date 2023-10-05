using System;
using System.Collections.Generic;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Pool;
public class FencePole : GridObject
{
    [SerializeField] private GameObject m_connectorPrefab;
    [SerializeField] private GameObject m_diagonalConnectorPrefab;

    private readonly List<GameObject> m_connectors = new();
    private static MaterialPropertyBlock s_propertyBlock;
    private static readonly int s_colorId = Shader.PropertyToID("_Color");
    private Color m_color;
    public float FenceLength { get; private set; }

    public override bool BlocksMud => true;

    protected void Awake()
    {
        s_propertyBlock ??= new();
    }

    public override void Initialize(World world)
    {
        m_color = world.PickRandomColor();
    }
    

    public override void OnNeighborsChanged(Chunk chunk, int dirtyIndex)
    {
        // update connections
        FenceLength = 0;
        
        foreach (var obj in m_connectors)
        {
            Destroy(obj.gameObject);
        }
        m_connectors.Clear();

        Vector2Int myPos = chunk.GetGridPos(dirtyIndex);

        bool anyStraight = false;
        anyStraight |= TryMakeConnector(chunk, m_connectorPrefab, new Vector2Int(myPos.x + 1,myPos.y), 0f, LengthStraight);
        anyStraight |= TryMakeConnector(chunk, m_connectorPrefab, new Vector2Int(myPos.x,myPos.y + 1), -90f, LengthStraight);
        
        if(!chunk.World.ReduceFenceConnections.Value || !anyStraight)
            TryMakeConnector(chunk, m_diagonalConnectorPrefab, new Vector2Int(myPos.x + 1,myPos.y+1), -45f, LengthDiagonal);

        bool anyOtherStraight = false;
        anyOtherStraight |= CheckIsPole(chunk, new Vector2Int(myPos.x - 1, myPos.y));
        anyOtherStraight |= CheckIsPole(chunk, new Vector2Int(myPos.x, myPos.y + 1));
        
        if(!chunk.World.ReduceFenceConnections.Value || !anyOtherStraight)
            TryMakeConnector(chunk, m_diagonalConnectorPrefab, new Vector2Int(myPos.x-1,myPos.y + 1), -135f, LengthDiagonal);

        Repaint();
    }
    private const float LengthDiagonal = 1.41421356237f;
    private const float LengthStraight = 1f;

    private void Repaint()
    {
        s_propertyBlock.SetColor(s_colorId, m_color);
        ListPool<Renderer>.Get(out var renderers);
        GetComponentsInChildren(renderers);
        foreach (var rend in renderers)
        {
            rend.SetPropertyBlock(s_propertyBlock);
        }
        ListPool<Renderer>.Release(renderers);
    }

    private bool CheckIsPole(Chunk chunk, Vector2Int position)
    {
        if (chunk.GetCellDataInNeighborChunks(position, out var cell))
        {
            return cell.GridObject is FencePole;
        }
        return false;
    }
    
    private bool TryMakeConnector(Chunk chunk, GameObject connectorPrefab, Vector2Int neighborPos, float azimuth, float length)
    {
        if(chunk.GetCellDataInNeighborChunks(neighborPos, out CellData cell))
        {
            if(cell.GridObject is not FencePole)
                return false;
            
            var connector = Instantiate(connectorPrefab, transform);
            connector.transform.localPosition = Vector3.zero;
            connector.transform.localRotation = Quaternion.Euler(0, azimuth, 0);
            m_connectors.Add(connector);
            FenceLength += length;
            return true;
        }
        return false;
    }
}
