using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using GrpcService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Testclient
{
    class Program
    {
        private static string gRPCUrl = "https://localhost:5001";

        public IConfiguration Configuration { get; }

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
          
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddGrpcClient<Account.AccountClient>(o => { o.Address = new Uri(gRPCUrl); });
                    services.AddGrpcClient<Asset.AssetClient>(o => { o.Address = new Uri(gRPCUrl); });

              //      services.AddHostedService<gRPCAccountClient>();
                    services.AddHostedService<gRPCAssetClient>();
                });





    }
}
