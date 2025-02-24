using ESCPOS_NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App
{
    internal class PrinterStatusReader
    {
        private PrinterStatusEventArgs _status;
        private string _ip;
        private int _port;

        public PrinterStatusReader(string ip, int port)
        {
            _ip = ip;
            _port = port;
            _status = new PrinterStatusEventArgs();
        }

        public async Task<PrinterStatusReader> CheckOnlineStatus()
        {
            try
            {
                var result = await GetStatus(new byte[]
                {
                    0x10, 0x04, 0x01
                });

                _status.IsPrinterOnline = (result.Value & 0x02) != 0;
                _status.IsCoverOpen = (result.Value & 0x04) != 0;
                _status.IsPaperOut = (result.Value & 0x08) != 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }

            return this;
        }

        public async Task<PrinterStatusReader> CheckPaperStatus()
        {
            try
            {
                var result = await GetStatus(new byte[]
                {
                    0x10, 0x04, 0x02
                });

                _status.IsPaperLow = (result.Value & 0x02) != 0;
                _status.IsPaperOut = (result.Value & 0x04) != 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }

            return this;
        }

        public PrinterStatusEventArgs? GetStatus() => _status.IsPrinterOnline ?? false ? _status : null;

        private async Task<byte?> GetStatus(byte[] command)
        {
            try
            {
                using (var client = new TcpClient(_ip, _port))
                using (var stream = client.GetStream())
                {
                    await stream.WriteAsync(command, 0, command.Length);

                    byte[] response = new byte[1];
                    int bytesRead = await stream.ReadAsync(response, 0, response.Length);

                    if (bytesRead > 0)
                    {
                        return response[0];
                    }
                    else
                    {
                        Logger.LogError("No response from printer.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"{ex.Message}");                
            }

            return null;
        }
    }
}
