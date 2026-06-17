using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SuperNoNo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new PetForm());
    }
}

internal sealed class PetForm : Form
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExTopMost = 0x00000008L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtTransparent = -1;
    private const int PetWidth = 280;
    private const int PetHeight = 336;
    private const double PetBodyHitCenterX = 0.50;
    private const double PetBodyHitCenterY = 0.51;
    private const double PetBodyHitRadiusX = 0.22;
    private const double PetBodyHitRadiusY = 0.17;
    private const double PetRightEarHitCenterX = 0.62;
    private const double PetRightEarHitCenterY = 0.45;
    private const double PetRightEarHitRadiusX = 0.075;
    private const double PetRightEarHitRadiusY = 0.085;
    private const double PetLeftCapHitCenterX = 0.42;
    private const double PetLeftCapHitCenterY = 0.43;
    private const double PetLeftCapHitRadiusX = 0.075;
    private const double PetLeftCapHitRadiusY = 0.055;
    private const int RoamTimerIntervalMs = 16;
    private const int RoamInitialDelayMinMs = 8000;
    private const int RoamInitialDelayMaxMs = 16000;
    private const int RoamDelayMinMs = 25000;
    private const int RoamDelayMaxMs = 60000;
    private const int RoamRetryDelayMinMs = 5000;
    private const int RoamRetryDelayMaxMs = 12000;
    private const int RoamQuietAfterUserMs = 8000;
    private const int RoamCursorGuardPadding = 96;
    private const int RoamMinDistance = 24;
    private const int RoamMaxStepX = 86;
    private const int RoamMaxStepY = 58;
    private const int RoamDurationMinMs = 3200;
    private const int RoamDurationMaxMs = 5200;
    private const int MouseFollowTimerIntervalMs = 8;
    private const int MouseFollowComfortDistance = 118;
    private const int MouseFollowPoseStartDistance = 2;
    private const double MouseFollowSpring = 92.0;
    private const double MouseFollowDamping = 18.0;
    private const double MouseFollowMaxSpeed = 1120.0;
    private const double MouseFollowStopSpeed = 18.0;
    private const double MouseFollowMinFrameSeconds = 0.001;
    private const double MouseFollowMaxFrameSeconds = 0.033;
    private const int MouseFollowThrowLimit = 3;
    private const int MouseFollowThrowMinDistance = 18;
    private const int MouseFollowThrowRecentMoveMs = 160;
    private const double MouseFollowThrowMinSpeed = 880.0;
    private const double MouseFollowThrowMaxSpeed = 1800.0;
    private const double MouseFollowThrowAwayDamping = 2.25;
    private const double MouseFollowThrowAwayMinFinishSpeed = 120.0;
    private const double MouseFollowThrowAwayMinDurationSeconds = 0.42;
    private const double MouseFollowThrowAwayMaxDurationSeconds = 0.74;
    private const double MouseFollowThrowNopeHoldSeconds = 3.55;
    private const double MouseFollowThrowReturnMinDurationSeconds = 1.05;
    private const double MouseFollowThrowReturnMaxDurationSeconds = 1.7;
    private const double MouseFollowThrowReturnDistanceDurationFactor = 560.0;
    private const int CodexStatusTimerIntervalMs = 1000;
    private const int CodexStatusStaleMs = 300000;
    private const string VirtualHost = "desktop-pet.local";
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly string CodexStatusFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopPet",
        "codex-progress.json");
    private static readonly HashSet<string> CodexChargingStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "thinking",
        "planning",
        "working",
        "coding",
        "editing",
        "building",
        "testing",
        "reviewing"
    };

    private enum MouseFollowThrowPhase
    {
        None,
        ThrowingAway,
        Reacting,
        Returning
    }

    private readonly Icon appIcon = LoadAppIcon();
    private readonly WebView2 webView = new();
    private readonly NotifyIcon trayIcon;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly ToolStripMenuItem clickThroughMenuItem;
    private readonly ToolStripMenuItem autoMotionMenuItem;
    private readonly ToolStripMenuItem gazeTrackingMenuItem;
    private readonly ToolStripMenuItem mouseFollowMenuItem;
    private readonly ToolStripMenuItem desktopRoamMenuItem;
    private readonly ToolStripMenuItem codexStatusMenuItem;
    private readonly InputOverlayForm inputOverlay;
    private readonly System.Windows.Forms.Timer gazeTimer = new();
    private readonly System.Windows.Forms.Timer roamTimer = new();
    private readonly System.Windows.Forms.Timer codexStatusTimer = new();
    private static readonly (string Label, string Motion)[] MotionMenuItems =
    [
        ("开心", "Happy"),
        ("喜悦跳", "JoyJump"),
        ("跳舞", "Dance"),
        ("充电", "Charge"),
        ("流汗", "Sweat"),
        ("迷糊", "Sleepy"),
        ("沮丧", "Failed"),
        ("大咩", "Nope"),
        ("哈气", "Yawn")
    ];

    private bool clickThrough;
    private bool autoMotions = true;
    private bool gazeTracking = true;
    private bool mouseFollow;
    private bool desktopRoam = true;
    private bool codexStatusIntegration = true;
    private bool dragging;
    private bool inputPointerActive;
    private bool roamAnimating;
    private bool mouseFollowPoseActive;
    private bool mouseFollowTimerPrecisionActive;
    private MouseFollowThrowPhase mouseFollowThrowPhase;
    private Point lastDragScreen;
    private Point webDragStartScreen;
    private DateTime webDragStartedAt;
    private Point roamStartLocation;
    private Point roamTargetLocation;
    private long roamStartedTick;
    private long nextRoamTick;
    private int roamDurationMs;
    private double mouseFollowX;
    private double mouseFollowY;
    private double mouseFollowVelocityX;
    private double mouseFollowVelocityY;
    private double mouseFollowThrowVelocityX;
    private double mouseFollowThrowVelocityY;
    private double mouseFollowThrowDirectionX;
    private double mouseFollowThrowDirectionY;
    private double mouseFollowThrowStartX;
    private double mouseFollowThrowStartY;
    private double mouseFollowThrowTargetX;
    private double mouseFollowThrowTargetY;
    private double mouseFollowThrowElapsedSeconds;
    private double mouseFollowThrowDurationSeconds;
    private int mouseFollowThrowCount;
    private long mouseFollowLastFrameTick;
    private int mouseFollowFramePending;
    private System.Threading.Timer? mouseFollowTimer;
    private long lastUserActivityTick = Environment.TickCount64;
    private bool codexPlayerReady;
    private DateTime codexStatusLastWriteUtc = DateTime.MinValue;
    private string codexStatusLastPayload = string.Empty;
    private string codexStatusState = "idle";

    public PetForm()
    {
        Text = "SuperNoNo";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(PetWidth, PetHeight);
        MinimumSize = Size;
        MaximumSize = Size;
        BackColor = Color.Black;
        TransparencyKey = Color.Black;
        StartPosition = FormStartPosition.Manual;
        Location = GetInitialLocation(Size);

        webView.Dock = DockStyle.Fill;
        webView.DefaultBackgroundColor = Color.Transparent;
        Controls.Add(webView);

        clickThroughMenuItem = new ToolStripMenuItem("鼠标穿透模式", null, (_, _) => ToggleClickThrough())
        {
            Checked = clickThrough,
            CheckOnClick = false
        };
        trayMenu.Items.Add(clickThroughMenuItem);
        trayMenu.Items.Add("随机动作", null, (_, _) => ExecutePlayerScript("window.desktopPet?.playRandomMotion()"));
        autoMotionMenuItem = new ToolStripMenuItem("自动随机动作", null, (_, _) => ToggleAutoMotions())
        {
            Checked = autoMotions,
            CheckOnClick = false
        };
        trayMenu.Items.Add(autoMotionMenuItem);
        gazeTrackingMenuItem = new ToolStripMenuItem("视线跟随", null, (_, _) => ToggleGazeTracking())
        {
            Checked = gazeTracking,
            CheckOnClick = false
        };
        trayMenu.Items.Add(gazeTrackingMenuItem);
        mouseFollowMenuItem = new ToolStripMenuItem("鼠标追随", null, (_, _) => ToggleMouseFollow())
        {
            Checked = mouseFollow,
            CheckOnClick = false
        };
        trayMenu.Items.Add(mouseFollowMenuItem);
        desktopRoamMenuItem = new ToolStripMenuItem("桌面巡游", null, (_, _) => ToggleDesktopRoam())
        {
            Checked = desktopRoam,
            CheckOnClick = false
        };
        trayMenu.Items.Add(desktopRoamMenuItem);
        codexStatusMenuItem = new ToolStripMenuItem("Codex 进度联动", null, (_, _) => ToggleCodexStatusIntegration())
        {
            Checked = codexStatusIntegration,
            CheckOnClick = false
        };
        trayMenu.Items.Add(codexStatusMenuItem);
        ToolStripMenuItem motionsMenu = new("动作");
        foreach ((string label, string motion) in MotionMenuItems)
        {
            motionsMenu.DropDownItems.Add(label, null, (_, _) => PlayMotion(motion));
        }

        trayMenu.Items.Add(motionsMenu);
        trayMenu.Items.Add("移动到左上角", null, (_, _) => MoveTo(new Point(80, 80)));
        trayMenu.Items.Add("移动到右下角", null, (_, _) => MoveTo(GetInitialLocation(Size)));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) => Close());

        trayIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "SuperNoNo",
            ContextMenuStrip = trayMenu,
            Visible = true
        };

        inputOverlay = new InputOverlayForm(this, trayMenu);
        gazeTimer.Interval = 33;
        gazeTimer.Tick += (_, _) => SendGlobalGazePoint();
        roamTimer.Interval = RoamTimerIntervalMs;
        roamTimer.Tick += (_, _) => UpdateDesktopRoam();
        codexStatusTimer.Interval = CodexStatusTimerIntervalMs;
        codexStatusTimer.Tick += (_, _) => PollCodexStatus();
        Load += async (_, _) => await InitializeLive2DAsync();
        Shown += (_, _) =>
        {
            UpdateInputOverlay();
            UpdateGazeTimer();
            SyncMouseFollowKinematics(resetVelocity: true);
            UpdateMouseFollowTimer();
            ScheduleNextRoam(Environment.TickCount64, RoamInitialDelayMinMs, RoamInitialDelayMaxMs);
            UpdateDesktopRoamTimer();
            UpdateCodexStatusTimer();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= (int)(WsExToolWindow | WsExTopMost | WsExNoActivate | WsExTransparent);

            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyNativeStyles();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        SyncInputOverlayBounds();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        SyncInputOverlayBounds();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            Point screenPoint = GetScreenPointFromLParam(m.LParam);
            m.Result = clickThrough || !IsPetInteractiveScreenPoint(screenPoint)
                ? new IntPtr(HtTransparent)
                : new IntPtr(HtClient);
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            appIcon.Dispose();
            inputOverlay.Dispose();
            gazeTimer.Dispose();
            StopMouseFollowTimer();
            roamTimer.Dispose();
            codexStatusTimer.Dispose();
            trayMenu.Dispose();
            webView.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task InitializeLive2DAsync()
    {
        string playerIndex = Path.Combine(AppContext.BaseDirectory, "Live2DPlayer", "dist", "index.html");
        if (!File.Exists(playerIndex))
        {
            MessageBox.Show(
                "Live2D player files were not found. Run `npm install` and `npm run build` in Live2DPlayer, then rebuild the app.",
                "SuperNoNo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SuperNoNo",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);

            CoreWebView2EnvironmentOptions options = new("--autoplay-policy=no-user-gesture-required");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder,
                options);
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHost,
                AppContext.BaseDirectory,
                CoreWebView2HostResourceAccessKind.Allow);
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            webView.CoreWebView2.NavigationStarting += (_, _) =>
            {
                codexPlayerReady = false;
                codexStatusLastWriteUtc = DateTime.MinValue;
                codexStatusLastPayload = string.Empty;
            };
            webView.Source = new Uri($"https://{VirtualHost}/Live2DPlayer/dist/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not start WebView2 Live2D player:\n{ex.Message}",
                "SuperNoNo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using JsonDocument document = JsonDocument.Parse(e.WebMessageAsJson);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("type", out JsonElement typeElement))
        {
            return;
        }

        string? type = typeElement.GetString();
        if (type == "player-ready")
        {
            codexPlayerReady = true;
            codexStatusLastWriteUtc = DateTime.MinValue;
            codexStatusLastPayload = string.Empty;
            PollCodexStatus();
            return;
        }

        if (type is not ("drag-start" or "drag-move" or "drag-end"))
        {
            return;
        }

        if (clickThrough)
        {
            dragging = false;
            return;
        }

        MarkUserActivity();
        if (type == "drag-end")
        {
            dragging = false;
            ResetDragPose();
            return;
        }

        Point screenPoint = GetScreenPoint(root);
        if (type == "drag-start")
        {
            dragging = true;
            lastDragScreen = screenPoint;
            webDragStartScreen = screenPoint;
            webDragStartedAt = DateTime.UtcNow;
            SendDragPose(active: true, PointToClient(screenPoint), 0, 0, 0, 0);
            return;
        }

        if (!dragging)
        {
            return;
        }

        int dx = screenPoint.X - lastDragScreen.X;
        int dy = screenPoint.Y - lastDragScreen.Y;
        Location = new Point(Location.X + dx, Location.Y + dy);
        lastDragScreen = screenPoint;
        SendDragPose(
            active: true,
            PointToClient(screenPoint),
            dx,
            dy,
            GetDistance(webDragStartScreen, screenPoint),
            Math.Max(0, (int)(DateTime.UtcNow - webDragStartedAt).TotalMilliseconds));
    }

    private static Point GetScreenPoint(JsonElement root)
    {
        int x = (int)Math.Round(root.GetProperty("screenX").GetDouble());
        int y = (int)Math.Round(root.GetProperty("screenY").GetDouble());
        return new Point(x, y);
    }

    private void ToggleClickThrough()
    {
        MarkUserActivity();
        clickThrough = !clickThrough;
        clickThroughMenuItem.Checked = clickThrough;
        ApplyNativeStyles();
        UpdateInputOverlay();
        UpdateGazeTimer();
        ExecutePlayerScript($"window.desktopPet?.setDragMode({JsonSerializer.Serialize(!clickThrough)})");
    }

    private void ToggleAutoMotions()
    {
        MarkUserActivity();
        autoMotions = !autoMotions;
        autoMotionMenuItem.Checked = autoMotions;
        ExecutePlayerScript($"window.desktopPet?.setAutoMotions({JsonSerializer.Serialize(autoMotions)})");
    }

    private void ToggleGazeTracking()
    {
        MarkUserActivity();
        gazeTracking = !gazeTracking;
        gazeTrackingMenuItem.Checked = gazeTracking;
        UpdateGazeTimer();
        ExecutePlayerScript($"window.desktopPet?.setGazeTracking({JsonSerializer.Serialize(gazeTracking)})");
    }

    private void ToggleMouseFollow()
    {
        MarkUserActivity();
        mouseFollow = !mouseFollow;
        mouseFollowMenuItem.Checked = mouseFollow;
        if (mouseFollow)
        {
            mouseFollowThrowCount = 0;
            if (roamAnimating)
            {
                roamAnimating = false;
                ResetRoamPose();
            }

            CancelMouseFollowThrow();
            SyncMouseFollowKinematics(resetVelocity: true);
        }
        else
        {
            mouseFollowThrowCount = 0;
            CancelMouseFollowThrow();
            StopMouseFollowPose();
            SyncMouseFollowKinematics(resetVelocity: true);
            if (desktopRoam)
            {
                ScheduleNextRoam(Environment.TickCount64, RoamRetryDelayMinMs, RoamRetryDelayMaxMs);
            }
        }

        UpdateMouseFollowTimer();
        UpdateDesktopRoamTimer();
    }

    private void ToggleDesktopRoam()
    {
        MarkUserActivity();
        desktopRoam = !desktopRoam;
        desktopRoamMenuItem.Checked = desktopRoam;
        if (desktopRoam)
        {
            ScheduleNextRoam(Environment.TickCount64, RoamInitialDelayMinMs, RoamInitialDelayMaxMs);
        }
        else
        {
            roamAnimating = false;
            ResetRoamPose();
        }

        UpdateDesktopRoamTimer();
    }

    private void ToggleCodexStatusIntegration()
    {
        MarkUserActivity();
        codexStatusIntegration = !codexStatusIntegration;
        codexStatusMenuItem.Checked = codexStatusIntegration;
        if (!codexStatusIntegration)
        {
            SendCodexStatusIdle();
        }

        UpdateCodexStatusTimer();
    }

    private void ExecutePlayerScript(string script)
    {
        if (webView.CoreWebView2 is not null)
        {
            _ = webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    private void PlayMotion(string motion)
    {
        MarkUserActivity();
        ExecutePlayerScript($"window.desktopPet?.playMotion({JsonSerializer.Serialize(motion)})");
    }

    internal void PlayMotionForClientPoint(Point point)
    {
        TriggerInteraction("click", point);
    }

    internal bool GazeTrackingEnabled => gazeTracking;

    internal void BeginPointerActivity()
    {
        inputPointerActive = true;
        MarkUserActivity();
        CancelMouseFollowThrow();
        StopMouseFollowPose();
        SyncMouseFollowKinematics(resetVelocity: true);
    }

    internal void EndPointerActivity()
    {
        inputPointerActive = false;
        SyncMouseFollowKinematics(resetVelocity: true);
    }

    internal bool TryStartMouseFollowThrow(
        Point dragVector,
        Point flingVector,
        double flingSpeed,
        int lastMoveAgeMs,
        int distance,
        int durationMs)
    {
        if (!mouseFollow || distance < MouseFollowThrowMinDistance)
        {
            return false;
        }

        bool hasRecentFling = lastMoveAgeMs <= MouseFollowThrowRecentMoveMs
            && GetVectorLength(flingVector.X, flingVector.Y) >= 1
            && flingSpeed > 0;
        Point direction = hasRecentFling ? flingVector : dragVector;
        double directionLength = GetVectorLength(direction.X, direction.Y);
        if (directionLength < 1)
        {
            return false;
        }

        double dragSpeed = durationMs > 0
            ? distance * 1000.0 / durationMs
            : MouseFollowThrowMinSpeed;
        double speed = Math.Clamp(
            Math.Max(MouseFollowThrowMinSpeed, Math.Max(dragSpeed, flingSpeed) * 1.15 + distance * 2.0),
            MouseFollowThrowMinSpeed,
            MouseFollowThrowMaxSpeed);
        StartMouseFollowThrow(direction.X / directionLength, direction.Y / directionLength, speed, distance);
        return true;
    }

    internal void TriggerInteraction(string interaction, Point point, int distance = 0, int durationMs = 0)
    {
        MarkUserActivity();
        ExecutePlayerScript(
            $"window.desktopPet?.handleInteraction({JsonSerializer.Serialize(interaction)}, {point.X}, {point.Y}, {distance}, {durationMs})");
    }

    internal void FocusClientPoint(Point point)
    {
        if (gazeTracking)
        {
            ExecutePlayerScript($"window.desktopPet?.focusPoint({point.X}, {point.Y})");
        }
    }

    internal bool IsPetInteractiveScreenPoint(Point screenPoint)
    {
        return IsPetInteractiveClientPoint(PointToClient(screenPoint));
    }

    private bool IsPetInteractiveClientPoint(Point clientPoint)
    {
        if (!ClientRectangle.Contains(clientPoint))
        {
            return false;
        }

        return IsInNormalizedEllipse(
                clientPoint,
                ClientSize,
                PetBodyHitCenterX,
                PetBodyHitCenterY,
                PetBodyHitRadiusX,
                PetBodyHitRadiusY)
            || IsInNormalizedEllipse(
                clientPoint,
                ClientSize,
                PetRightEarHitCenterX,
                PetRightEarHitCenterY,
                PetRightEarHitRadiusX,
                PetRightEarHitRadiusY)
            || IsInNormalizedEllipse(
                clientPoint,
                ClientSize,
                PetLeftCapHitCenterX,
                PetLeftCapHitCenterY,
                PetLeftCapHitRadiusX,
                PetLeftCapHitRadiusY);
    }

    private void SendGlobalGazePoint()
    {
        if (!gazeTracking || clickThrough || roamAnimating || mouseFollow)
        {
            return;
        }

        Point clientPoint = PointToClient(Cursor.Position);
        ExecutePlayerScript($"window.desktopPet?.focusPoint({clientPoint.X}, {clientPoint.Y})");
    }

    internal void MoveBy(int dx, int dy)
    {
        MarkUserActivity();
        Location = new Point(Location.X + dx, Location.Y + dy);
        SyncMouseFollowKinematics(resetVelocity: true);
    }

    internal void BeginDragPose(Point clientPoint)
    {
        MarkUserActivity();
        SendDragPose(active: true, clientPoint, 0, 0, 0, 0);
    }

    internal void UpdateDragPose(Point clientPoint, int dx, int dy, int distance, int durationMs)
    {
        MarkUserActivity();
        SendDragPose(active: true, clientPoint, dx, dy, distance, durationMs);
    }

    internal void ResetDragPose()
    {
        SendDragPose(active: false, Point.Empty, 0, 0, 0, 0);
    }

    private void MoveTo(Point point)
    {
        MarkUserActivity();
        Location = point;
        SyncMouseFollowKinematics(resetVelocity: true);
    }

    internal void MarkUserActivity()
    {
        lastUserActivityTick = Environment.TickCount64;
        if (roamAnimating)
        {
            roamAnimating = false;
            ResetRoamPose();
            ScheduleNextRoam(lastUserActivityTick, RoamRetryDelayMinMs, RoamRetryDelayMaxMs);
        }
    }

    private void UpdateDesktopRoamTimer()
    {
        if (desktopRoam && !mouseFollow)
        {
            roamTimer.Start();
            return;
        }

        roamTimer.Stop();
    }

    private void UpdateMouseFollowTimer()
    {
        if (mouseFollow)
        {
            StartMouseFollowTimer();
            return;
        }

        StopMouseFollowTimer();
    }

    private void StartMouseFollowTimer()
    {
        if (mouseFollowTimer is not null)
        {
            return;
        }

        BeginMouseFollowTimerPrecision();
        mouseFollowLastFrameTick = System.Diagnostics.Stopwatch.GetTimestamp();
        Interlocked.Exchange(ref mouseFollowFramePending, 0);
        mouseFollowTimer = new System.Threading.Timer(
            QueueMouseFollowFrame,
            state: null,
            dueTime: 0,
            period: MouseFollowTimerIntervalMs);
    }

    private void StopMouseFollowTimer()
    {
        System.Threading.Timer? timer = mouseFollowTimer;
        if (timer is null)
        {
            EndMouseFollowTimerPrecision();
            return;
        }

        mouseFollowTimer = null;
        timer.Change(Timeout.Infinite, Timeout.Infinite);
        timer.Dispose();
        Interlocked.Exchange(ref mouseFollowFramePending, 0);
        mouseFollowLastFrameTick = 0;
        EndMouseFollowTimerPrecision();
    }

    private void BeginMouseFollowTimerPrecision()
    {
        if (mouseFollowTimerPrecisionActive)
        {
            return;
        }

        if (TimeBeginPeriod(1) == 0)
        {
            mouseFollowTimerPrecisionActive = true;
        }
    }

    private void EndMouseFollowTimerPrecision()
    {
        if (!mouseFollowTimerPrecisionActive)
        {
            return;
        }

        TimeEndPeriod(1);
        mouseFollowTimerPrecisionActive = false;
    }

    private void QueueMouseFollowFrame(object? state)
    {
        if (IsDisposed || !IsHandleCreated || Interlocked.Exchange(ref mouseFollowFramePending, 1) == 1)
        {
            return;
        }

        try
        {
            BeginInvoke((MethodInvoker)RunMouseFollowFrame);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref mouseFollowFramePending, 0);
        }
    }

    private void RunMouseFollowFrame()
    {
        Interlocked.Exchange(ref mouseFollowFramePending, 0);
        if (!mouseFollow)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double elapsedSeconds = mouseFollowLastFrameTick > 0
            ? (now - mouseFollowLastFrameTick) / (double)System.Diagnostics.Stopwatch.Frequency
            : MouseFollowTimerIntervalMs / 1000.0;
        mouseFollowLastFrameTick = now;
        UpdateMouseFollow(Math.Clamp(elapsedSeconds, MouseFollowMinFrameSeconds, MouseFollowMaxFrameSeconds));
    }

    private void UpdateCodexStatusTimer()
    {
        if (codexStatusIntegration)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CodexStatusFile)!);
            codexStatusTimer.Start();
            PollCodexStatus();
            return;
        }

        codexStatusTimer.Stop();
    }

    private void PollCodexStatus()
    {
        if (!codexStatusIntegration || webView.CoreWebView2 is null || !codexPlayerReady)
        {
            return;
        }

        try
        {
            if (!File.Exists(CodexStatusFile))
            {
                SendCodexStatusIdle();
                return;
            }

            FileInfo statusFile = new(CodexStatusFile);
            if (DateTime.UtcNow - statusFile.LastWriteTimeUtc > TimeSpan.FromMilliseconds(CodexStatusStaleMs))
            {
                SendCodexStatusIdle();
                return;
            }

            if (statusFile.LastWriteTimeUtc == codexStatusLastWriteUtc)
            {
                return;
            }

            string payload = ReadAllTextShared(CodexStatusFile).TrimStart('\uFEFF');
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            UpdateCodexHostState(document.RootElement);
            payload = document.RootElement.GetRawText();
            if (payload == codexStatusLastPayload)
            {
                codexStatusLastWriteUtc = statusFile.LastWriteTimeUtc;
                return;
            }

            codexStatusLastWriteUtc = statusFile.LastWriteTimeUtc;
            codexStatusLastPayload = payload;
            ExecutePlayerScript($"window.desktopPet?.setCodexStatus({payload})");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private void SendCodexStatusIdle()
    {
        if (!codexPlayerReady)
        {
            return;
        }

        codexStatusState = "idle";
        const string idlePayload = "{\"state\":\"idle\"}";
        if (codexStatusLastPayload == idlePayload)
        {
            return;
        }

        codexStatusLastWriteUtc = DateTime.MinValue;
        codexStatusLastPayload = idlePayload;
        ExecutePlayerScript($"window.desktopPet?.setCodexStatus({idlePayload})");
    }

    private static string ReadAllTextShared(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private bool IsCodexChargingState()
    {
        return CodexChargingStates.Contains(codexStatusState);
    }

    private void UpdateCodexHostState(JsonElement status)
    {
        codexStatusState = ResolveCodexHostState(status);
        if (!IsCodexChargingState() || !roamAnimating)
        {
            return;
        }

        roamAnimating = false;
        ResetRoamPose();
        ScheduleNextRoam(Environment.TickCount64, RoamRetryDelayMinMs, RoamRetryDelayMaxMs);
    }

    private static string ResolveCodexHostState(JsonElement status)
    {
        string state = NormalizeCodexState(GetStringProperty(status, "state", "status"));
        if (status.TryGetProperty("progress", out JsonElement progressElement)
            && progressElement.TryGetDouble(out double progress)
            && progress >= 100
            && state != "error"
            && state != "warning"
            && state != "blocked")
        {
            return "success";
        }

        return state;
    }

    private static string NormalizeCodexState(string? state)
    {
        string normalized = (state ?? "idle").Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized == "none")
        {
            return "idle";
        }

        return normalized;
    }

    private static string GetStringProperty(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out JsonElement property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private void UpdateMouseFollow(double elapsedSeconds)
    {
        if (!mouseFollow)
        {
            return;
        }

        if (mouseFollowThrowPhase != MouseFollowThrowPhase.None)
        {
            UpdateMouseFollowThrow(elapsedSeconds);
            return;
        }

        if (!CanFollowMouse())
        {
            StopMouseFollowPose();
            SyncMouseFollowKinematics(resetVelocity: true);
            return;
        }

        SyncMouseFollowKinematics(resetVelocity: false);
        Point targetLocation = GetMouseFollowTargetLocation();
        double dx = targetLocation.X - mouseFollowX;
        double dy = targetLocation.Y - mouseFollowY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < MouseFollowPoseStartDistance
            && GetVectorLength(mouseFollowVelocityX, mouseFollowVelocityY) < MouseFollowStopSpeed)
        {
            mouseFollowVelocityX = 0;
            mouseFollowVelocityY = 0;
            StopMouseFollowPose();
            return;
        }

        mouseFollowVelocityX += dx * MouseFollowSpring * elapsedSeconds;
        mouseFollowVelocityY += dy * MouseFollowSpring * elapsedSeconds;
        double damping = Math.Exp(-MouseFollowDamping * elapsedSeconds);
        mouseFollowVelocityX *= damping;
        mouseFollowVelocityY *= damping;
        LimitMouseFollowVelocity();

        mouseFollowX += mouseFollowVelocityX * elapsedSeconds;
        mouseFollowY += mouseFollowVelocityY * elapsedSeconds;
        Point nextLocation = new((int)Math.Round(mouseFollowX), (int)Math.Round(mouseFollowY));
        if (nextLocation != Location)
        {
            Location = nextLocation;
        }

        double speed = GetVectorLength(mouseFollowVelocityX, mouseFollowVelocityY);
        double poseProgress = 0.12 + Math.Clamp(speed / MouseFollowMaxSpeed, 0, 1) * 0.44;
        SendRoamPose(
            active: true,
            mouseFollowVelocityX,
            mouseFollowVelocityY,
            poseProgress,
            source: "mouse-follow");
        mouseFollowPoseActive = true;
    }

    private void StartMouseFollowThrow(double directionX, double directionY, double speed, int distance)
    {
        ResetDragPose();
        StopMouseFollowPose();
        SyncMouseFollowKinematics(resetVelocity: true);
        mouseFollowThrowDirectionX = directionX;
        mouseFollowThrowDirectionY = directionY;
        mouseFollowThrowVelocityX = directionX * speed;
        mouseFollowThrowVelocityY = directionY * speed;
        mouseFollowThrowElapsedSeconds = 0;
        mouseFollowThrowDurationSeconds = Math.Clamp(
            MouseFollowThrowAwayMinDurationSeconds + distance / 900.0,
            MouseFollowThrowAwayMinDurationSeconds,
            MouseFollowThrowAwayMaxDurationSeconds);
        mouseFollowThrowPhase = MouseFollowThrowPhase.ThrowingAway;
    }

    private void UpdateMouseFollowThrow(double elapsedSeconds)
    {
        if (!Visible || WindowState != FormWindowState.Normal || inputPointerActive || dragging)
        {
            CancelMouseFollowThrow();
            SyncMouseFollowKinematics(resetVelocity: true);
            return;
        }

        if (mouseFollowThrowPhase == MouseFollowThrowPhase.ThrowingAway)
        {
            ContinueMouseFollowThrowAway(elapsedSeconds);
            return;
        }

        if (mouseFollowThrowPhase == MouseFollowThrowPhase.Reacting)
        {
            ContinueMouseFollowThrowReaction(elapsedSeconds);
            return;
        }

        if (mouseFollowThrowPhase == MouseFollowThrowPhase.Returning)
        {
            ContinueMouseFollowThrowReturn(elapsedSeconds);
        }
    }

    private void ContinueMouseFollowThrowAway(double elapsedSeconds)
    {
        mouseFollowThrowElapsedSeconds += elapsedSeconds;
        double damping = Math.Exp(-MouseFollowThrowAwayDamping * elapsedSeconds);
        mouseFollowThrowVelocityX *= damping;
        mouseFollowThrowVelocityY *= damping;
        mouseFollowX += mouseFollowThrowVelocityX * elapsedSeconds;
        mouseFollowY += mouseFollowThrowVelocityY * elapsedSeconds;

        Point candidate = new((int)Math.Round(mouseFollowX), (int)Math.Round(mouseFollowY));
        Point nextLocation = ClampLocationToWorkingArea(candidate, Screen.FromPoint(GetPetCenter()).WorkingArea);
        if (nextLocation.X != candidate.X)
        {
            mouseFollowThrowVelocityX = 0;
            mouseFollowX = nextLocation.X;
        }

        if (nextLocation.Y != candidate.Y)
        {
            mouseFollowThrowVelocityY = 0;
            mouseFollowY = nextLocation.Y;
        }

        if (nextLocation != Location)
        {
            Location = nextLocation;
        }

        double speed = GetVectorLength(mouseFollowThrowVelocityX, mouseFollowThrowVelocityY);
        SendRoamPose(
            active: true,
            mouseFollowThrowVelocityX,
            mouseFollowThrowVelocityY,
            0.56 + Math.Clamp(speed / MouseFollowThrowMaxSpeed, 0, 1) * 0.28,
            source: "mouse-follow-throw");
        mouseFollowPoseActive = true;

        if (mouseFollowThrowElapsedSeconds >= mouseFollowThrowDurationSeconds
            || speed < MouseFollowThrowAwayMinFinishSpeed)
        {
            CompleteMouseFollowThrowAway();
        }
    }

    private void CompleteMouseFollowThrowAway()
    {
        StopMouseFollowPose();
        SyncMouseFollowKinematics(resetVelocity: true);
        mouseFollowThrowCount++;
        bool shouldStopFollowing = mouseFollowThrowCount >= MouseFollowThrowLimit;
        if (shouldStopFollowing)
        {
            PlayMotion("Failed");
            DisableMouseFollowAfterThrow();
            return;
        }

        PlayMotion("Nope");
        mouseFollowThrowElapsedSeconds = 0;
        mouseFollowThrowDurationSeconds = MouseFollowThrowNopeHoldSeconds;
        mouseFollowThrowVelocityX = 0;
        mouseFollowThrowVelocityY = 0;
        mouseFollowThrowPhase = MouseFollowThrowPhase.Reacting;
    }

    private void ContinueMouseFollowThrowReaction(double elapsedSeconds)
    {
        mouseFollowThrowElapsedSeconds += elapsedSeconds;
        if (mouseFollowThrowElapsedSeconds < mouseFollowThrowDurationSeconds)
        {
            return;
        }

        StartMouseFollowThrowReturn();
    }

    private void StartMouseFollowThrowReturn()
    {
        Point targetLocation = GetMouseFollowThrowTargetLocation(mouseFollowThrowDirectionX, mouseFollowThrowDirectionY);
        mouseFollowThrowStartX = mouseFollowX;
        mouseFollowThrowStartY = mouseFollowY;
        mouseFollowThrowTargetX = targetLocation.X;
        mouseFollowThrowTargetY = targetLocation.Y;
        mouseFollowThrowElapsedSeconds = 0;
        double pathDistance = GetVectorLength(mouseFollowThrowTargetX - mouseFollowThrowStartX, mouseFollowThrowTargetY - mouseFollowThrowStartY);
        mouseFollowThrowDurationSeconds = Math.Clamp(
            MouseFollowThrowReturnMinDurationSeconds + pathDistance / MouseFollowThrowReturnDistanceDurationFactor,
            MouseFollowThrowReturnMinDurationSeconds,
            MouseFollowThrowReturnMaxDurationSeconds);
        mouseFollowThrowPhase = MouseFollowThrowPhase.Returning;
    }

    private void ContinueMouseFollowThrowReturn(double elapsedSeconds)
    {
        double previousX = mouseFollowX;
        double previousY = mouseFollowY;
        mouseFollowThrowElapsedSeconds += elapsedSeconds;
        double progress = Math.Clamp(mouseFollowThrowElapsedSeconds / Math.Max(0.001, mouseFollowThrowDurationSeconds), 0, 1);
        double easedProgress = SmoothStep(progress);
        mouseFollowX = mouseFollowThrowStartX + (mouseFollowThrowTargetX - mouseFollowThrowStartX) * easedProgress;
        mouseFollowY = mouseFollowThrowStartY + (mouseFollowThrowTargetY - mouseFollowThrowStartY) * easedProgress;
        mouseFollowThrowVelocityX = (mouseFollowX - previousX) / Math.Max(0.001, elapsedSeconds);
        mouseFollowThrowVelocityY = (mouseFollowY - previousY) / Math.Max(0.001, elapsedSeconds);

        Point nextLocation = new((int)Math.Round(mouseFollowX), (int)Math.Round(mouseFollowY));
        if (nextLocation != Location)
        {
            Location = nextLocation;
        }

        double speed = GetVectorLength(mouseFollowThrowVelocityX, mouseFollowThrowVelocityY);
        SendRoamPose(
            active: true,
            mouseFollowThrowVelocityX,
            mouseFollowThrowVelocityY,
            0.32 + Math.Sin(Math.PI * progress) * 0.34 + Math.Clamp(speed / MouseFollowThrowMaxSpeed, 0, 1) * 0.18,
            source: "mouse-follow-throw");
        mouseFollowPoseActive = true;

        if (progress >= 1)
        {
            CompleteMouseFollowThrowReturn();
        }
    }

    private void CompleteMouseFollowThrowReturn()
    {
        mouseFollowThrowPhase = MouseFollowThrowPhase.None;
        mouseFollowThrowVelocityX = 0;
        mouseFollowThrowVelocityY = 0;
        mouseFollowThrowDirectionX = 0;
        mouseFollowThrowDirectionY = 0;
        mouseFollowThrowStartX = 0;
        mouseFollowThrowStartY = 0;
        mouseFollowThrowTargetX = 0;
        mouseFollowThrowTargetY = 0;
        StopMouseFollowPose();
        SyncMouseFollowKinematics(resetVelocity: true);
    }

    private void DisableMouseFollowAfterThrow()
    {
        mouseFollow = false;
        mouseFollowMenuItem.Checked = false;
        mouseFollowThrowCount = 0;
        CancelMouseFollowThrow();
        StopMouseFollowPose();
        SyncMouseFollowKinematics(resetVelocity: true);
        UpdateMouseFollowTimer();
        UpdateDesktopRoamTimer();
    }

    private void CancelMouseFollowThrow()
    {
        mouseFollowThrowPhase = MouseFollowThrowPhase.None;
        mouseFollowThrowVelocityX = 0;
        mouseFollowThrowVelocityY = 0;
        mouseFollowThrowDirectionX = 0;
        mouseFollowThrowDirectionY = 0;
        mouseFollowThrowStartX = 0;
        mouseFollowThrowStartY = 0;
        mouseFollowThrowTargetX = 0;
        mouseFollowThrowTargetY = 0;
        mouseFollowThrowElapsedSeconds = 0;
        mouseFollowThrowDurationSeconds = 0;
    }

    private bool CanFollowMouse()
    {
        return Visible
            && WindowState == FormWindowState.Normal
            && !dragging
            && !inputPointerActive;
    }

    private Point GetMouseFollowTargetLocation()
    {
        Point cursor = Cursor.Position;
        Point center = GetPetCenter();
        double dx = cursor.X - center.X;
        double dy = cursor.Y - center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= MouseFollowComfortDistance)
        {
            return Location;
        }

        double targetCenterX = cursor.X - dx / distance * MouseFollowComfortDistance;
        double targetCenterY = cursor.Y - dy / distance * MouseFollowComfortDistance;
        Point targetLocation = new(
            (int)Math.Round(targetCenterX - Width / 2.0),
            (int)Math.Round(targetCenterY - Height / 2.0));
        return ClampLocationToWorkingArea(targetLocation, Screen.FromPoint(cursor).WorkingArea);
    }

    private Point GetMouseFollowThrowTargetLocation(double directionX, double directionY)
    {
        Point cursor = Cursor.Position;
        double length = GetVectorLength(directionX, directionY);
        if (length < 0.001)
        {
            Point center = GetPetCenter();
            directionX = cursor.X - center.X;
            directionY = cursor.Y - center.Y;
            length = GetVectorLength(directionX, directionY);
        }

        if (length < 0.001)
        {
            directionX = 0;
            directionY = -1;
            length = 1;
        }

        directionX /= length;
        directionY /= length;
        double targetCenterX = cursor.X - directionX * MouseFollowComfortDistance;
        double targetCenterY = cursor.Y - directionY * MouseFollowComfortDistance;
        Point targetLocation = new(
            (int)Math.Round(targetCenterX - Width / 2.0),
            (int)Math.Round(targetCenterY - Height / 2.0));
        return ClampLocationToWorkingArea(targetLocation, Screen.FromPoint(cursor).WorkingArea);
    }

    private void LimitMouseFollowVelocity()
    {
        double speed = GetVectorLength(mouseFollowVelocityX, mouseFollowVelocityY);
        if (speed <= MouseFollowMaxSpeed)
        {
            return;
        }

        double scale = MouseFollowMaxSpeed / speed;
        mouseFollowVelocityX *= scale;
        mouseFollowVelocityY *= scale;
    }

    private void SyncMouseFollowKinematics(bool resetVelocity)
    {
        if (Math.Abs(mouseFollowX - Location.X) > 2 || Math.Abs(mouseFollowY - Location.Y) > 2)
        {
            mouseFollowX = Location.X;
            mouseFollowY = Location.Y;
        }

        if (resetVelocity)
        {
            mouseFollowVelocityX = 0;
            mouseFollowVelocityY = 0;
        }
    }

    private void StopMouseFollowPose()
    {
        if (!mouseFollowPoseActive)
        {
            return;
        }

        mouseFollowPoseActive = false;
        ResetRoamPose(source: "mouse-follow");
    }

    private void UpdateDesktopRoam()
    {
        long now = Environment.TickCount64;
        if (!desktopRoam || mouseFollow)
        {
            return;
        }

        if (roamAnimating)
        {
            ContinueRoam(now);
            return;
        }

        if (now < nextRoamTick)
        {
            return;
        }

        if (!CanStartRoam(now))
        {
            ScheduleNextRoam(now, RoamRetryDelayMinMs, RoamRetryDelayMaxMs);
            return;
        }

        StartRoam(now);
    }

    private void StartRoam(long now)
    {
        Point target = GetRoamTarget();
        if (target == Location)
        {
            ScheduleNextRoam(now, RoamRetryDelayMinMs, RoamRetryDelayMaxMs);
            return;
        }

        roamStartLocation = Location;
        roamTargetLocation = target;
        roamStartedTick = now;
        roamDurationMs = Random.Shared.Next(RoamDurationMinMs, RoamDurationMaxMs + 1);
        roamAnimating = true;
        SendRoamPose(active: true, target.X - Location.X, target.Y - Location.Y, progress: 0);
    }

    private void ContinueRoam(long now)
    {
        if (!CanContinueRoam(now))
        {
            roamAnimating = false;
            ResetRoamPose();
            ScheduleNextRoam(now, RoamRetryDelayMinMs, RoamRetryDelayMaxMs);
            return;
        }

        double progress = Math.Clamp((now - roamStartedTick) / (double)Math.Max(1, roamDurationMs), 0, 1);
        double easedProgress = SmoothStep(progress);
        int x = roamStartLocation.X + (int)Math.Round((roamTargetLocation.X - roamStartLocation.X) * easedProgress);
        int y = roamStartLocation.Y + (int)Math.Round((roamTargetLocation.Y - roamStartLocation.Y) * easedProgress);
        Location = new Point(x, y);
        SendRoamPose(
            active: true,
            roamTargetLocation.X - roamStartLocation.X,
            roamTargetLocation.Y - roamStartLocation.Y,
            progress);

        if (progress >= 1)
        {
            roamAnimating = false;
            ResetRoamPose();
            ScheduleNextRoam(now, RoamDelayMinMs, RoamDelayMaxMs);
        }
    }

    private bool CanStartRoam(long now)
    {
        return Visible
            && WindowState == FormWindowState.Normal
            && !dragging
            && !mouseFollow
            && !IsCodexChargingState()
            && now - lastUserActivityTick >= RoamQuietAfterUserMs
            && !IsCursorNearPet();
    }

    private bool CanContinueRoam(long now)
    {
        return Visible
            && WindowState == FormWindowState.Normal
            && !dragging
            && !mouseFollow
            && !IsCodexChargingState()
            && now - lastUserActivityTick >= 400
            && !IsCursorNearPet();
    }

    private Point GetRoamTarget()
    {
        for (int i = 0; i < 10; i++)
        {
            int dx = Random.Shared.Next(-RoamMaxStepX, RoamMaxStepX + 1);
            int dy = Random.Shared.Next(-RoamMaxStepY, RoamMaxStepY + 1);
            if (GetDistance(Point.Empty, new Point(dx, dy)) < RoamMinDistance)
            {
                continue;
            }

            Point candidate = new(Location.X + dx, Location.Y + dy);
            if (candidate != Location)
            {
                return candidate;
            }
        }

        return Location;
    }

    private Point GetPetCenter()
    {
        return new Point(Location.X + Width / 2, Location.Y + Height / 2);
    }

    private bool IsCursorNearPet()
    {
        Rectangle guardBounds = Bounds;
        guardBounds.Inflate(RoamCursorGuardPadding, RoamCursorGuardPadding);
        return guardBounds.Contains(Cursor.Position);
    }

    private void ScheduleNextRoam(long now, int minDelayMs, int maxDelayMs)
    {
        nextRoamTick = now + Random.Shared.Next(minDelayMs, maxDelayMs + 1);
    }

    private void SendRoamPose(bool active, double dx, double dy, double progress, string source = "roam")
    {
        object payload = new
        {
            active,
            dx,
            dy,
            progress,
            source
        };
        ExecutePlayerScript($"window.desktopPet?.setRoamPose({JsonSerializer.Serialize(payload)})");
    }

    private void ResetRoamPose(string source = "roam")
    {
        SendRoamPose(active: false, dx: 0, dy: 0, progress: 0, source);
    }

    private void SendDragPose(bool active, Point clientPoint, int dx, int dy, int distance, int durationMs)
    {
        object payload = new
        {
            active,
            clientX = clientPoint.X,
            clientY = clientPoint.Y,
            dx,
            dy,
            distance,
            durationMs
        };
        ExecutePlayerScript($"window.desktopPet?.setDragPose({JsonSerializer.Serialize(payload)})");
    }

    internal static Point GetScreenPointFromLParam(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }

    private static bool IsInNormalizedEllipse(
        Point point,
        Size size,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY)
    {
        double width = Math.Max(1, size.Width);
        double height = Math.Max(1, size.Height);
        double normalizedX = (point.X / width - centerX) / radiusX;
        double normalizedY = (point.Y / height - centerY) / radiusY;
        return normalizedX * normalizedX + normalizedY * normalizedY <= 1;
    }

    private static int GetDistance(Point start, Point end)
    {
        int dx = end.X - start.X;
        int dy = end.Y - start.Y;
        return (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
    }

    private Point ClampLocationToWorkingArea(Point point, Rectangle workingArea)
    {
        int maxX = Math.Max(workingArea.Left, workingArea.Right - Width);
        int maxY = Math.Max(workingArea.Top, workingArea.Bottom - Height);
        return new Point(
            Math.Clamp(point.X, workingArea.Left, maxX),
            Math.Clamp(point.Y, workingArea.Top, maxY));
    }

    private static double GetVectorLength(double x, double y)
    {
        return Math.Sqrt(x * x + y * y);
    }

    private static double SmoothStep(double value)
    {
        double t = Math.Clamp(value, 0, 1);
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static Point GetInitialLocation(Size size)
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new Point(area.Right - size.Width - 96, area.Bottom - size.Height - 72);
    }

    private void SyncInputOverlayBounds()
    {
        if (inputOverlay is not null && !inputOverlay.IsDisposed)
        {
            inputOverlay.Bounds = Bounds;
        }
    }

    private void UpdateInputOverlay()
    {
        if (inputOverlay is null || inputOverlay.IsDisposed)
        {
            return;
        }

        SyncInputOverlayBounds();
        if (clickThrough)
        {
            inputOverlay.Hide();
            return;
        }

        if (!inputOverlay.Visible)
        {
            inputOverlay.Show(this);
        }

        inputOverlay.TopMost = true;
        inputOverlay.BringToFront();
    }

    private void UpdateGazeTimer()
    {
        if (gazeTracking && !clickThrough)
        {
            gazeTimer.Start();
            return;
        }

        gazeTimer.Stop();
        if (webView.CoreWebView2 is not null)
        {
            ExecutePlayerScript("window.desktopPet?.focusCenter()");
        }
    }

    private void ApplyNativeStyles()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        long exStyle = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
        exStyle |= WsExToolWindow | WsExTopMost | WsExNoActivate | WsExTransparent;

        SetWindowLongPtr(Handle, GwlExStyle, new IntPtr(exStyle));
        SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", ExactSpelling = true)]
    private static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", ExactSpelling = true)]
    private static extern uint TimeEndPeriod(uint period);

    private static Icon LoadAppIcon()
    {
        try
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null)
            {
                return icon;
            }
        }
        catch (ArgumentException)
        {
        }
        catch (FileNotFoundException)
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}

internal sealed class InputOverlayForm : Form
{
    private const int DragThreshold = 4;
    private const int LongPressMilliseconds = 650;
    private const int HoverFocusMilliseconds = 45;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtTransparent = -1;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExTopMost = 0x00000008L;
    private const long WsExNoActivate = 0x08000000L;

    private readonly PetForm petForm;
    private readonly System.Windows.Forms.Timer longPressTimer = new();
    private readonly System.Windows.Forms.Timer singleClickTimer = new();
    private bool dragging;
    private bool moved;
    private bool longPressTriggered;
    private bool hasPendingClick;
    private bool dragPoseActive;
    private long lastFocusTick;
    private DateTime pressStartedAt;
    private Point pressClient;
    private Point pendingClickClient;
    private Point dragStartScreen;
    private Point lastDragScreen;
    private Point lastMoveDelta;
    private DateTime lastDragMoveAt;
    private double lastMoveSpeed;

    public InputOverlayForm(PetForm petForm, ContextMenuStrip contextMenu)
    {
        this.petForm = petForm;
        ContextMenuStrip = contextMenu;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.01;
        Cursor = Cursors.Hand;
        StartPosition = FormStartPosition.Manual;
        Size = petForm.Size;

        longPressTimer.Interval = LongPressMilliseconds;
        longPressTimer.Tick += (_, _) => TriggerLongPress();
        singleClickTimer.Interval = Math.Max(150, SystemInformation.DoubleClickTime);
        singleClickTimer.Tick += (_, _) => TriggerPendingSingleClick();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= (int)(WsExToolWindow | WsExTopMost | WsExNoActivate);
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            Point screenPoint = PetForm.GetScreenPointFromLParam(m.LParam);
            m.Result = petForm.IsPetInteractiveScreenPoint(screenPoint)
                ? new IntPtr(HtClient)
                : new IntPtr(HtTransparent);
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            longPressTimer.Dispose();
            singleClickTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        dragging = true;
        moved = false;
        longPressTriggered = false;
        dragPoseActive = false;
        petForm.BeginPointerActivity();
        pressStartedAt = DateTime.UtcNow;
        pressClient = e.Location;
        dragStartScreen = PointToScreen(e.Location);
        lastDragScreen = dragStartScreen;
        lastMoveDelta = Point.Empty;
        lastDragMoveAt = pressStartedAt;
        lastMoveSpeed = 0;
        longPressTimer.Stop();
        longPressTimer.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging || e.Button != MouseButtons.Left)
        {
            SendFocus(e.Location);
            return;
        }

        Point screenPoint = PointToScreen(e.Location);
        if (!moved
            && Math.Abs(screenPoint.X - dragStartScreen.X) < DragThreshold
            && Math.Abs(screenPoint.Y - dragStartScreen.Y) < DragThreshold)
        {
            return;
        }

        int dx = screenPoint.X - lastDragScreen.X;
        int dy = screenPoint.Y - lastDragScreen.Y;
        int distance = GetDistance(dragStartScreen, screenPoint);
        DateTime now = DateTime.UtcNow;
        int durationMs = Math.Max(0, (int)(now - pressStartedAt).TotalMilliseconds);
        double moveElapsedMs = Math.Max(1, (now - lastDragMoveAt).TotalMilliseconds);
        lastMoveDelta = new Point(dx, dy);
        lastMoveSpeed = GetDistance(Point.Empty, lastMoveDelta) * 1000.0 / moveElapsedMs;
        lastDragMoveAt = now;

        if (!moved)
        {
            moved = true;
            dragPoseActive = true;
            petForm.BeginDragPose(e.Location);
        }

        longPressTimer.Stop();
        CancelPendingClick();
        petForm.MoveBy(dx, dy);
        petForm.UpdateDragPose(e.Location, dx, dy, distance, durationMs);
        lastDragScreen = screenPoint;
    }

    protected override void OnMouseHover(EventArgs e)
    {
        base.OnMouseHover(e);
        SendFocus(PointToClient(MousePosition));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (dragging)
        {
            return;
        }

        petForm.FocusClientPoint(new Point(Width / 2, Height / 2));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        longPressTimer.Stop();
        dragging = false;
        petForm.EndPointerActivity();
        Point releaseScreen = PointToScreen(e.Location);
        int distance = GetDistance(dragStartScreen, releaseScreen);
        DateTime releasedAt = DateTime.UtcNow;
        int durationMs = Math.Max(0, (int)(releasedAt - pressStartedAt).TotalMilliseconds);
        int lastMoveAgeMs = Math.Max(0, (int)(releasedAt - lastDragMoveAt).TotalMilliseconds);
        if (moved)
        {
            if (dragPoseActive)
            {
                dragPoseActive = false;
            }

            Point dragVector = new(releaseScreen.X - dragStartScreen.X, releaseScreen.Y - dragStartScreen.Y);
            if (petForm.TryStartMouseFollowThrow(
                dragVector,
                lastMoveDelta,
                lastMoveSpeed,
                lastMoveAgeMs,
                distance,
                durationMs))
            {
                return;
            }

            petForm.TriggerInteraction("dragRelease", e.Location, distance, durationMs);
            return;
        }

        if (longPressTriggered)
        {
            return;
        }

        if (IsSecondClick(e.Location))
        {
            singleClickTimer.Stop();
            hasPendingClick = false;
            petForm.TriggerInteraction("doubleClick", e.Location, distance, durationMs);
            return;
        }

        pendingClickClient = e.Location;
        hasPendingClick = true;
        singleClickTimer.Stop();
        singleClickTimer.Start();
    }

    private void TriggerLongPress()
    {
        longPressTimer.Stop();
        if (!dragging || moved || longPressTriggered)
        {
            return;
        }

        longPressTriggered = true;
        CancelPendingClick();
        int durationMs = Math.Max(0, (int)(DateTime.UtcNow - pressStartedAt).TotalMilliseconds);
        petForm.TriggerInteraction("longPress", pressClient, 0, durationMs);
    }

    private void TriggerPendingSingleClick()
    {
        singleClickTimer.Stop();
        if (!hasPendingClick)
        {
            return;
        }

        hasPendingClick = false;
        petForm.TriggerInteraction("click", pendingClickClient);
    }

    private void CancelPendingClick()
    {
        if (!hasPendingClick)
        {
            return;
        }

        singleClickTimer.Stop();
        hasPendingClick = false;
    }

    private bool IsSecondClick(Point point)
    {
        if (!hasPendingClick)
        {
            return false;
        }

        Size doubleClickSize = SystemInformation.DoubleClickSize;
        return Math.Abs(point.X - pendingClickClient.X) <= doubleClickSize.Width / 2
            && Math.Abs(point.Y - pendingClickClient.Y) <= doubleClickSize.Height / 2;
    }

    private void SendFocus(Point clientPoint)
    {
        if (!petForm.GazeTrackingEnabled)
        {
            return;
        }

        long tick = Environment.TickCount64;
        if (tick - lastFocusTick < HoverFocusMilliseconds)
        {
            return;
        }

        lastFocusTick = tick;
        petForm.FocusClientPoint(clientPoint);
    }

    private static int GetDistance(Point start, Point end)
    {
        int dx = end.X - start.X;
        int dy = end.Y - start.Y;
        return (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
    }
}
