namespace ZenTimings
{
    partial class DebugDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DebugDialog));
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonDebug = new System.Windows.Forms.Button();
            this.buttonDebugSave = new System.Windows.Forms.Button();
            this.buttonDebugCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.textBoxDebugOutput = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.ColumnCount = 4;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this.buttonDebug, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.buttonDebugSave, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.buttonDebugCancel, 3, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 475);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.Padding = new System.Windows.Forms.Padding(5);
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(434, 39);
            this.tableLayoutPanel2.TabIndex = 1;
            // 
            // buttonDebug
            // 
            this.buttonDebug.Location = new System.Drawing.Point(8, 8);
            this.buttonDebug.Name = "buttonDebug";
            this.buttonDebug.Size = new System.Drawing.Size(75, 23);
            this.buttonDebug.TabIndex = 0;
            this.buttonDebug.Text = "Debug";
            this.buttonDebug.UseVisualStyleBackColor = true;
            this.buttonDebug.Click += new System.EventHandler(this.ButtonDebug_Click);
            // 
            // buttonDebugSave
            // 
            this.buttonDebugSave.Enabled = false;
            this.buttonDebugSave.Location = new System.Drawing.Point(89, 8);
            this.buttonDebugSave.Name = "buttonDebugSave";
            this.buttonDebugSave.Size = new System.Drawing.Size(75, 23);
            this.buttonDebugSave.TabIndex = 1;
            this.buttonDebugSave.Text = "Save";
            this.buttonDebugSave.UseVisualStyleBackColor = true;
            this.buttonDebugSave.Click += new System.EventHandler(this.ButtonDebugSave_Click);
            // 
            // buttonDebugCancel
            // 
            this.buttonDebugCancel.Location = new System.Drawing.Point(351, 8);
            this.buttonDebugCancel.Name = "buttonDebugCancel";
            this.buttonDebugCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonDebugCancel.TabIndex = 2;
            this.buttonDebugCancel.Text = "Cancel";
            this.buttonDebugCancel.UseVisualStyleBackColor = true;
            this.buttonDebugCancel.Click += new System.EventHandler(this.ButtonDebugCancel_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.textBoxDebugOutput, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(434, 475);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // textBoxDebugOutput
            // 
            this.textBoxDebugOutput.BackColor = System.Drawing.SystemColors.Window;
            this.textBoxDebugOutput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxDebugOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxDebugOutput.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxDebugOutput.Location = new System.Drawing.Point(3, 3);
            this.textBoxDebugOutput.Multiline = true;
            this.textBoxDebugOutput.Name = "textBoxDebugOutput";
            this.textBoxDebugOutput.ReadOnly = true;
            this.textBoxDebugOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxDebugOutput.Size = new System.Drawing.Size(428, 469);
            this.textBoxDebugOutput.TabIndex = 3;
            this.textBoxDebugOutput.Text = "Click on Debug button to generate a report.";
            // 
            // DebugDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(434, 514);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.tableLayoutPanel2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "DebugDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Debug Report";
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Button buttonDebug;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox textBoxDebugOutput;
        private System.Windows.Forms.Button buttonDebugSave;
        private System.Windows.Forms.Button buttonDebugCancel;
    }
}