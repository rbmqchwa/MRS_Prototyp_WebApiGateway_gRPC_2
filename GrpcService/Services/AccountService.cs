using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace GrpcService
{
    public class AccountService : Account.AccountBase
    {
        private readonly ILogger<AccountService> _logger;
        public AccountService(ILogger<AccountService> logger)
        {
            _logger = logger;
        }

        public override Task<LoginReply> LoginUser(LoginRequest request, ServerCallContext context)
        {
            return Task.FromResult(new LoginReply()
            {
                Success = true,
                Message = "Logged in " + request.Username
            });
        }

        public override Task<ReturnReply> LogoutUser(LogoutRequest request, ServerCallContext context)
        {
            return Task.FromResult(new ReturnReply()
            {
                Success = true,
                Message = "Logged out " + request.Username
            });
        }
    }
}
