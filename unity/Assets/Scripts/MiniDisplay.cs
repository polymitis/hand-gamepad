﻿using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

unsafe public class MiniDisplay : MonoBehaviour
{
    public void UpdateDisplay(XRCameraImage image)
    {
        var conversionParams = new XRCameraImageConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
            outputFormat = TextureFormat.RGBA32,
            transformation = CameraImageTransformation.MirrorY
        };

        if (conversionParams != m_ConversionParams)
        {
            m_ConversionParams = conversionParams;

            Texture2D.Destroy(m_DisplayTexture);
            m_DisplayTexture = new Texture2D(
                m_ConversionParams.outputDimensions.x,
                m_ConversionParams.outputDimensions.y,
                m_ConversionParams.outputFormat,
                false);
            m_DisplayTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        image.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);
        m_DisplayTexture.LoadRawTextureData(buffer);
        m_DisplayTexture.Apply();

        m_Renderer.material.SetTexture("_MainTex", m_DisplayTexture);
    }

    void Awake()
    {
        m_Renderer = GetComponent<Renderer>();
        if (!m_Renderer)
            throw new Exception("Missing Renderer");
    }

    void Update()
    {
        // Do nothing
    }

    private XRCameraImageConversionParams m_ConversionParams;

    private Texture2D m_DisplayTexture;

    private Renderer m_Renderer;
}
