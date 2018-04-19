﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sembium.ContentStorage.Service.Results;
using Sembium.ContentStorage.Service.Security;
using Sembium.ContentStorage.Service.ServiceModels;
using Sembium.ContentStorage.Storage.Common;
using Sembium.ContentStorage.Storage.HostingResults;
using Sembium.ContentStorage.Utils;
using Sembium.ContentStorage.Utils.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Service.Library.Controllers
{
    [Route("[controller]")]
    public class SystemController : Controller
    {
        private readonly ISystem _system;

        public SystemController(ISystem system)
        {
            _system = system;
        }

        [Route("containers")]
        [HttpGet]
        public IEnumerable<string> GetContainerNames([FromQuery]string auth)
        {
            return _system.GetContainerNames(auth);
        }

        /// <summary>
        /// Adds a container
        /// </summary>
        /// <param name="containerName">Name of the container to be created</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>HTTP response code</returns>
        [Route("containers/add/{containerName}")]
        [HttpPut]
        public void CreateContainer(string containerName, [FromQuery]string auth)
        {
            _system.CreateContainer(containerName, auth);
        }
        
        /// <summary>
        /// Maintains a container
        /// </summary>
        /// <param name="containerName">Name of the container to be maintain</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>HTTP response code</returns>
        [Route("containers/{containerName}/maintain")]
        [HttpPut]
        public async Task<string> MaintainContainerAsync(string containerName, [FromQuery]string auth, CancellationToken cancellationToken)
        {
            return await _system.MaintainContainerAsync(containerName, auth, cancellationToken);
        }

        /// <summary>
        /// Compacts a container content names index
        /// </summary>
        /// <param name="containerName">Name of the container to be maintain</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>HTTP response code</returns>
        [Route("containers/{containerName}/compactcontentnames")]
        [HttpPut]
        public async Task CompactContainerContentNamesAsync(string containerName, [FromQuery]string auth, CancellationToken cancellationToken)
        {
            await _system.CompactContainerContentNamesAsync(containerName, auth, cancellationToken);
        }

        /// <summary>
        /// Maintains all containers
        /// </summary>
        /// <param name="auth">Authentication token for the request</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>HTTP response code</returns>
        [Route("maintain")]
        [HttpPut]
        public IActionResult Maintain([FromQuery]string auth, CancellationToken cancellationToken)
        {
            //return _contents.Maintain(auth, cancellationToken);

            return new FileCallbackResult(
                new MediaTypeHeaderValue("text/plain"),
                async (outputStream, _) =>
                {
                    var messages = _system.Maintain(auth, cancellationToken);

                    foreach (var message in messages)
                    {
                        var buffer = Encoding.UTF8.GetBytes(message + Environment.NewLine);
                        await outputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                    }
                });
        }

        /// <summary>
        /// Gets list of the container states
        /// </summary>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>List of strings</returns>
        [Route("containers/states")]
        [HttpGet]
        public IEnumerable<IContainerState> GetContainerStates([FromQuery]string auth)
        {
            return _system.GetContainerStates(auth);
        }

        /// <summary>
        /// Gets list of the readonly containers in the system
        /// </summary>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>List of strings</returns>
        [Route("readonlycontainers")]
        [HttpGet]
        public IEnumerable<string> GetReadOnlyContainerNames([FromQuery]string auth)
        {
            return _system.GetReadOnlyContainerNames(auth);
        }

        /// <summary>
        /// Gets list of the readonly containers in the system
        /// </summary>
        /// <param name="containerName">Name of the container to be made readonly</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>List of strings</returns>
        [Route("readonlycontainers/add/{containerName}")]
        [HttpPut]
        public void SetContainerReadOnly(string containerName, [FromQuery]string auth)
        {
            _system.SetContainerReadOnlyState(containerName, true, auth);
        }

        /// <summary>
        /// Gets list of the readonly containers in the system
        /// </summary>
        /// <param name="containerName">Name of the container to be made readonly</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>List of strings</returns>
        [Route("readonlycontainers/delete/{containerName}")]
        [HttpDelete]
        public void SetContainerNotReadOnly(string containerName, [FromQuery]string auth)
        {
            _system.SetContainerReadOnlyState(containerName, false, auth);
        }

        /// <summary>
        /// Gets readonly subcontainer names from a container
        /// </summary>
        /// <param name="containerName">Container name</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>HTTP response code</returns>
        [Route("containers/{containerName}/subcontainers")]
        [HttpGet]
        public IEnumerable<string> GetContainerSubcontainers(string containerName, [FromQuery]string auth)
        {
            return _system.GetContainerReadOnlySubcontainerNames(containerName, auth);
        }

        /// <summary>
        /// Adds new readonly subcontainer to a container
        /// </summary>
        /// <param name="containerName">Container name</param>
        /// <param name="subcontainerName">Read only subcontainer name</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>HTTP response code</returns>
        [Route("containers/{containerName}/subcontainers/add/{subcontainerName}")]
        [HttpPut]
        public void AddContainerSubcontainer(string containerName, string subcontainerName, [FromQuery]string auth)
        {
            _system.AddContainerReadOnlySubcontainer(containerName, subcontainerName, auth);
        }

        /// <summary>
        /// Removes a readonly subcontainer from a container
        /// </summary>
        /// <param name="containerName">Container name</param>
        /// <param name="subcontainerName">Read only subcontainer name</param>
        /// <param name="auth">Authentication token for the request</param>
        /// <returns>HTTP response code</returns>
        [Route("containers/{containerName}/subcontainers/delete/{subcontainerName}")]
        [HttpDelete]
        public void RemoveContainerSubcontainer(string containerName, string subcontainerName, [FromQuery]string auth)
        {
            _system.RemoveContainerReadOnlySubcontainer(containerName, subcontainerName, auth);
        }
    }
}