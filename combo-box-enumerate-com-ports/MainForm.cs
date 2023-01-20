using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Management;

namespace combo_box_modify_selections
{
    public partial class MainForm : Form
    {
        public MainForm() => InitializeComponent();
        const int WM_DEVICECHANGE = 0x0219;
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if(m.Msg == WM_DEVICECHANGE) 
            {
                onDeviceChange();
            }
        }
        BindingList<ComPort> ComPorts = new BindingList<ComPort>();

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            comboBoxCom.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCom.DropDownClosed += (sender, e) => ActiveControl = null;
            comboBoxCom.DataSource = ComPorts;
            ComPorts.ListChanged += onComPortChanged;
            enumerateComPorts();
        }
        bool _isDeviceChange = false;

        private void onComPortChanged(object? sender, ListChangedEventArgs e)
        {
            if(_isDeviceChange) BeginInvoke(() => richTextBox.Clear());
            ComPort comPort;
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                    comPort = ComPorts[e.NewIndex];
                    using (ManagementClass manager = new ManagementClass("Win32_PnPEntity"))
                    {
                        foreach (ManagementObject record in manager.GetInstances())
                        {
                            var pnpDeviceId = record.GetPropertyValue("PnpDeviceID")?.ToString();
                            if (pnpDeviceId != null)
                            {
                                var subkey = Path.Combine("System", "CurrentControlSet", "Enum", pnpDeviceId, "Device Parameters");
                                var regKey = Registry.LocalMachine.OpenSubKey(subkey);
                                if (regKey != null)
                                {
                                    var names = regKey.GetValueNames();
                                    if (names.Contains("PortName"))
                                    {
                                        var portName = regKey.GetValue("PortName");
                                        if (Equals(comPort.PortName, portName))
                                        {
                                            var subkeyParent = Path.Combine("System", "CurrentControlSet", "Enum", pnpDeviceId);
                                            var regKeyParent = Registry.LocalMachine.OpenSubKey(subkeyParent);
                                            comPort.FriendlyName = $"{regKeyParent?.GetValue("FriendlyName")}";
                                            comPort.PnpDeviceId = pnpDeviceId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                case ListChangedType.ItemDeleted:
                    comPort = _removedItem!;
                    break;
                default: return;
            }
            BeginInvoke(() =>
            {
                if (_isDeviceChange) 
                {
                    richTextBox.SelectionColor =
                         e.ListChangedType.Equals(ListChangedType.ItemAdded) ?
                         Color.Green : Color.Red;
                    richTextBox.AppendText($"{e.ListChangedType}{Environment.NewLine}");
                }
                else
                {
                    richTextBox.SelectionColor = Color.Blue;
                    richTextBox.AppendText($"Detected{Environment.NewLine}");
                }
                richTextBox.SelectionColor = Color.FromArgb(0x20, 0x20, 0x20);
                richTextBox.AppendText(comPort?.GetFullDescription());
            });
        }

        int _wdtCount = 0;
        private ComPort? _removedItem = null;

        private void onDeviceChange()
        {
            _isDeviceChange = true;
            int captureCount = ++_wdtCount;
            Task
                .Delay(TimeSpan.FromMilliseconds(500))
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    if(captureCount.Equals(_wdtCount))
                    {
                        // The events have settled out
                        enumerateComPorts();
                    }
                });
        }
        private void enumerateComPorts()
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                if (!ComPorts.Any(_ => Equals(_.PortName, port)))
                {
                    ComPorts.Add(new ComPort { PortName = port });
                }
            }
            foreach (var port in ComPorts.ToArray())
            {
                if (!ports.Contains(port.PortName))
                {
                    _removedItem = port;
                    ComPorts.Remove(port);
                }
            }
            BeginInvoke(()=> ActiveControl = null); // Remove focus rectangle
        }
    }
    class ComPort
    {
        public string? PortName { get; set; }
        public string? FriendlyName { get; set; }
        public string? PnpDeviceId { get; set; }
        public override string ToString() => PortName ?? "None";
        public string GetFullDescription()
        {
            var builder = new List<string>();
            builder.Add($"{PortName}");
            builder.Add($"{FriendlyName}");
            builder.Add($"{nameof(PnpDeviceId)}: ");
            builder.Add($"{PnpDeviceId}");
            return $"{string.Join(Environment.NewLine, builder)}{Environment.NewLine}{Environment.NewLine}";
        }
    }
}