Here is an alternative to using `Clear`. 

One solution is to have a binding list that is the data source of your ComboBox. When you enumerate the available COM ports (shown below as a simulation) you will add or remove com ports from the binding source based on availability. This should result in the behavior you want, because if a com port is "still" available the selection won't change.

[![screenshot][1]][1]

    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            comboBoxCom.DataSource = MockDataSource;
            refreshAvailableComPorts(init: true);
            comboBoxCom.DropDown += onComboBoxComDropDown;
        }
        BindingList<string> MockDataSource = new BindingList<string>();

        private void onComboBoxComDropDown(object? sender, EventArgs e)
        {
            refreshAvailableComPorts(init: false);
        }

        /// <summary>
        /// This is a simulation that will choose a different
        /// set of "available" ports on each drop down.
        /// </summary>
        private void refreshAvailableComPorts(bool init)
        {
            if(init) 
            {
                MockDataSource.Clear();
                MockDataSource.Add("COM2");
                comboBoxCom.DropDownStyle= ComboBoxStyle.DropDownList;
                comboBoxCom.SelectedIndex = 0;
            }
            else
            {
                string 
                    selB4 = string.Join(",", MockDataSource),
                    selFtr;
                do
                {
                    var flags = _rando.Next(1, 0x10);
                    if ((flags & 0x1) == 0) MockDataSource.Remove("COM1");
                    else if(!MockDataSource.Contains("COM1")) MockDataSource.Add("COM1");
                    if ((flags & 0x2) == 0) MockDataSource.Remove("COM2");
                    else if (!MockDataSource.Contains("COM2")) MockDataSource.Add("COM2");
                    if ((flags & 0x4) == 0) MockDataSource.Remove("COM3");
                    else if (!MockDataSource.Contains("COM3")) MockDataSource.Add("COM3");
                    if ((flags & 0x8) == 0) MockDataSource.Remove("COM4");
                    else if (!MockDataSource.Contains("COM4")) MockDataSource.Add("COM4");
                    selFtr = string.Join(",", MockDataSource);
                    if (!selFtr.Equals(selB4))
                    {
                        break;
                    }
                } while (true); 
            }
        }
        private Random _rando = new Random(5);
    }

  [1]: https://i.stack.imgur.com/TiycF.png