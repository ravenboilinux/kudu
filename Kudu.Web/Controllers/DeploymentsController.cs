﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Mvc.Async;

namespace Kudu.Web.Controllers
{
    public class DeploymentsController : TaskAsyncController
    {
        private readonly IApplicationService _applicationService;
        private readonly ICredentialProvider _credentialProvider;

        public DeploymentsController(IApplicationService applicationService,
                                     ICredentialProvider credentialProvider)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.tab = "deployments";

            base.OnActionExecuting(filterContext);
        }

        public async Task<ActionResult> Index(string slug)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            Task<IEnumerable<DeployResult>> deployResults = deploymentManager.GetResultsAsync();
            Task<RepositoryInfo> repositoryInfo = application.GetRepositoryInfo(credentials);

            await Task.WhenAll(deployResults, repositoryInfo);

            var appViewModel = new ApplicationViewModel(application)
            {
                RepositoryInfo = repositoryInfo.Result,
                Deployments = deployResults.Result.ToList()
            };

            ViewBag.slug = slug;
            ViewBag.appName = appViewModel.Name;

            return View(appViewModel);
        }

        [HttpPost]
        public async Task<ActionResult> TriggerFetch(string slug, string repositoryUrl, RepositoryType repositoryType)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteFetchManager fetchManager = application.GetFetchManager(credentials);

            try
            {
                await fetchManager.TriggerFetch(repositoryUrl, repositoryType);
            }
            catch (HttpUnsuccessfulRequestException)
            {
                // Ignore any failures in triggering the deployment
            }
            return new EmptyResult();
        }

        public Task<ActionResult> Deploy(string slug, string id, bool? clean)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            return deploymentManager.DeployAsync(id, clean ?? false)
                                    .ContinueWith(task =>
                                    {
                                        return (ActionResult)RedirectToAction("Index", new { slug });
                                    });
        }

        public Task<ActionResult> Log(string slug, string id)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            return deploymentManager.GetLogEntriesAsync(id).Then(entries =>
            {
                ViewBag.slug = slug;
                ViewBag.appName = application.Name;
                ViewBag.id = id;

                return (ActionResult)View(entries);
            });
        }

        public Task<ActionResult> Details(string slug, string id, string logId)
        {
            IApplication application = _applicationService.GetApplication(slug);
            if (application == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            return deploymentManager.GetLogEntryDetailsAsync(id, logId).Then(entries =>
            {
                ViewBag.slug = slug;
                ViewBag.appName = application.Name;
                ViewBag.id = id;
                ViewBag.verbose = true;

                return (ActionResult)View("Log", entries);
            });
        }
    }
}
