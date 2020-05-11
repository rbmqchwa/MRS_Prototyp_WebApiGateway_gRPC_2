using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using GrpcService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FileInfo = System.IO.FileInfo;

namespace Testclient
{
    public class gRPCAssetClient : IHostedService
    {
        private const int FileChunkSize = 0x8000;

        private readonly Asset.AssetClient _client;
        private readonly IConfiguration _config;
        private readonly ILogger<gRPCAssetClient> _logger;
        private readonly string _filePath;

        public gRPCAssetClient(Asset.AssetClient client, ILogger<gRPCAssetClient> logger, IConfiguration config,
            ILoggerFactory loggerFactory)
        {
            _filePath = config["FileSourcePath"];
            _client = client;
            _logger = logger;
            _config = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var uploadId = Guid.NewGuid().ToString();
            var username = "fritz";
        
            var uploadFilename = Path.Combine(_filePath, @"GatewayTest.zip");

            //         while (!cancellationToken.IsCancellationRequested)
            //        {
            _logger.LogInformation("gRPCAssetClient running at: {time}", DateTimeOffset.Now);

            // upload file
            _logger.LogInformation("gRPCAssetClient try UploadFile at: {time} UploadId:{1}, Username:{2}, Filename:{3}",
                DateTimeOffset.Now, uploadId, username, uploadFilename);
            await UploadFile(uploadId, username, uploadFilename, cancellationToken).ConfigureAwait(false);

            // Download uploaded image
            _logger.LogInformation("gRPCAssetClient try downloadFile at: {time} UploadId:{1}, Username:{2}, Filename:{3}",
                DateTimeOffset.Now, uploadId, username, uploadFilename);
            await DownloadFile(uploadId, username, uploadFilename.Substring(uploadFilename.LastIndexOf("\\")+1), cancellationToken).ConfigureAwait(false);

            // Wait a while...
            await Task.Delay(_config.GetValue<int>("Service:DelayInterval"), cancellationToken).ConfigureAwait(false);
            //           }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("gRPCAssetClient StopAsync at: {time}", DateTimeOffset.Now);
            return Task.CompletedTask;
        }

        private async Task UploadFile(string fileId, string username, string fileName,
            CancellationToken cancellationToken)
        {
            var ioFileInfo = new FileInfo(fileName);
            var uploadRequest = new UploadFileRequest
            {
                Username = username,
                TransferStatus = TransferStatus.Undefined,
                Info = new GrpcService.FileInfo
                {
                    Name = ioFileInfo.Name,
                    Id = fileId,
                    FileChecksum = GetSha1Hash(fileName)
                }
            };
            var stream = _client.UploadFile();

            var file = File.ReadAllBytes(fileName);
            var fileOffset = 0;
            var fileChunk = new byte[FileChunkSize];
            while (fileOffset < file.Length && !cancellationToken.IsCancellationRequested)
            {
                var length = Math.Min(FileChunkSize, file.Length - fileOffset);
                Buffer.BlockCopy(file, fileOffset, fileChunk, 0, length);
                fileOffset += length;
                var byteString = ByteString.CopyFrom(fileChunk);
                uploadRequest.ChunkData = byteString;
                await stream.RequestStream.WriteAsync(uploadRequest).ConfigureAwait(false);
                _logger.LogInformation("gRPCAssetClient chunk uploaded at: {time}, Length:{1}, Offset:{2}",
                    DateTimeOffset.Now, length, fileOffset);
            }

            await stream.RequestStream.CompleteAsync().ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                var response = await stream.ResponseAsync.ConfigureAwait(false);

                _logger.LogInformation("gRPCAssetClient finish UploadFile at: {time}, {1}, {2}", DateTimeOffset.Now,
                    response.Id, response.Status);
            }
        }

        private async Task DownloadFile(string fileId, string username, string fileName,
            CancellationToken cancellationToken)
        {
            var success = true;
            var tmpFile = Path.Combine(Path.GetTempPath(), string.Format("tmp_{0}", Guid.NewGuid().ToString()));
            var targetFile =
                Path.Combine(@"C:\Users\Chris\source\repos\TestGateway\Testclient\Files\Client\Download", fileName);
            try
            {
                var downloadRequest = new DownloadFileRequest {Id = fileId, Username = username, Filename = fileName};
                using var streamingCall = _client.DownloadFile(downloadRequest);
                await using (Stream fs = File.OpenWrite(tmpFile))
                {
                    await foreach (var downloadResponse in
                        streamingCall.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                        fs.Write(downloadResponse.ChunkData.ToByteArray());
                }
            }
            // Is thrown on cancellation -> ignore...
            catch (OperationCanceledException)
            {
                success = false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown");
                success = false;
            }

            if (!success)
                File.Delete(tmpFile);
            else
                File.Move(tmpFile, targetFile, true);
        }

        public string GetSha1Hash(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            {
                SHA1 sha = new SHA1Managed();
                return BitConverter.ToString(sha.ComputeHash(fs));
            }
        }
    }
}