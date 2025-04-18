﻿using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.Web.Share;

namespace LiveSplit.View;

public partial class ShareRunDialog : Form
{
    public IRun Run { get; set; }
    public LiveSplitState State { get; set; }
    public ISettings Settings { get; set; }
    public Func<Image> ScreenShotFunction { get; set; }

    protected IRunUploadPlatform CurrentPlatform { get; set; }

    public ShareRunDialog(LiveSplitState state, ISettings settings, Func<Image> screenShotFunction)
    {
        State = state;
        if (State.CurrentPhase != TimerPhase.Ended)
        {
            Run = state.Run;
        }
        else
        {
            var model = new TimerModel
            {
                CurrentState = State
            };
            model.ResetAndSetAttemptAsPB();
            Run = State.Run;
        }

        ScreenShotFunction = screenShotFunction;
        Settings = settings;
        InitializeComponent();
    }

    private bool HasPersonalBest(IRun run)
    {
        return run.LastOrDefault().PersonalBestSplitTime.RealTime.HasValue;
    }

    private void SubmitDialog_Load(object sender, EventArgs e)
    {
        cbxPlatform.Items.Add("X (Twitter)");

        if (State.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended)
        {
            if (HasPersonalBest(Run))
            {
                cbxPlatform.Items.Add("Speedrun.com");
            }
        }

        cbxPlatform.Items.Add("Twitch");
        cbxPlatform.Items.Add("Screenshot");
        cbxPlatform.Items.Add("Imgur");
        cbxPlatform.Items.Add("Excel");

        cbxPlatform.SelectedIndex = 0;
        cbxPlatform_SelectionChangeCommitted(null, null);
    }

    private void RefreshDescription()
    {
        lblDescription.Text = CurrentPlatform.Description;
    }

    private void cbxPlatform_SelectionChangeCommitted(object sender, EventArgs e)
    {
        switch (cbxPlatform.SelectedItem.ToString())
        {
            case "X (Twitter)": CurrentPlatform = Twitter.Instance; break;
            case "Twitch": CurrentPlatform = Twitch.Instance; break;
            case "Screenshot": CurrentPlatform = Screenshot.Instance; break;
            case "Imgur": CurrentPlatform = Imgur.Instance; break;
            case "Excel": CurrentPlatform = Excel.Instance; break;
            case "Speedrun.com": CurrentPlatform = SpeedrunComRunUploadPlatform.Instance; break;
        }

        CurrentPlatform.Settings = Settings;

        txtNotes.Enabled = btnInsertCategory.Enabled = btnInsertDeltaTime.Enabled = btnInsertGame.Enabled
            = btnInsertPB.Enabled = btnInsertSplitName.Enabled = btnInsertSplitTime.Enabled
            = btnInsertStreamLink.Enabled = btnInsertTitle.Enabled = btnPreview.Enabled =
            CurrentPlatform == Twitter.Instance || CurrentPlatform == Twitch.Instance || CurrentPlatform == Imgur.Instance;

        if (State.CurrentPhase == TimerPhase.Ended || State.CurrentPhase == TimerPhase.NotRunning
            || State.CurrentSplitIndex == 0)
        {
            btnInsertDeltaTime.Enabled = btnInsertSplitName.Enabled = btnInsertSplitTime.Enabled = false;
        }

        if (Run.Last().PersonalBestSplitTime[State.CurrentTimingMethod] == null)
        {
            btnInsertPB.Enabled = false;
        }

        if (string.IsNullOrEmpty(Run.GameName))
        {
            btnInsertGame.Enabled = false;
        }

        if (string.IsNullOrEmpty(Run.CategoryName))
        {
            btnInsertCategory.Enabled = false;
        }

        if (string.IsNullOrEmpty(Run.GameName) && string.IsNullOrEmpty(Run.CategoryName))
        {
            btnInsertTitle.Enabled = false;
        }

        RefreshDescription();
        RefreshNotes();
    }

    private string FormatNotes(string notePlaceholder)
    {
        var timeFormatter = new RegularTimeFormatter(TimeAccuracy.Seconds);
        var deltaTimeFormatter = new DeltaTimeFormatter();

        string game = Run.GameName ?? "";
        string category = Run.GetExtendedCategoryName();
        string pb = timeFormatter.Format(Run.Last().PersonalBestSplitTime[State.CurrentTimingMethod]) ?? "";
        string title = Run.GetExtendedName();

        string splitName = "";
        string splitTime = "-";
        string deltaTime = "-";

        if ((State.CurrentPhase == TimerPhase.Running
            || State.CurrentPhase == TimerPhase.Paused)
            && State.CurrentSplitIndex > 0)
        {
            ISegment lastSplit = Run[State.CurrentSplitIndex - 1];

            splitName = lastSplit.Name ?? "";
            splitTime = timeFormatter.Format(lastSplit.SplitTime[State.CurrentTimingMethod]);
            deltaTime = deltaTimeFormatter.Format(lastSplit.SplitTime[State.CurrentTimingMethod] - lastSplit.PersonalBestSplitTime[State.CurrentTimingMethod]);
        }

        string streamLink = "";

        if (notePlaceholder.Contains("$stream"))
        {
            try
            {
                if (Twitch.Instance.IsLoggedIn || Twitch.Instance.VerifyLogin(true))
                {
                    string userName = Twitch.Instance.ChannelName;
                    streamLink = string.Format("http://twitch.tv/{0}", userName);
                }
            }
            catch { }
        }

        return notePlaceholder
            .Replace("$game", game)
            .Replace("$category", category)
            .Replace("$title", title)
            .Replace("$pb", pb)
            .Replace("$splitname", splitName)
            .Replace("$splittime", splitTime)
            .Replace("$delta", deltaTime)
            .Replace("$stream", streamLink);
    }

    private void RefreshNotes()
    {
        if (CurrentPlatform == Twitter.Instance)
        {
            ShareSettings.Default.Reload();
            if (State.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended)
            {
                txtNotes.Text = ShareSettings.Default.TwitterFormat;
                if (string.IsNullOrEmpty(txtNotes.Text))
                {
                    txtNotes.Text = "I got a $pb in $title.";
                }
            }
            else
            {
                txtNotes.Text = ShareSettings.Default.TwitterFormatRunning;
                if (string.IsNullOrEmpty(txtNotes.Text))
                {
                    txtNotes.Text = "I'm $delta in $title.";
                }
            }
        }
        else if (CurrentPlatform == Twitch.Instance)
        {
            ShareSettings.Default.Reload();
            txtNotes.Text = ShareSettings.Default.TwitchFormat;
            if (string.IsNullOrEmpty(txtNotes.Text))
            {
                txtNotes.Text = "$title Speedrun";
            }
        }
        else
        {
            txtNotes.Text = "";
        }
    }

    private void SaveNotesFormat()
    {
        if (CurrentPlatform == Twitter.Instance)
        {
            if (State.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended)
            {
                ShareSettings.Default.TwitterFormat = txtNotes.Text;
            }
            else
            {
                ShareSettings.Default.TwitterFormatRunning = txtNotes.Text;
            }

            ShareSettings.Default.Save();
        }
        else if (CurrentPlatform == Twitch.Instance)
        {
            ShareSettings.Default.TwitchFormat = txtNotes.Text;
            ShareSettings.Default.Save();
        }
    }

    private void btnSubmit_Click(object sender, EventArgs e)
    {
        string notes = FormatNotes(txtNotes.Text);

        SaveNotesFormat();

        try
        {
            Cursor = Cursors.WaitCursor;

            if (!CurrentPlatform.VerifyLogin())
            {
                MessageBox.Show("Your login information seems to be incorrect.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool runSubmitted = CurrentPlatform.SubmitRun(
                Run,
                ScreenShotFunction,
                State.CurrentTimingMethod,
                notes);

            if (runSubmitted)
            {
                MessageBox.Show(string.Format("Your run was successfully shared to {0}.", CurrentPlatform.PlatformName), "Run Shared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        MessageBox.Show("The run could not be shared.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void btnPreview_Click(object sender, EventArgs e)
    {
        MessageBox.Show(FormatNotes(txtNotes.Text), "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Insert(string insertText)
    {
        int selectionIndex = txtNotes.SelectionStart;
        txtNotes.Text = txtNotes.Text.Insert(selectionIndex, insertText);
        txtNotes.SelectionStart = selectionIndex + insertText.Length;
        txtNotes.Focus();
    }

    private void btnInsertGame_Click(object sender, EventArgs e)
    {
        Insert("$game");
    }

    private void btnInsertCategory_Click(object sender, EventArgs e)
    {
        Insert("$category");
    }

    private void btnInsertTitle_Click(object sender, EventArgs e)
    {
        Insert("$title");
    }

    private void btnInsertPB_Click(object sender, EventArgs e)
    {
        Insert("$pb");
    }

    private void btnInsertSplitName_Click(object sender, EventArgs e)
    {
        Insert("$splitname");
    }

    private void btnInsertDeltaTime_Click(object sender, EventArgs e)
    {
        Insert("$delta");
    }

    private void btnInsertSplitTime_Click(object sender, EventArgs e)
    {
        Insert("$splittime");
    }

    private void btnInsertStreamLink_Click(object sender, EventArgs e)
    {
        Insert("$stream");
    }
}
