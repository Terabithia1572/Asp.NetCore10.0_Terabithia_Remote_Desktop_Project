using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
using System.Drawing.Imaging;
using Device = SharpDX.Direct3D11.Device;

namespace TerabithiaRemote.Server.Services;

public class DXGICapture : IDisposable
{
    private Device _device;
    private OutputDuplication _deskDupl;
    private Texture2D _desktopImage;
    private OutputDescription _outputDesc;

    public DXGICapture()
    {
        // GPU ve Ekran Kartı ayarlarını başlat
        var factory = new Factory1();
        var adapter = factory.GetAdapter1(0);
        _device = new Device(adapter);
        var output = adapter.GetOutput(0);
        var output1 = output.QueryInterface<Output1>();
        _outputDesc = output.Description;
        _deskDupl = output1.DuplicateOutput(_device);
    }

    public Bitmap GetNextFrame()
    {
        try
        {
            SharpDX.DXGI.Resource desktopResource;
            OutputDuplicateFrameInformation frameInfo;

            // AcquireNextFrame yerine TryAcquireNextFrame daha güvenlidir ve hata fırlatmaz
            // 100ms bekleme süresi verdik
            var result = _deskDupl.TryAcquireNextFrame(100, out frameInfo, out desktopResource);

            // Eğer sonuç başarılı değilse (timeout veya başka hata) null dön
            if (result.Failure || desktopResource == null)
            {
                return null;
            }

            using (var tempTexture = desktopResource.QueryInterface<Texture2D>())
            {
                var desc = tempTexture.Description;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.Usage = ResourceUsage.Staging;
                desc.BindFlags = BindFlags.None;
                desc.OptionFlags = ResourceOptionFlags.None;

                using (var stagingTexture = new Texture2D(_device, desc))
                {
                    _device.ImmediateContext.CopyResource(tempTexture, stagingTexture);
                    var dataBox = _device.ImmediateContext.MapSubresource(stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                    Bitmap bitmap = new Bitmap(desc.Width, desc.Height, PixelFormat.Format32bppArgb);
                    var boundsRect = new Rectangle(0, 0, desc.Width, desc.Height);
                    BitmapData mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

                    Utilities.CopyMemory(mapDest.Scan0, dataBox.DataPointer, desc.Width * desc.Height * 4);

                    bitmap.UnlockBits(mapDest);
                    _device.ImmediateContext.UnmapSubresource(stagingTexture, 0);

                    // Kaynakları serbest bırakmak çok kritik!
                    desktopResource.Dispose();
                    _deskDupl.ReleaseFrame();

                    return bitmap;
                }
            }
        }
        catch (SharpDXException ex)
        {
            // Zaman aşımı normaldir, ekran değişmemiştir
            if (ex.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout)
                return null;

            return null;
        }
    }

    public void Dispose()
    {
        _deskDupl?.Dispose();
        _device?.Dispose();
    }
}