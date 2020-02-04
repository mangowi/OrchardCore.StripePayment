using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LefeWareLearning.TenantBilling;
using LefeWareLearning.TenantBilling.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.TenantBilling.Models;
using Stripe;

namespace LefeWareLearning.StripePayment.Controllers
{
    [Route("api/stripewebhook")]
    [ApiController]
    [IgnoreAntiforgeryToken, AllowAnonymous]
    public class StripeWebHookController : Controller
    {
        private readonly ILogger<StripeWebHookController> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly StripeConfigurationOptions _options;
        public StripeWebHookController(IServiceProvider serviceProvider, ILogger<StripeWebHookController> logger, IOptions<StripeConfigurationOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
        }

        [HttpPost]
        [Route("sync")]
        public async Task<IActionResult> Sync()
        {
            //TODO: Add this somewhere global
            // if (!IsDefaultShell())
            // {
            //     return Unauthorized();
            // }

            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _options.WebhookSecret);

                switch (stripeEvent.Type)
                {
                    case Events.InvoicePaymentSucceeded:
                    {
                        var invoice = stripeEvent.Data.Object as Invoice;

                        //Line Items
                        var lineItem = invoice.Lines.Data[0];//Only interested in the first item 
                        var tenantId = lineItem.Metadata["TenantId"];
                        var tenantName = lineItem.Metadata["TenantName"];
                        var amount = lineItem.Plan.AmountDecimal.Value;
                        var period = new BillingPeriod(lineItem.Period.Start.Value, lineItem.Period.End.Value);

                        //Payment Method
                        var paymentMethod = await GetPaymentInformation(invoice);

                        var paymentSuccessEventHandlers = _serviceProvider.GetRequiredService<IEnumerable<IPaymentSuccessEventHandler>>();
                        await paymentSuccessEventHandlers.InvokeAsync(x => x.PaymentSuccess(tenantId, tenantName, period, amount, paymentMethod), _logger);
                        break;
                    }
                    case Events.InvoicePaymentFailed:
                    {
                        //Handle Failed Payment
                         var invoice = stripeEvent.Data.Object as Invoice;
                        var tenantId = invoice.Lines.Data[0].Metadata["TenantId"];

                        //TODO: Handle
                        _logger.LogError($"Unable to process payment for {tenantId}");
    
                        break;
                    }
                }
            }
            catch (StripeException e)
            {
                return BadRequest(e.Message);
            }

            return Ok();
        }

        private async Task<TenantBilling.Models.PaymentMethod> GetPaymentInformation(Invoice invoice)
        {
            //Get Customer
            var stripeCustomerId = invoice.CustomerId;
            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(stripeCustomerId);

            //User Customer to extract subscription payment
            var paymentMethodId = customer.Subscriptions.Data[0].DefaultPaymentMethodId;
            var paymentService = new PaymentMethodService();
            var paymentMethod = await paymentService.GetAsync(paymentMethodId);

            //Get card info
            var card = paymentMethod.Card;
            var cardType = (CardType)Enum.Parse(typeof(CardType), card.Brand.ToLower());
            var creditCardInformation = new CreditCardInformation(cardType, Int32.Parse(card.Last4), Convert.ToInt32(card.ExpMonth), Convert.ToInt32(card.ExpYear));
            var paymentInformation = new TenantBilling.Models.PaymentMethod(true,  creditCardInformation);

            return paymentInformation;
        }
    }
}

