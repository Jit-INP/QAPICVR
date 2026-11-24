using UnityEngine;

// This script orchestrates the entire point cloud generation and rendering process.
// Attach it to a GameObject in your scene.
[ExecuteAlways]
public class PointCloudGenerator : MonoBehaviour
{
    [Header("Control")]
    [Tooltip("If checked, the point cloud will be rendered. Uncheck to hide it.")]
    public bool showPointCloud = true;

    [Header("Assets")]
    [Tooltip("The compute shader that calculates point positions.")]
    public ComputeShader computeShader;
    [Tooltip("The URP shader used to render the points.")]
    public Shader pointRenderShader;
    [Tooltip("The panoramic (equirectangular) color texture.")]
    public Texture2D colorTexture;
    [Tooltip("The panoramic depth texture. White is far, black is near.")]
    public Texture2D depthTexture;

    [Header("Settings")]
    [Tooltip("Invert the depth map (use when your depth map has opposite values).")]
    public bool invertDepth = false;
    [Tooltip("Distance of the nearest point from the origin")] 
    public float nearPlane = 0.1f;
    [Tooltip("Distance of the furthest point from the origin")] 
    public float farPlane = 100f;
    [Tooltip("Vertical offset for the pointcloud center, typical VR height is ~1.6m.")]
    public float heightOffset = 1.6f;

    [Tooltip("Angular size of the point in degrees")]
    public float angularSize = 0.5f;
    [Tooltip("Downsamples the input textures to generate fewer points (1 = full res, 2 = half res, etc.). Increases performance.")]
    [Range(1, 8)]
    public int downsample = 1;

    // Private fields
    private Material pointMaterial;
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer colorsBuffer;
    private ComputeBuffer depthsBuffer;
    private int totalPoints;
    private int kernelHandle;
    private int textureWidth;
    private int textureHeight;

    // Track previous values to detect changes
    private float previousNearPlane;
    private float previousFarPlane;
    private bool previousInvertDepth;
    private float previousHeightOffset;
    private Texture2D previousColorTexture;
    private Texture2D previousDepthTexture;

    // This function is called when the object becomes enabled and active.
    void OnEnable()
    {
        // Basic validation to ensure all required assets are assigned.
        if (computeShader == null || pointRenderShader == null || colorTexture == null || depthTexture == null)
        {
            Debug.LogError("Please assign all required assets in the PointCloudGenerator inspector.");
            return;
        }

        InitializeResources();
        previousNearPlane = nearPlane;
        previousFarPlane = farPlane;
        previousInvertDepth = invertDepth;
        previousHeightOffset = heightOffset;
        previousColorTexture = colorTexture;
        previousDepthTexture = depthTexture;
    }

    // This function is called when the object becomes disabled or inactive.
    void OnDisable()
    {
        ReleaseResources();
    }

    // This is called in the editor whenever a value is changed in the inspector.
    void OnValidate()
    {
        // If the application is running, re-initialize everything to reflect the new settings.
        if (Application.isPlaying && positionsBuffer != null)
        {
            ReleaseResources();
            InitializeResources();
        }
    }

    // Handles resource creation (buffers, material) and dispatches the compute shader.
    private void InitializeResources()
    {
        // Calculate the dimensions and total number of points based on the downsample factor.
        textureWidth = colorTexture.width / downsample;
        textureHeight = colorTexture.height / downsample;
        totalPoints = textureWidth * textureHeight;

        // Initialize the compute buffers.
        // Each element is a float4 (Vector4), so the stride is 4 * sizeof(float).
        positionsBuffer = new ComputeBuffer(totalPoints, sizeof(float) * 4);
        colorsBuffer = new ComputeBuffer(totalPoints, sizeof(float) * 4);
        depthsBuffer = new ComputeBuffer(totalPoints, sizeof(float));

        // Create a new material using the point rendering shader.
        pointMaterial = new Material(pointRenderShader);
        pointMaterial.hideFlags = HideFlags.HideAndDontSave;
        pointMaterial.enableInstancing = true; // Enable GPU instancing for stereo rendering

        // Find the main kernel in the compute shader.
        kernelHandle = computeShader.FindKernel("CSMain");

        // Dispatch the compute shader to generate the point cloud data.
        DispatchComputeShader();
    }

    // Dispatches the compute shader with all the necessary data.
    private void DispatchComputeShader()
    {
        computeShader.SetTexture(kernelHandle, "_ColorTex", colorTexture);
        computeShader.SetTexture(kernelHandle, "_DepthTex", depthTexture);

        computeShader.SetBuffer(kernelHandle, "positions", positionsBuffer);
        computeShader.SetBuffer(kernelHandle, "colors", colorsBuffer);
        computeShader.SetBuffer(kernelHandle, "depths", depthsBuffer);

        computeShader.SetInt("_TexWidth", textureWidth);
        computeShader.SetInt("_TexHeight", textureHeight);
        computeShader.SetInt("_TotalPoints", totalPoints);
        computeShader.SetFloat("_NearPlane", nearPlane);
        computeShader.SetFloat("_FarPlane", farPlane);
        computeShader.SetInt("_InvertDepth", invertDepth ? 1 : 0);
        computeShader.SetFloat("_HeightOffset", heightOffset);

        // Calculate the number of thread groups needed to process all points.
        // Our compute shader uses [numthreads(256,1,1)], so we divide by 256.
        int threadGroups = Mathf.CeilToInt((float)totalPoints / 256.0f);
        computeShader.Dispatch(kernelHandle, threadGroups, 1, 1);
    }


    // Release all allocated resources.
    private void ReleaseResources()
    {
        if (positionsBuffer != null)
        {
            positionsBuffer.Release();
            positionsBuffer = null;
        }

        if (colorsBuffer != null)
        {
            colorsBuffer.Release();
            colorsBuffer = null;
        }

        if (depthsBuffer != null)
        {
            depthsBuffer.Release();
            depthsBuffer = null;
        }

        if (pointMaterial != null)
        {
            DestroyImmediate(pointMaterial);
            pointMaterial = null;
        }
    }

    // Update is called once per frame. We use it to draw the point cloud.
    void Update()
    {
        if (!showPointCloud)
        {
            return;
        }

        // If either texture is missing now, release resources and stop.
        if (colorTexture == null || depthTexture == null)
        {
            ReleaseResources();
            previousColorTexture = colorTexture;
            previousDepthTexture = depthTexture;
            return;
        }

        // Detect runtime changes to the linked textures (assigned from other scripts, etc.)
        if (colorTexture != previousColorTexture || depthTexture != previousDepthTexture)
        {
            // Recreate buffers/material sized for the new textures and regenerate the cloud
            ReleaseResources();
            InitializeResources();

            // Update previous refs and early-out; InitializeResources already dispatched
            previousColorTexture = colorTexture;
            previousDepthTexture = depthTexture;
            previousNearPlane = nearPlane;
            previousFarPlane = farPlane;
            previousInvertDepth = invertDepth;
            previousHeightOffset = heightOffset;
            return;
        }

        // Don't try to draw if initialization failed.
        if (pointMaterial == null || positionsBuffer == null || colorsBuffer == null || depthsBuffer == null)
        {
            return;
        }

        // Check if near/far/invert/height changed
        if (nearPlane != previousNearPlane || farPlane != previousFarPlane || invertDepth != previousInvertDepth || heightOffset != previousHeightOffset)
        {
            // Recompute positions with new parameters
            DispatchComputeShader();
            previousNearPlane = nearPlane;
            previousFarPlane = farPlane;
            previousInvertDepth = invertDepth;
            previousHeightOffset = heightOffset;
        }

        // Update shader properties that can be changed at runtime.
        pointMaterial.SetFloat("_AngularSize", angularSize);
        pointMaterial.SetInt("_InvertDepth", invertDepth ? 1 : 0);
        pointMaterial.SetBuffer("positions", positionsBuffer);
        pointMaterial.SetBuffer("colors", colorsBuffer);
        pointMaterial.SetBuffer("depths", depthsBuffer);

        // Define a bounding box that is large enough to contain the entire point cloud.
        // This is necessary for the GPU to know when to render the points.
        var bounds = new Bounds(transform.position, Vector3.one * 2 * farPlane);

        // Draw the points procedurally.
        Graphics.DrawProcedural(
            pointMaterial,
            bounds,
            MeshTopology.Points,
            totalPoints,
            1,
            null,
            null,
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }
}
