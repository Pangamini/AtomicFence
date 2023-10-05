using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour
{
    [SerializeField] private Camera m_camera;
    [SerializeField] private World m_world;
    [SerializeField] private Renderer m_gridCursor;
    [SerializeField] private Color m_colorOutside;
    [SerializeField] private Color m_colorMud;
    [SerializeField] private Color m_colorNoMud;
    
    private MaterialPropertyBlock m_propertyBlock;
    private static readonly int s_colorId =Shader.PropertyToID("_Color");

    protected void Awake()
    {
        m_propertyBlock = new();
    }

    protected void Update()
    {
        var ray = m_camera.ScreenPointToRay(Input.mousePosition);
        
        if(m_world.WorldPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector2 gridPoint = m_world.WorldToGrid(worldPoint);
            Vector2Int gridCellPos = Vector2Int.RoundToInt(gridPoint);
            m_gridCursor.transform.position = m_world.GridToWorld(gridCellPos);

            if(Input.GetMouseButton(0))
            {
                m_world.SetGridObject(gridCellPos, m_world.FencePrefab);
            }
            else if(Input.GetMouseButton(1))
            {
                m_world.SetGridObject(gridCellPos, null);
            }
            
            if(Input.GetKeyDown(KeyCode.F5))
                m_world.SaveToDatabase();

            if(Input.GetKeyDown(KeyCode.F8))
                m_world.LoadFromDatabase();

            Color cursorColor = m_world.TryGetCellIndex(gridCellPos, out int cellIndex) 
                ? (m_world.GetCellData(cellIndex).IsMud ? m_colorMud : m_colorNoMud)
                : m_colorOutside;
            
            m_gridCursor.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetColor(s_colorId, cursorColor);
            m_gridCursor.SetPropertyBlock(m_propertyBlock);
        }
    }
}
