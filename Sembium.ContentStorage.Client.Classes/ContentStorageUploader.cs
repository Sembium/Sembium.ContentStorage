﻿using Sembium.ContentStorage.Common;
using Sembium.ContentStorage.Misc;
using Sembium.ContentStorage.Storage.Common;
using Sembium.ContentStorage.Storage.HostingResults;
using Sembium.ContentStorage.Storage.Tools;
using Sembium.ContentStorage.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Client
{
    public class ContentStorageUploader : IContentStorageUploader
    {
        private readonly ISerializer _serializer;
        private readonly IContentIdentifierSerializer _contentIdentifierSerializer;
        private readonly IContentStorageServiceURLProvider _contentStorageServiceURLProvider;
        private bool _sslDisabled;

        public ContentStorageUploader(
            ISerializer serializer,
            IContentIdentifierSerializer contentIdentifierSerializer,
            IContentStorageServiceURLProvider contentStorageServiceURLProvider)
        {
            _serializer = serializer;
            _contentIdentifierSerializer = contentIdentifierSerializer;
            _contentStorageServiceURLProvider = contentStorageServiceURLProvider;
        }

        public void UploadContent(Stream stream, string contentStorageServiceURL, string containerName, IContentIdentifier contentIdentifier, long size, string authenticationToken)
        {
            using (var httpClient = GetHttpClient())
            {
                var multiPartIDUploadInfo = GetMultiPartIDUploadInfo(contentIdentifier, size, httpClient, contentStorageServiceURL, containerName, authenticationToken);

                if ((multiPartIDUploadInfo == null) || string.IsNullOrEmpty(multiPartIDUploadInfo.UploadID))
                    return;

                var results = MultiPartUpload(multiPartIDUploadInfo, stream, size);

                CommitMultiPartUpload(multiPartIDUploadInfo.UploadID, results, httpClient, contentStorageServiceURL, containerName, authenticationToken);
            }
        }

        private System.Net.Http.HttpClient GetHttpClient(TimeSpan? timeout = null)
        {
            if (!_sslDisabled)
            {
                _sslDisabled = true;
                System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            }

            var httpClient = new HttpClient();
            httpClient.Timeout = (timeout ?? TimeSpan.FromMinutes(5));

            return httpClient;
        }

        private void CommitMultiPartUpload(string uploadID, IEnumerable<KeyValuePair<string, string>> partUploadResults, HttpClient httpClient, string contentStorageServiceURL, string containerName, string authenticationToken)
        {
            var commitURL = _contentStorageServiceURLProvider.GetURLForCommitMultiPartUpload(contentStorageServiceURL, containerName, authenticationToken);

            var commitMultiPartUploadParams = new { UploadID = uploadID, PartUploadResults = partUploadResults };
            var serializedCommitMultiPartUploadParams = _serializer.Serialize(commitMultiPartUploadParams);

            var request = new HttpRequestMessage(HttpMethod.Post, commitURL);
            request.Content = new StringContent(serializedCommitMultiPartUploadParams, Encoding.UTF8, "application/json");
            httpClient.CheckedSendAsync(request).Wait();
        }

        private IEnumerable<KeyValuePair<string, string>> MultiPartUpload(IMultiPartIDUploadInfo multiPartIDUploadInfo, Stream stream, long streamSize)
        {
            var result = new List<KeyValuePair<string, string>>();

            if (multiPartIDUploadInfo.PartSize <= 0)
            {
                System.Diagnostics.Debug.Assert(multiPartIDUploadInfo.PartUploadInfos.Count() == 1);
                UploadPart(multiPartIDUploadInfo.HttpMethod, stream, streamSize, multiPartIDUploadInfo.PartUploadInfos.First(), "");
            }
            else
            {
                int writePartIndex = 0;

                CopyUtils.CopyAsync(
                    async (buffer, cancellationToken) =>
                    {
                        return await CopyUtils.FillBufferAsync(buffer,
                            async (buf, offset, ct) =>
                            {
                                return await stream.ReadAsync(buf, offset, (int)multiPartIDUploadInfo.PartSize - offset);
                            },
                            cancellationToken
                        );
                    },
                    (buffer, size, cancellationToken) =>
                    {
                        var partUploadInfo = multiPartIDUploadInfo.PartUploadInfos.ElementAt(writePartIndex);

                        var uploadPartResult = UploadPart(multiPartIDUploadInfo.HttpMethod, new MemoryStream(buffer), size, partUploadInfo, multiPartIDUploadInfo.MultiPartUploadResultHeaderName);

                        result.Add(new KeyValuePair<string, string>(partUploadInfo.Identifier, uploadPartResult));

                        writePartIndex++;

                        return Task.CompletedTask;
                    },
                    (int)multiPartIDUploadInfo.PartSize,
                    CancellationToken.None
                )
                .Wait();

                //foreach (var partUploadInfo in multiPartIDUploadInfo.PartUploadInfos)
                //{
                //    var uploadPartResult = UploadPart(multiPartIDUploadInfo.HttpMethod, stream, multiPartIDUploadInfo.PartSize, partUploadInfo, multiPartIDUploadInfo.MultiPartUploadResultHeaderName);
                //    yield return new KeyValuePair<string, string>(partUploadInfo.Identifier, uploadPartResult);
                //}
            }

            return result;
        }

        private HttpContent GetHttpContent(Stream stream, long size, bool formFile)
        {
            if (formFile)
            {
                var streamContent = new StreamContent(stream);

                var content = new MultipartFormDataContent();
                content.Add(streamContent, "dummyname", "dummyfilename");

                return content;
            }
            else
            {
                using (var br = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var bytes = br.ReadBytes((int)size);

                    var content = new ByteArrayContent(bytes);

                    return content;
                }
            }
        }

        private string UploadPart(string httpMethod, Stream stream, long partSize, IHttpPartUploadInfo httpPartUploadInfo, string resultHeaderName)
        {
            using (var httpClient = GetHttpClient(TimeSpan.FromMinutes(15)))  // todo: config
            {
                var httpMethodInfo = httpMethod.Split('/');
                var formFile = (httpMethodInfo.Length == 2) && (httpMethodInfo[1].Equals("FORMFILE", StringComparison.InvariantCultureIgnoreCase));

                using (var content = GetHttpContent(stream, partSize, formFile))
                {
                    using (var request = new HttpRequestMessage(new HttpMethod(httpMethodInfo[0]), httpPartUploadInfo.URL))
                    {
                        request.Content = content;

                        request.SetHeaders(httpPartUploadInfo.RequestHttpHeaders);

                        var response = httpClient.CheckedSendAsync(request).Result;

                        return string.IsNullOrEmpty(resultHeaderName) ? null : response.Headers.Where(x => x.Key == resultHeaderName).Select(x => x.Value.FirstOrDefault()).FirstOrDefault();
                    }
                }
            }
        }

        private IMultiPartIDUploadInfo GetMultiPartIDUploadInfo(IContentIdentifier contentIdentifier, long size, HttpClient httpClient, string contentStorageServiceURL, string containerName, string authenticationToken)
        {
            var contentID = _contentIdentifierSerializer.Serialize(contentIdentifier);

            var requestURL = _contentStorageServiceURLProvider.GetURLForGetMultiPartUploadInfo(contentStorageServiceURL, containerName, contentID, size, authenticationToken);

            var serializedIDUploadInfo = httpClient.CheckedGetStringAsync(requestURL).Result;

            return _serializer.Deserialize<IMultiPartIDUploadInfo>(serializedIDUploadInfo);
        }

        public void UploadContent(Stream stream, string contentStorageServiceURL, string containerName, string contentID, long size, string authenticationToken)
        {
            var contentIdentifier = _contentIdentifierSerializer.Deserialize(contentID);
            UploadContent(stream, contentStorageServiceURL, containerName, contentIdentifier, size, authenticationToken);
        }

        public void UploadContent(string contentUrl, string contentStorageServiceURL, string containerName, string contentID, long size, string authenticationToken)
        {
            using (var httpClient = GetHttpClient())
            {
                using (var response = httpClient.GetAsync(contentUrl, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    CheckResponseSuccess(response);

                    using (var contentStream = response.Content.ReadAsStreamAsync().Result)
                    {
                        UploadContent(contentStream, contentStorageServiceURL, containerName, contentID, size, authenticationToken);
                    }
                }
            }
        }

        private void CheckResponseSuccess(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = response.Content.ReadAsStringAsync().Result;
                throw new HttpRequestException(response.ReasonPhrase + " " + (int)response.StatusCode + Environment.NewLine + errorMessage);
            }
        }
    }
}
