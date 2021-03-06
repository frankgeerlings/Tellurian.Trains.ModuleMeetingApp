using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tellurian.Trains.Clocks.Server.Tests
{
    [TestClass]
    public class ClockTests
    {
        [TestMethod]
        public void Level1Message()
        {
            using var target = new ClockServer(ClockServerOptions.Default);
            var message = target.Level1Message;
            Assert.AreEqual("0 06 00 6", message);
        }

        [TestMethod]
        public void Level2Message()
        {
            using var target = new ClockServer(ClockServerOptions.Default);
            var message = target.Level2Message;
            Assert.AreEqual("clocktype=fastclock\r\nclock=06:00:00\r\nactive=no\r\nweekday=0\r\nspeed=6\r\ntext=\r\ninterval=2", message);
        }

        [TestMethod]
        public void MulticastMessage()
        {
            using var target = new ClockServer(ClockServerOptions.Default);
            
            var message = target.MulticastMessage;
            Assert.AreEqual("fastclock\r\nname=Kolding\r\nversion=2\r\nip-address=" + TestMachineIPAdress + "\r\nip-port=2500\r\nclocktype=fastclock\r\nclock=06:00:00\r\nactive=no\r\nweekday=0\r\nspeed=6\r\ntext=\r\ninterval=2", message);
        }

        private static string TestMachineIPAdress => ClockServer.GetLocalIPAddress().ToString();

        [TestMethod]
        public void RunningClock()
        {
            var receivedUdp = new List<string>();
            var settings = new ClockSettings() { IsRunning = true };
            var options = ClockServerOptions.Default.Value;
            using (var receiver = new UdpClient(2000, AddressFamily.InterNetwork))
            {
                receiver.JoinMulticastGroup(IPAddress.Parse(options.Multicast.IPAddress), ClockServer.GetLocalIPAddress());
                var state = new State { u = receiver, l = receivedUdp };
                using var t = Task.Factory.StartNew(() => Receiver(receiver, state), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                using var target = new ClockServer(
                    Options.Create(new ClockServerOptions()
                    {
                        Multicast = new MulticastOptions { IntervalSeconds = 1, IPAddress = "239.50.50.20", IsEnabled = true, PortNumber = 2000 },
                        Polling = new PollingOptions { IsEnabled = true, PortNumber = 2500 },
                        StartTime = TimeSpan.FromHours(5.5),
                        Speed = 6,
                    }));
                {
                }
                target.Start(settings);
                Assert.IsTrue(target.IsRunning);
                Thread.Sleep(10000);
                target.Stop();
                receiver.Close();
                t.Wait();
            }
            Assert.IsTrue(receivedUdp.Count > 0);
        }

        internal class State
        {
            public UdpClient? u;
            public IList<string>? l;
        }

        private void Receiver(UdpClient receiver, State state)
        {
            while (true)
            {
                try { receiver.BeginReceive(new AsyncCallback(ReceiveUdp), state); }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception) { break; }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        private void ReceiveUdp(IAsyncResult r)
        {
            IPEndPoint? ip = null;
            var state = (State?)r.AsyncState;
            try
            {
                var bytes = state?.u?.EndReceive(r, ref ip);
                if (bytes == null) return;
                var text = Encoding.UTF8.GetString(bytes);
                state?.l?.Add(text);
            }
            finally
            {
            }
        }
    }
}
