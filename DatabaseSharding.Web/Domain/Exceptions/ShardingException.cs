using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    public class ShardingException : DomainException
    {
        public ShardingException(string message) : base(message) { }
        public ShardingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
