using System;
using System.Windows.Forms;
using System.IO;
using Gma.System.MouseKeyHook;
using NAudio.Wave;
using System.Drawing;

namespace BingoCat;

public class CatForm : Form
{
    private PictureBox catPictureBox;
    private readonly Image[] catImages;
    private readonly System.Windows.Forms.Timer animationTimer = new();
    private IKeyboardMouseEvents? globalHook;
    private readonly Random random = new();
    private WasapiLoopbackCapture? soundCapture;
    private float soundThreshold = 0.01f;
    private bool soundTriggered = false;
    private readonly Settings settings;

    public CatForm()
    {
        settings = Settings.Load();

        // Load images first to determine form size
        catImages = LoadCatImages();
        
        // Use the size of the first image for the form
        var baseSize = new Size(
            (int)(catImages[0].Width * settings.ScaleFactor),
            (int)(catImages[0].Height * settings.ScaleFactor)
        );

        // Form settings
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.TransparencyKey = BackColor;
        this.Size = baseSize;

        // Position the window above the taskbar
        this.StartPosition = FormStartPosition.Manual;
        this.Location = new Point(
            Screen.PrimaryScreen.WorkingArea.Right - this.Width - settings.XOffset,
            Screen.PrimaryScreen.WorkingArea.Bottom - this.Height - settings.YOffset
        );

        // Initialize PictureBox with scaling
        catPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        this.Controls.Add(catPictureBox);
        catPictureBox.Image = catImages[0];

        // Set up animation timer
        animationTimer.Interval = 100;
        animationTimer.Tick += AnimationTimer_Tick;

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

    private Image[] LoadCatImages()
    {
        string projectDir = AppDomain.CurrentDomain.BaseDirectory;
        if (projectDir.Contains("bin"))
        {
            projectDir = Path.GetFullPath(Path.Combine(projectDir, "../../.."));
        }

        // Construct path to the specific image set folder
        string imagesDir = Path.Combine(projectDir, "Resources", "Images", settings.ImageSet);
        
        // Ensure the directory exists
        if (!Directory.Exists(imagesDir))
        {
            // Fall back to default if specified set doesn't exist
            imagesDir = Path.Combine(projectDir, "Resources", "Images", "default");
            if (!Directory.Exists(imagesDir))
            {
                throw new DirectoryNotFoundException($"Could not find image directory: {imagesDir}");
            }
        }

        // Load the three required images
        return new Image[]
        {
            Image.FromFile(Path.Combine(imagesDir, "catBothDown.png")),
            Image.FromFile(Path.Combine(imagesDir, "catRightHand.png")),
            Image.FromFile(Path.Combine(imagesDir, "catLeftHand.png"))
        };
    }

    private void SoundCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        float sum = 0;
        int bytesPerSample = 4;
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
