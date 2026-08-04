using System.Diagnostics;
using ScreenTextMonitor.Core;
using ScreenTextMonitor.Ui;

namespace ScreenTextMonitor;

public sealed partial class MainForm : Form
{
    // ---------------- Constants (mirroring Python edition) ----------------
    private const int ChangeThumbW = 480;
    private const double ChangeThreshold = 2.0;
    private const int PerfMaxDim = 1000;
    private const int IdleBackoffBase = 3;
    private const double IdleBackoffStep = 0.6;
    private const double IdleBackoffCap = 5.0;

    // ---------------- Navigation ----------------
    private NavRail _rail;
    private TopBar _topBar;
    private Panel _tabRun;
    private ScrollStack _tabSet;

    // ---------------- Run Tab Controls ----------------
    private FlatTextBox _entryX, _entryY, _entryW, _entryH, _entryText;
    private RoundedButton _btnSelect, _btnStart, _btnPreview, _btnClear, _btnQq;
    private Label _labelCpu, _labelStats;
    private RoundedProgressBar _cpuBar;
    private LogBox _log;
    private RoundedCard _logCard;

    // ---------------- Settings Tab Controls ----------------
    private SegmentedControl _segAlert;
    private StackPanel _alertBody, _frameBeep, _frameAudio, _frameTts;
    private FlatTextBox _entryFreq, _entryDur, _entryAudio, _entryTts;
    private RoundedButton _btnBrowse;
    private RoundedSwitch _swQq, _swSkipStatic, _swSmartSkip, _swPerfMode, _swAutoBackoff;
    private FlatTextBox _entryQqUrl, _entryQqToken, _entryQqTarget, _entryQqMsg, _entryQqWs, _entryQqCmdStart, _entryQqCmdStop;
    private RoundedSwitch _swQqCtrlLock;
    private QqController _qqCtrl;
    private RoundedSlider _sliderInterval, _sliderDelay, _sliderSens;
    private Label _lblInterval, _lblDelay, _lblSens;

    // ---------------- Monitor State ----------------
    private SceneMonitor _monitor;
    private int _statOcr, _statSkip;
    private double _statInterval = 1.0;

    private volatile float _forceOcrIdle = 4.0f;
    private volatile float _ocrThreshold = 6.0f;
    private System.Windows.Forms.Timer _cpuTimer;
    private (double proc, double wall)? _cpuPrev;
    private readonly int _cpuNproc = Math.Max(1, Environment.ProcessorCount);
    private readonly bool _ttsAvailable = Alerts.TtsAvailable();

    private uint _origPriorityClass;
    private readonly SemaphoreSlim _alertGate = new(1, 1);
    private WheelMessageFilter _wheelFilter;

    public MainForm()
    {
        Text = "屏幕文字监控工具";
        ClientSize = new Size(1140, 720);
        MinimumSize = new Size(860, 560);
        BackColor = Theme.Bg;
        Font = Theme.FontUi;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        try
        {
            string ico = Path.Combine(AppConfig.AppDir, "app.ico");
            if (File.Exists(ico)) Icon = new Icon(ico);
        }
        catch { }

        BuildUi();
        LoadConfig();

        _cpuTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _cpuTimer.Tick += (_, _) => UpdateCpu();
    }
}
