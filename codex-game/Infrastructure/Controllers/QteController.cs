using CodexGame.Domain.QTE;
using Godot;
using CodexGame.Infrastructure.Scenes;
using System;

namespace CodexGame.Infrastructure.Controllers;

internal sealed class QteController
{
    private readonly QteUi _ui;
    private readonly float _defaultDuration;
    private readonly float _defaultCritWindow;
    private float _duration;
    private float _critWindow;
    private float _greatWindow;
    private float _goodWindow;

    private float _timer;
    private string? _attackerId;
    private string? _targetId;

    public QteController(QteUi ui, float defaultDuration = 1.5f, float defaultCritWindow = 0.1f)
    {
        _ui = ui;
        _defaultDuration = defaultDuration;
        _defaultCritWindow = defaultCritWindow;
        _duration = _defaultDuration;
        _critWindow = _defaultCritWindow;
        _greatWindow = _critWindow * 2.5f;
        _goodWindow = _critWindow * 4f;
        TargetTime = _duration * 0.5f;
    }

    public bool IsActive { get; private set; }
    public float TargetTime { get; private set; }

    public event Action<string, string, TimingBarInput>? Completed;

    public void Begin(string attackerId, string targetId, QTEProfile profile)
    {
        IsActive = true;
        _timer = 0f;
        _attackerId = attackerId;
        _targetId = targetId;
        ConfigureFromProfile(profile);
        _ui.Configure(_duration, TargetTime, _critWindow, _greatWindow, _goodWindow, "Timing! Press Space");
    }

    public void Update(double delta)
    {
        if (!IsActive) return;

        _timer += (float)delta;
        var progress = Mathf.Clamp(_timer / _duration, 0f, 1f);
        _ui.UpdateProgress(progress);

        if (Input.IsActionJustPressed("attack"))
        {
            Finish(_timer);
            return;
        }

        if (_timer >= _duration)
            Finish(_duration + 0.5f);
    }

    public void Cancel()
    {
        IsActive = false;
        _timer = 0f;
        _attackerId = null;
        _targetId = null;
        _ui.Hide();
    }

    private void Finish(float pressTime)
    {
        if (_attackerId is null || _targetId is null)
        {
            Cancel();
            return;
        }

        var input = new TimingBarInput(TargetTime, pressTime);
        Completed?.Invoke(_attackerId, _targetId, input);
        Cancel();
    }

    private void ConfigureFromProfile(QTEProfile profile)
    {
        var difficulty = profile.Difficulty <= 0 ? 1f : profile.Difficulty;
        var baseDuration = profile.DurationSeconds > 0 ? profile.DurationSeconds : _defaultDuration;
        var baseCrit = profile.CritWindow > 0 ? profile.CritWindow : _defaultCritWindow;

        _duration = baseDuration / difficulty;
        _critWindow = baseCrit / difficulty;
        _greatWindow = _critWindow * 2.5f;
        _goodWindow = _critWindow * 4f;
        TargetTime = _duration * 0.5f;
    }
}
