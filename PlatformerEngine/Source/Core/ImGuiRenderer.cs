using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PlatformerEngine.Source.Core;

/// <summary>
/// ImGui renderer for MonoGame - handles initialization, input, and rendering
/// </summary>
public class ImGuiRenderer
{
    private Game _game;
    private GraphicsDevice _graphicsDevice;
    private BasicEffect _effect;
    private RasterizerState _rasterizerState;
    
    private byte[] _vertexData;
    private VertexBuffer _vertexBuffer;
    private int _vertexBufferSize;
    
    private byte[] _indexData;
    private IndexBuffer _indexBuffer;
    private int _indexBufferSize;
    
    // Texture management
    private Dictionary<IntPtr, Texture2D> _loadedTextures;
    private int _textureId;
    private IntPtr? _fontTextureId;
    
    // Input state tracking
    private int _scrollWheelValue;
    private Keys[] _allKeys = Enum.GetValues<Keys>();
    
    public ImGuiRenderer(Game game)
    {
        _game = game;
        _graphicsDevice = game.GraphicsDevice;
        _loadedTextures = new Dictionary<IntPtr, Texture2D>();
        
        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        
        // Setup display size
        io.DisplaySize = new System.Numerics.Vector2(
            _graphicsDevice.PresentationParameters.BackBufferWidth,
            _graphicsDevice.PresentationParameters.BackBufferHeight
        );
        io.DisplayFramebufferScale = System.Numerics.Vector2.One;
        
        CreateDeviceResources();
        SetKeyMappings();
        
        _scrollWheelValue = 0;
        
        RebuildFontAtlas();
    }
    
    /// <summary>
    /// Creates graphics device resources (shaders, buffers, etc.)
    /// </summary>
    private void CreateDeviceResources()
    {
        _effect = new BasicEffect(_graphicsDevice);
        _effect.VertexColorEnabled = true;
        _effect.TextureEnabled = true;
        
        _rasterizerState = new RasterizerState()
        {
            CullMode = CullMode.None,
            DepthBias = 0,
            FillMode = FillMode.Solid,
            MultiSampleAntiAlias = false,
            ScissorTestEnable = true,
            SlopeScaleDepthBias = 0
        };
        
        SetupInput();
    }
    
    /// <summary>
    /// Maps keyboard keys to ImGui (no longer needed in ImGui 1.91+)
    /// </summary>
    private void SetKeyMappings()
    {
        // Key mapping is now automatic in ImGui 1.91+
        // We'll handle keys directly in UpdateInput
    }
    
    /// <summary>
    /// Setup text input handling
    /// </summary>
    private void SetupInput()
    {
        _game.Window.TextInput += (s, a) =>
        {
            if (a.Character == '\t') return;
            ImGui.GetIO().AddInputCharacter(a.Character);
        };
    }
    
    /// <summary>
    /// Rebuilds the font atlas texture
    /// </summary>
    private void RebuildFontAtlas()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
        
        byte[] data = new byte[width * height * bytesPerPixel];
        Marshal.Copy(pixels, data, 0, data.Length);
        
        Texture2D fontTexture = new Texture2D(_graphicsDevice, width, height, false, SurfaceFormat.Color);
        fontTexture.SetData(data);
        
        if (_fontTextureId.HasValue)
        {
            UnbindTexture(_fontTextureId.Value);
        }
        
        _fontTextureId = BindTexture(fontTexture);
        io.Fonts.SetTexID(_fontTextureId.Value);
        io.Fonts.ClearTexData();
    }
    
    /// <summary>
    /// Binds a texture and returns its ID for ImGui
    /// </summary>
    public IntPtr BindTexture(Texture2D texture)
    {
        IntPtr id = new IntPtr(_textureId++);
        _loadedTextures.Add(id, texture);
        return id;
    }
    
    /// <summary>
    /// Unbinds a texture
    /// </summary>
    public void UnbindTexture(IntPtr textureId)
    {
        _loadedTextures.Remove(textureId);
    }
    
    /// <summary>
    /// Begin ImGui frame - call this before any ImGui calls
    /// </summary>
    public void BeginLayout(GameTime gameTime)
    {
        ImGui.GetIO().DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        UpdateInput();
        ImGui.NewFrame();
    }
    
    /// <summary>
    /// End ImGui frame and render - call this after all ImGui calls
    /// </summary>
    public void EndLayout()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }
    
    /// <summary>
    /// Updates input state for ImGui
    /// </summary>
    private void UpdateInput()
    {
        var io = ImGui.GetIO();
        
        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();
        
        // Mouse position
        io.MousePos = new System.Numerics.Vector2(mouse.X, mouse.Y);
        
        // Mouse buttons
        io.MouseDown[0] = mouse.LeftButton == ButtonState.Pressed;
        io.MouseDown[1] = mouse.RightButton == ButtonState.Pressed;
        io.MouseDown[2] = mouse.MiddleButton == ButtonState.Pressed;
        
        // Mouse wheel
        int scrollDelta = mouse.ScrollWheelValue - _scrollWheelValue;
        io.MouseWheel = scrollDelta / 120f;
        _scrollWheelValue = mouse.ScrollWheelValue;
        
        // Keyboard modifiers
        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows));
        
        // Key events for common keys
        UpdateKeyEvent(io, keyboard, Keys.Tab, ImGuiKey.Tab);
        UpdateKeyEvent(io, keyboard, Keys.Left, ImGuiKey.LeftArrow);
        UpdateKeyEvent(io, keyboard, Keys.Right, ImGuiKey.RightArrow);
        UpdateKeyEvent(io, keyboard, Keys.Up, ImGuiKey.UpArrow);
        UpdateKeyEvent(io, keyboard, Keys.Down, ImGuiKey.DownArrow);
        UpdateKeyEvent(io, keyboard, Keys.PageUp, ImGuiKey.PageUp);
        UpdateKeyEvent(io, keyboard, Keys.PageDown, ImGuiKey.PageDown);
        UpdateKeyEvent(io, keyboard, Keys.Home, ImGuiKey.Home);
        UpdateKeyEvent(io, keyboard, Keys.End, ImGuiKey.End);
        UpdateKeyEvent(io, keyboard, Keys.Delete, ImGuiKey.Delete);
        UpdateKeyEvent(io, keyboard, Keys.Back, ImGuiKey.Backspace);
        UpdateKeyEvent(io, keyboard, Keys.Enter, ImGuiKey.Enter);
        UpdateKeyEvent(io, keyboard, Keys.Escape, ImGuiKey.Escape);
        UpdateKeyEvent(io, keyboard, Keys.Space, ImGuiKey.Space);
        UpdateKeyEvent(io, keyboard, Keys.A, ImGuiKey.A);
        UpdateKeyEvent(io, keyboard, Keys.C, ImGuiKey.C);
        UpdateKeyEvent(io, keyboard, Keys.V, ImGuiKey.V);
        UpdateKeyEvent(io, keyboard, Keys.X, ImGuiKey.X);
        UpdateKeyEvent(io, keyboard, Keys.Y, ImGuiKey.Y);
        UpdateKeyEvent(io, keyboard, Keys.Z, ImGuiKey.Z);
    }
    
    private void UpdateKeyEvent(ImGuiIOPtr io, KeyboardState keyboard, Keys monogameKey, ImGuiKey imguiKey)
    {
        io.AddKeyEvent(imguiKey, keyboard.IsKeyDown(monogameKey));
    }
    
    /// <summary>
    /// Renders ImGui draw data to the screen
    /// </summary>
    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
            return;
        
        var io = ImGui.GetIO();
        
        // Backup graphics device state
        var lastViewport = _graphicsDevice.Viewport;
        var lastScissorBox = _graphicsDevice.ScissorRectangle;
        var lastBlendState = _graphicsDevice.BlendState;
        var lastRasterizerState = _graphicsDevice.RasterizerState;
        var lastDepthStencilState = _graphicsDevice.DepthStencilState;
        var lastSamplerState = _graphicsDevice.SamplerStates[0];
        
        // Setup render state
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;
        _graphicsDevice.RasterizerState = _rasterizerState;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
        
        // Setup viewport and projection
        _graphicsDevice.Viewport = new Viewport(0, 0, 
            _graphicsDevice.PresentationParameters.BackBufferWidth, 
            _graphicsDevice.PresentationParameters.BackBufferHeight);
        
        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
        
        Matrix projection = Matrix.CreateOrthographicOffCenter(L, R, B, T, -1, 1);
        _effect.Projection = projection;
        _effect.View = Matrix.Identity;
        _effect.World = Matrix.Identity;
        
        // Render command lists
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            
            // Expand buffers if needed
            int vertexSize = cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
            if (vertexSize > _vertexBufferSize)
            {
                _vertexBuffer?.Dispose();
                _vertexBufferSize = (int)(vertexSize * 1.5f);
                _vertexBuffer = new VertexBuffer(_graphicsDevice, ImGuiVertexDeclaration.Declaration, 
                    _vertexBufferSize / Unsafe.SizeOf<ImDrawVert>(), BufferUsage.None);
                _vertexData = new byte[_vertexBufferSize];
            }
            
            int indexSize = cmdList.IdxBuffer.Size * sizeof(ushort);
            if (indexSize > _indexBufferSize)
            {
                _indexBuffer?.Dispose();
                _indexBufferSize = (int)(indexSize * 1.5f);
                _indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, 
                    _indexBufferSize / sizeof(ushort), BufferUsage.None);
                _indexData = new byte[_indexBufferSize];
            }
            
            // Copy vertex/index data
            Marshal.Copy(cmdList.VtxBuffer.Data, _vertexData, 0, vertexSize);
            Marshal.Copy(cmdList.IdxBuffer.Data, _indexData, 0, indexSize);
            _vertexBuffer.SetData(_vertexData, 0, vertexSize);
            _indexBuffer.SetData(_indexData, 0, indexSize);
            
            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;
            
            // Render commands
            int vtxOffset = 0;
            int idxOffset = 0;
            
            for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[cmdi];
                
                if (!_loadedTextures.ContainsKey(drawCmd.TextureId))
                {
                    throw new InvalidOperationException($"Texture ID {drawCmd.TextureId} not found");
                }
                
                _graphicsDevice.ScissorRectangle = new Rectangle(
                    (int)drawCmd.ClipRect.X,
                    (int)drawCmd.ClipRect.Y,
                    (int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                    (int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)
                );
                
                _effect.Texture = _loadedTextures[drawCmd.TextureId];
                
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        vtxOffset,
                        idxOffset,
                        (int)drawCmd.ElemCount / 3
                    );
                }
                
                idxOffset += (int)drawCmd.ElemCount;
            }
            
            vtxOffset += cmdList.VtxBuffer.Size;
        }
        
        // Restore graphics device state
        _graphicsDevice.Viewport = lastViewport;
        _graphicsDevice.ScissorRectangle = lastScissorBox;
        _graphicsDevice.BlendState = lastBlendState;
        _graphicsDevice.RasterizerState = lastRasterizerState;
        _graphicsDevice.DepthStencilState = lastDepthStencilState;
        _graphicsDevice.SamplerStates[0] = lastSamplerState;
    }
    
    /// <summary>
    /// Check if mouse is over any ImGui window
    /// </summary>
    public bool WantCaptureMouse()
    {
        return ImGui.GetIO().WantCaptureMouse;
    }
    
    /// <summary>
    /// Check if keyboard is being used by ImGui
    /// </summary>
    public bool WantCaptureKeyboard()
    {
        return ImGui.GetIO().WantCaptureKeyboard;
    }
}

/// <summary>
/// Vertex structure for ImGui rendering
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ImGuiVertexDeclaration
{
    public static readonly VertexDeclaration Declaration;
    
    static ImGuiVertexDeclaration()
    {
        unsafe
        {
            Declaration = new VertexDeclaration(
                Unsafe.SizeOf<ImDrawVert>(),
                
                // Position
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
                
                // UV
                new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                
                // Color
                new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );
        }
    }
}

/// <summary>
/// Helper class for unsafe operations
/// </summary>
public static class Unsafe
{
    public static int SizeOf<T>() where T : struct
    {
        return Marshal.SizeOf<T>();
    }
}
