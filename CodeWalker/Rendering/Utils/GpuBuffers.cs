using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace CodeWalker.Rendering;

public class GpuVarsBuffer<T> where T : struct //shader vars buffer helper!
{
    public Buffer Buffer;
    public bool Flag; //for external use
    public int Size;
    public T Vars;

    public GpuVarsBuffer(Device device)
    {
        Size = Marshal.SizeOf<T>(); // (sizeof(T));
        Buffer = new Buffer(device, Size, ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write,
            ResourceOptionFlags.None, 0); // //DXUtility::CreateShaderVarsBuffer(Size, name);
    }

    public void Dispose()
    {
        if (Buffer != null)
        {
            Buffer.Dispose();
            Buffer = null;
        }
    }

    public void Update(DeviceContext context)
    {
        try
        {
            var dataBox = context.MapSubresource(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Utilities.Write(dataBox.DataPointer, ref Vars);
            context.UnmapSubresource(Buffer, 0);
        }
        catch
        {
        } //not much we can do about this except ignore it..
    }

    public void SetVSCBuffer(DeviceContext context, int slot)
    {
        context.VertexShader.SetConstantBuffer(slot, Buffer);
    }

    public void SetPSCBuffer(DeviceContext context, int slot)
    {
        context.PixelShader.SetConstantBuffer(slot, Buffer);
    }
}

public class GpuABuffer<T>
    where T : struct //Dynamic GPU buffer of items updated by CPU, for array of structs used as a constant buffer
{
    public Buffer Buffer;
    public int BufferSize;
    public int StructCount;
    public int StructSize;

    public GpuABuffer(Device device, int count)
    {
        StructCount = count;
        StructSize = Marshal.SizeOf<T>();
        BufferSize = StructCount * StructSize;
        Buffer = new Buffer(device, BufferSize, ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write,
            ResourceOptionFlags.None, 0);
    }

    public void Dispose()
    {
        if (Buffer != null)
        {
            Buffer.Dispose();
            Buffer = null;
        }
    }

    public void Update(DeviceContext context, T[] data)
    {
        var dataBox = context.MapSubresource(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
        Utilities.Write(dataBox.DataPointer, data, 0, Math.Min(data.Length, StructCount));
        context.UnmapSubresource(Buffer, 0);
    }

    public void SetVSCBuffer(DeviceContext context, int slot)
    {
        context.VertexShader.SetConstantBuffer(slot, Buffer);
    }

    public void SetPSCBuffer(DeviceContext context, int slot)
    {
        context.PixelShader.SetConstantBuffer(slot, Buffer);
    }
}

public class GpuSBuffer<T> where T : struct //for static struct data as resource view
{
    public Buffer Buffer;
    public int BufferSize;
    public ShaderResourceView SRV;
    public int StructCount;
    public int StructSize;

    public GpuSBuffer(Device device, T[] data)
    {
        StructCount = data.Length;
        StructSize = Marshal.SizeOf<T>(); // (sizeof(T));
        BufferSize = StructCount * StructSize;
        //var ds = new DataStream(new DataPointer()
        Buffer = Buffer.Create(device, BindFlags.ShaderResource, data, BufferSize, ResourceUsage.Default,
            CpuAccessFlags.None, ResourceOptionFlags.BufferStructured, StructSize);
        //Buffer = new Buffer(device, BufferSize, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.BufferStructured, StructSize);
        SRV = DXUtility.CreateShaderResourceView(device, Buffer, Format.Unknown, ShaderResourceViewDimension.Buffer, 0,
            0, StructCount, 0);
    }

    public void Dispose()
    {
        if (SRV != null)
        {
            SRV.Dispose();
            SRV = null;
        }

        if (Buffer != null)
        {
            Buffer.Dispose();
            Buffer = null;
        }
    }
}

public class GpuCBuffer<T> where T : struct //Dynamic GPU buffer of items updated by CPU
{
    public Buffer Buffer;
    public int BufferSize;
    public int CurrentCount;
    public List<T> Data;
    public T[] DataArray;
    public ShaderResourceView SRV;
    public int StructCount;
    public int StructSize;

    public GpuCBuffer(Device device, int count)
    {
        StructCount = count;
        StructSize = Marshal.SizeOf<T>(); // (sizeof(T));
        BufferSize = StructCount * StructSize;
        //Buffer = Buffer.Create<T>(device, BindFlags.ShaderResource, null, BufferSize, ResourceUsage.Dynamic, CpuAccessFlags.Write, ResourceOptionFlags.BufferStructured, StructSize);
        Buffer = new Buffer(device, BufferSize, ResourceUsage.Dynamic, BindFlags.ShaderResource, CpuAccessFlags.Write,
            ResourceOptionFlags.BufferStructured, StructSize);
        SRV = DXUtility.CreateShaderResourceView(device, Buffer, Format.Unknown, ShaderResourceViewDimension.Buffer, 0,
            0, StructCount, 0);
        Data = new List<T>(count);
        DataArray = new T[count];
    }

    public void Dispose()
    {
        if (SRV != null)
        {
            SRV.Dispose();
            SRV = null;
        }

        if (Buffer != null)
        {
            Buffer.Dispose();
            Buffer = null;
        }
    }

    public void Clear()
    {
        Data.Clear();
        CurrentCount = 0;
    }

    public bool Add(T item)
    {
        if (CurrentCount < StructCount)
        {
            Data.Add(item);
            CurrentCount++;
            return true;
        }

        return false;
    }

    public void Update(DeviceContext context)
    {
        for (var i = 0; i < CurrentCount; i++) DataArray[i] = Data[i];
        var dataBox = context.MapSubresource(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
        Utilities.Write(dataBox.DataPointer, DataArray, 0, CurrentCount);
        context.UnmapSubresource(Buffer, 0);
    }

    public void Update(DeviceContext context, T[] data)
    {
        var dataBox = context.MapSubresource(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
        Utilities.Write(dataBox.DataPointer, data, 0, data.Length);
        context.UnmapSubresource(Buffer, 0);
    }

    public void SetVSResource(DeviceContext context, int slot)
    {
        context.VertexShader.SetShaderResource(slot, SRV);
    }

    public void SetPSResource(DeviceContext context, int slot)
    {
        context.PixelShader.SetShaderResource(slot, SRV);
    }
}

public class GpuBuffer<T> where T : struct //Dynamic GPU buffer of items updated by compute shader
{
    public Buffer Buffer;
    public int ItemCount;
    public int ItemTotalSize;
    public int Size;
    public ShaderResourceView SRV;
    public int StructCount;
    public int StructSize;
    public UnorderedAccessView UAV;

    public GpuBuffer(Device device, int itemSize, int itemCount)
    {
        StructSize = Marshal.SizeOf<T>(); // sizeof(T)),
        StructCount = itemCount * itemSize;
        ItemTotalSize = itemSize * StructSize;
        ItemCount = itemCount;
        Size = StructSize * itemSize * itemCount;
        Buffer = DXUtility.CreateBuffer(device, Size, ResourceUsage.Default,
            BindFlags.ShaderResource | BindFlags.UnorderedAccess, 0, ResourceOptionFlags.BufferStructured, StructSize);
        SRV = DXUtility.CreateShaderResourceView(device, Buffer, Format.Unknown, ShaderResourceViewDimension.Buffer, 0,
            0, StructCount, 0);
        UAV = DXUtility.CreateUnorderedAccessView(device, Buffer, Format.Unknown, UnorderedAccessViewDimension.Buffer,
            0, StructCount, 0, 0);
    }

    public void Dispose()
    {
        if (UAV != null)
        {
            UAV.Dispose();
            UAV = null;
        }

        if (SRV != null)
        {
            SRV.Dispose();
            SRV = null;
        }

        if (Buffer != null)
        {
            Buffer.Dispose();
            Buffer = null;
        }
    }
}

public class GpuTexture //texture and render targets (depth, MS).
{
    public Texture2D Depth;
    public Texture2D DepthMS;
    public DepthStencilView DSV;
    public DepthStencilView MSDSV;
    public RenderTargetView MSRTV;
    public bool Multisampled;
    public RenderTargetView RTV;
    public ShaderResourceView SRV;
    public Texture2D Texture;
    public Texture2D TextureMS;

    public bool UseDepth;

    //public ShaderResourceView DepthSRV; //possibly causing crash on DX10 hardware when multisampled
    public int VramUsage;

    public GpuTexture(Device device, int w, int h, Format f, int sc, int sq, bool depth, Format df)
    {
        Init(device, w, h, f, sc, sq, depth, df);
    }

    public GpuTexture(Device device, int w, int h, Format f, int sc, int sq)
    {
        Init(device, w, h, f, sc, sq, false, Format.Unknown);
    }

    public GpuTexture(Device device, int w, int h, Format f)
    {
        Init(device, w, h, f, 1, 0, false, Format.Unknown);
    }

    public void Init(Device device, int w, int h, Format f, int sc, int sq, bool depth, Format df)
    {
        VramUsage = 0;
        Multisampled = sc > 1;
        UseDepth = depth;
        var u = ResourceUsage.Default;
        var b = BindFlags.RenderTarget | BindFlags.ShaderResource;
        var rtvd = RenderTargetViewDimension.Texture2D;
        var srvd = ShaderResourceViewDimension.Texture2D; // D3D11_SRV_DIMENSION_TEXTURE2D;
        var fs = DXUtility.ElementSize(f);
        var wh = w * h;
        var db = BindFlags.DepthStencil; // | BindFlags.ShaderResource;// D3D11_BIND_DEPTH_STENCIL;
        var dsvd = DepthStencilViewDimension.Texture2D;
        var dtexf = GetDepthTexFormat(df);
        var dsrvf = GetDepthSrvFormat(df);

        Texture = DXUtility.CreateTexture2D(device, w, h, 1, 1, f, 1, 0, u, b, 0, 0);
        RTV = DXUtility.CreateRenderTargetView(device, Texture, f, rtvd, 0, 0, 0);
        SRV = DXUtility.CreateShaderResourceView(device, Texture, f, srvd, 1, 0, 0, 0);
        VramUsage += wh * fs;

        if (Multisampled)
        {
            b = BindFlags.RenderTarget;
            rtvd = RenderTargetViewDimension.Texture2DMultisampled;
            dsvd = DepthStencilViewDimension.Texture2DMultisampled;
            //srvd = ShaderResourceViewDimension.Texture2DMultisampled;

            TextureMS = DXUtility.CreateTexture2D(device, w, h, 1, 1, f, sc, sq, u, b, 0, 0);
            MSRTV = DXUtility.CreateRenderTargetView(device, TextureMS, f, rtvd, 0, 0, 0);
            VramUsage += wh * fs * sc;

            if (depth)
            {
                DepthMS = DXUtility.CreateTexture2D(device, w, h, 1, 1, dtexf, sc, sq, u, db, 0, 0);
                MSDSV = DXUtility.CreateDepthStencilView(device, DepthMS, df, dsvd);
                //DepthSRV = DXUtility.CreateShaderResourceView(device, DepthMS, dsrvf, srvd, 1, 0, 0, 0);
                VramUsage += wh * DXUtility.ElementSize(df) * sc;
            }
        }
        else
        {
            if (depth)
            {
                Depth = DXUtility.CreateTexture2D(device, w, h, 1, 1, dtexf, sc, sq, u, db, 0, 0);
                DSV = DXUtility.CreateDepthStencilView(device, Depth, df, dsvd);
                //DepthSRV = DXUtility.CreateShaderResourceView(device, Depth, dsrvf, srvd, 1, 0, 0, 0);
                VramUsage += wh * DXUtility.ElementSize(df);
            }
        }
    }

    public void Dispose()
    {
        if (SRV != null)
        {
            SRV.Dispose();
            SRV = null;
        }

        if (MSDSV != null)
        {
            MSDSV.Dispose();
            MSDSV = null;
        }

        if (MSRTV != null)
        {
            MSRTV.Dispose();
            MSRTV = null;
        }

        if (DSV != null)
        {
            DSV.Dispose();
            DSV = null;
        }

        if (RTV != null)
        {
            RTV.Dispose();
            RTV = null;
        }

        if (DepthMS != null)
        {
            DepthMS.Dispose();
            DepthMS = null;
        }

        if (Depth != null)
        {
            Depth.Dispose();
            Depth = null;
        }

        if (TextureMS != null)
        {
            TextureMS.Dispose();
            TextureMS = null;
        }

        if (Texture != null)
        {
            Texture.Dispose();
            Texture = null;
        }
    }

    public void Clear(DeviceContext context, Color4 colour)
    {
        if (Multisampled)
        {
            context.ClearRenderTargetView(MSRTV, colour);
            if (UseDepth) context.ClearDepthStencilView(MSDSV, DepthStencilClearFlags.Depth, 0.0f, 0);
        }
        else
        {
            context.ClearRenderTargetView(RTV, colour);
            if (UseDepth) context.ClearDepthStencilView(DSV, DepthStencilClearFlags.Depth, 0.0f, 0);
        }
    }

    public void ClearDepth(DeviceContext context)
    {
        if (!UseDepth) return;
        if (Multisampled)
            context.ClearDepthStencilView(MSDSV, DepthStencilClearFlags.Depth, 0.0f, 0);
        else
            context.ClearDepthStencilView(DSV, DepthStencilClearFlags.Depth, 0.0f, 0);
    }

    public void SetRenderTarget(DeviceContext context)
    {
        if (Multisampled)
            context.OutputMerger.SetRenderTargets(UseDepth ? MSDSV : null, MSRTV);
        else
            context.OutputMerger.SetRenderTargets(UseDepth ? DSV : null, RTV);
    }


    public static Format GetDepthTexFormat(Format df)
    {
        var dtexf = Format.R32_Typeless;
        switch (df)
        {
            case Format.D16_UNorm:
                dtexf = Format.R16_Typeless;
                break;
            case Format.D24_UNorm_S8_UInt:
                dtexf = Format.R24G8_Typeless;
                break;
            case Format.D32_Float:
                dtexf = Format.R32_Typeless;
                break;
            case Format.D32_Float_S8X24_UInt:
                dtexf = Format.R32G8X24_Typeless; //is this right? who uses this anyway??
                break;
        }

        return dtexf;
    }

    public static Format GetDepthSrvFormat(Format df)
    {
        var dsrvf = Format.R32_Float;
        switch (df)
        {
            case Format.D16_UNorm:
                dsrvf = Format.R16_UNorm;
                break;
            case Format.D24_UNorm_S8_UInt:
                dsrvf = Format.R24_UNorm_X8_Typeless;
                break;
            case Format.D32_Float:
                dsrvf = Format.R32_Float;
                break;
            case Format.D32_Float_S8X24_UInt:
                dsrvf = Format.R32_Float_X8X24_Typeless;
                break;
        }

        return dsrvf;
    }
}

public class GpuMultiTexture //multiple texture and render targets (depth).
{
    public int Count;
    public Texture2D Depth;
    public ShaderResourceView DepthSRV;
    public DepthStencilView DSV;
    public int MultisampleCount;
    public bool Multisampled;
    public RenderTargetView[] RTVs;
    public ShaderResourceView[] SRVs;
    public Texture2D[] Textures;
    public bool UseDepth;
    public int VramUsage;

    public GpuMultiTexture(Device device, int w, int h, int count, Format f, bool depth, Format df, int msc = 1)
    {
        Init(device, w, h, count, f, depth, df, msc);
    }

    public GpuMultiTexture(Device device, int w, int h, int count, Format f, int msc = 1)
    {
        Init(device, w, h, count, f, false, Format.Unknown, msc);
    }

    public void Init(Device device, int w, int h, int count, Format f, bool depth, Format df, int multisamplecount)
    {
        Count = count;
        VramUsage = 0;
        UseDepth = depth;
        MultisampleCount = multisamplecount;
        Multisampled = multisamplecount > 1;
        var u = ResourceUsage.Default;
        var b = BindFlags.RenderTarget | BindFlags.ShaderResource;
        var fs = DXUtility.ElementSize(f);
        var wh = w * h;
        var db = BindFlags.DepthStencil | BindFlags.ShaderResource; // D3D11_BIND_DEPTH_STENCIL;
        var rtvd = RenderTargetViewDimension.Texture2D;
        var srvd = ShaderResourceViewDimension.Texture2D; // D3D11_SRV_DIMENSION_TEXTURE2D;
        var dsvd = DepthStencilViewDimension.Texture2D;

        if (Multisampled)
        {
            rtvd = RenderTargetViewDimension.Texture2DMultisampled;
            srvd = ShaderResourceViewDimension.Texture2DMultisampled;
            dsvd = DepthStencilViewDimension.Texture2DMultisampled;
        }

        Textures = new Texture2D[count];
        RTVs = new RenderTargetView[count];
        SRVs = new ShaderResourceView[count];

        for (var i = 0; i < count; i++)
        {
            Textures[i] = DXUtility.CreateTexture2D(device, w, h, 1, 1, f, multisamplecount, 0, u, b, 0, 0);
            RTVs[i] = DXUtility.CreateRenderTargetView(device, Textures[i], f, rtvd, 0, 0, 0);
            SRVs[i] = DXUtility.CreateShaderResourceView(device, Textures[i], f, srvd, 1, 0, 0, 0);
            VramUsage += wh * fs * multisamplecount;
        }

        if (depth)
        {
            var dtexf = GpuTexture.GetDepthTexFormat(df);
            var dsrvf = GpuTexture.GetDepthSrvFormat(df);
            Depth = DXUtility.CreateTexture2D(device, w, h, 1, 1, dtexf, multisamplecount, 0, u, db, 0, 0);
            DSV = DXUtility.CreateDepthStencilView(device, Depth, df, dsvd);
            DepthSRV = DXUtility.CreateShaderResourceView(device, Depth, dsrvf, srvd, 1, 0, 0, 0);
            VramUsage += wh * DXUtility.ElementSize(df) * multisamplecount;
        }
    }

    public void Dispose()
    {
        for (var i = 0; i < Count; i++)
        {
            SRVs[i].Dispose();
            RTVs[i].Dispose();
            Textures[i].Dispose();
        }

        SRVs = null;
        RTVs = null;
        Textures = null;

        if (DSV != null)
        {
            DSV.Dispose();
            DSV = null;
        }

        if (DepthSRV != null)
        {
            DepthSRV.Dispose();
            DepthSRV = null;
        }

        if (Depth != null)
        {
            Depth.Dispose();
            Depth = null;
        }
    }

    public void Clear(DeviceContext context, Color4 colour)
    {
        for (var i = 0; i < Count; i++) context.ClearRenderTargetView(RTVs[i], colour);
        if (UseDepth) context.ClearDepthStencilView(DSV, DepthStencilClearFlags.Depth, 0.0f, 0);
    }

    public void ClearDepth(DeviceContext context)
    {
        if (!UseDepth) return;
        context.ClearDepthStencilView(DSV, DepthStencilClearFlags.Depth, 0.0f, 0);
    }

    public void SetRenderTargets(DeviceContext context)
    {
        context.OutputMerger.SetRenderTargets(UseDepth ? DSV : null, RTVs);
    }
}