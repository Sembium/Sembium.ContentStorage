﻿using Sembium.ContentStorage.Service.ServiceResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Service.Results
{
    public class DocumentUploadInfo : IDocumentUploadInfo
    {
        public IUploadInfo UploadInfo { get; }
        public IDocumentIdentifier DocumentIdentifier { get; }

        public DocumentUploadInfo(IUploadInfo uploadInfo, IDocumentIdentifier documentIdentifier)
        {
            UploadInfo = uploadInfo;
            DocumentIdentifier = documentIdentifier;
        }
    }
}
