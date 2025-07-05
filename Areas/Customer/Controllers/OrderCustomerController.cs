using BookStore.DataAccess.Data;
using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Models.ViewModels;
using BookStore.Services.IService;
using BookStore.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using System.Security.Claims;

namespace BookStoreWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = SD.Role_Customer)]
    public class OrderCustomerController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderStatusService _orderStatusService;
        private readonly ICreateOrderCodeService _createOrderCodeService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;

        [BindProperty]
        public OrderVM OrderVM { get; set; }
        public OrderCustomerController(
            IUnitOfWork unitOfWork, IOrderStatusService orderService,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext db, 
            ICreateOrderCodeService createOrderCodeService)
        {
            _unitOfWork = unitOfWork;
            _orderStatusService = orderService;
            _userManager = userManager;
            _db = db;
            _createOrderCodeService = createOrderCodeService;
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Details(int orderId)
        {
            OrderVM = new OrderVM()
            {
                OrderHeader = _unitOfWork.OrderHeader.GetValue(u => u.Id == orderId, includeProperties: "ApplicationUser"),
                OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product")
            };
            return View(OrderVM);
        }
        [HttpPost]
        public IActionResult UpdateOrderDetail(int orderId)
        {
            var orderHeaderFromDb = _unitOfWork.OrderHeader.GetValue(u => u.Id == OrderVM.OrderHeader.Id);

            orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
            orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
            orderHeaderFromDb.City = OrderVM.OrderHeader.City;
            orderHeaderFromDb.State = OrderVM.OrderHeader.State;
            orderHeaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;
            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
                orderHeaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
                orderHeaderFromDb.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
            _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
            _unitOfWork.Save();
            TempData["Success"] = "Order Details Updated Successfully";
            return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
        }
        [HttpPost]
        public IActionResult StartProcessing()
        {
            _orderStatusService.StartProcessing(OrderVM.OrderHeader.Id);
            TempData["Success"] = "Order processing started successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }

        [HttpPost]
        public IActionResult ShipOrder()
        {
            _orderStatusService.ShipOrder(
                OrderVM.OrderHeader.Id,
                OrderVM.OrderHeader.TrackingNumber,
                OrderVM.OrderHeader.Carrier
            );
            TempData["Success"] = "Order shipped successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder()
        {
            var result = await _orderStatusService.CancelOrderAsync(OrderVM.OrderHeader.Id);
            if (!result)
            {
                TempData["Error"] = "Refund failed. Please try again.";
            }
            else
            {
                TempData["Success"] = "Order cancelled successfully";
            }
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }
        [HttpPost]
        public IActionResult MarkAsCompleted()
        {
            var orderHeader = _unitOfWork.OrderHeader.GetValue(u => u.Id == OrderVM.OrderHeader.Id);
            if (orderHeader.OrderStatus == SD.StatusShipped)
            {
                orderHeader.OrderStatus = SD.StatusCompleted;
                _unitOfWork.OrderHeader.Update(orderHeader);
                _unitOfWork.Save();

                TempData["Success"] = "Order marked as completed successfully.";
            }
            else
            {
                TempData["Error"] = "Only shipped orders can be marked as completed.";
            }

            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }
        [HttpPost]
        public async Task<IActionResult> PlaceOrder()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            string userId = claim.Value;

            var orderHeader = new OrderHeader
            {
                ApplicationUserId = userId,
                OrderDate = DateTime.Now,
                OrderStatus = SD.StatusPending,
                PaymentStatus = SD.PaymentStatusPending,
                OrderTotal = 0 // Bạn có thể tính lại từ giỏ hàng
            };

            orderHeader.OrderCode = await _createOrderCodeService.GenerateOrderCodeAsync();

            _unitOfWork.OrderHeader.Add(orderHeader);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("OrderConfirmation", new { id = orderHeader.Id });
        }










        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            string userId = claim.Value;

            var orders = _unitOfWork.OrderHeader.GetAll(
                u => u.ApplicationUserId == userId,
                includeProperties: "ApplicationUser").ToList();

            // Filter theo status
            if (!string.IsNullOrEmpty(status))
            {
                switch (status.ToLower())
                {
                    case "pending":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusPending).ToList();
                        break;
                    case "approved":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusApproved).ToList();
                        break;
                    case "inprocess":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusInProcess).ToList();
                        break;
                    case "shipped":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusShipped).ToList();
                        break;
                    case "completed":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusShipped).ToList(); // hoặc SD.StatusCompleted nếu bạn có
                        break;
                    case "cancelled":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusCancelled).ToList();
                        break;
                    case "refunded":
                        orders = orders.Where(u => u.OrderStatus == SD.StatusRefunded).ToList();
                        break;
                }
            }

            var result = orders.Select(o => new
            {
                o.Id,
                o.OrderCode,
                o.Name,
                o.PhoneNumber,
                o.OrderTotal,
                o.OrderStatus,
                o.PaymentStatus,
                PaymentMethod = o.PaymentMethod.ToString(),
                o.ApplicationUser.Email
            });

            return Json(new { data = result });
        }
        #endregion
    }

}
