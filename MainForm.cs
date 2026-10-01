using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace UnMetalTextTool
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox _logBox;
        private readonly Button _browseButton;
        private readonly Label _dropLabel;

        internal MainForm()
        {
            SuspendLayout();

            Text = "UnMetal Text Tool";
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            Panel rootPanel = new Panel();
            rootPanel.Dock = DockStyle.Fill;
            rootPanel.Padding = new Padding(12);

            FlowLayoutPanel topPanel = new FlowLayoutPanel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 42;
            topPanel.FlowDirection = FlowDirection.LeftToRight;
            topPanel.WrapContents = false;
            topPanel.AutoSize = false;
            topPanel.Margin = new Padding(0);
            topPanel.Padding = new Padding(0);

            _browseButton = new Button();
            _browseButton.Text = "Select Files...";
            _browseButton.Size = new Size(130, 30);
            _browseButton.Margin = new Padding(0);
            _browseButton.Click += OnBrowseClick;
            topPanel.Controls.Add(_browseButton);

            _dropLabel = new Label();
            _dropLabel.Dock = DockStyle.Top;
            _dropLabel.Height = 145;
            _dropLabel.Text = "Drag and drop .bin or .txt files here\r\n\r\n.bin -> .txt\r\n.txt -> .bin";
            _dropLabel.TextAlign = ContentAlignment.MiddleCenter;
            _dropLabel.BorderStyle = BorderStyle.FixedSingle;
            _dropLabel.Font = new Font(Font.FontFamily, 14f, FontStyle.Bold);
            _dropLabel.BackColor = SystemColors.ControlLight;
            _dropLabel.Margin = new Padding(0, 8, 0, 8);

            _logBox = new TextBox();
            _logBox.Dock = DockStyle.Fill;
            _logBox.Multiline = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.ReadOnly = true;
            _logBox.Font = new Font("Consolas", 10f);

            rootPanel.Controls.Add(_logBox);
            rootPanel.Controls.Add(_dropLabel);
            rootPanel.Controls.Add(topPanel);
            Controls.Add(rootPanel);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            AppendLog("Application is ready.");
            AppendLog("TXT format supports [base] and [extra] sections for files with repeated IDs.");

            ResumeLayout(false);
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select .bin or .txt files";
                dialog.Filter = "Supported files (*.bin;*.txt)|*.bin;*.txt|BIN files (*.bin)|*.bin|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.Multiselect = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                foreach (string file in dialog.FileNames)
                {
                    ProcessFile(file);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = files.Any(IsSupportedFile) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
            {
                return;
            }

            foreach (string file in files)
            {
                ProcessFile(file);
            }
        }

        private void ProcessFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    AppendLog("[ERR] File does not exist: " + path);
                    return;
                }

                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".bin")
                {
                    string output = Path.ChangeExtension(path, ".txt");
                    DictioCodec.UnpackBinToTxt(path, output);
                    AppendLog("[OK ] " + Path.GetFileName(path) + " -> " + Path.GetFileName(output));
                }
                else if (ext == ".txt")
                {
                    string output = Path.ChangeExtension(path, ".bin");
                    DictioCodec.PackTxtToBin(path, output);
                    AppendLog("[OK ] " + Path.GetFileName(path) + " -> " + Path.GetFileName(output));
                }
                else
                {
                    AppendLog("[SKIP] Unsupported extension: " + Path.GetFileName(path));
                }
            }
            catch (Exception ex)
            {
                AppendLog("[ERR] " + Path.GetFileName(path) + ": " + ex.Message);
            }
        }

        private static bool IsSupportedFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".bin" || ext == ".txt";
        }

        private void AppendLog(string message)
        {
            _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }
    }
}
