using System;
using System.Linq;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.IO;
using System.Diagnostics;

namespace GenericHRLib
{
    public enum ContactSensorStatus
    {
        NotSupported,
        NotSupported2,
        NoContact,
        Contact
    }

    [Flags]
    public enum HeartRateFlags
    {
        None = 0,
        IsShort = 1,
        HasEnergyExpended = 1 << 3,
        HasRRInterval = 1 << 4,
    }

    public struct HeartRateReading
    {
        public HeartRateFlags Flags { get; set; }
        public ContactSensorStatus Status { get; set; }
        public int BeatsPerMinute { get; set; }
        public int? EnergyExpended { get; set; }
        public int[] RRIntervals { get; set; }
    }

    public class HRDeviceException : Exception
    {
        public HRDeviceException(string message) : base(message) { }
    }

    public class GenericHRDevice : IDisposable
    {
        public event HeartRateUpdateEventHandler HeartRateUpdated;
        public delegate void HeartRateUpdateEventHandler(HeartRateReading reading);

        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml
        private const int _hrCharacteristicId = 0x2A37;
        private static readonly Guid _hrCharacteristicUuid =
            BluetoothUuidHelper.FromShortId(_hrCharacteristicId);

        private GattDeviceService _service;

        public void Initialize()
        {
            var device = GetHeartRateDevice();

            if (device == null)
                throw new HRDeviceException("No device with heart rate measurement characteristic available.");
            Debug.WriteLine($"Found device {device.Name}");

            _service?.Dispose();
            var service = ServiceFromDevice(device);
            if (service == null)
                throw new HRDeviceException($"Could not instantiate service from device '{device.Name}.");
            Debug.WriteLine($"Found service {service.Uuid}");

            _service = service;

            var characteristic = CharacteristicFromService(service);
            if (characteristic == null)
                throw new HRDeviceException($"Could not find heart rate characteristic on device '{device.Name}'.");
            Debug.WriteLine($"Found characteristic {characteristic.UserDescription}");

            var status = characteristic
                .WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify)
                .GetAwaiter()
                .GetResult();

            if (status != GattCommunicationStatus.Success)
                throw new HRDeviceException("Could write characteristic configuration to device.");

            characteristic.ValueChanged += CharacteristicListener;
        }

        private DeviceInformation GetHeartRateDevice()
            => DeviceInformation
                .FindAllAsync(GattDeviceService
                    .GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate))
                .GetAwaiter()
                .GetResult()
                .FirstOrDefault();

        private GattDeviceService ServiceFromDevice(DeviceInformation device)
            => GattDeviceService.FromIdAsync(device.Id)
                .GetAwaiter()
                .GetResult();

        private GattCharacteristic CharacteristicFromService(GattDeviceService service)
            => service
                .GetCharacteristicsForUuidAsync(_hrCharacteristicUuid)
                .GetAwaiter()
                .GetResult()
                .Characteristics
                .FirstOrDefault();

        private void CharacteristicListener(GattCharacteristic characteristic, GattValueChangedEventArgs args)
        {
            var deviceBuffer = args.CharacteristicValue;
            if (deviceBuffer.Length == 0)
                return;

            var byteBuffer = new byte[deviceBuffer.Length];
            using (var stream = DataReader.FromBuffer(deviceBuffer))
                stream.ReadBytes(byteBuffer);

            using (var stream = new MemoryStream(byteBuffer))
            {
                var flags   = (HeartRateFlags)stream.ReadByte();
                var isShort = flags.HasFlag(HeartRateFlags.IsShort);
                var contact = (ContactSensorStatus)(((int)flags >> 1) & 3);
                var hasEE   = flags.HasFlag(HeartRateFlags.HasEnergyExpended);
                var hasRR   = flags.HasFlag(HeartRateFlags.HasRRInterval);
                var minLength = isShort ? 3 : 2;

                var reading = new HeartRateReading
                {
                    Flags = flags,
                    Status = contact,
                    BeatsPerMinute = isShort ? stream.ReadUInt16() : stream.ReadByte()
                };

                if (hasEE)
                    reading.EnergyExpended = stream.ReadUInt16();

                if (hasRR)
                {
                    var intervals = new int[(deviceBuffer.Length - stream.Position) / sizeof(ushort)];

                    for (var i = 0; i < intervals.Length; i++)
                        intervals[i] = stream.ReadUInt16();

                    reading.RRIntervals = intervals;
                }

                HeartRateUpdated.Invoke(reading);
            }
        }

        public void Dispose()
            => _service?.Dispose();
    }

    static class MemoryStreamExtensions
    {
        public static ushort ReadUInt16(this Stream s)
            => (ushort)(s.ReadByte() | (s.ReadByte() << 8));
    }
}
