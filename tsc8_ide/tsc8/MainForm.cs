using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace tsc8
{
    public partial class MainForm : Form
    {
        public enum Keys
        {
            Left = 37,
            Up = 38,
            Right = 39,
            Down = 40
        }


        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(Keys keyCode);

        Emu emu = new Emu();
        public MainForm()
        {
            InitializeComponent();
            Size = new Size(Size.Width + 1, Size.Height);
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }
        
        void ReportError(Assembler asm,int line, string error)
        {
           // MessageBox.Show(error);
            lstErrors.Items.Add(new ListViewItem(new string[] { line==-1?"-":line.ToString(), error }));
        }
        void CompileStart(Assembler asm)
        {
            lstErrors.Items.Clear();
        }
        void CompileEnd(Assembler asm)
        {
            
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Assembler asm = new Assembler();
            asm.OnCompileStart += CompileStart;
            asm.OnCompileEnd += CompileEnd;
            asm.OnReportError += ReportError;
            
            asm.Assemble(richTextBox1.Text);
            
            emu.Init(asm.BinaryCode);
            tbMif.Text = asm.MifFile;
        }

        private void listView1_Resize(object sender, EventArgs e)
        {
            columnMessage.Width = lstErrors.Width - columnLine.Width - 6;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstErrors.SelectedItems.Count > 0)
            {
                string line = lstErrors.SelectedItems[0].SubItems[0].Text;
                int v;
                if (int.TryParse(line, out v))
                {
                    string[] lines = richTextBox1.Text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                    int cc = 0, ec = 0;
                    if (lines.Length >= v)
                    {
                        for (int i = 0; i < v; i++)
                        {
                            ec += lines[i].Length + 1;
                            cc = lines[i].Length + 1;
                        }

                        richTextBox1.Select(ec - cc, cc - 1);
                    }
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            EmuTimer.Enabled = !EmuTimer.Enabled;
            btnEmuStart.Text = EmuTimer.Enabled ? "Stop" : "Start";
            this.Text = "Emulation " + (EmuTimer.Enabled ? "Start" : "Stop") + "ed!";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            emu.KEY_1 = 0 != (GetAsyncKeyState(Keys.Left) & 0x8000);
            emu.KEY_2 = 0 != (GetAsyncKeyState(Keys.Right) & 0x8000);
            if (!emu.ExecuteFrame())
            {
                EmuTimer.Enabled = false;
                this.Text = "Emulation Timeout..";
                btnEmuStart.Text = "Start";
            }
            pbEmuScreen.Refresh();
            label1.Text = emu.reg[10].ToString();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            
            float startx = 0;
            float starty = 0;
            float wpx = pbEmuScreen.Size.Width / 64;
            float hpy = pbEmuScreen.Size.Height / 32;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (emu.vram[y][x])
                        g.FillRectangle(Brushes.White, startx + x * wpx, starty + y * hpy, wpx, hpy);
                }
            }
        }
    }

}
