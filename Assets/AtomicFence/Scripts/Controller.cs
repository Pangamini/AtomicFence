using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    [SerializeField] private Camera m_camera;
    [SerializeField] private World m_world;
    [SerializeField] private Renderer m_gridCursor;
    [SerializeField] private Color m_colorOutside;
    [SerializeField] private Color m_colorMud;
    [SerializeField] private Color m_colorNoMud;

    [Header("UI")]
    [SerializeField] private Toggle m_reduceFences;
    [SerializeField] private Button m_loadDb;
    [SerializeField] private Button m_saveDb;
    [SerializeField] private Slider m_mudPowerSlider;
    [SerializeField] private TMP_Text m_dbDate;
    [SerializeField] private TMP_Text m_fenceLength;
    [SerializeField] private TMP_Text m_fenceCount;

    [SerializeField] private MudRenderer m_mudRenderer;
    
    private MaterialPropertyBlock m_propertyBlock;
    private static readonly int s_colorId =Shader.PropertyToID("_Color");

    protected void Awake()
    {
        m_propertyBlock = new();

        m_world.ReduceFenceConnections.Changed += OnReduceFenceConnectionsOnChanged;
        m_reduceFences.onValueChanged.AddListener((val)=>m_world.ReduceFenceConnections.Value = val);
        
        m_loadDb.onClick.AddListener(m_world.LoadFromDatabase);
        m_saveDb.onClick.AddListener(m_world.SaveToDatabase);

        m_world.DatabaseUpdateTime.Changed += OnDatabaseUpdateChanged;
        m_world.FenceLength.Changed += OnFenceLengthChanged;
        m_world.FenceCount.Changed += OnFenceCountChanged;
        // m_mudPowerSlider.value = m_mudRenderer.MudPower;
        // m_mudPowerSlider.onValueChanged.AddListener(OnMudPowerChanged);
    }

    protected void OnDestroy()
    {
        m_world.ReduceFenceConnections.Changed -= OnReduceFenceConnectionsOnChanged;
        m_world.DatabaseUpdateTime.Changed -= OnDatabaseUpdateChanged;
        m_world.FenceLength.Changed -= OnFenceLengthChanged;
        m_world.FenceCount.Changed -= OnFenceCountChanged;
        // m_mudPowerSlider.onValueChanged.RemoveListener(OnMudPowerChanged);
    }

    private void OnMudPowerChanged(float sliderValue) => m_mudRenderer.MudPower = sliderValue;

    private void OnFenceCountChanged(int obj) => m_fenceCount.text = obj.ToString(CultureInfo.CurrentCulture);
 
    private void OnFenceLengthChanged(float obj) => m_fenceLength.text = $"{obj.ToString("N2", CultureInfo.CurrentCulture)} m";

    private void OnDatabaseUpdateChanged(DateTime obj)
    {
        m_dbDate.text = obj.ToString(CultureInfo.CurrentCulture);
    }

    private void OnReduceFenceConnectionsOnChanged(bool val) => m_reduceFences.isOn = val;

    protected void Update()
    {
        var ray = m_camera.ScreenPointToRay(Input.mousePosition);
        
        if(m_world.WorldPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector2 gridPoint = m_world.WorldToGrid(worldPoint);
            Vector2Int gridCellPos = Vector2Int.RoundToInt(gridPoint);

            if(!EventSystem.current.IsPointerOverGameObject(-1))
            {
                m_gridCursor.gameObject.SetActive(true);
                m_gridCursor.transform.position = m_world.GridToWorld(gridCellPos);
                if(Input.GetMouseButton(0))
                {
                    m_world.SetGridObject(gridCellPos, m_world.FencePrefab);
                }
                else if(Input.GetMouseButton(1))
                {
                    m_world.SetGridObject(gridCellPos, null);
                }
            }
            else
            {
                m_gridCursor.gameObject.SetActive(false);
            }

            if(Input.GetKeyDown(KeyCode.F5))
                m_world.SaveToDatabase();

            if(Input.GetKeyDown(KeyCode.F8))
                m_world.LoadFromDatabase();

            Color cursorColor = Color.magenta;
            // Color cursorColor = m_world.TryGetCellIndex(gridCellPos, out int cellIndex) 
            //     ? (m_world.GetCellData(cellIndex).IsMud ? m_colorMud : m_colorNoMud)
            //     : m_colorOutside;
            
            m_gridCursor.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetColor(s_colorId, cursorColor);
            m_gridCursor.SetPropertyBlock(m_propertyBlock);
        }
    }
}
