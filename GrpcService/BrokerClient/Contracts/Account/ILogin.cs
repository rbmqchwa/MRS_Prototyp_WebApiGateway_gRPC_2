using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcService.BrokerClient.Contracts.Account
{
    public interface ILogin
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }

    }
}
