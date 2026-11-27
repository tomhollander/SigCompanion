using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SigCompanion.Lib.Sigen.Modbus;

namespace SigCompanion.Lib.Tests;

public sealed class SigenModbusClientTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ReadsChargersIndependentlyAndDoesNotRetryFailures(bool failDc, bool failAc)
    {
        using var server = new ModbusServer { FailDc = failDc, FailAc = failAc };
        using var client = new SigenModbusClient("127.0.0.1", server.Port, 247);

        var first = await client.GetPlantInformationAsync();
        server.FailDc = false;
        server.FailAc = false;
        var second = await client.GetPlantInformationAsync();

        Assert.Equal(failDc ? null : -1.234m, first.DcEvChargingPower);
        Assert.Equal(failAc ? null : 7.654m, first.AcEvChargingPower);
        Assert.Equal(first.DcEvChargingPower, second.DcEvChargingPower);
        Assert.Equal(first.AcEvChargingPower, second.AcEvChargingPower);
        Assert.Equal(failDc ? 1 : 2, server.Requests.Count(request => request.Address == 31502));
        Assert.Equal(failAc ? 1 : 2, server.Requests.Count(request => request.Address == 32003));
        Assert.All(server.Requests.Where(request => request.Address is 31502 or 32003), request =>
        {
            Assert.Equal(3, request.Function);
            Assert.Equal(2, request.Count);
        });
        Assert.All(server.Requests.Where(request => request.Address is 30000 or 30284 or 30194), request =>
            Assert.Equal(3, request.Function));

        using var newSession = new SigenModbusClient("127.0.0.1", server.Port, 247);
        var refreshed = await newSession.GetPlantInformationAsync();
        Assert.Equal(-1.234m, refreshed.DcEvChargingPower);
        Assert.Equal(7.654m, refreshed.AcEvChargingPower);
    }

    [Theory]
    [InlineData(0, 0d)]
    [InlineData(157, 15.7)]
    [InlineData(1000, 100d)]
    [InlineData(65535, null)]
    public async Task ReadsBackupSocFromOneHoldingRegister(int rawValue, double? expectedPercent)
    {
        using var server = new ModbusServer { BackupSocRaw = (ushort)rawValue };
        using var client = new SigenModbusClient("127.0.0.1", server.Port, 247);

        var percent = await client.GetBackupSocAsync();

        Assert.Equal((decimal?)expectedPercent, percent);
        Assert.Equal(((byte)3, (ushort)40046, (ushort)1), Assert.Single(server.Requests));
    }

    private sealed class ModbusServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _acceptTask;

        public ModbusServer()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptTask = AcceptConnectionsAsync();
        }

        public int Port { get; }
        public bool FailDc { get; set; }
        public bool FailAc { get; set; }
        public ushort BackupSocRaw { get; init; }
        public ConcurrentQueue<(byte Function, ushort Address, ushort Count)> Requests { get; } = new();

        private async Task AcceptConnectionsAsync()
        {
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                    _ = HandleConnectionAsync(client);
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
        }

        private async Task HandleConnectionAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var request = new byte[12];
                    while (!_cancellation.IsCancellationRequested)
                    {
                        await stream.ReadExactlyAsync(request, _cancellation.Token);
                        var function = request[7];
                        var address = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(8));
                        var count = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(10));
                        Requests.Enqueue((function, address, count));
                        var fail = (address == 31502 && FailDc) || (address == 32003 && FailAc);
                        var response = new byte[fail ? 9 : 9 + count * 2];
                        request.AsSpan(0, 4).CopyTo(response);
                        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4), (ushort)(response.Length - 6));
                        response[6] = request[6];
                        response[7] = fail ? (byte)(function | 0x80) : function;
                        response[8] = fail ? (byte)2 : (byte)(count * 2);
                        if (!fail && address is 31502 or 32003)
                        {
                            BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(9), address == 31502 ? -1234 : 7654);
                        }
                        else if (address == 40046)
                        {
                            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(9), BackupSocRaw);
                        }

                        await stream.WriteAsync(response, _cancellation.Token);
                    }
                }
                catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
                {
                }
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _listener.Stop();
            _acceptTask.GetAwaiter().GetResult();
            _cancellation.Dispose();
        }
    }
}
