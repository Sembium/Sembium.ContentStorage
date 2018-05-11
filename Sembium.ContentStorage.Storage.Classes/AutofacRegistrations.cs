﻿using Autofac;
using Sembium.ContentStorage.Storage.Common;
using Sembium.ContentStorage.Storage.ContentNames;
using Sembium.ContentStorage.Storage.Hosting;
using Sembium.ContentStorage.Storage.HostingResults;
using Sembium.ContentStorage.Storage.Tools;
using Sembium.ContentStorage.Utils.Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Storage
{
    public static class AutofacRegistrations
    {
        public static void RegisterFor(ContainerBuilder builder)
        {
            builder.RegisterType<MultiPartUploadInfo>().As<IMultiPartUploadInfo>();
            builder.RegisterType<UploadIdentifier>().As<IUploadIdentifier>();
            builder.RegisterType<DownloadInfo>().As<IDownloadInfo>();
            builder.RegisterType<HttpPartUploadInfo>().As<IHttpPartUploadInfo>();

            builder.RegisterType<UploadIdentifierProvider>().As<IUploadIdentifierProvider>();

            builder.RegisterType<ContentNameProvider>().As<IContentNameProvider>();
            builder.RegisterType<ContentIdentifier>().As<IContentIdentifier>();
            builder.RegisterType<ContentIdentifierGenerator>().As<IContentIdentifierGenerator>();
            builder.RegisterType<ContentIdentifiersProvider>().As<IContentIdentifiersProvider>();
            builder.RegisterType<ContentHashValidator>().As<IContentHashValidator>();
            builder.RegisterType<SystemContainerProvider>().As<ISystemContainerProvider>();

            builder.RegisterType<ContentNamesVault>().As<IContentNamesVault>();
            builder.RegisterType<ContentNamesVaultItem>().As<IContentNamesVaultItem>();

            builder.RegisterType<HttpRequestInfo>().As<IHttpRequestInfo>();

            builder.RegisterOptionsAdapter<ContentNamesRepositorySettings>().As<IContentNamesRepositorySettings>();
        }
    }
}
