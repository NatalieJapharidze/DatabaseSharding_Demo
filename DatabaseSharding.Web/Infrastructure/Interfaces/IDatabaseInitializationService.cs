﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Interfaces
{
    public interface IDatabaseInitializationService
    {
        Task InitializeAllDatabasesAsync(CancellationToken cancellationToken = default);
    }
}
