using SaveState.Core.Api;
using SaveState.UI.Controls;

namespace SaveState.UI;

/// <summary>Log in / create account. Closes with DialogResult.OK once the user has a session.</summary>
internal sealed class LoginForm : Form
{
    private const int MinPassword = 8;

    private readonly SupabaseApi _api;
    private readonly Label _title = Ui.Heading("");
    private readonly Label _subtitle = Ui.Muted("", maxWidth: 360);
    private readonly Label _banner = Ui.Banner();
    private readonly InputBox _email = new() { AccessibleName = "Email" };
    private readonly InputBox _password = new() { Password = true, AccessibleName = "Password" };
    private readonly Label _emailError = Ui.Text("", Theme.BodySmall, Theme.Danger);
    private readonly Label _passwordHint = Ui.Text("", Theme.BodySmall, Theme.InkMuted);
    private readonly RoundedButton _submit = new() { Height = 44 };
    private readonly LinkLabel _switchMode = new() { AutoSize = true, Font = Theme.Body };
    private bool _signup;
    private bool _busy;

    public LoginForm(SupabaseApi api, string? message = null)
    {
        _api = api;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "SaveState";
        Icon = AppIcon.Load();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        Font = Theme.Body;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 620);

        var wordmark = Ui.Text("SaveState", Theme.Wordmark);
        wordmark.Margin = new Padding(0, 0, 0, Theme.S5);

        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(Theme.S6) };
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Theme.Surface,
            AutoSize = false,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _subtitle.Margin = new Padding(0, Theme.S2, 0, Theme.S5);
        var emailLabel = Ui.FieldLabel("Email");
        emailLabel.Margin = new Padding(0, 0, 0, Theme.S2);
        var passwordLabel = Ui.FieldLabel("Password");
        passwordLabel.Margin = new Padding(0, Theme.S4, 0, Theme.S2);
        _emailError.Margin = new Padding(0, Theme.S1, 0, 0);
        _emailError.Visible = false;
        _passwordHint.Margin = new Padding(0, Theme.S1, 0, 0);
        _submit.Margin = new Padding(0, Theme.S5, 0, Theme.S4);
        _switchMode.Margin = new Padding(0);
        _switchMode.LinkColor = Theme.Accent;
        _switchMode.ActiveLinkColor = Theme.AccentHover;
        _switchMode.VisitedLinkColor = Theme.Accent;
        _switchMode.LinkBehavior = LinkBehavior.HoverUnderline;
        _switchMode.BackColor = Theme.Surface;
        _switchMode.LinkClicked += (_, _) => SetMode(!_signup);

        foreach (var c in new Control[] { _title, _subtitle, _banner, emailLabel, _email, _emailError, passwordLabel, _password, _passwordHint, _submit, _switchMode })
        {
            // Anchor (not Dock) so auto-size rows keep each control's own height.
            if (c is InputBox or RoundedButton) { c.Dock = DockStyle.None; c.Anchor = AnchorStyles.Left | AnchorStyles.Right; }
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(c);
        }
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        card.Controls.Add(form);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(Theme.S6, Theme.S5, Theme.S6, Theme.S6),
            BackColor = Theme.Bg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(wordmark);
        root.Controls.Add(card);
        Controls.Add(root);

        AcceptButton = _submit;
        _submit.Click += async (_, _) => await SubmitAsync();
        _email.TextChanged += (_, _) => { _email.Invalid = false; _emailError.Visible = false; };
        _password.TextChanged += (_, _) => _password.Invalid = false;

        SetMode(signup: false);
        ResumeLayout(true);

        if (message is not null) Ui.ShowBanner(_banner, message, Ui.Tone.Warning);
        Shown += (_, _) => _email.Focus();
    }

    private void SetMode(bool signup)
    {
        _signup = signup;
        _title.Text = signup ? "Create your account" : "Welcome back";
        _subtitle.Text = signup
            ? "Your backup is saved to this account. Use the same login on the website after reinstalling."
            : "Log in to back up your apps and files.";
        _submit.Text = signup ? "Create account" : "Log in";
        _switchMode.Text = signup ? "Already have an account? Log in" : "New to SaveState? Create an account";
        _switchMode.LinkArea = signup ? new LinkArea(25, 6) : new LinkArea(18, 17);
        _passwordHint.Text = signup ? $"At least {MinPassword} characters." : "";
        _passwordHint.Visible = signup;
        _passwordHint.ForeColor = Theme.InkMuted;
        Ui.HideBanner(_banner);
    }

    private async Task SubmitAsync()
    {
        if (_busy) return;
        Ui.HideBanner(_banner);

        var email = _email.Text.Trim();
        var password = _password.Text;
        var valid = true;
        if (!email.Contains('@') || !email.Contains('.') || email.Contains(' '))
        {
            _email.Invalid = true;
            _emailError.Text = "Enter a valid email address.";
            _emailError.Visible = true;
            valid = false;
        }
        if (password.Length == 0 || (_signup && password.Length < MinPassword))
        {
            _password.Invalid = true;
            _passwordHint.Text = password.Length == 0 ? "Enter your password." : $"Use at least {MinPassword} characters.";
            _passwordHint.ForeColor = Theme.Danger;
            _passwordHint.Visible = true;
            valid = false;
        }
        if (!valid) return;

        SetBusy(true);
        try
        {
            if (_signup)
            {
                var result = await _api.SignUpAsync(email, password);
                if (result.NeedsEmailConfirmation)
                {
                    SetMode(signup: false);
                    Ui.ShowBanner(_banner, "Check your inbox to confirm your email, then log in here.", Ui.Tone.Success);
                    return;
                }
            }
            else
            {
                await _api.SignInAsync(email, password);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException e)
        {
            Ui.ShowBanner(_banner, e.Message, Ui.Tone.Error);
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _submit.Enabled = !busy;
        _email.Enabled = !busy;
        _password.Enabled = !busy;
        _switchMode.Enabled = !busy;
        _submit.Text = busy ? (_signup ? "Creating account…" : "Logging in…") : (_signup ? "Create account" : "Log in");
        UseWaitCursor = busy;
    }
}
