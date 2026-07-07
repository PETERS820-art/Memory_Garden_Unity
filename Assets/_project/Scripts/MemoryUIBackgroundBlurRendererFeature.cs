using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature]
public class MemoryUIBackgroundBlurRendererFeature : ScriptableRendererFeature
{
    private const string BlurShaderName = "Hidden/MemoryGarden/MemoryUIBlur";
    private static readonly int BlurTextureId = Shader.PropertyToID("_MemoryUIBlurTexture");
    private static readonly int BlurAvailableId = Shader.PropertyToID("_MemoryUIBlurAvailable");
    private static readonly int BlurOffsetId = Shader.PropertyToID("_BlurOffset");

    [System.Serializable]
    public sealed class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        [Range(2, 8)] public int downsample = 4;
        [Range(1, 4)] public int blurPasses = 2;
        [Range(0.5f, 3f)] public float blurRadius = 1.2f;
        public bool runInSceneView = true;
    }

    private sealed class BlurPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private readonly ProfilingSampler blurProfilingSampler = new ProfilingSampler("Memory UI Background Blur");
        private Material blurMaterial;
        private RTHandle copiedColor;
        private RTHandle blurPing;
        private RTHandle blurPong;

        public BlurPass(Settings settings, Material blurMaterial)
        {
            this.settings = settings;
            this.blurMaterial = blurMaterial;
            renderPassEvent = settings.renderPassEvent;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void UpdateMaterial(Material material)
        {
            blurMaterial = material;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor fullResDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            fullResDescriptor.depthBufferBits = 0;
            fullResDescriptor.msaaSamples = 1;
            fullResDescriptor.useMipMap = false;
            fullResDescriptor.autoGenerateMips = false;

            RenderingUtils.ReAllocateIfNeeded(ref copiedColor, fullResDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_MemoryUIBlurSourceCopy");

            RenderTextureDescriptor blurDescriptor = fullResDescriptor;
            blurDescriptor.width = Mathf.Max(1, blurDescriptor.width / Mathf.Max(1, settings.downsample));
            blurDescriptor.height = Mathf.Max(1, blurDescriptor.height / Mathf.Max(1, settings.downsample));

            RenderingUtils.ReAllocateIfNeeded(ref blurPing, blurDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_MemoryUIBlurPing");
            RenderingUtils.ReAllocateIfNeeded(ref blurPong, blurDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_MemoryUIBlurPong");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (blurMaterial == null || copiedColor == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, blurProfilingSampler))
            {
                CoreUtils.SetRenderTarget(cmd, copiedColor);
                Blitter.BlitTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, new Vector4(1f, 1f, 0f, 0f), 0f, false);

                blurMaterial.SetFloat(BlurOffsetId, Mathf.Max(0.5f, settings.blurRadius));
                Blitter.BlitCameraTexture(cmd, copiedColor, blurPing, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blurMaterial, 0);

                RTHandle read = blurPing;
                RTHandle write = blurPong;
                int passes = Mathf.Max(1, settings.blurPasses);
                for (int i = 0; i < passes; i++)
                {
                    blurMaterial.SetFloat(BlurOffsetId, settings.blurRadius + (i * 0.85f));
                    Blitter.BlitCameraTexture(cmd, read, write, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blurMaterial, 1);

                    RTHandle temp = read;
                    read = write;
                    write = temp;
                }

                cmd.SetGlobalTexture(BlurTextureId, read.nameID);
                cmd.SetGlobalFloat(BlurAvailableId, 1f);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            copiedColor?.Release();
            blurPing?.Release();
            blurPong?.Release();
            copiedColor = null;
            blurPing = null;
            blurPong = null;
        }
    }

    [SerializeField] private Settings settings = new Settings();

    private Material blurMaterial;
    private BlurPass blurPass;

    public override void Create()
    {
        if (blurMaterial == null)
        {
            Shader shader = Shader.Find(BlurShaderName);
            if (shader != null)
            {
                blurMaterial = CoreUtils.CreateEngineMaterial(shader);
                blurMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        if (blurPass == null)
        {
            blurPass = new BlurPass(settings, blurMaterial);
        }
        else
        {
            blurPass.UpdateMaterial(blurMaterial);
        }

        Shader.SetGlobalFloat(BlurAvailableId, 0f);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldRun(renderingData) || blurPass == null || blurMaterial == null)
        {
            Shader.SetGlobalFloat(BlurAvailableId, 0f);
            return;
        }

        renderer.EnqueuePass(blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        blurPass?.Dispose();
        blurPass = null;

        if (blurMaterial != null)
        {
            CoreUtils.Destroy(blurMaterial);
            blurMaterial = null;
        }
    }

    private bool ShouldRun(in RenderingData renderingData)
    {
        CameraData cameraData = renderingData.cameraData;
        CameraType cameraType = cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
        {
            return false;
        }

        if (!settings.runInSceneView && cameraData.isSceneViewCamera)
        {
            return false;
        }

        return true;
    }
}
