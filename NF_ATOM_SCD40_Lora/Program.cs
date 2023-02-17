using System;
using System.Diagnostics;
using System.Threading;

using nanoFramework.Hardware.Esp32;
using System.Device.I2c;
using System.Buffers.Binary;
using System.IO.Ports;
using System.Device.Gpio;

namespace NF_ATOM_SCD40_Lora
{
    public class Program
    {
        static SerialPort serial;
        static GpioPin resetPin;
        private static SpanByte start => new byte[] { 0x21, 0xb1 };
        private static SpanByte read => new byte[] { 0xec, 0x05 };

        public static void Main()
        {
            //Lora
            Configuration.SetPinFunction(22, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(19, DeviceFunction.COM2_TX);

            serial = new SerialPort("COM2", 115200, Parity.None, 8, StopBits.One);
            serial.DataReceived += Serial_DataReceived;

            serial.Open();
            Thread.Sleep(500);

            LoraInit();

            //SCD40
            Configuration.SetPinFunction(26, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(32, DeviceFunction.I2C1_CLOCK);

            I2cConnectionSettings settings = new I2cConnectionSettings(1, 0x62);

            using I2cDevice device=I2cDevice.Create(settings);

            device.Write(start);

            Thread.Sleep(1000);

            SpanByte buffer = new byte[9];

            device.Write(read);

            Thread.Sleep(50);

            while (true)
            {
                device.Read(buffer);
                var co2 = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(0,3));
                var tmp = -45 + 175 * (float)(BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(3, 3))) / 65536;
                var hum = 100 * (float)(BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(6, 3))) / 65536;

                string data = $"{{\"co2\":{co2.ToString()},\"tmp\":{tmp.ToString("F2")},\"hum\":{hum.ToString("F2")}}}\r\n";

                serial.Write(data);
                Debug.Write(data);

                Thread.Sleep(60000);
            }
        }

        private static void LoraInit()
        {
            LoraReset();
            Debug.WriteLine("config");
            serial.Write("config\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("2");
            serial.Write("2\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("a 2");     //ノードタイプ（子機）
            serial.Write("a 2\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("c 12");
            serial.Write("c 12\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("d 3");
            serial.Write("d 3\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("e 2345");
            serial.Write("e 2345\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("f 0001");
            serial.Write("f 0001\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("g 0");
            serial.Write("g 0\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("z");
            serial.Write("z\r\n");
            Thread.Sleep(100);
        }

        static void LoraReset()
        {
            var gpioController = new GpioController();
            resetPin = gpioController.OpenPin(23, PinMode.Output);
            resetPin.Write(PinValue.Low);
            Thread.Sleep(200);
            resetPin.SetPinMode(PinMode.Input);
        }

        private static void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serial.BytesToRead == 0)
            {
                return;
            }

            var data = serial.ReadLine();
            Debug.WriteLine(data);
        }
    }
}
