using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ZenStates.Core;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.OHWM;

namespace ZenTimings.Linux
{
	public partial class MainWindow : Window
	{
		private static readonly IBrush Accent = Brush.Parse("#7CCAA2");

		private Cpu cpu;
		private MemoryConfig memConfig;
		private DispatcherTimer timer;
		private bool loading;
		private bool built;
		private readonly Dictionary<string, TextBlock> values = new Dictionary<string, TextBlock>();

		public MainWindow()
		{
			InitializeComponent();
			Find<MenuItem>("ExitItem").Click += (s, e) => Close();
			Opened += (s, e) => Start();
			Closed += (s, e) =>
			{
				timer?.Stop();
				cpu?.Dispose();
			};
		}

		private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

		private T Find<T>(string name) where T : Control => this.FindControl<T>(name);

		private void Start()
		{
			try
			{
				cpu = new Cpu();
				Find<TextBlock>("CpuText").Text = cpu.info.cpuName;

				var smbios = new SMBios();
				Find<TextBlock>("BoardText").Text = $"{smbios.Board?.ManufacturerName} {smbios.Board?.ProductName} | BIOS {smbios.Bios?.Version}".Trim();
				Find<TextBlock>("BiosText").Text = $"AGESA {smbios.Bios?.AgesaVersion}".Trim();

				memConfig = cpu.GetMemoryConfig();
				if (memConfig == null)
					throw new InvalidOperationException("Could not read the memory controller configuration.");

				var combo = Find<ComboBox>("ModuleCombo");
				var modules = memConfig.Modules ?? new List<MemoryModule>();
				combo.ItemsSource = modules.Select(m =>
					$"{m.Slot}: {m.Manufacturer} {m.PartNumber} ({m.Capacity}, {m.Rank})").ToList();
				combo.SelectionChanged += (s, e) => Refresh();
				if (modules.Count > 0)
					combo.SelectedIndex = 0;

				timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
				timer.Tick += (s, e) => Refresh();
				timer.Start();
				Refresh();
			}
			catch (Exception ex)
			{
				Find<TextBlock>("StatusText").Text = "Error: " + ex.Message + " (run as root; msr and i2c-dev modules are required)";
			}
		}

		private uint SelectedOffset()
		{
			int idx = Find<ComboBox>("ModuleCombo").SelectedIndex;
			var modules = memConfig.Modules;
			if (modules != null && idx >= 0 && idx < modules.Count)
				return modules[idx].DctOffset;

			if (memConfig.Channels != null)
				foreach (var c in memConfig.Channels)
					if (c.Enabled)
						return c.Offset;
			return 0;
		}

		private static string Volt(float v) => v > 0 ? v.ToString("F4") + "V" : null;

		private void Refresh()
		{
			if (loading || memConfig == null)
				return;

			var status = Find<TextBlock>("StatusText");
			loading = true;
			try
			{
				var t = memConfig.ReadTimings(SelectedOffset());
				if (t == null)
				{
					status.Text = "No timings available";
					return;
				}

				var pt = cpu.powerTable;
				if (pt != null)
				{
					pt.ConfiguredClockSpeed = t.Frequency;
					pt.MemRatio = t.Ratio;
					cpu.RefreshPowerTable();
				}

				var d5 = t as Ddr5Timings;
				var d4 = t as Ddr4Timings;

				var col1 = new List<(string, object)>
				{
					(memConfig.Type.ToString(), $"{t.ClockToDataRate:F0} MT/s"), ("BGS", t.BGS), ("GDM", t.GDM),
					("tCL", t.CL), ("tRCDWR", t.RCDWR), ("tRCDRD", t.RCDRD), ("tRP", t.RP), ("tRAS", t.RAS), ("tRC", t.RC),
					("tRRDS", t.RRDS), ("tRRDL", t.RRDL), ("tFAW", t.FAW), ("tWTRS", t.WTRS), ("tWTRL", t.WTRL), ("tWR", t.WR),
					("tRFC (ns)", t.RFCns.ToString("F0")), ("tRFC", t.RFC), ("tRFC2", t.RFC2),
					d5 != null ? ("tRFCsb", (object)d5.RFCsb) : ("tRFC4", d4 != null ? (object)d4.RFC4 : null),
					("tMOD", t.MOD), ("tMODPDA", t.MODPDA), ("tPHYWRD", t.PHYWRD), ("tPHYWRL", t.PHYWRL),
				};
				if (d5 != null)
					col1.Add(("tRDPRE", t.RDPRE));

				var col2 = new List<(string, object)>
				{
					("Capacity", memConfig.TotalCapacity), d5 != null ? ("PwrDwn", (object)t.PowerDown) : ("BGS Alt", t.BGSAlt), ("Cmd2T", t.Cmd2T),
					("tRDRDSCL", t.RDRDSCL), ("tWRWRSCL", t.WRWRSCL), ("tCWL", t.CWL), ("tRTP", t.RTP),
					("tRDWR", t.RDWR), ("tWRRD", t.WRRD), ("tRDRDSC", t.RDRDSC), ("tRDRDSD", t.RDRDSD), ("tRDRDDD", t.RDRDDD),
					("tWRWRSC", t.WRWRSC), ("tWRWRSD", t.WRWRSD), ("tWRWRDD", t.WRWRDD), ("tCKE", t.CKE),
					("tREFI", t.REFI), ("Refresh", t.RefreshMode), ("tSTAG", t.STAG), ("tMRD", t.MRD), ("tMRDPDA", t.MRDPDA), ("tPHYRDL", t.PHYRDL),
				};
				if (d5 != null)
					col2.Add(("tWRPRE", t.WRPRE));
				else
					col2.Add(("PowerDown", t.PowerDown));

				var col3 = new List<(string, object)>
				{
					("MCLK", pt != null && pt.MCLK > 0 ? pt.MCLK.ToString("F2") : $"{t.Frequency:F2}"),
					("FCLK", pt != null ? pt.FCLK.ToString("F2") : null),
					("UCLK", pt != null ? pt.UCLK.ToString("F2") : null),
					("VSOC", pt != null ? Volt(pt.VDDCR_SOC) : null),
					("CLDO VDDP", pt != null ? Volt(pt.CLDO_VDDP) : null),
					("VDDG CCD", pt != null ? Volt(pt.CLDO_VDDG_CCD) : null),
					("VDDG IOD", pt != null ? Volt(pt.CLDO_VDDG_IOD) : null),
				};
				if (d5 != null)
				{
					col3.Add(("WCK:CK", d5.WckCkRatio));
					col3.Add(("Burst", t.BurstLength));
				}

				Fill(0, col1);
				Fill(1, col2);
				Fill(2, col3);
				built = true;

				status.Text = (t.CL == 0 && t.RAS == 0 ? "All timings read as 0. " + Cpu.LinuxSmnDiagnostics() + " " : "Updated ") + DateTime.Now.ToLongTimeString();
			}
			catch (Exception ex)
			{
				status.Text = "Refresh failed: " + ex.Message;
			}
			finally
			{
				loading = false;
			}
		}

		private void Fill(int column, List<(string label, object value)> rows)
		{
			if (!built)
			{
				var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
				grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
				grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
				for (int i = 0; i < rows.Count; i++)
				{
					grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
					bool dim = rows[i].label == "tRFC";
					var label = new TextBlock { Text = rows[i].label, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 8, 2), Opacity = dim ? 0.4 : 1 };
					var value = new TextBlock { Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2), Opacity = dim ? 0.4 : 1 };
					Grid.SetRow(label, i);
					Grid.SetRow(value, i);
					Grid.SetColumn(value, 1);
					grid.Children.Add(label);
					grid.Children.Add(value);
					values[column + ":" + i] = value;
				}
				Grid.SetColumn(grid, column);
				Find<Grid>("TimingsGrid").Children.Add(grid);
			}

			for (int i = 0; i < rows.Count; i++)
				if (values.TryGetValue(column + ":" + i, out TextBlock tb))
					tb.Text = rows[i].value?.ToString() ?? "N/A";
		}
	}
}
