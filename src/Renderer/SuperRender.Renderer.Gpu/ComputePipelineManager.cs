using Microsoft.Extensions.Logging;
using Silk.NET.Vulkan;

namespace SuperRender.Renderer.Gpu;

/// <summary>
/// Manages a Vulkan compute pipeline for JPEG 8x8 IDCT.
/// Each workgroup (64 threads) processes one 8x8 DCT block.
/// Input: dequantized DCT coefficients (int[64] per block).
/// Output: clamped pixel values 0-255 (int[64] per block).
/// </summary>
public sealed unsafe class ComputePipelineManager : IDisposable
{
    private readonly VulkanContext _ctx;
    private readonly ILogger? _logger;

    public Pipeline Pipeline { get; private set; }
    public PipelineLayout PipelineLayout { get; private set; }
    public DescriptorSetLayout DescriptorSetLayout { get; private set; }

    private bool _disposed;

    public ComputePipelineManager(VulkanContext ctx, ILogger? logger = null)
    {
        _ctx = ctx;
        _logger = logger;
        CreateDescriptorSetLayout();
        CreatePipelineLayout();
        CreateComputePipeline();
    }

    private void CreateDescriptorSetLayout()
    {
        var bindings = new DescriptorSetLayoutBinding[2];

        // Binding 0: input DCT coefficients SSBO (readonly)
        bindings[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit,
        };

        // Binding 1: output pixel values SSBO (writeonly)
        bindings[1] = new DescriptorSetLayoutBinding
        {
            Binding = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit,
        };

        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            var layoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 2,
                PBindings = pBindings,
            };

            var result = _ctx.Vk.CreateDescriptorSetLayout(_ctx.Device, in layoutInfo, null,
                out var layout);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create compute descriptor set layout: {result}");
            DescriptorSetLayout = layout;
        }
    }

    private void CreatePipelineLayout()
    {
        var setLayout = DescriptorSetLayout;
        var pushConstantRange = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.ComputeBit,
            Offset = 0,
            Size = 4, // uint blockCount
        };

        var layoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange,
        };

        var result = _ctx.Vk.CreatePipelineLayout(_ctx.Device, in layoutInfo, null,
            out var pipelineLayout);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create compute pipeline layout: {result}");
        PipelineLayout = pipelineLayout;
    }

    private void CreateComputePipeline()
    {
        var shaderCode = ShaderCompiler.LoadOrCompileComputeShader(
            "Resources.Shaders.idct.comp.spv",
            "Shaders.idct.comp.glsl", _logger);

        if (shaderCode is null)
        {
            _logger?.LogWarning("Could not load IDCT compute shader. GPU IDCT unavailable.");
            return;
        }

        ShaderModule shaderModule;
        fixed (byte* pCode = shaderCode)
        {
            var moduleInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)shaderCode.Length,
                PCode = (uint*)pCode,
            };

            var result = _ctx.Vk.CreateShaderModule(_ctx.Device, in moduleInfo, null,
                out shaderModule);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create compute shader module: {result}");
        }

        var entryPoint = System.Text.Encoding.UTF8.GetBytes("main\0");
        fixed (byte* pEntry = entryPoint)
        {
            var stageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.ComputeBit,
                Module = shaderModule,
                PName = pEntry,
            };

            var pipelineInfo = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = stageInfo,
                Layout = PipelineLayout,
            };

            var result = _ctx.Vk.CreateComputePipelines(_ctx.Device, default, 1, in pipelineInfo,
                null, out var pipeline);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create compute pipeline: {result}");
            Pipeline = pipeline;
        }

        _ctx.Vk.DestroyShaderModule(_ctx.Device, shaderModule, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (Pipeline.Handle != 0)
            _ctx.Vk.DestroyPipeline(_ctx.Device, Pipeline, null);
        if (PipelineLayout.Handle != 0)
            _ctx.Vk.DestroyPipelineLayout(_ctx.Device, PipelineLayout, null);
        if (DescriptorSetLayout.Handle != 0)
            _ctx.Vk.DestroyDescriptorSetLayout(_ctx.Device, DescriptorSetLayout, null);
    }
}
