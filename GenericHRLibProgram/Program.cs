using System;
using System.Threading;
using GenericHRLib;

namespace GenericHRLibProgram
{
    class Program
    {
        static void Main(string[] args)
            => new Program().Start();


        GenericHRDevice device;
        void Start()
        {
            device = new GenericHRDevice();
            device.HeartRateUpdated += OnHeartRate;
            device.HeartRateDisconnected += OnDisconnect;
            OnDisconnect();
            while (true) ;
        }

        void OnDisconnect()
        {
            Console.WriteLine("Connecting to heart rate monitor...");
            var reconnected = false;
            while (!reconnected)
            {
                try
                {
                    device.FindAndConnect();
                    reconnected = true;
                }
                catch (HRDeviceException)
                {
                    Console.WriteLine("Could not connect to heart rate monitor. Retrying in 5s...");
                    Thread.Sleep(5000);
                }
            }
            Console.WriteLine("Connected to heart rate monitor!");
        }

        void OnHeartRate(HeartRateReading reading)
        {
            Console.WriteLine($"{DateTime.Now}\t{reading.BeatsPerMinute}");
        }
    }
}
