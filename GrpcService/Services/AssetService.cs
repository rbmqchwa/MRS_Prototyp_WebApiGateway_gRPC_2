using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GrpcService
{
    public class AssetService : Asset.AssetBase
    {

        private readonly string _filePath;
        private const int FileChunkSize = 0x8000;

        private readonly ILogger<AssetService> _logger ;

        public AssetService(ILogger<AssetService> logger, IConfiguration config)
        {
            _filePath = config["FileSourcePath"];
            _logger = logger;
        }

        public override async Task<UploadFileResponse> UploadFile(
           IAsyncStreamReader<UploadFileRequest> requestStream, ServerCallContext context)
        {
            UploadFileResponse transferStatusMessage = new UploadFileResponse();
            transferStatusMessage.Status = TransferStatus.Success;
           // var filename = string.Format("{0}_{1}_{2}", requestStream.Current.Username, requestStream.Current.Info.Id , requestStream.Current.Info.Name);
            var tmpFile = Path.Combine(Path.GetTempPath(), string.Format("tmp_{0}",Guid.NewGuid().ToString()));
            string targetFile = string.Empty;
            try
            {
                await Task.Run(
                    async () =>
                    {
                        CancellationToken cancellationToken = context.CancellationToken;
                        await using (Stream fs = File.OpenWrite(tmpFile))
                        {
                            await foreach (UploadFileRequest uploadFileRequest in
                                requestStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                            {
                                if (string.IsNullOrWhiteSpace(targetFile))
                                {
                                    transferStatusMessage.Id = uploadFileRequest.Info.Id;
                                    targetFile = string.Format("{0}_{1}_{2}", requestStream.Current.Username, requestStream.Current.Info.Id, requestStream.Current.Info.Name);
                                }
                                fs.Write(uploadFileRequest.ChunkData.ToByteArray());
                            }
                        }
                    }).ConfigureAwait(false);
            }
            // Is thrown on cancellation -> ignore
            catch (OperationCanceledException)
            {
                transferStatusMessage.Status = TransferStatus.Cancelled;
            }
            catch (RpcException rpcEx)
            {
                if (rpcEx.StatusCode == StatusCode.Cancelled)
                {
                    transferStatusMessage.Status = TransferStatus.Cancelled;
                }
                else
                {
                    _logger.LogError($"Exception while processing image file '{tmpFile}'. Exception: '{requestStream}'");
                    transferStatusMessage.Status = TransferStatus.Failure;
                }
            }
            // Delete incomplete file
            if (transferStatusMessage.Status != TransferStatus.Success)
            {
                File.Delete(tmpFile);
            }
            else
            {
                File.Move(tmpFile, Path.Combine(_filePath, targetFile));
            }
            return transferStatusMessage;
        }

        public override async Task DownloadFile(DownloadFileRequest request, 
            IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
        {
            string filePath = Path.Combine(_filePath, string.Format("{0}_{1}_{2}", request.Username, request.Id, request.Filename));
          
            // Example of exception
            if (File.Exists(filePath) == false)
            {
                _logger.LogError($"File '{filePath}' not found.");
                Metadata metadata = new Metadata()
                    {{"Filename", filePath}};
                throw new RpcException(new Status(StatusCode.NotFound, "Image file not found."), 
                    metadata, "More details for the exception...");
            }
            DownloadFileResponse downloadResponse = new DownloadFileResponse();
            downloadResponse.Info = new FileInfo();
            downloadResponse.Status = TransferStatus.Success;
         
            byte[] image;
            try
            {
                image = File.ReadAllBytes(filePath);
            }
            catch (Exception)
            {
                _logger.LogError($"Exception while reading image file '{filePath}'.");
                throw new RpcException(Status.DefaultCancelled, "Exception while reading image file.");
            }
            int fileOffset = 0;
            byte[] fileChunk = new byte[FileChunkSize];
            while (fileOffset < image.Length)
            {
                int length = Math.Min(FileChunkSize, image.Length - fileOffset);
                Buffer.BlockCopy(image, fileOffset, fileChunk, 0, length);
                fileOffset += length;
                ByteString byteString = ByteString.CopyFrom(fileChunk);
                downloadResponse.ChunkData = byteString;
                await responseStream.WriteAsync(downloadResponse).ConfigureAwait(false);
            }
        }
    }
}
