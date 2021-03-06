﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Service.Results
{
    public class ContainerState : IContainerState
    {
        public string ContainerName { get; }
        public bool IsReadOnly { get; }

        public bool IsMaintained { get; }

        private ContainerState()
        {
            // do nothing
        }

        public ContainerState(string containerName, bool isReadOnly, bool isMaintained)
        {
            ContainerName = containerName;
            IsReadOnly = isReadOnly;
            IsMaintained = isMaintained;
        }
    }
}
