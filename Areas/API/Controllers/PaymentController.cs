using BookStore.DataAccess.Repository.IRepository;
using BookStore.Utility;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BookStoreWeb.Areas.API.Controllers
{
    [Area("API")]
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public PaymentController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpPost("ipn")]
        public IActionResult MomoIPN([FromBody] JsonElement data)
        {
            string orderId = data.GetProperty("orderId").GetString();
            string requestId = data.GetProperty("requestId").GetString();
            int resultCode = data.GetProperty("resultCode").GetInt32();
            decimal amount = data.GetProperty("amount").GetDecimal();

            var order = _unitOfWork.OrderHeader.GetValue(o => o.PaymentIntentId == orderId && o.SessionId == requestId);
            if (order == null)
                return NotFound();

            if (resultCode == 0)
            {
                order.PaymentStatus = SD.PaymentStatusApproved;
                order.OrderStatus = SD.StatusApproved;
                _unitOfWork.Save();
            }

            return Ok(new { message = "IPN received" });
        }
    }

}
