using System;
using System.Windows.Forms;
using System.IO;
using Gma.System.MouseKeyHook;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace BingoCat;

public class CatForm : Form
{
    private PictureBox catPictureBox;
    private readonly Image[] catImages;
    private readonly System.Windows.Forms.Timer animationTimer = new();
    private IKeyboardMouseEvents? globalHook;
    private readonly Random random = new();
    private WasapiLoopbackCapture? soundCapture;
    private float soundThreshold = 0.01f; // Adjust as needed
    private bool soundTriggered = false;

    public CatForm()
    {
        // Load settings
        var settings = Settings.Load();

        // Form settings
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.TransparencyKey = BackColor;
        this.Size = new Size(64, 64);  // Adjust based on your image size

        // Position the window above the taskbar, using settings
        this.StartPosition = FormStartPosition.Manual;
        this.Location = new Point(
            Screen.PrimaryScreen.WorkingArea.Right - this.Width - settings.XOffset,
            Screen.PrimaryScreen.WorkingArea.Bottom - this.Height - settings.YOffset
        );

        // Initialize PictureBox
        catPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        this.Controls.Add(catPictureBox);

        // Load cat images from Resources/Images
        string projectDir = AppDomain.CurrentDomain.BaseDirectory;
        if (projectDir.Contains("bin"))
        {
            projectDir = Path.GetFullPath(Path.Combine(projectDir, "../../.."));
        }
        string imagesDir = Path.Combine(projectDir, "Resources", "Images");
        catImages = new Image[]
        {
            Image.FromFile(Path.Combine(imagesDir, "catBothDown.png")),
            Image.FromFile(Path.Combine(imagesDir, "catRightHand.png")),
            Image.FromFile(Path.Combine(imagesDir, "catLeftHand.png"))
        };
        catPictureBox.Image = catImages[0];

        // Set up animation timer
        animationTimer.Interval = 100;
        animationTimer.Tick += AnimationTimer_Tick;

        // Removed context menu for closing (no right-click exit)

        // Allow dragging the window
        this.MouseDown += (s, e) => 
        {
            if (e.Button == MouseButtons.Left)
            {
                var msg = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                this.DefWndProc(ref msg);
            }
        };

        if (settings.TriggerMode == "sound")
        {
            // Set up sound-based trigger
            soundCapture = new WasapiLoopbackCapture();
            soundCapture.DataAvailable += SoundCapture_DataAvailable;
            soundCapture.StartRecording();
        }
        else
        {
            // Set up global keyboard and mouse hooks
            globalHook = Hook.GlobalEvents();
            globalHook.KeyDown += GlobalInputHandler;
            globalHook.MouseDownExt += GlobalInputHandler;
        }
    }

    private void SoundCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        // Calculate RMS (root mean square) volume
        float sum = 0;
        int bytesPerSample = 4; // 32-bit float
        int samples = e.BytesRecorded / bytesPerSample;
        for (int i = 0; i < e.BytesRecorded; i += bytesPerSample)
        {
            float sample = BitConverter.ToSingle(e.Buffer, i);
            sum += sample * sample;
        }
        float rms = (float)Math.Sqrt(sum / samples);
        if (rms > soundThreshold && !soundTriggered)
        {
            soundTriggered = true;
            this.BeginInvoke(new Action(TriggerCat));
        }
        else if (rms <= soundThreshold)
        {
            soundTriggered = false;
        }
    }

    private void TriggerCat()
    {
        int idx = random.Next(2) + 1; // 1 or 2
        catPictureBox.Image = catImages[idx];
        animationTimer.Stop();
        animationTimer.Start();
    }

    private void GlobalInputHandler(object? sender, EventArgs e)
    {
        TriggerCat();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // Reset to default image
        catPictureBox.Image = catImages[0];
        animationTimer.Stop();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (globalHook != null)
        {
            globalHook.KeyDown -= GlobalInputHandler;
            globalHook.MouseDownExt -= GlobalInputHandler;
            globalHook.Dispose();
        }
        if (soundCapture != null)
        {
            soundCapture.DataAvailable -= SoundCapture_DataAvailable;
            soundCapture.StopRecording();
            soundCapture.Dispose();
        }
        base.OnFormClosing(e);
    }
}
