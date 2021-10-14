using System;
using GenericHRLib;

namespace GenericHRLibProgram
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hi!");
            var device = new GenericHRDevice();
            device.HeartRateUpdated += onHeartRate;
            device.Initialize();
            while (true) ;
        }

        private static void onHeartRate(HeartRateReading reading)
        {
            Console.WriteLine($"{DateTime.Now}\t{reading.BeatsPerMinute}");
        }
    }
}
