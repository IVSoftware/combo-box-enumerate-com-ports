Based on your helpful comment, it appears that your "underlying" reason for posting is wanting to keep the `ComboBox` up to date with COM ports that are currently connected. 

I don't make a habit of posting multiple answers on the same thread, but there "might" be a more optimal solution to the thing you were trying to do in the first place (this is known as an [X-Y Problem](https://meta.stackexchange.com/a/66378)). In my old job I used to do serial port and USB enumeration quite a bit. One trick I learned along the way is that WinOS is going to post a message `WM_DEVICECHANGED` whenever a physical event occurs, things like plugging in or disconnecting a USB serial port device.

In a WinForms app, it's straightforward to detect it by overriding `WndProc`:

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

***
**BindingList<ComPort>**

I would still advocate for assigning the `DataSource` property of the combo box to a `BindingList<ComPort>` where `ComPort` is a class we design to represent the connection info:

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

 ***
 **Enumeration**

There's a little extra work this way but the enumeration yields a descriptor with additional useful information:

[![screenshot][1]][1]

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

The `DropDown` event of the combo box is now no longer required. Problem solved!

  [1]: https://i.stack.imgur.com/lkoL7.png