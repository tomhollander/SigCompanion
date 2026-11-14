// #define SIMULATE_MODBUS
using System.Buffers.Binary;
using System.Net.Sockets;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Sigen.Modbus;

public sealed class SigenModbusClient : IDisposable
{
    private const byte ReadInputRegistersFunction = 0x04;
    private const ushort PlantInfoStartAddress = 30000;
    private const ushort PlantInfoRegisterCount = 72;
    private const int MbapHeaderLength = 6;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly string _host;
    private readonly int _port;
    private readonly byte _slaveAddress;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ushort _transactionId;

    public SigenModbusClient(string host, int port, byte slaveAddress)
    {
        _host = host;
        _port = port;
        _slaveAddress = slaveAddress;
    }

    public async Task<SigenPlantInformation> GetPlantInformationAsync(CancellationToken cancellationToken = default)
    {
#if SIMULATE_MODBUS
        return new SigenPlantInformation
        {
            SystemTimeEpochSeconds = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SystemTimeZoneMinutes = 600,
            EmsWorkMode = 1,
            GridSensorStatus = 1,
            GridSensorActivePower = Random.Shared.Next(0, 10000) - 5000,
            GridSensorReactivePower = 1000,
            OnOffGridStatus = 0,
            MaxActivePower = 10000,
            MaxApparentPower = 12000,
            EnergyStorageSystemSoc = 80,
            PlantPhaseAActivePower = 2000,
            PlantPhaseBActivePower = 1500,
            PlantPhaseCActivePower = 1500,
            PlantPhaseAReactivePower = 500,
            PlantPhaseBReactivePower = 300,
            PlantPhaseCReactivePower = 200,
            GeneralAlarm1 = 0,
            GeneralAlarm2 = 0,
            GeneralAlarm3 = 0,
            GeneralAlarm4 = 0,
            PlantActivePower = Random.Shared.Next(0, 10000),
            PlantReactivePower = 1000,
            PhotovoltaicPower = Random.Shared.Next(0, 10000),
            EssPower = Random.Shared.Next(0, 10000) - 5000,
            AvailableMaxActivePower = 8000,
            AvailableMinActivePower = -8000,
            AvailableMaxReactivePower = 4000,
            AvailableMinReactivePower = -4000,
            AvailableMaxChargingPower = -5000,
            AvailableMaxDischargingPower = 5000,
            PlantRunningState = 1
        };
#endif

        var registers = await ReadInputRegistersAsync(PlantInfoStartAddress, PlantInfoRegisterCount, cancellationToken);

        return new SigenPlantInformation
        {
            SystemTimeEpochSeconds = ReadU32(registers, 30000),
            SystemTimeZoneMinutes = ReadU16(registers, 30002),
            EmsWorkMode = ReadU16(registers, 30003),
            GridSensorStatus = ReadU16(registers, 30004),
            GridSensorActivePower = ReadS32Scaled(registers, 30005, 1000),
            GridSensorReactivePower = ReadS32Scaled(registers, 30007, 1000),
            OnOffGridStatus = ReadU16(registers, 30009),
            MaxActivePower = ReadU32Scaled(registers, 30010, 1000),
            MaxApparentPower = ReadU32Scaled(registers, 30012, 1000),
            EnergyStorageSystemSoc = ReadU16Scaled(registers, 30014, 10),
            PlantPhaseAActivePower = ReadS32Scaled(registers, 30015, 1000),
            PlantPhaseBActivePower = ReadS32Scaled(registers, 30017, 1000),
            PlantPhaseCActivePower = ReadS32Scaled(registers, 30019, 1000),
            PlantPhaseAReactivePower = ReadS32Scaled(registers, 30021, 1000),
            PlantPhaseBReactivePower = ReadS32Scaled(registers, 30023, 1000),
            PlantPhaseCReactivePower = ReadS32Scaled(registers, 30025, 1000),
            GeneralAlarm1 = ReadU16(registers, 30027),
            GeneralAlarm2 = ReadU16(registers, 30028),
            GeneralAlarm3 = ReadU16(registers, 30029),
            GeneralAlarm4 = ReadU16(registers, 30030),
            PlantActivePower = ReadS32Scaled(registers, 30031, 1000),
            PlantReactivePower = ReadS32Scaled(registers, 30033, 1000),
            PhotovoltaicPower = ReadS32Scaled(registers, 30035, 1000),
            EssPower = ReadS32Scaled(registers, 30037, 1000),
            AvailableMaxActivePower = ReadU32Scaled(registers, 30039, 1000),
            AvailableMinActivePower = ReadU32Scaled(registers, 30041, 1000),
            AvailableMaxReactivePower = ReadU32Scaled(registers, 30043, 1000),
            AvailableMinReactivePower = ReadU32Scaled(registers, 30045, 1000),
            AvailableMaxChargingPower = ReadU32Scaled(registers, 30047, 1000),
            AvailableMaxDischargingPower = ReadU32Scaled(registers, 30049, 1000),
            PlantRunningState = ReadU16(registers, 30051),
            GridSensorPhaseAActivePower = ReadS32Scaled(registers, 30052, 1000),
            GridSensorPhaseBActivePower = ReadS32Scaled(registers, 30054, 1000),
            GridSensorPhaseCActivePower = ReadS32Scaled(registers, 30056, 1000),
            GridSensorPhaseAReactivePower = ReadS32Scaled(registers, 30058, 1000),
            GridSensorPhaseBReactivePower = ReadS32Scaled(registers, 30060, 1000),
            GridSensorPhaseCReactivePower = ReadS32Scaled(registers, 30062, 1000),
            AvailableMaxChargingCapacity = ReadU32Scaled(registers, 30064, 100),
            AvailableMaxDischargingCapacity = ReadU32Scaled(registers, 30066, 100),
            RatedEssChargingPower = ReadU32Scaled(registers, 30068, 1000),
            RatedEssDischargingPower = ReadU32Scaled(registers, 30070, 1000)
        };
    }

    private async Task<byte[]> ReadInputRegistersAsync(ushort startAddress, ushort count, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);
        var token = timeoutCts.Token;

        await EnsureConnectedAsync(token);

        var request = new byte[12];
        _transactionId++;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0), _transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4), 6);
        request[6] = _slaveAddress;
        request[7] = ReadInputRegistersFunction;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10), count);

        try
        {
            await _stream!.WriteAsync(request, token);

            var header = new byte[MbapHeaderLength];
            await _stream.ReadExactlyAsync(header, token);

            if (BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0)) != _transactionId)
            {
                throw new IOException("Unexpected Modbus transaction ID in response.");
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
            if (length < 2)
            {
                throw new IOException("Invalid Modbus response length.");
            }

            var payload = new byte[length];
            await _stream.ReadExactlyAsync(payload, token);

            if (payload[0] != _slaveAddress)
            {
                throw new IOException("Unexpected Modbus unit ID in response.");
            }

            var body = payload.AsSpan(1).ToArray();
            return ParseResponse(body, startAddress, count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Dispose();
            throw new TimeoutException($"Modbus request timed out after {RequestTimeout.TotalSeconds}s.");
        }
        catch (IOException)
        {
            Dispose();
            throw;
        }
    }

    private static byte[] ParseResponse(byte[] body, ushort startAddress, ushort count)
    {
        if (body[0] == (ReadInputRegistersFunction | 0x80))
        {
            throw new IOException($"Modbus exception code {body[1]} reading registers starting at {startAddress}.");
        }

        var byteCount = body[1];
        if (body[0] != ReadInputRegistersFunction || byteCount != count * 2)
        {
            throw new IOException("Unexpected Modbus response.");
        }

        return body.AsSpan(2, byteCount).ToArray();
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_tcpClient is { Connected: true } && _stream is not null)
        {
            return;
        }

        Dispose();
        _tcpClient = new TcpClient { NoDelay = true };
        await _tcpClient.ConnectAsync(_host, _port, cancellationToken);
        _stream = _tcpClient.GetStream();
    }

    private static ushort ReadU16(byte[] registers, ushort address) =>
        BinaryPrimitives.ReadUInt16BigEndian(registers.AsSpan(RegisterOffset(address)));

    private static uint ReadU32(byte[] registers, ushort address) =>
        BinaryPrimitives.ReadUInt32BigEndian(registers.AsSpan(RegisterOffset(address)));

    private static int ReadS32(byte[] registers, ushort address) =>
        BinaryPrimitives.ReadInt32BigEndian(registers.AsSpan(RegisterOffset(address)));

    private static decimal ReadU16Scaled(byte[] registers, ushort address, int gain) =>
        (decimal)ReadU16(registers, address) / gain;

    private static decimal ReadU32Scaled(byte[] registers, ushort address, int gain) =>
        (decimal)ReadU32(registers, address) / gain;

    private static decimal ReadS32Scaled(byte[] registers, ushort address, int gain) =>
        (decimal)ReadS32(registers, address) / gain;

    private static int RegisterOffset(ushort address) => (address - PlantInfoStartAddress) * 2;

    public void Dispose()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _stream = null;
        _tcpClient = null;
    }
}
