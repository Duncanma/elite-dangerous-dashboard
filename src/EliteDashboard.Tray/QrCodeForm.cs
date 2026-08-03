using QRCoder;

namespace EliteDashboard.Tray;

public sealed class QrCodeForm : Form
{
    public QrCodeForm(string url)
    {
        Text = "Elite Dangerous Dashboard";
        Width = 340;
        Height = 420;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrData);
        var qrBytes = pngQrCode.GetGraphic(10);

        using var stream = new MemoryStream(qrBytes);
        var qrImage = Image.FromStream(stream);

        var pictureBox = new PictureBox
        {
            Image = qrImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Top,
            Height = 300,
        };

        var label = new Label
        {
            Text = url,
            Dock = DockStyle.Bottom,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11),
        };

        Controls.Add(pictureBox);
        Controls.Add(label);
    }
}
