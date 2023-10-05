using System.Collections.Generic;
using UnityEngine;
public class FencePole : GridObject
{
    [SerializeField] private GameObject m_connectorPrefab;
    [SerializeField] private GameObject m_diagonalConnectorPrefab;

    private List<GameObject> m_connectors = new();

    public override bool BlocksMud => true;
    
    public override void OnNeighborsChanged(World world, int dirtyIndex)
    {
        foreach (var obj in m_connectors)
        {
            Destroy(obj.gameObject);
        }
        m_connectors.Clear();

        Vector2Int myPos = world.GetGridPos(dirtyIndex);

        bool anyStraight = false;
        anyStraight |= TryMakeConnector(world, m_connectorPrefab, new Vector2Int(myPos.x + 1,myPos.y), 0f);
        anyStraight |= TryMakeConnector(world, m_connectorPrefab, new Vector2Int(myPos.x,myPos.y + 1), -90f);
        if(!anyStraight)
            TryMakeConnector(world, m_diagonalConnectorPrefab, new Vector2Int(myPos.x + 1,myPos.y+1), -45f);

        bool anyOtherStraight = false;
        anyOtherStraight |= CheckIsPole(world, new Vector2Int(myPos.x - 1, myPos.y));
        anyOtherStraight |= CheckIsPole(world, new Vector2Int(myPos.x, myPos.y + 1));
        if(!anyOtherStraight)
            TryMakeConnector(world, m_diagonalConnectorPrefab, new Vector2Int(myPos.x-1,myPos.y + 1), -135f);
    }

    private bool CheckIsPole(World world, Vector2Int position)
    {
        if(!world.TryGetCellIndex(position, out int rightIndex))
            return false;
        
        var cell = world.GetCellData(rightIndex);
        return cell.GridObject is FencePole;
    }
    
    private bool TryMakeConnector(World world, GameObject connectorPrefab, Vector2Int neighborPos, float azimuth)
    {
        if(world.TryGetCellIndex(neighborPos, out int rightIndex))
        {
            var cell = world.GetCellData(rightIndex);

            if(cell.GridObject is not FencePole)
                return false;
            
            var connector = Instantiate(connectorPrefab, transform);
            connector.transform.localPosition = Vector3.zero;
            connector.transform.localRotation = Quaternion.Euler(0, azimuth, 0);
            m_connectors.Add(connector);
            return true;
        }
        return false;
    }
}
