using SegLcd.Client;
using SegLcd.Localization;

namespace SegLcd.Gui;

/// <summary>
/// The main window: connects to seglcdd, shows its state and live values, and edits interval and sensor settings.
/// Every change is applied and saved at once (D-Bus set + <c>SaveConfig</c>). All methods run on the GTK thread.
/// </summary>
internal sealed class MainWindow
{
    public const string ApplicationId = "io.github.fubar_coder.SegLcd";
    private const uint ValuesIntervalMs = 1000;
    private const uint ReconnectIntervalMs = 3000;

    private readonly Adw.ApplicationWindow _window;
    private readonly Adw.ToastOverlay _toasts;
    private readonly Gtk.Stack _stack;
    private readonly Adw.StatusPage _missing;

    private readonly Adw.ActionRow _modelRow;
    private readonly Adw.ActionRow _serviceRow;
    private readonly Adw.SwitchRow _enabledRow;
    private readonly Adw.SpinRow _updateRow;
    private readonly Adw.SpinRow _sampleRow;
    private readonly Dictionary<string, Gtk.Label> _valueLabels = [];
    private readonly Dictionary<string, Adw.ComboRow> _sensorRows = [];
    private Adw.ComboRow _clockModeRow = null!;
    private readonly Dictionary<string, IReadOnlyList<SensorChoice>> _choices = [];

    private SegLcdConnection? _connection;
    private IDisposable? _watch;
    private ServiceState? _state;
    private IReadOnlyList<ServiceSensor> _sensors = [];
    private uint _valuesTimer;
    private uint _reconnectTimer;
    private bool _updating;
    private bool _connecting;

    private readonly ServiceBus? _bus;

    public MainWindow(Adw.Application app, ServiceBus? bus)
    {
        _bus = bus;
        _window = Adw.ApplicationWindow.New(app);
        _window.SetTitle("SegLcd");
        _window.SetDefaultSize(680, 760);

        // Pages of the connected state.
        var views = Adw.ViewStack.New();
        views.AddTitledWithIcon(BuildStatusPage(out _modelRow, out _serviceRow, out _enabledRow, out _updateRow, out _sampleRow),
            "status", T("Status"), "utilities-system-monitor-symbolic");
        views.AddTitledWithIcon(BuildSensorPage(), "sensors", T("Sensors"), "preferences-system-symbolic");

        var header = Adw.HeaderBar.New();
        var switcher = Adw.ViewSwitcher.New();
        switcher.SetStack(views);
        switcher.SetPolicy(Adw.ViewSwitcherPolicy.Wide);
        header.SetTitleWidget(switcher);

        views.SetVexpand(true);

        var toolbar = Adw.ToolbarView.New();
        toolbar.AddTopBar(header);
        toolbar.SetContent(views);

        // Connecting / service missing.
        var spinner = Adw.Spinner.New();
        spinner.SetSizeRequest(48, 48);
        spinner.SetHalign(Gtk.Align.Center);
        spinner.SetValign(Gtk.Align.Center);

        _missing = Adw.StatusPage.New();
        _missing.SetIconName("dialog-warning-symbolic");
        _missing.SetTitle(T("Service not found"));
        _missing.SetDescription(T("seglcdd runs neither as system service nor as user service. Install a SegLcd package, or start the user service with \"systemctl --user start seglcdd\"."));
        var retry = Gtk.Button.NewWithMnemonic(T("_Try Again"));
        retry.AddCssClass("pill");
        retry.SetHalign(Gtk.Align.Center);
        retry.OnClicked += (_, _) => Run(ConnectAsync);
        _missing.SetChild(retry);

        var missingView = Adw.ToolbarView.New();
        missingView.AddTopBar(Adw.HeaderBar.New());
        missingView.SetContent(_missing);

        var connectingView = Adw.ToolbarView.New();
        connectingView.AddTopBar(Adw.HeaderBar.New());
        connectingView.SetContent(spinner);

        _stack = Gtk.Stack.New();
        _stack.SetTransitionType(Gtk.StackTransitionType.Crossfade);
        _stack.AddNamed(connectingView, "connecting");
        _stack.AddNamed(missingView, "missing");
        _stack.AddNamed(toolbar, "main");

        _toasts = Adw.ToastOverlay.New();
        _toasts.SetChild(_stack);
        _window.SetContent(_toasts);

        var about = Gio.SimpleAction.New("about", null);
        about.OnActivate += (_, _) => ShowAbout();
        app.AddAction(about);
        var reload = Gio.SimpleAction.New("reload", null);
        reload.OnActivate += (_, _) => Run(ReloadAsync);
        app.AddAction(reload);
        var menu = Gio.Menu.New();
        menu.Append(T("_Reload Configuration File"), "app.reload");
        menu.Append(T("_About SegLcd"), "app.about");
        var menuButton = Gtk.MenuButton.New();
        menuButton.SetIconName("open-menu-symbolic");
        menuButton.SetMenuModel(menu);
        header.PackEnd(menuButton);

        _window.OnCloseRequest += (_, _) =>
        {
            Disconnect();
            return false;
        };
        Run(ConnectAsync);
    }

    public void Present() => _window.Present();

    private static string T(string text) => Texts.Current.T(text);

    // --- pages ----------------------------------------------------------------------------------

    private Gtk.Widget BuildStatusPage(out Adw.ActionRow modelRow, out Adw.ActionRow serviceRow, out Adw.SwitchRow enabledRow,
        out Adw.SpinRow updateRow, out Adw.SpinRow sampleRow)
    {
        var page = Adw.PreferencesPage.New();

        var display = Adw.PreferencesGroup.New();
        display.SetTitle(T("Display"));
        modelRow = Adw.ActionRow.New();
        modelRow.SetTitle(T("Device"));
        modelRow.AddCssClass("property");
        display.Add(modelRow);
        enabledRow = Adw.SwitchRow.New();
        enabledRow.SetTitle(T("Feed display"));
        enabledRow.SetSubtitle(T("Off: the display goes dark and the device is free for seglcd"));
        enabledRow.OnNotify += OnEnabledChanged;
        display.Add(enabledRow);
        page.Add(display);

        var values = Adw.PreferencesGroup.New();
        values.SetTitle(T("Values"));
        values.SetDescription(T("The LB360-S shows only the CPU and GPU temperature; the other values are sent along."));
        foreach (var field in DisplayFields.All)
        {
            var row = Adw.ActionRow.New();
            row.SetTitle(field.Title);
            if (field.ShownByLb360S)
                row.SetSubtitle(T("shown on the display"));
            var label = Gtk.Label.New("–");
            label.AddCssClass("numeric");
            label.AddCssClass("dim-label");
            row.AddSuffix(label);
            _valueLabels[field.Name] = label;
            values.Add(row);
        }

        page.Add(values);

        var timing = Adw.PreferencesGroup.New();
        timing.SetTitle(T("Timing"));
        updateRow = Adw.SpinRow.NewWithRange(50, 400, 10);
        updateRow.SetTitle(T("Update interval"));
        updateRow.SetSubtitle(T("Milliseconds between packets; above 400 ms the display turns off"));
        updateRow.OnNotify += OnUpdateIntervalChanged;
        timing.Add(updateRow);
        sampleRow = Adw.SpinRow.NewWithRange(100, 10000, 100);
        sampleRow.SetTitle(T("Sample interval"));
        sampleRow.SetSubtitle(T("Milliseconds between sensor readings"));
        sampleRow.OnNotify += OnSampleIntervalChanged;
        timing.Add(sampleRow);
        page.Add(timing);

        var service = Adw.PreferencesGroup.New();
        service.SetTitle(T("Service"));
        serviceRow = Adw.ActionRow.New();
        serviceRow.SetTitle(T("Configuration"));
        serviceRow.AddCssClass("property");
        service.Add(serviceRow);
        page.Add(service);
        return page;
    }

    private Gtk.Widget BuildSensorPage()
    {
        var page = Adw.PreferencesPage.New();
        var group = Adw.PreferencesGroup.New();
        group.SetTitle(T("Sensors"));
        group.SetDescription(T("Which sensor delivers each value. Fan and pump depend on the mainboard header; see the mainboard manual."));
        var refresh = Gtk.Button.NewFromIconName("view-refresh-symbolic");
        refresh.SetTooltipText(T("Read the sensor list again"));
        refresh.AddCssClass("flat");
        refresh.SetValign(Gtk.Align.Center);
        refresh.OnClicked += (_, _) => Run(LoadSensorsAsync);
        group.SetHeaderSuffix(refresh);

        foreach (var field in DisplayFields.Configurable)
        {
            var row = Adw.ComboRow.New();
            row.SetTitle(field.Title);
            row.SetModel(Gtk.StringList.New([T("Automatic")]));
            var name = field.Name;
            row.SetListFactory(CreateChoiceFactory(name));
            row.OnNotify += (_, e) =>
            {
                if (e.Pspec.GetName() == "selected")
                    OnSensorSelected(name);
            };
            _sensorRows[field.Name] = row;
            group.Add(row);
        }

        page.Add(group);

        var clock = Adw.PreferencesGroup.New();
        clock.SetTitle(T("CPU clock"));
        _clockModeRow = Adw.ComboRow.New();
        _clockModeRow.SetTitle(T("Derived from"));
        _clockModeRow.SetModel(Gtk.StringList.New([.. CpuClockModes.All.Select(CpuClockModes.Title)]));
        _clockModeRow.OnNotify += (_, e) =>
        {
            if (e.Pspec.GetName() == "selected")
                OnClockModeSelected();
        };
        clock.Add(_clockModeRow);
        page.Add(clock);
        return page;
    }

    /// <summary>
    /// Popup entries of a sensor selection: the spec, below it value and source in small print, wrapped instead of
    /// cut off. The row itself shows only the spec (model strings).
    /// </summary>
    private Gtk.SignalListItemFactory CreateChoiceFactory(string field)
    {
        var factory = Gtk.SignalListItemFactory.New();
        factory.OnSetup += (_, e) =>
        {
            if (e.Object is not Gtk.ListItem item)
                return;
            var box = Gtk.Box.New(Gtk.Orientation.Vertical, 2);
            var title = Gtk.Label.New(null);
            title.SetXalign(0);
            var subtitle = Gtk.Label.New(null);
            subtitle.SetXalign(0);
            subtitle.SetWrap(true);
            subtitle.SetMaxWidthChars(56);
            subtitle.AddCssClass("caption");
            subtitle.AddCssClass("dim-label");
            box.Append(title);
            box.Append(subtitle);
            item.SetChild(box);
        };
        factory.OnBind += (_, e) =>
        {
            if (e.Object is not Gtk.ListItem item || item.GetChild() is not Gtk.Box box
                || box.GetFirstChild() is not Gtk.Label title || title.GetNextSibling() is not Gtk.Label subtitle
                || !_choices.TryGetValue(field, out var choices))
                return;
            var position = (int)item.GetPosition();
            if (position >= choices.Count)
                return;
            title.SetText(choices[position].Title);
            subtitle.SetText(choices[position].Subtitle);
        };
        return factory;
    }

    // --- connection -----------------------------------------------------------------------------

    private async Task ConnectAsync()
    {
        if (_connecting || _connection is not null)
            return;
        _connecting = true;
        StopTimer(ref _reconnectTimer);
        _stack.SetVisibleChildName("connecting");
        try
        {
            var connection = await SegLcdConnection.ConnectAsync(_bus);
            if (connection is null)
            {
                _stack.SetVisibleChildName("missing");
                ScheduleReconnect();
                return;
            }

            _connection = connection;
            _watch = await connection.WatchChangesAsync(() => Run(RefreshStateAsync));
            connection.Lost.Register(() => GLib.Functions.IdleAdd(GLib.Constants.PRIORITY_DEFAULT, () =>
            {
                OnLost();
                return false;
            }));
            await RefreshStateAsync();
            _stack.SetVisibleChildName("main");
            _valuesTimer = GLib.Functions.TimeoutAdd(GLib.Constants.PRIORITY_DEFAULT, ValuesIntervalMs, () =>
            {
                Run(RefreshValuesAsync);
                return true;
            });
            await RefreshValuesAsync();
            await LoadSensorsAsync();
        }
        finally
        {
            _connecting = false;
        }
    }

    private void OnLost()
    {
        Disconnect();
        _toasts.AddToast(Adw.Toast.New(T("Connection to the service lost")));
        Run(ConnectAsync);
    }

    private void ScheduleReconnect()
    {
        StopTimer(ref _reconnectTimer);
        _reconnectTimer = GLib.Functions.TimeoutAdd(GLib.Constants.PRIORITY_DEFAULT, ReconnectIntervalMs, () =>
        {
            _reconnectTimer = 0;
            Run(ConnectAsync);
            return false;
        });
    }

    private void Disconnect()
    {
        StopTimer(ref _valuesTimer);
        StopTimer(ref _reconnectTimer);
        _watch?.Dispose();
        _watch = null;
        _connection?.Dispose();
        _connection = null;
        _state = null;
    }

    private static void StopTimer(ref uint id)
    {
        if (id != 0)
            GLib.Functions.SourceRemove(id);
        id = 0;
    }

    // --- refresh --------------------------------------------------------------------------------

    private async Task RefreshStateAsync()
    {
        if (_connection is not { } connection)
            return;
        var state = await connection.GetStateAsync();
        if (_connection != connection)
            return;
        _state = state;
        _updating = true;
        try
        {
            _modelRow.SetSubtitle(state.Connected
                ? $"{state.Model} ({state.DevicePath})"
                : T("not connected – waiting for the display"));
            _enabledRow.SetActive(state.Enabled);
            _updateRow.SetValue(state.UpdateIntervalMs);
            _sampleRow.SetValue(state.SampleIntervalMs);
            _serviceRow.SetSubtitle((connection.Bus == ServiceBus.System ? T("System service") : T("User service")) + " · " + state.ConfigPath);
            UpdateSensorRows();
            var mode = CpuClockModes.All.ToList().IndexOf(state.CpuClockMode);
            _clockModeRow.SetSelected((uint)Math.Max(mode, 0));
        }
        finally
        {
            _updating = false;
        }
    }

    private async Task RefreshValuesAsync()
    {
        if (_connection is not { } connection)
            return;
        var values = await connection.GetCurrentValuesAsync();
        foreach (var field in DisplayFields.All)
            _valueLabels[field.Name].SetText(field.Format(values.TryGetValue(field.Name, out var v) ? v : null));
    }

    private async Task LoadSensorsAsync()
    {
        if (_connection is not { } connection)
            return;
        _sensors = await connection.ListSensorsAsync();
        _updating = true;
        try
        {
            UpdateSensorRows();
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>Rebuilds the selection lists from the sensor list and the configured specs.</summary>
    private void UpdateSensorRows()
    {
        if (_state is not { } state)
            return;
        foreach (var field in DisplayFields.Configurable)
        {
            var spec = state.SensorSpecs.GetValueOrDefault(field.Name, string.Empty);
            var choices = SensorChoices.For(field, _sensors, spec);
            var row = _sensorRows[field.Name];
            if (!_choices.TryGetValue(field.Name, out var old) || !old.SequenceEqual(choices))
            {
                _choices[field.Name] = choices;
                row.SetModel(Gtk.StringList.New([.. choices.Select(c => c.Title)]));
            }

            row.SetSelected((uint)SensorChoices.IndexOf(choices, spec));
            var source = state.Sources.GetValueOrDefault(field.Name, string.Empty);
            row.SetSubtitle(source.Length > 0 ? source : T("no sensor found"));
        }
    }

    // --- changes --------------------------------------------------------------------------------

    private void OnEnabledChanged(GObject.Object sender, GObject.Object.NotifySignalArgs e)
    {
        if (_updating || e.Pspec.GetName() != "active" || _connection is not { } connection)
            return;
        var value = _enabledRow.GetActive();
        Run(() => ApplyAsync(connection, () => connection.SetEnabledAsync(value)));
    }

    private void OnUpdateIntervalChanged(GObject.Object sender, GObject.Object.NotifySignalArgs e)
    {
        if (_updating || e.Pspec.GetName() != "value" || _connection is not { } connection)
            return;
        var value = (uint)_updateRow.GetValue();
        Run(() => ApplyAsync(connection, () => connection.SetUpdateIntervalMsAsync(value)));
    }

    private void OnSampleIntervalChanged(GObject.Object sender, GObject.Object.NotifySignalArgs e)
    {
        if (_updating || e.Pspec.GetName() != "value" || _connection is not { } connection)
            return;
        var value = (uint)_sampleRow.GetValue();
        Run(() => ApplyAsync(connection, () => connection.SetSampleIntervalMsAsync(value)));
    }

    private void OnSensorSelected(string field)
    {
        if (_updating || _connection is not { } connection || !_choices.TryGetValue(field, out var choices))
            return;
        var index = (int)_sensorRows[field].GetSelected();
        if (index < 0 || index >= choices.Count)
            return;
        var spec = choices[index].Spec;
        Run(() => ApplyAsync(connection, () => connection.SetSensorAsync(field, spec)));
    }

    private void OnClockModeSelected()
    {
        var index = (int)_clockModeRow.GetSelected();
        if (_updating || _connection is not { } connection || index < 0 || index >= CpuClockModes.All.Count)
            return;
        var mode = CpuClockModes.All[index];
        Run(() => ApplyAsync(connection, () => connection.SetCpuClockModeAsync(mode)));
    }

    /// <summary>Applies a change and saves it right away, so the settings survive a restart.</summary>
    private static async Task ApplyAsync(SegLcdConnection connection, Func<Task> change)
    {
        await change();
        await connection.SaveConfigAsync();
    }

    private async Task ReloadAsync()
    {
        if (_connection is not { } connection)
            return;
        await connection.ReloadConfigAsync();
        await RefreshStateAsync();
        _toasts.AddToast(Adw.Toast.New(T("Configuration reloaded")));
    }

    // --- helpers --------------------------------------------------------------------------------

    /// <summary>Runs <paramref name="action"/> and shows failures as a toast; the view is then refreshed from the service.</summary>
    private async void Run(Func<Task> action)
    {
        try
        {
            await action();
        }
#pragma warning disable CA1031 // Errors must not escape into the GTK main loop; they are shown to the user.
        catch (Exception e)
#pragma warning restore CA1031
        {
            var message = SegLcdConnection.IsAccessDenied(e)
                ? T("Not allowed: the change was not authorized.")
                : SegLcdConnection.ErrorText(e);
            var toast = Adw.Toast.New(message);
            toast.SetTimeout(6);
            _toasts.AddToast(toast);
            if (_connection is not null)
            {
                try
                {
                    await RefreshStateAsync();
                }
#pragma warning disable CA1031
                catch (Exception)
#pragma warning restore CA1031
                {
                    // The connection loss is handled by OnLost.
                }
            }
        }
    }

    private void ShowAbout()
    {
        var about = Adw.AboutDialog.New();
        about.SetApplicationName("SegLcd");
        about.SetApplicationIcon(ApplicationId);
        about.SetVersion(typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? string.Empty);
        about.SetComments(T("Sensor values on the segment LCD of the Thermaltake LB360-S. Not affiliated with Thermaltake."));
        about.SetWebsite("https://fubar-coder.github.io/SegLcd/");
        about.SetIssueUrl("https://github.com/fubar-coder/SegLcd/issues");
        about.SetLicenseType(Gtk.License.Gpl30);
        about.Present(_window);
    }
}
