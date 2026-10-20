using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Flipt")]
[assembly: System.Reflection.AssemblyProduct("Flipt")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

class Tile
{
    public string Cur = "", Old = "";
    public long Start = -1;
    const int Dur = 600;

    public void Set(string s, long now)
    {
        if (s == Cur) return;
        Old = Cur; Cur = s;
        Start = Old == "" ? -1 : now;
    }

    public bool Animating(long now) { return Start >= 0 && now - Start < Dur; }

    public void Paint(Graphics g, RectangleF r, Font f, long now)
    {
        if (!Animating(now))
        {
            Start = -1;
            Half(g, r, f, Cur, true, 1f, 0);
            Half(g, r, f, Cur, false, 1f, 0);
            return;
        }
        float p = (now - Start) / (float)Dur;
        Half(g, r, f, Cur, true, 1f, 0);
        Half(g, r, f, Old, false, 1f, 0);
        if (p < 0.5f)
        {
            float q = p * 2f;
            Half(g, r, f, Old, true, 1f - q * q, (int)(160 * q));
        }
        else
        {
            float q = (p - 0.5f) * 2f;
            float s = 1f - (1f - q) * (1f - q);
            Half(g, r, f, Cur, false, s, (int)(160 * (1f - q)));
        }
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void Half(Graphics g, RectangleF r, Font f, string text, bool top, float scale, int shade)
    {
        float mid = r.Y + r.Height / 2f;
        var st = g.Save();
        RectangleF clip = top ? new RectangleF(r.X, r.Y, r.Width, r.Height / 2f)
                              : new RectangleF(r.X, mid, r.Width, r.Height / 2f);
        g.SetClip(clip);
        if (scale < 1f)
        {
            g.TranslateTransform(0, mid);
            g.ScaleTransform(1, Math.Max(scale, 0.001f));
            g.TranslateTransform(0, -mid);
        }
        using (var path = Round(r, r.Height * 0.045f))
        using (var br = new SolidBrush(Color.FromArgb(22, 22, 25)))
            g.FillPath(br, path);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        using (var tb = new SolidBrush(Color.FromArgb(250, 250, 252)))
            g.DrawString(text, f, tb, new RectangleF(r.X, r.Y - r.Height * 0.02f, r.Width, r.Height), sf);
        if (shade > 0)
            using (var sh = new SolidBrush(Color.FromArgb(Math.Min(shade, 255), 0, 0, 12)))
                g.FillRectangle(sh, r);
        g.Restore(st);
        // seam between halves
        using (var pen = new Pen(Color.Black, Math.Max(1.5f, r.Height * 0.008f)))
            g.DrawLine(pen, r.X, mid, r.Right, mid);
    }
}

class MainForm : Form
{
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint f);
    const uint ES_CONTINUOUS = 0x80000000, ES_DISPLAY = 0x00000002, ES_SYSTEM = 0x00000001;

    Tile left = new Tile(), right = new Tile();
    Stopwatch clock = Stopwatch.StartNew();
    Stopwatch tick = new Stopwatch();
    Timer timer = new Timer();
    bool use24 = true, timerMode = false, running = false, full = false;
    double focusLen = 25 * 60, remaining = 25 * 60;
    long hintUntil, lastMove;
    string label = "";
    Font tileFont, smallFont;
    float fontFor = 0;
    Rectangle restore;
    bool cursorHidden;

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int one = 1, black = 0;
        DwmSetWindowAttribute(Handle, 20, ref one, 4);
        DwmSetWindowAttribute(Handle, 35, ref black, 4);
        DwmSetWindowAttribute(Handle, 34, ref black, 4);
    }

    public MainForm()
    {
        Text = "Flipt";
        AutoScaleMode = AutoScaleMode.None;
        float sc;
        using (var gg = CreateGraphics()) sc = gg.DpiX / 96f;
        ClientSize = new Size((int)(980 * sc), (int)(560 * sc));
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size((int)(420 * sc), (int)(260 * sc));
        BackColor = Color.Black;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY | ES_SYSTEM);
        timer.Interval = 16;
        timer.Tick += OnTick;
        timer.Start();
        hintUntil = 5000;
        MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sliderOpen && BtnAlpha() > 0 && Rectangle.Inflate(SliderRect(), 12, 12).Contains(e.Location)) { dragging = true; SetSlider(e.Location); Invalidate(); return; }
            int i = HitButton(e.Location); if (i >= 0) Press(i);
        };
        MouseUp += delegate { dragging = false; };
        MouseDoubleClick += delegate(object s, MouseEventArgs e)
        {
            if (HitButton(e.Location) < 0 && !(sliderOpen && Rectangle.Inflate(SliderRect(), 12, 12).Contains(e.Location))) ToggleFull();
        };
        MouseMove += delegate(object s, MouseEventArgs e) { if (dragging) { SetSlider(e.Location); Invalidate(); } };
        MouseMove += delegate { lastMove = clock.ElapsedMilliseconds; hintUntil = lastMove + 3000; ShowCursor(); };
        KeyPreview = true;
        KeyDown += OnKey;
        FormClosed += delegate { SetThreadExecutionState(ES_CONTINUOUS); };
        UpdateTiles();
    }

    void ShowCursor() { if (cursorHidden) { Cursor.Show(); cursorHidden = false; } }

    void ToggleFull()
    {
        if (!full)
        {
            restore = Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            full = true;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = restore;
            full = false;
            ShowCursor();
        }
    }

    void OnKey(object s, KeyEventArgs e)
    {
        hintUntil = clock.ElapsedMilliseconds + 3000;
        switch (e.KeyCode)
        {
            case Keys.F11: case Keys.F: ToggleFull(); break;
            case Keys.Escape: if (full) ToggleFull(); break;
            case Keys.Q: Close(); break;
            case Keys.H: use24 = !use24; break;
            case Keys.T: TopMost = !TopMost; break;
            case Keys.M: ToggleMode(); break;
            case Keys.Space: ToggleRun(); break;
            case Keys.R: running = false; tick.Reset(); remaining = focusLen; break;
            case Keys.Up: Adjust(5); break;
            case Keys.Down: Adjust(-5); break;
        }
        UpdateTiles();
        Invalidate();
    }

    void ToggleMode()
    {
        timerMode = !timerMode;
        if (!timerMode) running = false;
        tick.Reset();
    }

    void ToggleRun()
    {
        if (!timerMode) { timerMode = true; }
        if (remaining <= 0) remaining = focusLen;
        running = !running;
        if (running) tick.Restart(); else tick.Reset();
    }

    // right-side icons: 0 fullscreen, 1 clock/timer, 2 play/pause (timer only); 12/24h and on-top stay on H/T
    int[] Ids() { return timerMode ? new[] { 0, 1, 2, 3, 5 } : new[] { 0, 1, 3, 5 }; }

    float scale = 1f;
    bool sliderOpen, dragging;

    // vertical slider track, left of the button column
    Rectangle SliderRect()
    {
        var b = Buttons();
        int sz = b[0].Width, H = ClientSize.Height;
        int h = Math.Max(120, H * 2 / 5);
        return new Rectangle(b[0].X - sz - sz / 2, (H - h) / 2, sz, h);
    }

    void SetSlider(Point p)
    {
        var r = SliderRect();
        float t = 1f - Math.Max(0f, Math.Min(1f, (p.Y - r.Y) / (float)r.Height));
        scale = 0.5f + t * 1.3f;
    }

    Rectangle[] Buttons()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        int sz = (int)Math.Max(30, Math.Min(46, Math.Min(W, H) * 0.065));
        int gap = sz / 2, n = Ids().Length;
        int total = n * sz + (n - 1) * gap;
        int x = W - sz - Math.Max(16, sz * 2 / 3), y = (H - total) / 2;
        var r = new Rectangle[n];
        for (int i = 0; i < n; i++) r[i] = new Rectangle(x, y + i * (sz + gap), sz, sz);
        return r;
    }

    int HitButton(Point p)
    {
        if (BtnAlpha() <= 0) return -1;
        var b = Buttons();
        var ids = Ids();
        for (int i = 0; i < b.Length; i++) if (b[i].Contains(p)) return ids[i];
        return -1;
    }

    int BtnAlpha()
    {
        long left_ = hintUntil - clock.ElapsedMilliseconds;
        if (left_ <= 0) return 0;
        return (int)Math.Min(255, left_ * 255 / 600);
    }

    void Press(int i)
    {
        switch (i)
        {
            case 0: ToggleFull(); break;
            case 1: ToggleMode(); break;
            case 2: ToggleRun(); break;
            case 3: use24 = !use24; break;
            case 4: TopMost = !TopMost; break;
            case 5: sliderOpen = !sliderOpen; break;
        }
        UpdateTiles();
        Invalidate();
    }

    void DrawButtons(Graphics g)
    {
        int a = BtnAlpha();
        if (a <= 0) return;
        var b = Buttons();
        var ids = Ids();
        var mp = PointToClient(Cursor.Position);
        for (int k = 0; k < b.Length; k++)
        {
            int i = ids[k];
            bool on = (i == 1 && timerMode) || (i == 2 && running);
            bool hot = b[k].Contains(mp);
            int c = hot ? 235 : on ? 170 : 95;
            DrawIcon(g, i, b[k], Color.FromArgb(a, c, c, c), on || (i == 5 && sliderOpen));
        }
        if (sliderOpen)
        {
            var r = SliderRect();
            float cx = r.X + r.Width / 2f, t = (scale - 0.5f) / 1.3f;
            float ky = r.Bottom - t * r.Height, kr = r.Width * 0.22f;
            using (var pen = new Pen(Color.FromArgb(a, 70, 70, 70), Math.Max(3f, r.Width * 0.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, cx, r.Y, cx, r.Bottom);
            using (var pen = new Pen(Color.FromArgb(a, 235, 235, 235), Math.Max(3f, r.Width * 0.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, cx, ky, cx, r.Bottom);
            using (var br = new SolidBrush(Color.FromArgb(a, 250, 250, 252)))
                g.FillEllipse(br, cx - kr, ky - kr, kr * 2, kr * 2);
        }
    }

    void DrawIcon(Graphics g, int i, Rectangle r, Color col, bool on)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, u = r.Width * 0.17f;
        using (var pen = new Pen(col, Math.Max(1.6f, r.Width * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        using (var br = new SolidBrush(col))
        {
            switch (i)
            {
                case 0:
                    {
                        float s = u * 1.1f, k = u * 0.7f;
                        float[] sx = { -1, 1, 1, -1 }, sy = { -1, -1, 1, 1 };
                        for (int q = 0; q < 4; q++)
                        {
                            float px = cx + sx[q] * s, py = cy + sy[q] * s;
                            float dir = full ? -1 : 1;
                            g.DrawLine(pen, px, py - sy[q] * k * dir, px, py);
                            g.DrawLine(pen, px, py, px - sx[q] * k * dir, py);
                        }
                        break;
                    }
                case 1:
                    g.DrawEllipse(pen, cx - u * 1.5f, cy - u * 1.5f, u * 3, u * 3);
                    g.DrawLine(pen, cx, cy, cx, cy - u * 1.0f);
                    g.DrawLine(pen, cx, cy, cx + u * 0.75f, cy + u * 0.4f);
                    break;
                case 2:
                    if (running)
                    {
                        g.FillRectangle(br, cx - u * 0.9f, cy - u, u * 0.55f, u * 2);
                        g.FillRectangle(br, cx + u * 0.35f, cy - u, u * 0.55f, u * 2);
                    }
                    else
                    {
                        g.FillPolygon(br, new PointF[] { new PointF(cx - u * 0.7f, cy - u * 1.1f), new PointF(cx + u * 1.05f, cy), new PointF(cx - u * 0.7f, cy + u * 1.1f) });
                    }
                    break;
                case 3:
                    using (var f = new Font("Segoe UI", r.Width * 0.30f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(use24 ? "24" : "12", f, br, new RectangleF(r.X, r.Y, r.Width, r.Height), sf);
                    break;
                case 5:
                    using (var f = new Font("Segoe UI", r.Width * 0.34f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("Aa", f, br, new RectangleF(r.X, r.Y, r.Width, r.Height), sf);
                    break;
                case 4:
                    g.DrawRectangle(pen, cx - u * 1.4f, cy - u * 1.2f, u * 2.8f, u * 2.4f);
                    g.FillRectangle(br, cx - u * 1.4f, cy - u * 1.2f, u * 2.8f, u * 0.7f);
                    break;
            }
        }
    }

    void Adjust(int min)
    {
        if (!timerMode || running) return;
        focusLen = Math.Max(60, Math.Min(180 * 60, focusLen + min * 60));
        remaining = focusLen;
    }

    void UpdateTiles()
    {
        long now = clock.ElapsedMilliseconds;
        if (timerMode)
        {
            if (running)
            {
                remaining -= tick.Elapsed.TotalSeconds;
                tick.Restart();
                if (remaining <= 0)
                {
                    remaining = 0; running = false; tick.Reset();
                    SystemSounds.Exclamation.Play();
                }
            }
            int s = (int)Math.Ceiling(remaining);
            left.Set((s / 60).ToString("00"), now);
            right.Set((s % 60).ToString("00"), now);
            label = remaining <= 0 ? "DONE" : running ? "FOCUS" : "READY";
        }
        else
        {
            var d = DateTime.Now;
            int h = d.Hour;
            if (use24) { left.Set(h.ToString("00"), now); label = ""; }
            else
            {
                int h12 = h % 12; if (h12 == 0) h12 = 12;
                left.Set(h12.ToString(), now);
                label = h < 12 ? "AM" : "PM";
            }
            right.Set(d.Minute.ToString("00"), now);
        }
    }

    void OnTick(object s, EventArgs e)
    {
        long now = clock.ElapsedMilliseconds;
        UpdateTiles();
        if (full && !cursorHidden && now - lastMove > 2500) { Cursor.Hide(); cursorHidden = true; }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        int W = ClientSize.Width, H = ClientSize.Height;
        if (W < 10 || H < 10) return;
        g.Clear(Color.Black);

        string text = left.Cur + ":" + right.Cur;
        bool stack = H > W * 1.15f;
        string refText = stack ? "88" : left.Cur.Length >= 2 ? "88:88" : "8:88";
        float fs;
        using (var rf = new Font("Segoe UI", 100f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var sfm = new StringFormat(StringFormat.GenericTypographic))
        {
            float rw = g.MeasureString(refText, rf, 100000, sfm).Width;
            if (stack)
                fs = Math.Min(Math.Min(100f * W * 0.68f / rw, H * 0.34f) * scale, Math.Min(100f * W * 0.80f / rw, H * 0.44f));
            else
                fs = Math.Min(Math.Min(100f * W * 0.54f / rw, H * 0.45f) * scale, Math.Min(100f * W * 0.88f / rw, H * 0.85f));
        }
        if (Math.Abs(fontFor - fs) > 0.5f || tileFont == null)
        {
            if (tileFont != null) tileFont.Dispose();
            if (smallFont != null) smallFont.Dispose();
            tileFont = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
            smallFont = new Font("Segoe UI", Math.Max(9f, fs * 0.1f), FontStyle.Regular, GraphicsUnit.Pixel);
            fontFor = fs;
        }
        using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (var tb = new SolidBrush(Color.FromArgb(250, 250, 252)))
        {
            if (stack)
            {
                float lh = fs * 0.98f;
                g.DrawString(left.Cur, tileFont, tb, new RectangleF(0, H / 2f - lh * 1.5f - fs * 0.04f, W, lh * 2), sf);
                g.DrawString(right.Cur, tileFont, tb, new RectangleF(0, H / 2f - lh * 0.5f - fs * 0.04f, W, lh * 2), sf);
            }
            else g.DrawString(text, tileFont, tb, new RectangleF(0, -fs * 0.04f, W, H), sf);
        }

        if (timerMode && label != "")
            using (var b = new SolidBrush(Color.FromArgb(110, 110, 116)))
                g.DrawString(label, smallFont, b, W * 0.04f, H * 0.04f);

        DrawButtons(g);
    }

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr c);

    [STAThread]

    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); }
        catch { try { SetProcessDPIAware(); } catch { } }
        Application.EnableVisualStyles();
        if (args.Length == 2 && args[0] == "--shot")
        {
            var f = new MainForm();
            f.Show();
            if (Environment.GetEnvironmentVariable("FLIPT_SIZE") == "full") f.ClientSize = new Size(2000, 960);
            if (Environment.GetEnvironmentVariable("FLIPT_SIZE") == "tall") f.ClientSize = new Size(640, 1340);
            Application.DoEvents();
            System.Threading.Thread.Sleep(900);
            Application.DoEvents();
            using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                bmp.Save(args[1]);
            }
            return;
        }
        Application.Run(new MainForm());
    }
}
