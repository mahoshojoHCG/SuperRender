using Silk.NET.Vulkan;

namespace SuperRender.Gpu;

public sealed unsafe class PipelineManager : IDisposable
{
    private readonly VulkanContext _ctx;

    public Pipeline QuadPipeline { get; private set; }
    public PipelineLayout QuadPipelineLayout { get; private set; }
    public Pipeline TextPipeline { get; private set; }
    public PipelineLayout TextPipelineLayout { get; private set; }
    public DescriptorSetLayout TextDescriptorSetLayout { get; private set; }

    private bool _disposed;

    public PipelineManager(VulkanContext ctx, RenderPass renderPass, Extent2D extent)
    {
        _ctx = ctx;

        CreateQuadPipeline(renderPass, extent);
        CreateTextPipeline(renderPass, extent);
    }

    private void CreateQuadPipeline(RenderPass renderPass, Extent2D extent)
    {
        var vertBytes = ShaderCompiler.LoadOrCompileShader(
            "SuperRender.Gpu.Resources.Shaders.quad.vert.spv",
            "SuperRender.Gpu.Shaders.quad.vert.glsl", isVertex: true);
        var fragBytes = ShaderCompiler.LoadOrCompileShader(
            "SuperRender.Gpu.Resources.Shaders.quad.frag.spv",
            "SuperRender.Gpu.Shaders.quad.frag.glsl", isVertex: false);

        if (vertBytes == null || fragBytes == null)
        {
            Console.WriteLine("Warning: Quad shaders not available. Skipping quad pipeline creation.");
            return;
        }

        var vertModule = CreateShaderModule(vertBytes);
        var fragModule = CreateShaderModule(fragBytes);

        try
        {
            // Vertex input: vec2 position (offset 0), vec4 color (offset 8) — stride 24
            var bindingDesc = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = 24,
                InputRate = VertexInputRate.Vertex,
            };

            var attributeDescs = stackalloc VertexInputAttributeDescription[]
            {
                new() { Binding = 0, Location = 0, Format = Format.R32G32Sfloat, Offset = 0 },   // vec2 position
                new() { Binding = 0, Location = 1, Format = Format.R32G32B32A32Sfloat, Offset = 8 }, // vec4 color
            };

            // Push constants: mat4 projection (64 bytes, vertex stage)
            var pushConstantRange = new PushConstantRange
            {
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Size = 64,
            };

            var layoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 0,
                PSetLayouts = null,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange,
            };

            var result = _ctx.Vk.CreatePipelineLayout(_ctx.Device, in layoutInfo, null, out var pipelineLayout);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create quad pipeline layout: {result}");
            QuadPipelineLayout = pipelineLayout;

            QuadPipeline = CreateGraphicsPipeline(
                vertModule, fragModule,
                &bindingDesc, 1,
                attributeDescs, 2,
                QuadPipelineLayout,
                renderPass, extent,
                blendingEnabled: true);
        }
        finally
        {
            _ctx.Vk.DestroyShaderModule(_ctx.Device, vertModule, null);
            _ctx.Vk.DestroyShaderModule(_ctx.Device, fragModule, null);
        }
    }

    private void CreateTextPipeline(RenderPass renderPass, Extent2D extent)
    {
        var vertBytes = ShaderCompiler.LoadOrCompileShader(
            "SuperRender.Gpu.Resources.Shaders.text.vert.spv",
            "SuperRender.Gpu.Shaders.text.vert.glsl", isVertex: true);
        var fragBytes = ShaderCompiler.LoadOrCompileShader(
            "SuperRender.Gpu.Resources.Shaders.text.frag.spv",
            "SuperRender.Gpu.Shaders.text.frag.glsl", isVertex: false);

        if (vertBytes == null || fragBytes == null)
        {
            Console.WriteLine("Warning: Text shaders not available. Skipping text pipeline creation.");
            return;
        }

        var vertModule = CreateShaderModule(vertBytes);
        var fragModule = CreateShaderModule(fragBytes);

        try
        {
            // Vertex input: vec2 position (0), vec2 texcoord (8), vec4 color (16) — stride 32
            var bindingDesc = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = 32,
                InputRate = VertexInputRate.Vertex,
            };

            var attributeDescs = stackalloc VertexInputAttributeDescription[]
            {
                new() { Binding = 0, Location = 0, Format = Format.R32G32Sfloat, Offset = 0 },      // vec2 position
                new() { Binding = 0, Location = 1, Format = Format.R32G32Sfloat, Offset = 8 },      // vec2 texcoord
                new() { Binding = 0, Location = 2, Format = Format.R32G32B32A32Sfloat, Offset = 16 }, // vec4 color
            };

            // Descriptor set layout: binding 0 = combined image sampler (fragment)
            var samplerBinding = new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
                PImmutableSamplers = null,
            };

            var descriptorLayoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 1,
                PBindings = &samplerBinding,
            };

            var result = _ctx.Vk.CreateDescriptorSetLayout(
                _ctx.Device, in descriptorLayoutInfo, null, out var descriptorSetLayout);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create text descriptor set layout: {result}");
            TextDescriptorSetLayout = descriptorSetLayout;

            // Push constants: mat4 projection (64 bytes, vertex stage)
            var pushConstantRange = new PushConstantRange
            {
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Size = 64,
            };

            var setLayout = TextDescriptorSetLayout;
            var layoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &setLayout,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange,
            };

            result = _ctx.Vk.CreatePipelineLayout(_ctx.Device, in layoutInfo, null, out var pipelineLayout);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create text pipeline layout: {result}");
            TextPipelineLayout = pipelineLayout;

            TextPipeline = CreateGraphicsPipeline(
                vertModule, fragModule,
                &bindingDesc, 1,
                attributeDescs, 3,
                TextPipelineLayout,
                renderPass, extent,
                blendingEnabled: true);
        }
        finally
        {
            _ctx.Vk.DestroyShaderModule(_ctx.Device, vertModule, null);
            _ctx.Vk.DestroyShaderModule(_ctx.Device, fragModule, null);
        }
    }

    private Pipeline CreateGraphicsPipeline(
        ShaderModule vertModule,
        ShaderModule fragModule,
        VertexInputBindingDescription* pBindingDesc,
        uint bindingCount,
        VertexInputAttributeDescription* pAttributeDescs,
        uint attributeCount,
        PipelineLayout pipelineLayout,
        RenderPass renderPass,
        Extent2D extent,
        bool blendingEnabled)
    {
        var entryPoint = stackalloc byte[] { (byte)'m', (byte)'a', (byte)'i', (byte)'n', 0 };

        var shaderStages = stackalloc PipelineShaderStageCreateInfo[]
        {
            new()
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertModule,
                PName = entryPoint,
            },
            new()
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragModule,
                PName = entryPoint,
            },
        };

        var vertexInputInfo = new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = bindingCount,
            PVertexBindingDescriptions = pBindingDesc,
            VertexAttributeDescriptionCount = attributeCount,
            PVertexAttributeDescriptions = pAttributeDescs,
        };

        var inputAssembly = new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false,
        };

        var viewport = new Viewport
        {
            X = 0, Y = 0,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0, MaxDepth = 1,
        };

        var scissor = new Rect2D
        {
            Offset = new Offset2D(0, 0),
            Extent = extent,
        };

        var viewportState = new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor,
        };

        var rasterizer = new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1.0f,
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.CounterClockwise,
            DepthBiasEnable = false,
        };

        var multisampling = new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
        };

        var colorBlendAttachment = new PipelineColorBlendAttachmentState
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit |
                             ColorComponentFlags.BBit | ColorComponentFlags.ABit,
        };

        if (blendingEnabled)
        {
            colorBlendAttachment.BlendEnable = true;
            colorBlendAttachment.SrcColorBlendFactor = BlendFactor.SrcAlpha;
            colorBlendAttachment.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
            colorBlendAttachment.ColorBlendOp = BlendOp.Add;
            colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.One;
            colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.AlphaBlendOp = BlendOp.Add;
        }

        var colorBlending = new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment,
        };

        // Dynamic scissor so we can change clip rects per draw call
        var dynamicState = DynamicState.Scissor;
        var dynamicStateInfo = new PipelineDynamicStateCreateInfo
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 1,
            PDynamicStates = &dynamicState,
        };

        var pipelineInfo = new GraphicsPipelineCreateInfo
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = shaderStages,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PColorBlendState = &colorBlending,
            PDynamicState = &dynamicStateInfo,
            Layout = pipelineLayout,
            RenderPass = renderPass,
            Subpass = 0,
        };

        var result = _ctx.Vk.CreateGraphicsPipelines(
            _ctx.Device, default, 1, in pipelineInfo, null, out var pipeline);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create graphics pipeline: {result}");

        return pipeline;
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        fixed (byte* pCode = code)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)pCode,
            };

            var result = _ctx.Vk.CreateShaderModule(_ctx.Device, in createInfo, null, out var module);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create shader module: {result}");

            return module;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (QuadPipeline.Handle != 0)
            _ctx.Vk.DestroyPipeline(_ctx.Device, QuadPipeline, null);
        if (QuadPipelineLayout.Handle != 0)
            _ctx.Vk.DestroyPipelineLayout(_ctx.Device, QuadPipelineLayout, null);

        if (TextPipeline.Handle != 0)
            _ctx.Vk.DestroyPipeline(_ctx.Device, TextPipeline, null);
        if (TextPipelineLayout.Handle != 0)
            _ctx.Vk.DestroyPipelineLayout(_ctx.Device, TextPipelineLayout, null);
        if (TextDescriptorSetLayout.Handle != 0)
            _ctx.Vk.DestroyDescriptorSetLayout(_ctx.Device, TextDescriptorSetLayout, null);
    }
}
