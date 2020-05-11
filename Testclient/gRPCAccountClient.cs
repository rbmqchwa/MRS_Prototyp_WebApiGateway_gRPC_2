using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using GrpcService;
using Microsoft.Extensions.Hosting;

namespace Testclient
{
    public class gRPCAccountClient : IHostedService
    {
        private Account.AccountClient _client;
        public gRPCAccountClient(Account.AccountClient client)
        {
            _client = client;
        }
        public async Task TestAccount(CancellationToken cancellationToken)
        {
            //Test Login
            var loginRequest = new LoginRequest()
            {
                Username = "fritz",
                PasswordHash = "asdhjf2903uaksdlfh"
            };
            var result = await _client.LoginUserAsync(loginRequest);
            Console.WriteLine("Loggedin {0}; {1}", result.Success, result.Message);

            cancellationToken.ThrowIfCancellationRequested();

            //Test Logout
            var logoutRequest = new LogoutRequest()
            {
                Username = "fritz",
            };
            var resultLogout = await _client.LogoutUserAsync(logoutRequest);
            Console.WriteLine("Loggedout {0}; {1}", resultLogout.Success, resultLogout.Message);

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
             return TestAccount(cancellationToken); 
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
