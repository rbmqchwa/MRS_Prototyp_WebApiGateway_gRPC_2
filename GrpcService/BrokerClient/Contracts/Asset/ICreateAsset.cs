using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcService.BrokerClient.Contracts.Asset
{
    public interface ICreateAsset
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Filename { get; set; }
    }
}
