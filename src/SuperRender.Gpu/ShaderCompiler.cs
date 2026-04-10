using System.Reflection;
using System.Runtime.InteropServices;

namespace SuperRender.Gpu;

public static class ShaderCompiler
{
    public static byte[]? LoadOrCompileShader(string spvResourceName, string glslResourceName, bool isVertex)
    {
        // First try pre-compiled SPIR-V
        var spv = LoadEmbeddedResource(spvResourceName);
        if (spv != null) return spv;

        // Fallback: compile GLSL at runtime via shaderc
        var glsl = LoadEmbeddedResourceString(glslResourceName);
        if (glsl == null) return null;

        return CompileGlsl(glsl, isVertex ? "vert" : "frag", glslResourceName);
    }

    private static byte[]? CompileGlsl(string source, string stage, string filename)
    {
        try
        {
            var compiler = ShadercNative.shaderc_compiler_initialize();
            if (compiler == nint.Zero) return null;

            try
            {
                var options = ShadercNative.shaderc_compile_options_initialize();
                if (options != nint.Zero)
                {
                    ShadercNative.shaderc_compile_options_set_target_env(
                        options, 0 /* vulkan */, 0 /* vulkan 1.0 */);
                }

                var shaderKind = stage switch
                {
                    "vert" => 0,  // shaderc_vertex_shader
                    "frag" => 1,  // shaderc_fragment_shader
                    _ => 0
                };

                var sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
                var filenameBytes = System.Text.Encoding.UTF8.GetBytes(filename + "\0");
                var entryBytes = System.Text.Encoding.UTF8.GetBytes("main\0");

                unsafe
                {
                    fixed (byte* pSource = sourceBytes)
                    fixed (byte* pFilename = filenameBytes)
                    fixed (byte* pEntry = entryBytes)
                    {
                        var result = ShadercNative.shaderc_compile_into_spv(
                            compiler, (nint)pSource, (nuint)sourceBytes.Length,
                            shaderKind, (nint)pFilename, (nint)pEntry, options);

                        if (result == nint.Zero)
                        {
                            if (options != nint.Zero) ShadercNative.shaderc_compile_options_release(options);
                            return null;
                        }

                        try
                        {
                            var status = ShadercNative.shaderc_result_get_compilation_status(result);
                            if (status != 0)
                            {
                                var errPtr = ShadercNative.shaderc_result_get_error_message(result);
                                var err = errPtr != nint.Zero ? Marshal.PtrToStringUTF8(errPtr) : "Unknown error";
                                Console.WriteLine($"Shader compile error ({filename}): {err}");
                                return null;
                            }

                            var length = (int)ShadercNative.shaderc_result_get_length(result);
                            var dataPtr = ShadercNative.shaderc_result_get_bytes(result);
                            var bytes = new byte[length];
                            Marshal.Copy(dataPtr, bytes, 0, length);
                            return bytes;
                        }
                        finally
                        {
                            ShadercNative.shaderc_result_release(result);
                            if (options != nint.Zero) ShadercNative.shaderc_compile_options_release(options);
                        }
                    }
                }
            }
            finally
            {
                ShadercNative.shaderc_compiler_release(compiler);
            }
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("Warning: shaderc native library not found. Cannot compile shaders at runtime.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Shader compilation failed: {ex.Message}");
            return null;
        }
    }

    private static byte[]? LoadEmbeddedResource(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string? LoadEmbeddedResourceString(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

internal static partial class ShadercNative
{
    private const string LibName = "shaderc_shared";

    [LibraryImport(LibName)]
    public static partial nint shaderc_compiler_initialize();

    [LibraryImport(LibName)]
    public static partial void shaderc_compiler_release(nint compiler);

    [LibraryImport(LibName)]
    public static partial nint shaderc_compile_options_initialize();

    [LibraryImport(LibName)]
    public static partial void shaderc_compile_options_release(nint options);

    [LibraryImport(LibName)]
    public static partial void shaderc_compile_options_set_target_env(nint options, int target, uint version);

    [LibraryImport(LibName)]
    public static partial nint shaderc_compile_into_spv(
        nint compiler, nint source_text, nuint source_text_size,
        int shader_kind, nint input_file_name, nint entry_point_name, nint additional_options);

    [LibraryImport(LibName)]
    public static partial int shaderc_result_get_compilation_status(nint result);

    [LibraryImport(LibName)]
    public static partial nuint shaderc_result_get_length(nint result);

    [LibraryImport(LibName)]
    public static partial nint shaderc_result_get_bytes(nint result);

    [LibraryImport(LibName)]
    public static partial nint shaderc_result_get_error_message(nint result);

    [LibraryImport(LibName)]
    public static partial void shaderc_result_release(nint result);
}
