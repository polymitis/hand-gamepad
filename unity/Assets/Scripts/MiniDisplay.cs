using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

public class MiniDisplay : MonoBehaviour
{
    public void UpdateDisplay(byte[] managedPixelBuffer, Vector2Int size)
    {
        if (size != m_BufferSize)
        {
            m_BufferSize = size;

            Texture2D.Destroy(m_DisplayTexture);
            m_DisplayTexture = new Texture2D(
                m_BufferSize.x,
                m_BufferSize.y,
                TextureFormat.RGBA32,
                false);
            m_DisplayTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        var buffer = new NativeArray<byte>(managedPixelBuffer, Allocator.Temp);
        m_DisplayTexture.LoadRawTextureData(buffer);
        m_DisplayTexture.Apply();

        m_CanvasRenderer.SetTexture(m_DisplayTexture);
    }

    unsafe public void UpdateDisplay(XRCameraImage image)
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

        m_CanvasRenderer.SetTexture(m_DisplayTexture);
    }

    void Awake()
    {
        m_CanvasRenderer = GetComponent<CanvasRenderer>();
        if (!m_CanvasRenderer)
            throw new Exception("Missing CanvasRenderer");
    }

    private Vector2Int m_BufferSize;

    private XRCameraImageConversionParams m_ConversionParams;

    private Texture2D m_DisplayTexture;

    private CanvasRenderer m_CanvasRenderer;
}
