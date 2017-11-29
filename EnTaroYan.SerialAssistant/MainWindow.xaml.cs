using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.ComponentModel;
using System.IO.Ports;
using System.Windows.Interop;

namespace EnTaroYan.SerialAssistant
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort ComPort = new SerialPort();   //声明一个串口
        string[] Ports;  //可用串口数组
        ObservableCollection<AvailableCom> ComList = new ObservableCollection<AvailableCom>();   //可用串口集合
        bool PortIsOpen = false; //串口目前状态(如果用SerialPort中的IsOpen方法在,热插拔时会有点逻辑问题)

        byte[] RecBuffer;  //接受缓冲
        string RecData;  //接收到的数据
        byte[] SendBuffer; //发送缓冲

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_loaded(object sender, RoutedEventArgs e) //主窗体加载事件
        {
            //使用事件接受
            ComPort.DataReceived += new SerialDataReceivedEventHandler(ReceiveData);

            //combobox_com数据绑定
            comboBox_com.ItemsSource = ComList;
            comboBox_com.DisplayMemberPath = "Com";

            //更新可用串口
            ComboBoxComReset();

            //用于监听Windows消息
            HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;//窗口过程
            if (hwndSource != null)
                hwndSource.AddHook(new HwndSourceHook(DeveiceChanged));  //挂钩
        }

        //接受事件处理函数
        private void ReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            RecBuffer = new byte[ComPort.BytesToRead];
            ComPort.Read(RecBuffer, 0, RecBuffer.Length);
            RecData = Encoding.Default.GetString(RecBuffer);
            textBox_rx.Dispatcher.Invoke(new Action(delegate{ textBox_rx.Text += RecData; }));
        }

        //打开&关闭串口按钮Click事件
        private void ButtonOpencomClick(object sender, RoutedEventArgs e)
        {
            if (ComPort.IsOpen == false) //串口未打开
                OpenSerialPort();
            else
                CloseSerialPort();
        }

        //发送按钮Click事件
        private void ButtonTxClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SendBuffer = Encoding.Default.GetBytes(textBox_tx.Text);
                ComPort.Write(SendBuffer,0, SendBuffer.Length);
            }
            catch
            {
                MessageBox.Show("发送失败!");
            }
        }

        //清除发送按钮Click事件
        private void ButtonClearTxClick(object sender, RoutedEventArgs e)
        {
            textBox_tx.Clear();
        }

        //清除接受按钮Click事件
        private void ButtonClearRxClick(object sender, RoutedEventArgs e)
        {
            textBox_rx.Clear();
        }

        //更新可用串口
        private void ComboBoxComReset()
        {
            Ports = SerialPort.GetPortNames(); //获取可用串口
            ComList.Clear();
            foreach (var str in Ports)
                ComList.Add(new AvailableCom(str));
        }

        //打开串口
        private void OpenSerialPort()
        {
            if (comboBox_com.Text == "") //combobox_com控件未选择串口号
            {
                MessageBox.Show("请选择串口号!");
                return;
            }
            try //尝试打开串口  
            {
                ComPort.PortName = comboBox_com.Text; //设置要打开的串口  
                ComPort.BaudRate = Convert.ToInt32(comboBox_baudrate.Text);//设置当前波特率  
                string StrParity = comboBox_check.Text;
                if (StrParity == "无")//设置校验位
                    ComPort.Parity = Parity.None;
                else if (StrParity == "奇校验")
                    ComPort.Parity = Parity.Odd;
                else
                    ComPort.Parity = Parity.Even;
                ComPort.DataBits = Convert.ToInt32(comboBox_data.Text);//设置当前数据位  
                ComPort.StopBits = (StopBits)Convert.ToDouble(comboBox_stop.Text);//设置当前停止位 

                ComPort.Open();//打开串口  
            }
            catch //如果串口被其他占用，则无法打开  
            {
                MessageBox.Show("无法打开串口,请检测此串口是否有效或被其他占用！");
                ComboBoxComReset();
                return;//无法打开串口，提示后直接返回  
            }
            PortIsOpen = true;
            ControlChange(true);
        }

        //关闭串口
        private void CloseSerialPort()
        {
            try//尝试关闭串口  
            {
                ComPort.DiscardOutBuffer();//清发送缓存  
                ComPort.DiscardInBuffer();//清接收缓存  
                ComPort.Close();//关闭串口  
            }
            catch//如果在未关闭串口前，串口就已丢失，这时关闭串口会出现异常  
            {
                MessageBox.Show("无法关闭串口，原因未知！");
                return;//无法关闭串口，提示后直接返回
            }
            PortIsOpen = false;
            ControlChange(false);
        }

        //控件使能/失能
        private void ControlChange(bool PortOPen)
        {
            button_opencom.Content = "关闭串口";
            button_tx.IsEnabled = PortOPen;//发送按钮
            comboBox_com.IsEnabled = !PortOPen;//串口控件
            comboBox_baudrate.IsEnabled = !PortOPen;//波特率控件
            comboBox_check.IsEnabled = !PortOPen;//校验位控件
            comboBox_data.IsEnabled = !PortOPen;//数据位控件
            comboBox_stop.IsEnabled = !PortOPen;//停止位控件
        }

        public const int WM_DEVICECHANGE = 0x219;          //Windows消息编号
        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        //钩子函数
        private IntPtr DeveiceChanged(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                ComboBoxComReset();
                switch (wParam.ToInt32())
                {
                    case DBT_DEVICEARRIVAL://设备插入  
                        break;

                    case DBT_DEVICEREMOVECOMPLETE: //设备卸载
                        if(PortIsOpen)
                        {
                            PortIsOpen = false;
                            ControlChange(true);
                        }
                        break;

                    default:
                        break;
                }
            }
            return IntPtr.Zero;
        }
    }

    public class AvailableCom
    {
        public AvailableCom(string str)
        {
            this.Com = str;
        }

        public string Com { get; private set; }
    }
}
