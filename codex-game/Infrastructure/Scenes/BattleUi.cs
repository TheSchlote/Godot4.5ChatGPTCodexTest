using CodexGame.Domain.TurnSystem;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexGame.Infrastructure.Scenes;

internal sealed class BattleUi
{
    public Control Root { get; }
    public QteUi Qte { get; }
    public bool AbilityPanelVisible => _abilityRoot.Visible;
    public int TurnOrderSlotCount => _turnOrderLabels.Count;

    private readonly Label _phaseLabel;
    private readonly Label _actionsLabel;
    private readonly PanelContainer _abilityRoot;
    private readonly VBoxContainer _abilityList;
    private readonly Label _toastLabel;
    private readonly List<Label> _turnOrderLabels = new();
    private readonly Label _gameOverLabel;
    private readonly Button _restartButton;
    private readonly Button _endTurnButton;
    private Action? _cancelHandler;
    private Action? _cancelWrapper;
    private readonly Action _onEndTurn;

    public BattleUi(Node parent, Action onRestart, Action onEndTurn, Control? existingRoot = null)
    {
        Root = existingRoot ?? CreateRoot();
        _onEndTurn = onEndTurn;
        NormalizeRootLayout(Root);
        _phaseLabel = CreatePhaseLabel();
        _actionsLabel = CreateActionsLabel();
        _toastLabel = CreateToastLabel();
        _abilityRoot = CreateAbilityPanel(out _abilityList);
        _gameOverLabel = CreateGameOverLabel();
        _restartButton = CreateRestartButton(onRestart);
        _endTurnButton = CreateEndTurnButton(onEndTurn);
        var turnOrderPanel = CreateTurnOrderPanel();

        Qte = new QteUi(Root);

        Root.AddChild(_gameOverLabel);
        Root.AddChild(_restartButton);
        Root.AddChild(_endTurnButton);
        Root.AddChild(turnOrderPanel);
        Root.AddChild(_phaseLabel);
        Root.AddChild(_actionsLabel);
        Root.AddChild(_toastLabel);
        Root.AddChild(_abilityRoot);

        if (existingRoot is null)
            parent.AddChild(Root);
    }

    public void UpdatePhase(BattlePhase phase) => _phaseLabel.Text = $"Phase: {phase}";

    public void ShowGameOver()
    {
        Qte.Hide();
        _gameOverLabel.Visible = true;
        _restartButton.Visible = true;
    }

    public void UpdateTurnOrder(IReadOnlyList<TurnOrderEntry> predicted, string activeUnitId, Func<string, string>? nameResolver = null, Func<string, Color>? colorResolver = null)
    {
        for (int i = 0; i < _turnOrderLabels.Count; i++)
        {
            var labelIndex = _turnOrderLabels.Count - 1 - i; // active at bottom
            var label = _turnOrderLabels[labelIndex];
            if (i < predicted.Count)
            {
                var entry = predicted[i];
                var displayName = nameResolver is null ? entry.UnitId : nameResolver(entry.UnitId);
                if (i == 0)
                {
                    label.Text = $"Current: {displayName}";
                }
                else
                {
                    label.Text = $"{i + 1}. {displayName}";
                }
                if (colorResolver != null)
                    label.AddThemeColorOverride("font_color", colorResolver(entry.UnitId));
            }
            else
            {
                label.Text = "--";
            }
        }
    }

    public void UpdateActions(bool moveAvailable, bool actionAvailable)
    {
        var moveText = moveAvailable ? "Move: Ready" : "Move: Spent";
        var actText = actionAvailable ? "Action: Ready" : "Action: Spent";
        _actionsLabel.Text = $"{moveText} | {actText}";
    }

    public void ShowToast(string message, float durationSeconds = 1.5f)
    {
        _toastLabel.Text = message;
        _toastLabel.Visible = true;
        Root.GetTree().CreateTimer(durationSeconds).Timeout += () => _toastLabel.Visible = false;
    }

    public void ShowAbilityPanel(IEnumerable<AbilityOption> abilities, Action<string> onSelected, Action<string>? onHover = null, Action? onCancel = null)
    {
        ClearChildren(_abilityList);
        WireCancel(onCancel);
        Button? firstButton = null;
        foreach (var ability in abilities)
        {
            var button = new Button { Text = ability.Label, FocusMode = Control.FocusModeEnum.All };
            button.Pressed += () => onSelected(ability.Id);
            if (onHover != null)
            {
                button.MouseEntered += () => onHover(ability.Id);
                button.FocusEntered += () => onHover(ability.Id);
            }
            _abilityList.AddChild(button);
            firstButton ??= button;
        }

        _abilityRoot.Visible = true;
        firstButton?.GrabFocus();
    }

    private void WireCancel(Action? onCancel)
    {
        var cancelButton = _abilityRoot.FindChild("CancelButton", true, false) as Button;
        if (cancelButton == null)
            return;

        if (_cancelWrapper != null)
        {
            cancelButton.Pressed -= _cancelWrapper;
            _cancelWrapper = null;
        }

        _cancelHandler = onCancel;
        cancelButton.Visible = onCancel != null;
        cancelButton.Disabled = onCancel == null;
        cancelButton.ButtonPressed = false;
        if (onCancel != null)
        {
            _cancelWrapper = () =>
            {
                _cancelHandler?.Invoke();
            };
            cancelButton.Pressed += _cancelWrapper;
        }
    }

    public void HideAbilityPanel() => _abilityRoot.Visible = false;

    private static Control CreateRoot() => new()
    {
        Name = "BattleUI",
        AnchorLeft = 0,
        AnchorRight = 1,
        AnchorTop = 0,
        AnchorBottom = 1
    };

    private static void NormalizeRootLayout(Control root)
    {
        root.AnchorLeft = 0;
        root.AnchorTop = 0;
        root.AnchorRight = 1;
        root.AnchorBottom = 1;
        root.OffsetLeft = 0;
        root.OffsetTop = 0;
        root.OffsetRight = 0;
        root.OffsetBottom = 0;
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
    }

    private Label CreateGameOverLabel()
    {
        var label = new Label
        {
            Name = "GameOverLabel",
            Text = "Game Over",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.2f));
        label.AddThemeFontSizeOverride("font_size", 36);
        label.AnchorLeft = 0.5f;
        label.AnchorRight = 0.5f;
        label.AnchorTop = 0.5f;
        label.AnchorBottom = 0.5f;
        label.OffsetLeft = -150;
        label.OffsetRight = 150;
        label.OffsetTop = -40;
        label.OffsetBottom = 0;
        return label;
    }

    private Button CreateRestartButton(Action onRestart)
    {
        var button = new Button
        {
            Name = "RestartButton",
            Text = "Restart",
            Visible = false,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -60,
            OffsetRight = 60,
            OffsetTop = 20,
            OffsetBottom = 60
        };
        button.Pressed += onRestart;
        return button;
    }

    private Button CreateEndTurnButton(Action onEndTurn)
    {
        var button = new Button
        {
            Name = "EndTurnButton",
            Text = "End Turn (T / Start)",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 0,
            OffsetLeft = 10,
            OffsetTop = 50,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        button.Pressed += onEndTurn;
        return button;
    }

    private Label CreateToastLabel()
    {
        var label = new Label
        {
            Name = "ToastLabel",
            Text = string.Empty,
            Visible = false,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -150,
            OffsetRight = 150,
            OffsetTop = 60,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        label.AddThemeFontSizeOverride("font_size", 16);
        return label;
    }

    private PanelContainer CreateTurnOrderPanel()
    {
        const int slotCount = 6;
        var panel = new PanelContainer
        {
            Name = "TurnOrderPanel",
            AnchorRight = 1,
            AnchorLeft = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -220,
            OffsetRight = -20,
            OffsetTop = 20,
            OffsetBottom = 220
        };

        var vbox = new VBoxContainer
        {
            Name = "TurnOrderList",
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 10,
            OffsetRight = -10,
            OffsetTop = 10,
            OffsetBottom = -10
        };

        panel.AddChild(vbox);

        for (int i = 0; i < slotCount; i++)
        {
            var label = new Label
            {
                Text = "--",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _turnOrderLabels.Add(label);
            vbox.AddChild(label);
        }

        return panel;
    }

    private Label CreateActionsLabel()
    {
        var label = new Label
        {
            Text = "Move: Ready | Action: Ready",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 0,
            OffsetLeft = 10,
            OffsetTop = 28,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 16);
        return label;
    }

    private PanelContainer CreateAbilityPanel(out VBoxContainer abilityList)
    {
        var abilityRoot = new PanelContainer
        {
            Name = "AbilityPanel",
            Visible = false,
            AnchorLeft = 0.35f,
            AnchorRight = 0.65f,
            AnchorTop = 0.2f,
            AnchorBottom = 0.45f
        };

        var vbox = new VBoxContainer
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 10,
            OffsetRight = -10,
            OffsetTop = 10,
            OffsetBottom = -10
        };

        abilityList = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        vbox.AddChild(abilityList);

        var hintButton = new Button
        {
            Name = "CancelButton",
            Text = "Cancel (B / Escape / Back)",
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        // Wire up in ShowAbilityPanel to reuse the passed cancel handler.
        vbox.AddChild(hintButton);

        abilityRoot.AddChild(vbox);
        return abilityRoot;
    }

    private static Label CreatePhaseLabel() => new()
    {
        Name = "PhaseLabel",
        Text = "Phase: Idle",
        AnchorLeft = 0.4f,
        AnchorRight = 0.6f,
        AnchorTop = 0.02f,
        AnchorBottom = 0.1f,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private static void ClearChildren(Node container)
    {
        foreach (var child in container.GetChildren().OfType<Node>())
            child.QueueFree();
    }
}

internal sealed class QteUi
{
    public PanelContainer Root { get; }

    private Label _label = null!;
    private Control _trackContainer = null!;
    private ColorRect _track = null!;
    private ColorRect _goodZone = null!;
    private ColorRect _greatZone = null!;
    private ColorRect _critZone = null!;
    private ColorRect _indicator = null!;
    private Label _goodLabel = null!;
    private Label _greatLabel = null!;
    private Label _critLabel = null!;

    private float _duration = 1.5f;
    private float _targetTime = 0.75f;
    private float _critWindow = 0.1f;
    private float _greatWindow = 0.25f;
    private float _goodWindow = 0.4f;

    public QteUi(Control uiRoot)
    {
        Root = CreateQtePanel();
        uiRoot.AddChild(Root);
    }

    public void Configure(float duration, float targetTime, float critWindow, float greatWindow, float goodWindow, string labelText)
    {
        _duration = duration;
        _targetTime = targetTime;
        _critWindow = critWindow;
        _greatWindow = greatWindow;
        _goodWindow = goodWindow;
        _label.Text = labelText;
        Root.Visible = true;
        UpdateZoneLayout();
        UpdateProgress(0);
    }

    public void UpdateProgress(float progress)
    {
        UpdateIndicator(progress);
    }

    public void Hide()
    {
        Root.Visible = false;
        UpdateIndicator(0);
    }

    private PanelContainer CreateQtePanel()
    {
        var root = new PanelContainer
        {
            Name = "QTEPanel",
            Visible = false,
            AnchorLeft = 0.3f,
            AnchorRight = 0.7f,
            AnchorTop = 0.7f,
            AnchorBottom = 0.85f
        };

        var vbox = new VBoxContainer
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 12,
            OffsetRight = -12,
            OffsetTop = 12,
            OffsetBottom = -12
        };

        _label = new Label
        {
            Text = "Timing!",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        vbox.AddChild(_label);

        _trackContainer = new Control
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 0,
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(320, 24)
        };

        _track = new ColorRect { Color = new Color(0.75f, 0.65f, 0.35f) };
        _goodZone = new ColorRect { Color = new Color(0.95f, 0.8f, 0.25f, 0.9f) };
        _greatZone = new ColorRect { Color = new Color(0.95f, 0.45f, 0.35f, 0.9f) };
        _critZone = new ColorRect { Color = new Color(0.2f, 0.45f, 1f, 0.95f) };
        _indicator = new ColorRect { Color = new Color(0.08f, 0.08f, 0.08f), CustomMinimumSize = new Vector2(10, 28) };

        _trackContainer.AddChild(_track);
        _trackContainer.AddChild(_goodZone);
        _trackContainer.AddChild(_greatZone);
        _trackContainer.AddChild(_critZone);
        _trackContainer.AddChild(_indicator);

        _goodLabel = CreateZoneLabel(string.Empty);
        _greatLabel = CreateZoneLabel(string.Empty);
        _critLabel = CreateZoneLabel(string.Empty);
        _trackContainer.AddChild(_goodLabel);
        _trackContainer.AddChild(_greatLabel);
        _trackContainer.AddChild(_critLabel);

        _trackContainer.Resized += UpdateZoneLayout;
        UpdateZoneLayout();

        vbox.AddChild(_trackContainer);

        root.AddChild(vbox);
        return root;
    }

    private void UpdateIndicator(float progress)
    {
        if (!_indicator.IsInsideTree() || _trackContainer.Size.X <= 0) return;
        var width = _trackContainer.Size.X;
        var indicatorWidth = _indicator.Size.X > 0 ? _indicator.Size.X : 8;
        var x = Mathf.Clamp(progress * width - indicatorWidth * 0.5f, 0, Mathf.Max(0, width - indicatorWidth));
        _indicator.SetDeferred("position", new Vector2(x, 0));
    }

    private void UpdateZoneLayout()
    {
        if (!_trackContainer.IsInsideTree()) return;

        var width = Mathf.Max(_trackContainer.Size.X, _trackContainer.CustomMinimumSize.X);
        var height = Mathf.Max(_trackContainer.Size.Y, _trackContainer.CustomMinimumSize.Y);
        if (width <= 0 || height <= 0) return;

        var targetNorm = Mathf.IsZeroApprox(_duration) ? 0.5f : _targetTime / _duration;
        float critHalf = _critWindow / _duration;
        float greatHalf = _greatWindow / _duration;
        float goodHalf = _goodWindow / _duration;

        float critStart = Mathf.Clamp(targetNorm - critHalf, 0f, 1f) * width;
        float critEnd = Mathf.Clamp(targetNorm + critHalf, 0f, 1f) * width;
        float greatStart = Mathf.Clamp(targetNorm - greatHalf, 0f, 1f) * width;
        float greatEnd = Mathf.Clamp(targetNorm + greatHalf, 0f, 1f) * width;
        float goodStart = Mathf.Clamp(targetNorm - goodHalf, 0f, 1f) * width;
        float goodEnd = Mathf.Clamp(targetNorm + goodHalf, 0f, 1f) * width;

        SetDeferredRect(_goodZone, goodStart, goodEnd, height);
        SetDeferredRect(_greatZone, greatStart, greatEnd, height);
        SetDeferredRect(_critZone, critStart, critEnd, height);

        PositionZoneLabel(_goodLabel, goodStart, goodEnd, height);
        PositionZoneLabel(_greatLabel, greatStart, greatEnd, height);
        PositionZoneLabel(_critLabel, critStart, critEnd, height);
    }

    private static void SetDeferredRect(ColorRect rect, float start, float end, float height)
    {
        var size = Mathf.Max(0, end - start);
        rect.SetDeferred("position", new Vector2(start, 0));
        rect.SetDeferred("size", new Vector2(size, height));
    }

    private static void PositionZoneLabel(Label label, float start, float end, float height)
    {
        var zoneWidth = Mathf.Max(0, end - start);
        var labelWidth = label.GetMinimumSize().X;
        var center = start + zoneWidth * 0.5f;
        var posX = center - labelWidth * 0.5f;
        label.SetDeferred("position", new Vector2(posX, 0));
        label.SetDeferred("size", new Vector2(zoneWidth, height));
        label.SetDeferred("horizontal_alignment", (int)HorizontalAlignment.Center);
        label.SetDeferred("vertical_alignment", (int)VerticalAlignment.Center);
    }

    private static Label CreateZoneLabel(string text)
    {
        return new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(0, 0, 0, 0.85f)
        };
    }
}

internal readonly record struct AbilityOption(string Id, string Label);
