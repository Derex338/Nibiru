using Robust.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Shared.IoC;
using Content.Shared.IdentityManagement;
using Content.Shared._Nibiru.Factions;
using Robust.Client.UserInterface;
using System.Numerics;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Nibiru.Faction.UI;

public sealed partial class MiniMemberCardControl : Control
{
    private readonly PanelContainer _background;
    private readonly Button _mainButton;
    private readonly SpriteView _spriteView;
    private readonly RichTextLabel _nameLabel;
    private readonly Label _rankLabel;
    private readonly Button _moveUpButton;
    private readonly Button _moveDownButton;
    private readonly Button _kickButton;
    private readonly Button _rankButton;
    private readonly List<FactionRole> _roles;

    public MiniMemberCardControl(FactionMemberData memberData,
                                EntityUid? playerEntity,
                                List<FactionRole> roles,
                                IEntityManager entityManager)
    {
        _roles = roles;
        var member = entityManager.GetEntity(memberData.Entity);

        // Main container
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal
        };

        // Background panel (colored strip on the left)
        _background = new PanelContainer
        {
            StyleClasses = { "PdaBackground" },
            VerticalExpand = false,
            HorizontalExpand = false,
            MaxWidth = 10,
            Margin = new Thickness(0, 0, -5, 0)
        };

        // Main button
        _mainButton = new Button
        {
            Disabled = true,
            HorizontalExpand = true,
            VerticalExpand = false,
            StyleClasses = { "ButtonSquare" },
            Margin = new Thickness(0)
        };

        // Main button content
        var buttonContent = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(5)
        };

        // Sprite
        _spriteView = new SpriteView
        {
            OverrideDirection = Direction.South,
            Scale = new Vector2(2, 2),
            SetSize = new Vector2(32, 32)
        };
        _spriteView.SetEntity(member);

        // Spacer
        var spacer = new Control
        {
            MinWidth = 5
        };

        // Container for name and rank
        var infoContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };

        // Name
        _nameLabel = new RichTextLabel
        {
            StyleClasses = { "LabelSubText" }
        };
        _nameLabel.SetMessage(memberData.Name);

        // Rank
        _rankLabel = new Label
        {
            StyleClasses = { "LabelSmall" },
            FontColorOverride = Color.Gray
        };

        _rankLabel.Text = string.IsNullOrEmpty(memberData.Rank)
            ? Loc.GetString("faction-rank-no-rank")
            : memberData.Rank;

        infoContainer.AddChild(_nameLabel);
        infoContainer.AddChild(_rankLabel);

        // Change rank button
        _rankButton = new Button
        {
            StyleClasses = { "ButtonSquare" },
            HorizontalAlignment = HAlignment.Right,
            Text = "🏷️",
            MinWidth = 32,
            ToolTip = Loc.GetString("faction-button-change-rank-tooltip")
        };

        // Kick button
        _kickButton = new Button
        {
            StyleClasses = { "Caution" },
            HorizontalAlignment = HAlignment.Right,
            Text = "✕",
            MinWidth = 32,
            ToolTip = Loc.GetString("faction-button-kick-tooltip")
        };


        // Move buttons
        var moveContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(5, 0, 0, 0)
        };

        _moveUpButton = new Button
        {
            StyleClasses = { "ButtonSquare" },
            Text = "^",
            MinSize = new Vector2(20, 16),
            ToolTip = Loc.GetString("faction-button-move-up-tooltip")
        };

        _moveDownButton = new Button
        {
            StyleClasses = { "ButtonSquare" },
            Text = "v",
            MinSize = new Vector2(20, 16),
            ToolTip = Loc.GetString("faction-button-move-down-tooltip")
        };

        moveContainer.AddChild(_moveUpButton);
        moveContainer.AddChild(_moveDownButton);

        // Добавляем кнопки только если это не сам игрок
        if (playerEntity != null && playerEntity != member)
        {
            _rankButton.OnPressed += _ => ChangeRank(memberData, playerEntity, entityManager);
            _kickButton.OnPressed += _ => KickMember(memberData, playerEntity, entityManager);
            _moveUpButton.OnPressed += _ => MoveMember(memberData, true, entityManager);
            _moveDownButton.OnPressed += _ => MoveMember(memberData, false, entityManager);
        }
        else
        {
            _rankButton.Visible = false;
            _kickButton.Visible = false;
            _moveUpButton.Visible = false;
            _moveDownButton.Visible = false;
        }

        buttonContent.AddChild(_spriteView);
        buttonContent.AddChild(spacer);
        buttonContent.AddChild(infoContainer);
        buttonContent.AddChild(_rankButton);
        buttonContent.AddChild(_kickButton);
        buttonContent.AddChild(moveContainer);

        // Add content to main button
        _mainButton.AddChild(buttonContent);

        // Collect everything together
        mainContainer.AddChild(_background);
        mainContainer.AddChild(_mainButton);

        // Add main container to Control
        AddChild(mainContainer);
    }

    private void ChangeRank(FactionMemberData memberData, EntityUid? playerEntity, IEntityManager entityManager)
    {
        if (playerEntity == null)
            return;

        // Check permissions to change rank
        if (!entityManager.TryGetComponent<FactionComponent>(playerEntity.Value, out var playerFaction))
            return;

        if (!playerFaction.IsCreator)
            return;

        // Open rank change dialog
        var prompt = new RankChangePrompt(memberData.Entity, memberData.Rank, _roles, entityManager);
        prompt.OpenCentered();
    }

    private void KickMember(FactionMemberData memberData, EntityUid? playerEntity, IEntityManager entityManager)
    {
        if (playerEntity == null)
            return;

        if (!entityManager.TryGetComponent<FactionComponent>(playerEntity, out var faction))
            return;

        // Check if player is faction creator
        if (!faction.IsCreator)
            return;

        // Send kick message
        entityManager.RaisePredictiveEvent(new FactionKickMemberMessage
        {
            Member = memberData.Entity
        });

        // Remove card from UI
        Parent?.RemoveChild(this);
    }

    private void MoveMember(FactionMemberData memberData, bool moveUp, IEntityManager entityManager)
    {
        entityManager.RaisePredictiveEvent(new FactionMoveMemberMessage
        {
            Member = memberData.Entity,
            MoveUp = moveUp
        });
    }

    // Public properties for accessing elements
    public PanelContainer Background => _background;
    public SpriteView SpriteView => _spriteView;

    // Method for changing background color
    public void SetBackgroundColor(Color color)
    {
        _background.ModulateSelfOverride = color;
    }

    // Method for updating rank
    public void UpdateRank(string rank)
    {
        _rankLabel.Text = string.IsNullOrEmpty(rank) ? Loc.GetString("faction-rank-no-rank") : rank;
    }

    // Method for external removal
    public void Remove()
    {
        Parent?.RemoveChild(this);
    }
}

/// <summary>
/// Dialog for changing faction member rank
/// </summary>
public sealed class RankChangePrompt : DefaultWindow
{
    private readonly NetEntity _member;
    private readonly IEntityManager _entityManager;
    private readonly OptionButton _rankInput;

    private List<FactionRole> _roles;

    public RankChangePrompt(NetEntity member, string currentRank, List<FactionRole> roles, IEntityManager entityManager)
    {
        _member = member;
        _roles = roles;
        _entityManager = entityManager;

        Title = Loc.GetString("faction-rank-change-title");
        MinSize = new Vector2(300, 120);

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10)
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString("faction-rank-change-prompt"),
            Margin = new Thickness(0, 0, 0, 5)
        });

        _rankInput = new OptionButton
        {
            HorizontalExpand = true
        };

        _rankInput.AddItem(Loc.GetString("faction-rank-no-rank"), -1);
        _rankInput.SelectId(-1);

        for (var i = 0; i < _roles.Count; i++)
        {
            _rankInput.AddItem(_roles[i].Name, i);
            if (_roles[i].Name == currentRank)
                _rankInput.SelectId(i);
        }

        _rankInput.OnItemSelected += args => _rankInput.SelectId(args.Id);

        container.AddChild(_rankInput);

        var buttonsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var confirmButton = new Button
        {
            Text = Loc.GetString("faction-button-confirm"),
            MinSize = new Vector2(100, 30)
        };
        confirmButton.OnPressed += _ => OnConfirm();

        var cancelButton = new Button
        {
            Text = Loc.GetString("faction-button-cancel"),
            MinSize = new Vector2(100, 30),
            Margin = new Thickness(5, 0, 0, 0)
        };
        cancelButton.OnPressed += _ => Close();

        buttonsContainer.AddChild(confirmButton);
        buttonsContainer.AddChild(cancelButton);

        container.AddChild(buttonsContainer);

        Contents.AddChild(container);
    }

    private void OnConfirm()
    {
        var newRank = _rankInput.SelectedId >= 0 ? _roles[_rankInput.SelectedId].Name : string.Empty;

        _entityManager.RaisePredictiveEvent(new FactionChangeMemberRankMessage
        {
            Member = _member,
            NewRank = newRank
        });

        Close();
    }
}

public sealed class RoleManagePrompt : DefaultWindow
{
    private readonly IEntityManager _entityManager;
    private readonly OptionButton _roleSelector;
    private readonly LineEdit _roleNameInput;
    private readonly CheckBox _canInviteCheck;
    private readonly CheckBox _canResearchCheck;
    private readonly CheckBox _canManageRolesCheck;
    private readonly CheckBox _canInheritCheck;
    private readonly Button _deleteButton;
    private readonly Button _confirmButton;

    private readonly List<FactionRole> _roles;

    public RoleManagePrompt(List<FactionRole> roles, IEntityManager entityManager)
    {
        _entityManager = entityManager;
        _roles = roles ?? new List<FactionRole>();

        Title = Loc.GetString("faction-role-manage-title");
        MinSize = new Vector2(350, 320);

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10)
        };

        // Role selector
        var selectContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };
        selectContainer.AddChild(new Label { Text = Loc.GetString("faction-role-select") + ": ", VerticalAlignment = VAlignment.Center });

        _roleSelector = new OptionButton { HorizontalExpand = true };
        _roleSelector.AddItem(Loc.GetString("faction-role-new"), -1);
        for (int i = 0; i < _roles.Count; i++)
        {
            _roleSelector.AddItem(_roles[i].Name, i);
        }
        _roleSelector.OnItemSelected += OnRoleSelected;
        selectContainer.AddChild(_roleSelector);
        container.AddChild(selectContainer);

        _roleNameInput = new LineEdit
        {
            PlaceHolder = Loc.GetString("faction-role-name-placeholder"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(_roleNameInput);

        _canInviteCheck = new CheckBox { Text = Loc.GetString("faction-role-can-invite") };
        _canResearchCheck = new CheckBox { Text = Loc.GetString("faction-role-can-research") };
        _canManageRolesCheck = new CheckBox { Text = Loc.GetString("faction-role-can-manage-roles") };
        _canInheritCheck = new CheckBox { Text = Loc.GetString("faction-role-can-inherit") };

        container.AddChild(_canInviteCheck);
        container.AddChild(_canResearchCheck);
        container.AddChild(_canManageRolesCheck);
        container.AddChild(_canInheritCheck);

        var buttonsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        _deleteButton = new Button
        {
            Text = Loc.GetString("faction-role-delete"),
            StyleClasses = { "Caution" },
            Visible = false,
            MinSize = new Vector2(80, 30),
            Margin = new Thickness(0, 0, 10, 0)
        };
        _deleteButton.OnPressed += _ => OnDelete();

        _confirmButton = new Button
        {
            Text = Loc.GetString("faction-role-create"),
            MinSize = new Vector2(100, 30)
        };
        _confirmButton.OnPressed += _ => OnConfirm();

        var cancelButton = new Button
        {
            Text = Loc.GetString("faction-button-cancel"),
            MinSize = new Vector2(80, 30),
            Margin = new Thickness(10, 0, 0, 0)
        };
        cancelButton.OnPressed += _ => Close();

        buttonsContainer.AddChild(_deleteButton);
        buttonsContainer.AddChild(_confirmButton);
        buttonsContainer.AddChild(cancelButton);

        container.AddChild(buttonsContainer);

        Contents.AddChild(container);

        _roleSelector.SelectId(-1);
    }

    private void OnRoleSelected(OptionButton.ItemSelectedEventArgs args)
    {
        _roleSelector.SelectId(args.Id);
        if (args.Id == -1)
        {
            _roleNameInput.Text = string.Empty;
            _roleNameInput.Editable = true;
            _canInviteCheck.Pressed = false;
            _canResearchCheck.Pressed = false;
            _canManageRolesCheck.Pressed = false;
            _canInheritCheck.Pressed = false;
            _confirmButton.Text = Loc.GetString("faction-role-create");
            _deleteButton.Visible = false;
        }
        else
        {
            var role = _roles[args.Id];
            _roleNameInput.Text = role.Name;
            _roleNameInput.Editable = true;
            _canInviteCheck.Pressed = role.CanInvite;
            _canResearchCheck.Pressed = role.CanResearch;
            _canManageRolesCheck.Pressed = role.CanManageRoles;
            _canInheritCheck.Pressed = role.CanInherit;
            _confirmButton.Text = Loc.GetString("faction-role-update");
            _deleteButton.Visible = true;
        }
    }

    private void OnConfirm()
    {
        var roleName = _roleNameInput.Text.Trim();
        if (string.IsNullOrEmpty(roleName))
            return;

        string? oldName = null;
        if (_roleSelector.SelectedId >= 0)
        {
            oldName = _roles[_roleSelector.SelectedId].Name;
        }

        _entityManager.RaisePredictiveEvent(new FactionCreateRoleMessage
        {
            Role = new FactionRole
            {
                Name = roleName,
                CanInvite = _canInviteCheck.Pressed,
                CanResearch = _canResearchCheck.Pressed,
                CanManageRoles = _canManageRolesCheck.Pressed,
                CanInherit = _canInheritCheck.Pressed
            },
            OldName = oldName
        });

        Close();
    }

    private void OnDelete()
    {
        if (_roleSelector.SelectedId == -1)
            return;

        var roleName = _roles[_roleSelector.SelectedId].Name;
        _entityManager.RaisePredictiveEvent(new FactionDeleteRoleMessage
        {
            RoleName = roleName
        });

        Close();
    }
}
