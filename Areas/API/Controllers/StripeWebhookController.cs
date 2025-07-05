using BookStore.DataAccess.Data;
using BookStore.Models.DTOs;
using BookStore.Models;
using BookStore.Utility;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;

namespace BookStoreWeb.Areas.API.Controllers
{
    [ApiController]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly IConfiguration _config;

        public StripeWebhookController(ApplicationDbContext db, ILogger<StripeWebhookController> logger, IConfiguration config)
        {
            _db = db;
            _logger = logger;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _config["Stripe:WebhookSecret"]
                );

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;

                    var userId = session.Metadata["userId"];
                    var orderTotal = decimal.Parse(session.Metadata["orderTotal"]);
                    var cartItems = JsonSerializer.Deserialize<List<CartItemDTO>>(session.Metadata["cartItems"]);

                    var orderHeader = new OrderHeader
                    {
                        ApplicationUserId = userId,
                        OrderDate = DateTime.Now,
                        OrderTotal = orderTotal,
                        PaymentIntentId = session.PaymentIntentId,
                        SessionId = session.Id,
                        OrderStatus = SD.StatusApproved,
                        PaymentStatus = SD.PaymentStatusApproved
                    };

                    _db.OrderHeaders.Add(orderHeader);
                    await _db.SaveChangesAsync();

                    foreach (var item in cartItems)
                    {
                        _db.OrderDetails.Add(new OrderDetail
                        {
                            OrderHeaderId = orderHeader.Id,
                            ProductId = item.ProductId,
                            Count = item.Count,
                            Price = item.Price
                        });
                    }

                    var userCart = _db.ShoppingCarts.Where(c => c.ApplicationUserId == userId);
                    _db.ShoppingCarts.RemoveRange(userCart);

                    await _db.SaveChangesAsync();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("⚠️ Stripe Webhook error: " + ex.Message);
                return BadRequest();
            }
        }
    }

}
