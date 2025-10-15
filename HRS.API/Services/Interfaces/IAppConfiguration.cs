namespace HRS.API.Services.Interfaces;

public interface IAppConfiguration
{

    string FrontendUrl { get; set; }
    public string StripeApiKey { get; set; }
    public string PaymentReturnPath { get; set; }
}
