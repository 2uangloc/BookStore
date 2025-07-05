using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Models.ViewModels;
using System.Security.Claims;
using BookStore.Utility;
using Stripe.Checkout;
using BookStore.Models.Enums;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using BookStore.Services.IService;
using BookStore.Models.DTOs;
using Hangfire;
using Stripe.Climate;

namespace BookStoreWeb.Areas.Customer.Controllers
{
    [Area("customer")]
    [Authorize(Roles = SD.Role_Customer)]
    public class CartController : Controller
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ICreateOrderService _createOrderService;
        private readonly ICreateOrderCodeService _createOrderCodeService;
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork, ICreateOrderCodeService createOrderCodeService,
            ICreateOrderService createOrderService, IBackgroundJobClient backgroundJobClient)
        {
            _unitOfWork = unitOfWork;
            _createOrderCodeService = createOrderCodeService;
            _createOrderService = createOrderService;
            _backgroundJobClient = backgroundJobClient;
        }

        public IActionResult Index()
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(
                u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }
        private decimal GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
                return shoppingCart.Product.Price;
            else
            {
                if (shoppingCart.Count <= 100)
                    return shoppingCart.Product.Price50;
                else
                    return shoppingCart.Product.Price100;
            }
        }
        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.GetValue(u => u.Id == cartId);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.GetValue(u => u.Id == cartId);
            if (cartFromDb.Count <= 1)
            {
                //xoa khoi gio hang`
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.GetValue(u => u.Id == cartId);
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        
        public IActionResult OrderConfirmation(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderHeader = _unitOfWork.OrderHeader.GetValue(
                u => u.Id == id && u.ApplicationUserId == userId, includeProperties: "ApplicationUser");

            if (orderHeader == null)
                return RedirectToAction("AccessDenied", "Account", new { area = "Identity" });

            switch (orderHeader.PaymentMethod)
            {
                case PaymentMethod.Stripe:
                    if (orderHeader.SessionId == null)
                        throw new Exception("SessionId is NULL");

                    if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
                    {
                        var service = new SessionService();
                        var session = service.Get(orderHeader.SessionId);

                        if (session.PaymentStatus.ToLower() == "paid")
                        {
                            _unitOfWork.OrderHeader.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
                            _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                            _unitOfWork.Save();
                        }
                    }
                    break;

                case PaymentMethod.Momo:
                    if (orderHeader.OrderStatus == SD.StatusPending)
                    {
                        // Có thể kiểm tra trạng thái tại IPN hoặc kiểm tra theo kết quả trả về ở redirect
                        // Ở đây ta assume người dùng đã thanh toán thành công (đã được redirect về)
                        _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                        _unitOfWork.Save();
                    }
                    break;

                case PaymentMethod.BankTransfer:
                case PaymentMethod.COD:
                    // Không cần xác thực gì thêm ở đây
                    break;
            }

            // Xoá giỏ hàng sau khi xác nhận
            var shoppingCarts = _unitOfWork.ShoppingCart.GetAll(
                u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();

            return View(orderHeader);
        }


        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(
                u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.GetValue(u => u.Id == userId);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }
        [HttpPost]
        [ActionName("Summary")]
        public async Task<IActionResult> SummaryPost()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Lấy giỏ hàng
            var cartItems = _unitOfWork.ShoppingCart.GetAll(
                u => u.ApplicationUserId == userId,
                includeProperties: "Product")
                .Select(c => new CartItemDTO
                {
                    ProductId = c.ProductId,
                    ProductTitle = c.Product.Title,
                    Count = c.Count,
                    Price = GetPriceBasedOnQuantity(c)
                }).ToList();

            if (cartItems == null || !cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng trống.";
                return RedirectToAction(nameof(Index));
            }

            // 2. Lấy dữ liệu form nhập (OrderHeader từ View)
            var customerInfo = new OrderHeaderDTO
            {
                Name = ShoppingCartVM.OrderHeader.Name,
                PhoneNumber = ShoppingCartVM.OrderHeader.PhoneNumber,
                StreetAddress = ShoppingCartVM.OrderHeader.StreetAddress,
                City = ShoppingCartVM.OrderHeader.City,
                State = ShoppingCartVM.OrderHeader.State,
                PostalCode = ShoppingCartVM.OrderHeader.PostalCode
            };

            var paymentMethod = ShoppingCartVM.OrderHeader.PaymentMethod;

            // 3. Tạo đơn hàng
            var orderHeader = await _createOrderService.CreateOrderAsync(userId, cartItems, paymentMethod, customerInfo);
            ShoppingCartVM.OrderHeader = orderHeader;

            // 4. Đặt lịch hủy nếu không phải COD
            if (paymentMethod != PaymentMethod.COD)
            {
                BackgroundJob.Schedule<IOrderCleanupJob>(
                    job => job.CancelUnpaidOrderById(orderHeader.Id),
                    TimeSpan.FromMinutes(2));
            }
            // 5. Xử lý thanh toán
            return paymentMethod switch
            {
                PaymentMethod.Stripe => await HandleStripePaymentAsync(cartItems),
                PaymentMethod.COD => HandleCOD(),
                PaymentMethod.BankTransfer => HandleBankTransfer(),
                PaymentMethod.Momo => await HandleMomoPaymentAsync(),
                _ => RedirectToAction(nameof(OrderConfirmation), "Cart", new { id = orderHeader.Id })
            };



            //var claimsIdentity = (ClaimsIdentity)User.Identity;
            //var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(
            //    u => u.ApplicationUserId == userId,
            //    includeProperties: "Product");

            //ShoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
            //ShoppingCartVM.OrderHeader.ApplicationUserId = userId;

            //var applicationUser = _unitOfWork.ApplicationUser.GetValue(u => u.Id == userId);

            //foreach (var cart in ShoppingCartVM.ShoppingCartList)
            //{
            //    cart.Price = GetPriceBasedOnQuantity(cart);
            //    ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            //}

            //// Gán mặc định là Pending
            //ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            //ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;

            //_unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            //ShoppingCartVM.OrderHeader.OrderCode = await _createOrderCodeService.GenerateOrderCodeAsync();
            //_unitOfWork.Save();

            //foreach (var cart in ShoppingCartVM.ShoppingCartList)
            //{
            //    var orderDetail = new OrderDetail
            //    {
            //        ProductId = cart.ProductId,
            //        OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
            //        Price = cart.Price,
            //        Count = cart.Count,
            //    };
            //    _unitOfWork.OrderDetail.Add(orderDetail);
            //}
            //_unitOfWork.Save();

            //// Gọi phương thức xử lý tùy theo loại thanh toán
            //return ShoppingCartVM.OrderHeader.PaymentMethod switch
            //{
            //    PaymentMethod.Stripe => await HandleStripePaymentAsync(),
            //    PaymentMethod.COD => HandleCOD(),
            //    PaymentMethod.BankTransfer => HandleBankTransfer(),
            //    PaymentMethod.Momo => await HandleMomoPaymentAsync(),
            //    _ => RedirectToAction(nameof(OrderConfirmation), "Cart", new { id = ShoppingCartVM.OrderHeader.Id })
            //};
        }

        //Hàm thanh toán Stripe
        private async Task<IActionResult> HandleStripePaymentAsync(List<CartItemDTO> cartItems)
        {
            var domain = "http://localhost:5140/";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var metadata = new Dictionary<string, string>
            {
                { "userId", userId },
                { "cartItems", JsonSerializer.Serialize(cartItems) },
                { "orderId", ShoppingCartVM.OrderHeader.Id.ToString() }
            };

            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
                CancelUrl = domain + "customer/cart/index",
                LineItems = cartItems.Select(item => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = Convert.ToInt64(item.Price / 25000) * 100,
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.ProductTitle
                        }
                    },
                    Quantity = item.Count
                }).ToList(),
                Mode = "payment",
                Metadata = metadata
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _unitOfWork.OrderHeader.UpdateStripePaymentId(
                ShoppingCartVM.OrderHeader.Id,
                session.Id,
                session.PaymentIntentId);
            await _unitOfWork.SaveAsync();

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        //Hàm thanh toán COD
        private IActionResult HandleCOD()
        {
            _unitOfWork.OrderHeader.UpdateStatus(
                ShoppingCartVM.OrderHeader.Id,
                SD.StatusApproved,
                SD.PaymentStatusDelayedPayment
            );
            _unitOfWork.Save();

            return RedirectToAction(nameof(OrderConfirmation), "Cart", new { id = ShoppingCartVM.OrderHeader.Id });
        }
        //Hàm thanh toán ngan hang
        private IActionResult HandleBankTransfer()
        {
            _unitOfWork.OrderHeader.UpdateStatus(
                ShoppingCartVM.OrderHeader.Id,
                SD.StatusPending,
                SD.PaymentStatusDelayedPayment
            );
            _unitOfWork.Save();

            TempData["Success"] = "Vui lòng chuyển khoản theo thông tin hiển thị trên trang tiếp theo!";
            return RedirectToAction(nameof(BankTransferPage), "Cart", new { id = ShoppingCartVM.OrderHeader.Id });
        }
        //Hàm thanh toán momo
        private async Task<IActionResult> HandleMomoPaymentAsync()
        {
            var endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
            string partnerCode = "MOMO";
            string accessKey = "F8BBA842ECF85";
            string secretKey = "K951B6PE1waDMi640xX08PD3vg6EkVlz";
            string orderInfo = "Thanh toán đơn hàng BookStore";

            string domain = NgrokHelper.GetPublicUrl();
            if (string.IsNullOrEmpty(domain))
                throw new Exception("Ngrok chưa được bật");

            string redirectUrl = $"{domain}/Customer/Cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}";
            string ipnUrl = $"{domain}/api/payment/ipn";

            string amount = ShoppingCartVM.OrderHeader.OrderTotal.ToString("0");
            string orderId = Guid.NewGuid().ToString();
            string requestId = Guid.NewGuid().ToString();
            string extraData = "";

            string rawHash = $"accessKey={accessKey}&amount={amount}&extraData={extraData}" +
                             $"&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}" +
                             $"&partnerCode={partnerCode}&redirectUrl={redirectUrl}" +
                             $"&requestId={requestId}&requestType=captureWallet";

            var signature = GenerateSignature(rawHash, secretKey);

            var requestBody = new
            {
                partnerCode,
                partnerName = "BookStore",
                storeId = "BookStore123",
                requestId,
                amount,
                orderId,
                orderInfo,
                redirectUrl,
                ipnUrl,
                lang = "vi",
                extraData,
                requestType = "captureWallet",
                signature
            };

            using var http = new HttpClient();
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await http.PostAsync(endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.TryGetProperty("payUrl", out var payUrlElement))
            {
                var payUrl = payUrlElement.GetString();

                _unitOfWork.OrderHeader.UpdateStatus(
                    ShoppingCartVM.OrderHeader.Id,
                    SD.StatusPending,
                    SD.PaymentStatusPending
                );
                _unitOfWork.Save();

                // Chuyển người dùng đến trang thanh toán Momo
                return Redirect(payUrl);
            }
            else
            {
                TempData["Error"] = "Không thể khởi tạo thanh toán Momo. Phản hồi từ Momo:\n" + responseBody;
                Console.WriteLine(responseBody);
                return RedirectToAction(nameof(OrderConfirmation), "Cart", new { id = ShoppingCartVM.OrderHeader.Id });
            }
        }

        private string GenerateSignature(string rawData, string secretKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
        //Trang thanh toán ngân hàng
        public IActionResult BankTransferPage(int id)
        {
            var order = _unitOfWork.OrderHeader.GetValue(u => u.Id == id);
            return View("BankTransfer", order);
        }

        public IActionResult MomoPaymentPage(int id)
        {
            var order = _unitOfWork.OrderHeader.GetValue(u => u.Id == id);
            return View("MomoPayment", order);
        }

    }
}
