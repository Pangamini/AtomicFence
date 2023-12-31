using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class MudRenderer : MonoBehaviour
{
    [SerializeField] private World m_world;
    [SerializeField] private string m_shaderTextureField = "_MainTex";
    private readonly int m_mudPowerId = Shader.PropertyToID("_MudPower");
    private Texture2D m_texture;
    private MaterialPropertyBlock m_block;
    private Renderer m_renderer;

    public float MudPower
    {
        get
        {
            m_renderer.GetPropertyBlock(m_block);
            if(m_block.HasFloat(m_mudPowerId))
                return m_block.GetFloat(m_mudPowerId);
            else
                return m_renderer.sharedMaterial.GetFloat(m_mudPowerId);
        }
        set
        {
            m_renderer.GetPropertyBlock(m_block);
            m_block.SetFloat(m_mudPowerId, value);
            m_renderer.SetPropertyBlock(m_block);
        }
    }

    private void Awake()
    {
        m_renderer = GetComponent<Renderer>();
        m_block = new();
    }

    private void Start()
    {
        OnMudUpdated();
    }

    private void OnEnable()
    {
        m_texture = new Texture2D(m_world.Bounds.width, m_world.Bounds.height, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.DontInitializePixels);
        m_block.Clear();
        m_block.SetTexture(m_shaderTextureField, m_texture);
        m_renderer.SetPropertyBlock(m_block);
        m_world.MudUpdated += OnMudUpdated;
    }
    
    private void OnDisable()
    {
        m_block.Clear();
        m_renderer.SetPropertyBlock(m_block);
        m_world.MudUpdated -= OnMudUpdated;
        Destroy(m_texture);
    }
    
    private void OnMudUpdated()
    {
        var colors = new NativeArray<byte>(m_world.Bounds.width * m_world.Bounds.height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for( int i = 0; i < colors.Length; ++i )
        {
            var cell = m_world.GetCellData(i);
            colors[i] = cell.IsMud ? (byte)255 : (byte)0;
        }
        m_texture.SetPixelData(colors, 0);
        m_texture.Apply();
    }
}
