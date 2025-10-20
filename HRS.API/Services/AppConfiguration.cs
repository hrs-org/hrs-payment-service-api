using HRS.API.Services.Interfaces;

namespace HRS.API.Services;

public class AppConfiguration : IAppConfiguration
{
    private readonly IConfiguration _configuration;

    public AppConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
        Setup();
    }

    public string FrontendUrl { get; set; } = "http://localhost:4200/";
    public string StripeApiKey { get; set; } = string.Empty;
    public string PaymentReturnPath { get; set; } = string.Empty;

    private void Setup()
    {
        StripeApiKey = _configuration["Payment:Secretkey"] ?? "";
        PaymentReturnPath = FrontendUrl + (_configuration["Payment:ReturnUrl"] ?? "");
    }
}
