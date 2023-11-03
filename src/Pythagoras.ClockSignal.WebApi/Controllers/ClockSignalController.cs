using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Pythagoras.Infrastructure.Configuration;
using Pythagoras.Infrastructure.CubeClients.ClockSignal;
using Pythagoras.Infrastructure.Realtime;
using System.ComponentModel.DataAnnotations;
using System;
using Pythagoras.Infrastructure.WebApi;
using Pythagoras.ClockSignal.WebApi.Services;
using FluentValidation.Validators;

namespace Pythagoras.ClockSignal.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ClockSignalController : ControllerBase
    {
        private readonly ILogger<ClockSignalController> _logger;
        private readonly CustomConfigurationStore<ClockSignalSettings> _configurationStore;
        private readonly IValidator<ClockSignalSettings> _settingsValidator;
        private readonly ClockSignalService _service;

        public ClockSignalController(ClockSignalService service,
            CustomConfigurationStore<ClockSignalSettings> configurationStore,
            ILogger<ClockSignalController> logger,
            IValidator<ClockSignalSettings> settingsValidator)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        }

        [HttpGet]
        [Route("Settings")]
        public async Task<WebApiResult<ClockSignalSettings>> GetSettingsAsync()
        {
            try
            {
                var settings = await _configurationStore.GetAsync();
                return WebApiResult<ClockSignalSettings>.OK(settings);
            }
            catch (Exception ex)
            {
                return WebApiResult<ClockSignalSettings>.Error(ex, "Cannot load CustomConfiguration");
            }
        }

        [HttpPost]
        [Route("Settings")]
        public async Task<WebApiResult> SetSettingsAsync(ClockSignalSettings settings)
        {
            var result = await _settingsValidator.ValidateAsync(settings);
            if (!result.IsValid) return WebApiResult.Error(result.ToDictionary());

            try
            {
                await _configurationStore.SaveAsync(settings);
                return WebApiResult.OK();
            }
            catch (Exception ex)
            {
                return WebApiResult.Error(ex, "Cannot save CustomConfiguration");
            }
        }

        [HttpPost]
        [Route("Start")]
        public WebApiResult Start()
        {
            try
            {
                _service.Start();
                return WebApiResult.OK();
            }
            catch (Exception ex)
            {
                return WebApiResult.Error(ex, "Cannot start ClockSignalService");
            }
        }

        [HttpPost]
        [Route("Stop")]
        public WebApiResult Stop()
        {
            try
            {
                _service.Stop();
                return WebApiResult.OK();
            }
            catch (Exception ex)
            {
                return WebApiResult.Error(ex, "Cannot stop ClockSignalService");
            }
        }

        [HttpGet]
        [Route("State")]
        public WebApiResult<ClockSignalState> GetState()
        {
            try
            {
                var state = new ClockSignalState 
                { 
                    IsRunning = _service.IsRunning,
                    IsError = _service.IsError,
                    ErrorMessage = _service.ErrorMessage
                };
                return WebApiResult<ClockSignalState>.OK(state);
            }
            catch (Exception ex)
            {
                return WebApiResult<ClockSignalState>.Error(ex, "Cannot get ClockSignalState");
            }
        }
    }
}