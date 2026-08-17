using Robust.Client.Animations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Animations;
using System.Numerics;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client._Nibiru.ModularCraft.UI;

/// <summary>
/// Animated selector: ← [Name] →
/// Two labels swap places with a smooth slide animation.
/// </summary>
public sealed class SlidingSelector : BoxContainer
{
    private readonly Button          _btnLeft;
    private readonly Button          _btnRight;
    private readonly LayoutContainer _clipBox;
    private readonly Label           _labelA;
    private readonly Label           _labelB;

    private List<(string Id, string Name)> _items = new();
    private int   _index;

    // Animation
    private bool  _animating;
    private float _slideDir;       // +1 → right (pressed →), -1 ← left
    private float _slideT;
    private const float AnimDuration = 0.22f;
    private const float LabelWidth   = 200f;
    private bool  _useLabelA = true;

    /// <summary>Called only when the user presses an arrow. SetValueSilent/SetItems do not call it.</summary>
    public event Action<string?>? OnValueChanged;

    public string? CurrentId => _items.Count > 0 ? _items[_index].Id : null;

    public SlidingSelector()
    {
        Orientation      = LayoutOrientation.Horizontal;
        HorizontalExpand = true;

        _btnLeft = new Button { Text = "←", MinWidth = 32 };
        _btnLeft.OnPressed += _ => Step(-1);
        AddChild(_btnLeft);

        _clipBox = new LayoutContainer
        {
            MinWidth         = LabelWidth,
            HorizontalExpand = true,
            RectClipContent  = true,
        };
        AddChild(_clipBox);

        _labelA = MakeLabel();
        _clipBox.AddChild(_labelA);
        LayoutContainer.SetAnchorPreset(_labelA, LayoutContainer.LayoutPreset.Wide);

        _labelB = MakeLabel(visible: false);
        _clipBox.AddChild(_labelB);
        LayoutContainer.SetAnchorPreset(_labelB, LayoutContainer.LayoutPreset.Wide);

        _btnRight = new Button { Text = "→", MinWidth = 32 };
        _btnRight.OnPressed += _ => Step(+1);
        AddChild(_btnRight);
    }

    private static Label MakeLabel(bool visible = true) => new()
    {
        HorizontalAlignment = HAlignment.Center,
        VerticalAlignment   = VAlignment.Center,
        Visible             = visible,
    };

    // Public API

    /// <summary>
    /// Set the list of items. Does not call OnValueChanged.
    /// The animation is reset, the first element is displayed.
    /// </summary>
    public void SetItems(List<(string Id, string Name)> items)
    {
        _animating = false;
        _items     = items;
        _index     = 0;
        RefreshLabel();
        UpdateButtons();
    }

    /// <summary>
    /// Select by ID without animation and without calling OnValueChanged.
    /// </summary>
    public void SetValueSilent(string? id)
    {
        if (id == null) return;
        var idx = _items.FindIndex(x => x.Id == id);
        if (idx < 0) return;
        _animating = false;
        _index     = idx;
        RefreshLabel();
    }

    // Step

    private void Step(int dir)
    {
        if (_items.Count == 0 || _animating) return;

        var next = (_index + dir + _items.Count) % _items.Count;
        if (next == _index) return;

        // Update index BEFORE animation, so CurrentId is already actual
        _index = next;

        var nextLabel = _useLabelA ? _labelB : _labelA;
        nextLabel.Text    = _items[next].Name;
        nextLabel.Visible = true;

        float startOffset = dir > 0 ? LabelWidth : -LabelWidth;
        SetLabelOffset(nextLabel, startOffset);

        _slideDir  = dir;
        _slideT    = 0f;
        _animating = true;

        UpdateButtons();
        OnValueChanged?.Invoke(CurrentId);
    }

    // FrameUpdate

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_animating) return;

        _slideT = Math.Min(_slideT + args.DeltaSeconds / AnimDuration, 1f);
        float t = EaseOut(_slideT);

        var cur  = _useLabelA ? _labelA : _labelB;
        var next = _useLabelA ? _labelB : _labelA;

        SetLabelOffset(cur,  -_slideDir * LabelWidth * t);
        SetLabelOffset(next,  _slideDir * LabelWidth * (1f - t));

        if (_slideT >= 1f)
        {
            cur.Visible = false;
            SetLabelOffset(next, 0f);
            _useLabelA = !_useLabelA;
            _animating = false;
        }
    }

    private static float EaseOut(float t) => 1f - MathF.Pow(1f - t, 3f);

    private static void SetLabelOffset(Label label, float offset)
    {
        LayoutContainer.SetMarginLeft(label,  offset);
        LayoutContainer.SetMarginRight(label, offset);
    }

    private void RefreshLabel()
    {
        var active = _useLabelA ? _labelA : _labelB;
        var other  = _useLabelA ? _labelB : _labelA;

        active.Text    = _items.Count > 0 ? _items[_index].Name : "—";
        active.Visible = true;
        SetLabelOffset(active, 0f);

        other.Visible = false;
    }

    private void UpdateButtons()
    {
        _btnLeft.Disabled  = _items.Count <= 1;
        _btnRight.Disabled = _items.Count <= 1;
    }
}
