﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using DMT;
using DMT.Interfaces;
using DMT.Properties;

namespace DMTViewer
{
    public partial class frmMain : Form
    {

        bool clicked = false;
        CheckBoxState state;
        private Point DrawOffset = new Point(4, 2);

        public frmMain()
        {
            InitializeComponent();
            lstMods.HeaderStyle = ColumnHeaderStyle.Clickable;
            lstMods.CheckBoxes = true;
            lstMods.OwnerDraw = true;
            lstMods.ColumnClick += lstMods_ColumnClick;
            lstMods.DrawColumnHeader += lstMods_DrawColumnHeader;
            lstMods.DrawItem += lstMods_DrawItem;
            lstMods.DrawSubItem += lstMods_DrawSubItem;
            lstMods.ItemChecked += LstModsOnItemChecked;

            chkPlay.Checked = BuildSettings.Instance.AutoPlay;
            chkAutoClose.Checked = BuildSettings.Instance.AutoClose;

            LoadPlugins();
        }

        private void LoadPlugins()
        {

            var buttons = AppDomain.CurrentDomain.GetAssemblies().SelectMany(d => d.GetInterfaceImplementers<IViewerButton>());


            var top = 5;
            int x = 5;
            int spacer = 5;

            int maxHeight = 0;
            foreach (var b in buttons)
            {
                var btn = new Button();
                btn.Text = b.GetName();
                btn.Click += b.Action;
                btn.Location = new Point(x, top);
                b.AlterAppearance(btn);
                panButtons.Controls.Add(btn);
                x += btn.Width + spacer;
                maxHeight = btn.Height > maxHeight ? btn.Height : maxHeight;
            }

            if (maxHeight == 0)
                panButtons.Visible = false;
            else
                panButtons.Height = maxHeight + (2 * spacer);

        }

        private void LstModsOnItemChecked(object sender, ItemCheckedEventArgs e)
        {

            var mod = e.Item.Tag as ModInfo;

            if (mod != null)
                mod.Enabled = e.Item.Checked;

            if (lstMods.Enabled)
                BuildSettings.Save();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Icon = Resources.HAL9000;
            LoadModsUI();
        }

        private void LoadModsUI()
        {


            lstMods.Items.Clear();
            foreach (var m in BuildSettings.Instance.Mods.OrderBy(d => d.Name))
            {

                var i = lstMods.Items.Add(m.Name);
                i.Checked = m.Enabled;
                //i.SubItems.Add(m.Name);
                i.SubItems.Add(m.Author);
                i.Tag = m;
            }

        }


        private void lstMods_ColumnClick(object sender, ColumnClickEventArgs e)
        {

            lstMods.Enabled = false;
            if (!clicked)
            {
                clicked = true;
                state = CheckBoxState.CheckedPressed;

                foreach (ListViewItem item in lstMods.Items)
                {
                    item.Checked = true;
                }

                Invalidate();
            }
            else
            {
                clicked = false;
                state = CheckBoxState.UncheckedNormal;
                Invalidate();

                foreach (ListViewItem item in lstMods.Items)
                {
                    item.Checked = false;
                }
            }

            lstMods.Enabled = true;
            DMT.BuildSettings.Save();

        }

        private void lstMods_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            TextFormatFlags flags = TextFormatFlags.LeftAndRightPadding;
            e.DrawBackground();
            CheckBoxRenderer.DrawCheckBox(e.Graphics, DrawOffset, state);
            e.DrawText(flags);
        }

        private void lstMods_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void lstMods_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmSettings settings = new frmSettings();
            settings.ShowDialog();
            if (settings.ModsFolderChanged())
                RefreshMods();

        }

        private void btnBuild_Click(object sender, EventArgs e)
        {
            rtbOutput.Text = "";

            if (BuildSettings.IsLocalBuild)
                new RemoteBuilder().InternalBuild(this);
            else
                new RemoteBuilder().RemoteBuild(this);
        }

        public static ConsoleColor GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Info:
                    return ConsoleColor.Gray;
                case LogType.Event:
                    return ConsoleColor.Green;
                case LogType.Error:
                    return ConsoleColor.Red;
                case LogType.Warning:
                    return ConsoleColor.Yellow;
            }

            return ConsoleColor.White;
        }

        internal void TryFindError()
        {

            var lines = rtbOutput.Text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            foreach (var s in lines)
            {
                if (!s.Contains(".cs:line "))
                    continue;

                var start = s.IndexOf(")") + 5;
                var end = s.LastIndexOf(":");

                var path = s.Substring(start, end - start);

                var lastSpace = s.LastIndexOf(" ");
                var lineno = int.Parse(s.Substring(lastSpace, s.Length - lastSpace)) - 1;

                if (lineno < 0 || !File.Exists(path)) break;

                var fileLines = System.IO.File.ReadAllLines(path);

                OnLog("", LogType.Info);
                OnLog("Error occured in code", LogType.Info);
                OnLog(fileLines[lineno], LogType.Info);
                OnLog("", LogType.Info);
                OnLog("Open the file by clicking this link:", LogType.Info);
                OnLog("file://" + System.Net.WebUtility.UrlEncode(path), LogType.Info);
                break;
            }

        }

        internal void OnLog(string str, LogType logType)
        {

            if (str == null) return;

            Color selectionColor = Color.Black;
            if (logType == LogType.Error)
            {
                Program.ExitCode = -100;
                selectionColor = Color.Red;
            }
            else if (logType == LogType.Warning)
            {
                selectionColor = Color.DarkOrange;
            }
            else if (logType == LogType.Event)
            {
                selectionColor = Color.Green;
            }
            else if (logType == LogType.Info)
            {
                selectionColor = Color.Gray;
            }
            else if (logType == LogType.Popup)
            {
                if (BuildSettings.IsSilent)
                {
                    Application.Exit();
                    Program.ExitCode = -100;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(str.Substring(4));
                }

                return;
            }

            rtbOutput.Select(rtbOutput.TextLength, 0);
            rtbOutput.SelectionColor = selectionColor;
            rtbOutput.AppendText(str + "\n");
            rtbOutput.Select(rtbOutput.TextLength, 0);
            rtbOutput.ScrollToCaret();

            Application.DoEvents();


        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            lstMods.Height = this.ClientSize.Height - lstMods.Top - 10;
            //lstMods.Width = (int)(this.ClientSize.Width * 0.45);

            rtbOutput.Width = this.ClientSize.Width - lstMods.Width;
        }

        private void lstMods_MouseDown(object sender, MouseEventArgs e)
        {
            return;
            var control = this.lstMods.GetItemAt(e.X, e.Y);
            if (control != null)
                control.Selected = true;

            if (this.lstMods.SelectedItems.Count == 0) return;
            this.lstMods.DoDragDrop(this.lstMods.SelectedItems[0], DragDropEffects.Move);
        }

        private void lstMods_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void lstMods_DragDrop(object sender, DragEventArgs e)
        {

            //disabling for now until run order is decided
            return;

            Point point = lstMods.PointToClient(new Point(e.X, e.Y));
            var control = this.lstMods.GetItemAt(point.X, point.Y);

            if (control == null) return;

            List<ListViewItem> selected = new List<ListViewItem>();
            foreach (ListViewItem s in lstMods.SelectedItems)
                selected.Insert(0, s);

            foreach (ListViewItem s in selected)
                lstMods.Items.Remove(s);

            for (int x = 0; x < lstMods.Items.Count; x++)
            {
                var i = lstMods.Items[x];
                if (i.Text == control.Text)
                {
                    foreach (ListViewItem s in selected)
                        lstMods.Items.Insert(x, s);

                    break;
                }
            }

        }

        private void lstMods_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private void chkAutoClose_CheckedChanged(object sender, EventArgs e)
        {
            BuildSettings.Instance.AutoClose = chkAutoClose.Checked;
            BuildSettings.Save();
        }

        private void chkPlay_CheckedChanged(object sender, EventArgs e)
        {
            BuildSettings.Instance.AutoPlay = chkPlay.Checked;
            BuildSettings.Save();
        }

        private void btnModsFolder_Click(object sender, EventArgs e)
        {


            if (!Directory.Exists(BuildSettings.Instance.ModFolder))
            {
                return;
            }

            Process.Start("explorer.exe", BuildSettings.Instance.ModFolder.Replace('/', '\\'));
        }
        
        public void Play_Click(object sender, EventArgs e)
        {

            var data = PatchData.Create(BuildSettings.Instance);
            data.GameFolder = BuildSettings.Instance.GameFolders.FirstOrDefault();

            if (File.Exists(data.StartPath))
                Process.Start(data.StartPath);

        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshMods();
        }

        private void RefreshMods()
        {

            BuildSettings.Instance.Init();
            LoadModsUI();
        }

        private void rtbOutput_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            var link = e.LinkText;

            if (link.StartsWith("file://"))
            {
                link = System.Net.WebUtility.UrlDecode(e.LinkText.Substring(7, e.LinkText.Length - 7));
                Process.Start(link);
            }
        }

    }
}